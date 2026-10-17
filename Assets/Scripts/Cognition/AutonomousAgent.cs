using System.Collections.Generic;
using System.Threading;
using Simpiens.Cognition.Contracts;
using Simpiens.Cognition.Evaluators;
using Simpiens.Cognition.Groups;
using Simpiens.Cognition.Inventory;
using Simpiens.Cognition.Memory;
using Simpiens.Cognition.Planning;
using Simpiens.Cognition.Planning.Actions;
using Simpiens.Cognition.Social;
using Simpiens.Entities;
using Simpiens.Narrative;
using Simpiens.Simulation;
using Simpiens.Simulation.Economy;
using Simpiens.Simulation.Spatial;
using UnityEngine;

namespace Simpiens.Cognition
{
    public class AutonomousAgent : MonoBehaviour, ISocialAgent
    {
        public const int DefaultVisionRadius = 6;

        // Dependencies
        private ISpatialPartition _spatialPartition;
        private ICognitiveEvaluator[] _evaluators;
        private ISimulationManager _simulationManager;
        private ISimulationClock _clock;

        /// <summary>Ticks between looks around while carrying out an intent (walking, harvesting, sleeping).</summary>
        public const uint BusyPerceptionIntervalTicks = 5;
        private long _lastBusyPerceptionTick = -BusyPerceptionIntervalTicks;
        private IGoapPlanner _planner;
        private ActionGraph _actionGraph;
        private List<GoapGoal> _goals;
        private IAgentCognitionService _cognitionService;
        private CancellationTokenSource _cts;

        // Authoritative Pure C# Agent State
        private AgentState _state;
        public AgentState State
        {
            get
            {
                if (_state == null)
                {
                    _state = new AgentState();
                    if (transform != null) _state.Position = transform.position.ToSim();
                }
                return _state;
            }
            internal set => _state = value;
        }

        // Forwarded ISocialAgent & State Properties
        public EntityId AgentId => State.AgentId;
        public AgentMemory Memory => State.Memory;
        public GoapPlan ActivePlan => State.ActivePlan;
        public GoapGoal ActiveGoal => State.ActiveGoal;
        public bool HasActivePlan => State.HasActivePlan;

        public AgentNeeds Needs
        {
            get => State.Needs;
            set => State.Needs = value;
        }

        public float Hunger
        {
            get => State.Hunger;
            set => State.Hunger = value;
        }

        public float Energy
        {
            get => State.Energy;
            set => State.Energy = value;
        }

        public float Social
        {
            get => State.Social;
            set => State.Social = value;
        }

        public float Frustration
        {
            get => State.Frustration;
            set => State.Frustration = value;
        }

        public AgentInventory Inventory => State.Inventory;

        public bool HasFoodInInventory
        {
            get => State.HasFoodInInventory;
            set => State.HasFoodInInventory = value;
        }

        public int NavigationalStalls
        {
            get => State.NavigationalStalls;
            set => State.NavigationalStalls = value;
        }

        public bool IsExhaustionCollapsed
        {
            get => State.IsExhaustionCollapsed;
            set => State.IsExhaustionCollapsed = value;
        }

        public GroupMembership Membership
        {
            get => State.Membership;
            set => State.Membership = value;
        }

        public IdentityTag Identity
        {
            get => State.Identity;
            set => State.Identity = value;
        }

        public bool HasActiveIntent
        {
            get => State.HasActiveIntent;
            set => State.HasActiveIntent = value;
        }

        public bool IsInTantrum => State.IsInTantrum;
        public bool IsYielding => State.IsYielding;
        public bool IsGossiping => State.IsGossiping;
        public bool IsSharing => State.IsSharing;
        public bool IsRallying => State.IsRallying;
        public bool IsDefiant => State.IsDefiant;
        public bool IsStarvationPanicking => State.IsStarvationPanicking;

        public AgentVisualState VisualState
        {
            get => State.VisualState;
            set => State.VisualState = value;
        }

