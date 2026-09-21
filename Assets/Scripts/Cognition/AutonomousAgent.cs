using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Simpiens.Cognition.Contracts;
using Simpiens.Cognition.Evaluators;
using Simpiens.Cognition.Planning;
using Simpiens.Cognition.Planning.Actions;
using Simpiens.Entities;
using Simpiens.Simulation;
using Simpiens.Simulation.Spatial;
using UnityEngine;

namespace Simpiens.Cognition
{
    public class AutonomousAgent : MonoBehaviour
    {
        // Dependencies
        private ISpatialPartition _spatialPartition;
        private ICognitiveEvaluator[] _evaluators;
        private ISimulationManager _simulationManager;
        private ISimulationClock _clock;

        // GOAP Planning Dependencies
        private IGoapPlanner _planner;
        private ActionGraph _actionGraph;
        private List<GoapGoal> _goals;

        // Plan Execution State (Zero Allocation)
        private readonly GoapPlan _activePlan = new GoapPlan(16);
        private readonly GoapPlan _scratchPlan = new GoapPlan(16);
        private GoapGoal _activeGoal;

        // Private fields
        private Simpiens.Cognition.Memory.AgentMemory _memory;
        private bool _isEvaluating;
        private CancellationTokenSource _cts;

        // Public properties
        public UnityEngine.GUID AgentId { get; private set; }
        public Simpiens.Cognition.Memory.AgentMemory Memory => _memory;
        public GoapPlan ActivePlan => _activePlan;
        public GoapGoal ActiveGoal => _activeGoal;
        public bool HasActivePlan => !_activePlan.IsEmpty && !_activePlan.IsFinished;

        // State
        public AgentNeeds Needs { get; set; } = AgentNeeds.Default;
        public float Hunger
        {
            get => Needs.Hunger;
            set => Needs = new AgentNeeds(value, Needs.Energy, Needs.Social);
        }
        public float Energy
        {
            get => Needs.Energy;
            set => Needs = new AgentNeeds(Needs.Hunger, value, Needs.Social);
        }
        public float Social
        {
            get => Needs.Social;
            set => Needs = new AgentNeeds(Needs.Hunger, Needs.Energy, value);
        }
        public float Frustration { get; set; } = 0f;
        public bool HasFoodInInventory { get; set; } = false;
        public int NavigationalStalls { get; set; } = 0;
        public bool IsExhaustionCollapsed { get; private set; } = false;

        public bool HasActiveIntent { get; internal set; }

        // Visual State
        public AgentVisualState VisualState { get; set; } = AgentVisualState.Idle;
        public float LastGossipTimestamp { get; private set; } = -10f;

        public void TriggerGossipVisual(float timestamp)
        {
            LastGossipTimestamp = timestamp;
            VisualState = AgentVisualState.Gossiping;
        }

        public void RelieveFrustration(float amount)
        {
            Frustration = Mathf.Max(0f, Frustration - amount);
        }

        public void AbortActivePlan()
        {
            _activePlan.Clear();
            _activeGoal = null;
            if (!IsExhaustionCollapsed)
            {
                VisualState = AgentVisualState.Idle;
            }
        }

        public void Initialize(
            UnityEngine.GUID id,
            ISpatialPartition spatialPartition,
            ICognitiveEvaluator[] evaluators,
            ISimulationManager simulationManager,
            ISimulationClock clock,
            IGoapPlanner planner = null,
            ActionGraph actionGraph = null,
            List<GoapGoal> goals = null)
        {
            AgentId = id;
            _spatialPartition = spatialPartition;
            _evaluators = evaluators;
            _simulationManager = simulationManager;
            _clock = clock;
            _planner = planner;
            _actionGraph = actionGraph;
            _goals = goals;

            _memory = new Simpiens.Cognition.Memory.AgentMemory();
            _cts = new CancellationTokenSource();
        }

