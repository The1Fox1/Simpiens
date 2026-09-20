using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Simpiens.Cognition.Contracts;
using Simpiens.Cognition.Evaluators;
using Simpiens.Cognition.Planning;
using Simpiens.Cognition.Planning.Actions;
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
        public float Hunger { get; set; } = 50f;
        public float Energy { get; set; } = 100f;
        public float Frustration { get; set; } = 0f;
        public bool HasFoodInInventory { get; set; } = false;

        public bool HasActiveIntent { get; private set; }

        public void RelieveFrustration(float amount)
        {
            Frustration = Mathf.Max(0f, Frustration - amount);
        }

        public void AbortActivePlan()
        {
            _activePlan.Clear();
            _activeGoal = null;
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
            // Simple drive simulation
            Hunger += Time.deltaTime * 2f; // Hunger increases over time
            Frustration = Mathf.Max(0f, Frustration - Time.deltaTime * 1f); // Decays slowly

            // Tier 1 Reflexive Preemption Check:
            // If an agent is executing an active intent and an emergency condition occurs (Frustration > 80),
            // preempt the active intent on the main thread and cancel the long-term plan!
            if (HasActiveIntent && Frustration > 80f)
            {
                _simulationManager.AbortIntent(AgentId);
                AbortActivePlan();
                HasActiveIntent = false;

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
            var snapshot = _spatialPartition.GetActiveSnapshot();
            if (snapshot == null) return;

            _isEvaluating = true;

            Vector2Int currentPosInt = new Vector2Int(Mathf.RoundToInt(transform.position.x), Mathf.RoundToInt(transform.position.y));
            _memory.UpdateMemory(snapshot, currentPosInt, visionRadius: 20, currentTick: (uint)_clock.CurrentTick);

            var context = new AgentContext(AgentId, transform.position, Hunger, Energy, Frustration, snapshot, _memory, (uint)_clock.CurrentTick);

            try
            {
                // Explicitly jump off the main thread for cognitive evaluation
                await UniTask.SwitchToThreadPool();

                // Ensure the snapshot stays alive while we process in background
                snapshot.Retain();

                AgentIntent intent = null;

                // Tier 1: Check Reflexive Evaluators (Panic, Emergency Drives)
                if (_evaluators != null)
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
                    uint currentTick = (uint)_clock.CurrentTick;

                    if (intent is PanicIntent)
                    {
                        Frustration = 0f;
                    }
                    else if (result == IntentResult.Success)
                    {
                        Frustration = Mathf.Max(0f, Frustration - 50f);
                        if (intent is IdleIntent) Frustration += 5f;

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

                            _activePlan.AdvanceStep();
                            if (_activePlan.IsFinished)
                            {
                                AbortActivePlan();
                            }
                        }
                    }
                    else if (result == IntentResult.TargetMissing || result == IntentResult.PathBlocked || result == IntentResult.Aborted)
                    {
                        Frustration += 25f;
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

        private void OnDestroy()
        {
            _cts?.Cancel();
            _cts?.Dispose();
        }
    }
}