        /// <summary>Current logical tick, or -1 without a clock (headless). Read-only, for views.</summary>
        public long CurrentSimTick => _clock != null ? _clock.CurrentTick : -1;

        public float LastGossipTimestamp => State.LastGossipTimestamp;
        public float LastYieldTimestamp => State.LastYieldTimestamp;
        public float LastTantrumTimestamp => State.LastTantrumTimestamp;

#if UNITY_EDITOR
        [Header("Runtime Telemetry (Editor Debug)")]
        [SerializeField] private AgentVisualState _inspectorVisualState;
        [SerializeField] private float _inspectorFrustration;
        [SerializeField] private float _inspectorHunger;
        [SerializeField] private float _inspectorEnergy;
        [SerializeField] private float _inspectorSocial;
        [SerializeField] private string _inspectorActiveGoal;
        [SerializeField] private string _inspectorActiveAction;
        [SerializeField] private int _inspectorGold;
        [SerializeField] [TextArea(2, 5)] private string _inspectorInventory;
        [SerializeField] [TextArea(3, 6)] private string _inspectorRecentEvents;
        [SerializeField] [TextArea(3, 6)] private string _inspectorDyadicAffinities;
#endif

        private void Awake()
        {
            if (_state == null)
            {
                _state = new AgentState();
                if (transform != null) _state.Position = transform.position.ToSim();
            }
            if (_cts == null)
            {
                _cts = new CancellationTokenSource();
            }
            if (_cognitionService == null)
            {
                _cognitionService = new AgentCognitionService();
            }
        }

        public void TriggerGossipVisual(float timestamp = 0f)
        {
            State.TriggerGossipVisual(timestamp);
        }

        public void TriggerYieldVisual(float timestamp = 0f)
        {
            State.TriggerYieldVisual(timestamp);
        }

        public void TriggerTantrumVisual(float timestamp = 0f)
        {
            State.TriggerTantrumVisual(timestamp);

            uint tick = _clock != null ? (uint)_clock.CurrentTick : 0;
            _simulationManager?.RecordStoryEvent(new ChronicleEvent(
                ChronicleEventType.TantrumStarted,
                AgentId,
                default,
                0,
                tick,
                ChronicleIntensity.Dramatic));
        }

        public void TriggerSharingVisual(float duration = 1.2f)
        {
            State.TriggerSharingVisual(duration);
        }

        public void TriggerRallyVisual(float duration = 2.0f)
        {
            State.TriggerRallyVisual(duration);
        }

        public void TriggerDefiantVisual(float duration = 1.5f)
        {
            State.TriggerDefiantVisual(duration);
        }

        public void TriggerReliefVisual()
        {
            State.TriggerReliefVisual();
        }

        public void CollapseFromExhaustion()
        {
            State.CollapseFromExhaustion();

            uint tick = _clock != null ? (uint)_clock.CurrentTick : 0;
            _simulationManager?.RecordStoryEvent(new ChronicleEvent(
                ChronicleEventType.ExhaustionCollapsed,
                AgentId,
                default,
                0,
                tick,
                ChronicleIntensity.Crisis));
        }

        public void RelieveFrustration(float amount)
        {
            State.RelieveFrustration(amount);
        }

        public void AbortActivePlan()
        {
            State.AbortActivePlan();
        }

        public void Initialize(
            EntityId id,
            ISpatialPartition spatialPartition,
            ICognitiveEvaluator[] evaluators,
            ISimulationManager simulationManager,
            ISimulationClock clock,
            IGoapPlanner planner = null,
            ActionGraph actionGraph = null,
            List<GoapGoal> goals = null,
            IAgentCognitionService cognitionService = null)
        {
            State.AgentId = id;
            if (transform != null)
            {
                State.Position = transform.position.ToSim();
            }

            _spatialPartition = spatialPartition;
            _evaluators = evaluators;
            _simulationManager = simulationManager;
            _clock = clock;
            _planner = planner;
            _actionGraph = actionGraph;
            _goals = goals;
            _cognitionService = cognitionService ?? new AgentCognitionService();

            if (_cts == null)
            {
                _cts = new CancellationTokenSource();
            }

            _simulationManager?.Agents?.RegisterAgent(State);
        }