        public void ManualUpdate() // Called by SwarmSpawner to avoid MonoBehaviour Update overhead
        {
            // Multifaceted biological drive simulation
            float hunger = Mathf.Min(100f, Needs.Hunger + Time.deltaTime * 1.5f);

            float energy = Needs.Energy;
            if (IsExhaustionCollapsed || (HasActivePlan && _activePlan.CurrentAction is Simpiens.Cognition.Planning.Actions.RestAction))
            {
                // Rapidly replenish energy while actively resting or recovering from exhaustion collapse
                energy = Mathf.Min(100f, energy + Time.deltaTime * 15f);
                if (IsExhaustionCollapsed && energy >= 30f)
                {
                    IsExhaustionCollapsed = false;
                    VisualState = AgentVisualState.Idle;
                }
            }
            else if (VisualState == AgentVisualState.Walking)
            {
                // Walking drains energy faster
                energy = Mathf.Max(0f, energy - Time.deltaTime * 1.0f);
            }
            else
            {
                // Baseline metabolic energy drain
                energy = Mathf.Max(0f, energy - Time.deltaTime * 0.2f);
            }

            float social = Mathf.Max(0f, Needs.Social - Time.deltaTime * 0.5f);

            Needs = new AgentNeeds(hunger, energy, social);
            Frustration = Mathf.Max(0f, Frustration - Time.deltaTime * 1f); // Decays slowly

            // Tier 1 Reflexive Preemption Check:
            // If an agent is executing an active intent and an emergency condition occurs:
            // 1. Exhaustion Collapse (Energy <= 5f)
            // 2. Starvation Panic (Hunger >= 85f and no known food in inventory or memory)
            // Preempt the active intent on the main thread and cancel the long-term plan!
            bool emergencyExhaustion = energy <= Simpiens.Cognition.Evaluators.MentalBreakEvaluator.ExhaustionThreshold;
            bool emergencyStarvation = hunger >= Simpiens.Cognition.Evaluators.MentalBreakEvaluator.StarvationThreshold && !HasFoodInInventory && !HasKnownFood();

            if (emergencyExhaustion)
            {
                IsExhaustionCollapsed = true;
            }

            if (IsExhaustionCollapsed)
            {
                VisualState = AgentVisualState.Resting;
            }

            if (HasActiveIntent && (emergencyExhaustion || emergencyStarvation))
            {
                if (_simulationManager != null)
                {
                    _simulationManager.AbortIntent(AgentId);
                }
                AbortActivePlan();
                HasActiveIntent = false;

                if (IsExhaustionCollapsed)
                {
                    VisualState = AgentVisualState.Resting;
                }

                if (!_isEvaluating)
                {
                    EvaluateCognitionAsync().Forget();
                }
                return;
            }

            if (_isEvaluating || HasActiveIntent) return;

            EvaluateCognitionAsync().Forget();
        }

