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
        public const int DefaultVisionRadius = AgentRuntime.DefaultVisionRadius;

        /// <summary>Ticks between looks around while carrying out an intent (walking, harvesting, sleeping).</summary>
        public const uint BusyPerceptionIntervalTicks = AgentRuntime.BusyPerceptionIntervalTicks;

        // The crawler's simulation (engine-free): this component is only its view and editor telemetry (Epic CORE C5)
        private AgentRuntime _runtime;
        public AgentRuntime Runtime => _runtime;

        // Authoritative Pure C# Agent State
        private AgentState _state;
        public AgentState State
        {
            get
            {
                if (_state == null)
                {
                    // Not bound to a simulation crawler yet: a placeholder, so views can read it safely
                    _state = new AgentState();
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
        public long CurrentSimTick => _runtime != null ? _runtime.CurrentSimTick : -1;

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
            }
        }

        /// <summary>Binds this view to its crawler. The simulation owns the runtime; this component never ticks itself.</summary>
        public void Bind(AgentRuntime runtime)
        {
            _runtime = runtime;
            _state = runtime?.State;
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
            if (_runtime != null) _runtime.TriggerTantrumVisual(timestamp);
            else State.TriggerTantrumVisual(timestamp);
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
            if (_runtime != null) _runtime.CollapseFromExhaustion();
            else State.CollapseFromExhaustion();
        }

        public void RelieveFrustration(float amount)
        {
            State.RelieveFrustration(amount);
        }

        public void AbortActivePlan()
        {
            State.AbortActivePlan();
        }

        public const float FixedTickDeltaTime = AgentRuntime.FixedTickDeltaTime;

#if UNITY_EDITOR
        // The session ticks the crawler (SessionHost); the view only mirrors it into the Inspector
        private void Update()
        {
            if (_runtime != null) UpdateEditorTelemetry();
        }
#endif

        public bool HasKnownFood()
        {
            return _runtime != null ? _runtime.HasKnownFood() : State.HasKnownFood();
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

            IWorldRegistry registry = _registry;

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
                            var targetNode = registry?.GetEntity(evt.SubjectId);
                            subjectName = targetNode != null ? targetNode.Name : evt.SubjectId.ToString().Substring(0, 8);
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
                        var peerNode = registry?.GetEntity(kvp.Key);
                        string peerName = peerNode != null ? peerNode.Name : kvp.Key.ToString().Substring(0, 8);
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

        /// <summary>For editor telemetry only: names of the entities this crawler remembers.</summary>
        private IWorldRegistry _registry;
        public void SetTelemetryRegistry(IWorldRegistry registry) => _registry = registry;

        private void OnDestroy()
        {
            // The view going away stops its crawler, as before Epic CORE C5 (the session tears both down together)
            _runtime?.Dispose();
        }
    }
}