        public const float FixedTickDeltaTime = 0.1f;

        public void ManualUpdate() // Backward compatibility alias for tests and runners
        {
            ManualTick(_clock != null ? _clock.CurrentTick : 0);
        }

        public void ManualTick(long currentTick)
        {
            if (this == null || gameObject == null || State == null) return;
            if (_simulationManager != null && _simulationManager.IsPaused) return;
            if (_clock != null && _clock.IsPaused) return;

            if (transform != null)
            {
                State.Position = transform.position.ToSim();
            }

#if UNITY_EDITOR
            UpdateEditorTelemetry();
#endif

            // Advance biological drives and emotion timers on fixed logical tick dt
            State.Tick(FixedTickDeltaTime);

            // Keep membership synced with authoritative main-thread group registry
            if (_simulationManager?.Groups != null && State.Membership.HasGroup)
            {
                if (_simulationManager.Groups.TryGetMembership(State.AgentId, out var currentMem))
                {
                    State.Membership = currentMem;
                }
                else
                {
                    State.Membership = GroupMembership.None;
                }
            }

            // Reflexive preemption check
            bool emergencyExhaustion = State.Energy <= MentalBreakEvaluator.ExhaustionThreshold;
            bool emergencyStarvation = State.Hunger >= MentalBreakEvaluator.StarvationThreshold && !State.HasFoodInInventory && !State.HasKnownFood();

            if (emergencyExhaustion)
            {
                State.IsExhaustionCollapsed = true;
                State.VisualState = AgentVisualState.Resting;
            }

            bool shouldPreempt = false;
            if (emergencyExhaustion && !(State.CurrentIntent is IdleIntent))
            {
                shouldPreempt = true;
            }
            else if (emergencyStarvation && !(State.CurrentIntent is PanicIntent))
            {
                shouldPreempt = true;
            }

            if (State.HasActiveIntent && shouldPreempt)
            {
                if (_simulationManager != null)
                {
                    _simulationManager.AbortIntent(State.AgentId);
                }
                State.AbortActivePlan();
                State.CurrentIntent = null;
                State.HasActiveIntent = false;

                if (State.IsExhaustionCollapsed)
                {
                    State.VisualState = AgentVisualState.Resting;
                }
                else if (emergencyStarvation && State.TantrumTimer <= 0f)
                {
                    State.VisualState = AgentVisualState.Panicking;
                }

                if (!State.IsEvaluating && _cognitionService != null)
                {
                    _cognitionService.BeginEvaluation(
                        State,
                        _spatialPartition,
                        _simulationManager,
                        _clock,
                        _planner,
                        _actionGraph,
                        _goals,
                        _evaluators,
                        _cts.Token);
                }
                return;
            }

            if (!State.IsEvaluating && State.HasActiveIntent)
            {
                PerceiveWhileBusy();
                return;
            }
            if (State.IsEvaluating) return;

            if (_cognitionService != null)
            {
                _cognitionService.BeginEvaluation(
                    State,
                    _spatialPartition,
                    _simulationManager,
                    _clock,
                    _planner,
                    _actionGraph,
                    _goals,
                    _evaluators,
                    _cts.Token);
            }
        }