        private async UniTaskVoid EvaluateCognitionAsync()
        {
            if (_spatialPartition == null) return;
            var snapshot = _spatialPartition.GetActiveSnapshot();
            if (snapshot == null) return;

            _isEvaluating = true;
            if (IsExhaustionCollapsed)
            {
                VisualState = AgentVisualState.Resting;
            }
            else
            {
                VisualState = AgentVisualState.Thinking;
            }

            Vector2Int currentPosInt = new Vector2Int(Mathf.RoundToInt(transform.position.x), Mathf.RoundToInt(transform.position.y));
            uint tick = _clock != null ? (uint)_clock.CurrentTick : 0;
            _memory.UpdateMemory(snapshot, currentPosInt, visionRadius: 20, currentTick: tick);

            var context = new AgentContext(AgentId, transform.position, Needs, Frustration, snapshot, _memory, tick);

            try
            {
                // Explicitly jump off the main thread for cognitive evaluation
                await UniTask.SwitchToThreadPool();

                // Ensure the snapshot stays alive while we process in background
                snapshot.Retain();

                AgentIntent intent = null;

                // If agent is in exhaustion collapse, suppress normal planning and force rest
                if (IsExhaustionCollapsed)
                {
                    intent = new IdleIntent(context.AgentId, 2.0f);
                }

                // Tier 1: Check Reflexive Evaluators (Panic, Emergency Drives)
                if (intent == null && _evaluators != null)
                {
                    for (int i = 0; i < _evaluators.Length; i++)
                    {
                        intent = await _evaluators[i].EvaluateAsync(context, _cts.Token);
                        if (intent != null)
                        {
                            // Reflexive drive triggered; abort long-term plan
                            AbortActivePlan();
                            break;
                        }
                    }
                }

                // Tier 2: Deliberative GOAP Plan Execution & Generation
                if (intent == null && _planner != null && _actionGraph != null)
                {
                    // A. If an active plan is already in progress, execute its current step
                    if (HasActivePlan)
                    {
                        var currentAction = _activePlan.CurrentAction;
                        if (currentAction != null && currentAction.IsValid(context))
                        {
                            intent = await currentAction.CreateIntentAsync(context, _cts.Token);
                        }
                        else
                        {
                            // Action is no longer valid in this context; invalidate plan
                            AbortActivePlan();
                        }
                    }

                    // B. If no active plan, select highest priority goal and formulate a new plan
                    if (intent == null && !HasActivePlan && _goals != null && _goals.Count > 0)
                    {
                        var currentState = WorldStateBuilder.BuildWorldState(context, HasFoodInInventory);
                        GoapGoal bestGoal = null;
                        float highestPriority = -1f;

                        int goalCount = _goals.Count;
                        for (int i = 0; i < goalCount; i++)
                        {
                            var goal = _goals[i];
                            if (!goal.IsValid(context)) continue;
                            if (goal.IsSatisfied(currentState)) continue;

                            float priority = goal.CalculatePriority(context);
                            if (priority > highestPriority)
                            {
                                highestPriority = priority;
                                bestGoal = goal;
                            }
                        }

                        if (bestGoal != null)
                        {
                            bool planFound = _planner.Plan(currentState, bestGoal, _actionGraph, context, _scratchPlan);
                            if (planFound && !_scratchPlan.IsEmpty)
                            {
                                _activePlan.CopyFrom(_scratchPlan);
                                _activeGoal = bestGoal;

                                var firstAction = _activePlan.CurrentAction;
                                if (firstAction != null && firstAction.IsValid(context))
                                {
                                    intent = await firstAction.CreateIntentAsync(context, _cts.Token);
                                }
                            }
                        }
                    }
                }

                // Tier 3: Fallback Idle
                if (intent == null)
                {
                    intent = new IdleIntent(context.AgentId);
                }

                // Set intent tracking so agent waits until it completes
                HasActiveIntent = true;
                intent.OnComplete = (result) =>
                {
                    HasActiveIntent = false;
                    VisualState = AgentVisualState.Idle;
                    uint currentTick = (uint)_clock.CurrentTick;

                    if (intent is PanicIntent)
                    {
                        Frustration = 0f;
                    }
                    else if (result == IntentResult.Success)
                    {
                        NavigationalStalls = 0;

                        // If the completed intent was part of our active plan, advance the step
                        if (HasActivePlan)
                        {
                            var completedAction = _activePlan.CurrentAction;
                            if (completedAction is HarvestResourceAction)
                            {
                                Hunger = Mathf.Max(0f, Hunger - 50f);
                                HasFoodInInventory = true;
                            }
                            else if (completedAction is EatCarriedFoodAction)
                            {
                                Hunger = Mathf.Max(0f, Hunger - 40f);
                                HasFoodInInventory = false;
                            }
                            else if (completedAction is Simpiens.Cognition.Planning.Actions.RestAction)
                            {
                                Energy = Mathf.Min(100f, Energy + 30f);
                            }
                            else if (completedAction is Simpiens.Cognition.Planning.Actions.TravelToPeerAction)
                            {
                                Social = Mathf.Min(100f, Social + 25f);
                            }

                            _activePlan.AdvanceStep();
                            if (_activePlan.IsFinished)
                            {
                                AbortActivePlan();
                            }
                        }
                    }
                    else if (result == IntentResult.TargetMissing || result == IntentResult.PathBlocked || result == IntentResult.Aborted)
                    {
                        NavigationalStalls++;
                        AbortActivePlan();
                    }

                    if (intent is HarvestResourceIntent harvestIntent)
                    {
                        if (result == IntentResult.TargetMissing)
                        {
                            _memory.RemoveMemory(harvestIntent.TargetEntityId);
                        }
                        else if (result == IntentResult.PathBlocked || result == IntentResult.Aborted)
                        {
                            _memory.BlacklistEntity(harvestIntent.TargetEntityId, currentTick + 100);
                        }
                    }
                };

                // Pass back to simulation manager queue
                _simulationManager.EnqueueIntent(intent);
            }
            catch (System.OperationCanceledException)
            {
                // Task was cleanly canceled
            }
            finally
            {
                snapshot.Release();
                _isEvaluating = false;
            }
        }

        public bool HasKnownFood()
        {
            if (HasFoodInInventory) return true;
            if (_memory == null || _memory.SpatialMemoryMap == null) return false;

            uint currentTick = _clock != null ? (uint)_clock.CurrentTick : 0;
            foreach (var kvp in _memory.SpatialMemoryMap)
            {
                var record = kvp.Value;
                if (record.Type == Simpiens.Simulation.Spatial.EntityType.Resource && !_memory.IsBlacklisted(record.EntityId, currentTick))
                {
                    return true;
                }
            }
            return false;
        }

        private void OnDestroy()
        {
            _cts?.Cancel();
            _cts?.Dispose();
        }
    }
}