        public bool HasKnownFood()
        {
            return State.HasKnownFood(_clock != null ? (uint)_clock.CurrentTick : 0);
        }

#if UNITY_EDITOR
        private void UpdateEditorTelemetry()
        {
            _inspectorVisualState = VisualState;
            _inspectorFrustration = Frustration;
            _inspectorHunger = Needs.Hunger;
            _inspectorEnergy = Needs.Energy;
            _inspectorSocial = Needs.Social;
            _inspectorActiveGoal = ActiveGoal != null ? ActiveGoal.GetType().Name : "(None)";
            _inspectorActiveAction = (HasActivePlan && ActivePlan.CurrentAction != null) ? ActivePlan.CurrentAction.GetType().Name : "(None)";

            if (Inventory != null)
            {
                _inspectorGold = Inventory.Gold;
                var sbInv = new System.Text.StringBuilder();
                for (int i = 0; i < Inventory.Capacity; i++)
                {
                    var slot = Inventory.GetSlot(i);
                    if (!slot.IsEmpty)
                    {
                        sbInv.AppendLine($"[{i}] {slot.Type} x{slot.Quantity} (Q:{slot.Quality}, Val:{slot.BaseValue}g)");
                    }
                    else
                    {
                        sbInv.AppendLine($"[{i}] (Empty)");
                    }
                }
                _inspectorInventory = sbInv.ToString();
            }
            else
            {
                _inspectorGold = 0;
                _inspectorInventory = "(No inventory)";
            }

            IWorldRegistry registry = null;
            if (_simulationManager is SimulationManager sm)
            {
                registry = sm.WorldRegistry;
            }

            if (Memory != null)
            {
                var ledger = Memory.EventLedger;
                if (ledger != null && ledger.Count > 0)
                {
                    var sb = new System.Text.StringBuilder();
                    int start = SimMath.Max(0, ledger.Count - 6);
                    for (int i = ledger.Count - 1; i >= start; i--)
                    {
                        var evt = ledger.GetEvent(i);
                        string subjectName = "None";
                        if (evt.SubjectId != default)
                        {
                            var targetNode = registry?.GetNode(evt.SubjectId);
                            subjectName = targetNode != null ? targetNode.name : evt.SubjectId.ToString().Substring(0, 8);
                        }
                        sb.AppendLine($"[Tick {evt.SimulationTick}] {evt.EventType} (Subject: {subjectName}, Pos: {evt.Location})");
                    }
                    _inspectorRecentEvents = sb.ToString();
                }
                else
                {
                    _inspectorRecentEvents = "(No events recorded yet)";
                }

                if (Memory.AffinityMap != null && Memory.AffinityMap.Count > 0)
                {
                    var sb = new System.Text.StringBuilder();
                    foreach (var kvp in Memory.AffinityMap)
                    {
                        var peerNode = registry?.GetNode(kvp.Key);
                        string peerName = peerNode != null ? peerNode.name : kvp.Key.ToString().Substring(0, 8);
                        var rel = kvp.Value;
                        sb.AppendLine($"{peerName}: W:{rel.Warmth:+0;-#;0} T:{rel.Trust:+0;-#;0} F:{rel.Fear:+0;-#;0} R:{rel.Respect:+0;-#;0} (Interactions: {rel.InteractionCount})");
                    }
                    _inspectorDyadicAffinities = sb.ToString();
                }
                else
                {
                    _inspectorDyadicAffinities = "(No relational contacts yet)";
                }
            }
        }
#endif

        private void OnDestroy()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            if (_simulationManager != null && State != null)
            {
                _simulationManager.Agents?.UnregisterAgent(State.AgentId);
            }
        }

        /// <summary>
        /// Agents also see while they walk, not only between intents: on a walled floor a long walk would otherwise
        /// pass food, bunks and doors unseen (Epic MAP Phase 3). Main thread, never while a background evaluation
        /// is reading the memory; every BusyPerceptionIntervalTicks.
        /// </summary>
        private void PerceiveWhileBusy()
        {
            if (_spatialPartition == null || _clock == null || State.Memory == null) return;
            long tick = _clock.CurrentTick;
            // (a clock rewound by a respawn starts a new agent anyway; tick < last only after a rewind)
            if (tick >= _lastBusyPerceptionTick && tick - _lastBusyPerceptionTick < BusyPerceptionIntervalTicks) return;
            var snapshot = _spatialPartition.GetActiveSnapshot();
            if (snapshot == null) return;
            _lastBusyPerceptionTick = tick;
            State.Memory.UpdateMemory(snapshot, GridPoint.RoundToInt(State.Position), DefaultVisionRadius, (uint)tick,
                surveySectorId: BountyQueries.SurveyingSector(snapshot, State.AgentId));
        }
    }
}
