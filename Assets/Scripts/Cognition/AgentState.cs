using System;
using Simpiens.Cognition.Contracts;
using Simpiens.Cognition.Evaluators;
using Simpiens.Cognition.Groups;
using Simpiens.Cognition.Inventory;
using Simpiens.Cognition.Memory;
using Simpiens.Cognition.Planning;
using Simpiens.Entities;
using Simpiens.Simulation.Economy;
using Simpiens.Simulation.Persistence;
using Simpiens.Simulation.Spatial;
using Simpiens.Diagnostics;
using UnityEngine;
using ZLogger;

namespace Simpiens.Cognition
{
    /// <summary>
    /// Pure C# authoritative data representation of an agent's internal state.
    /// Completely decoupled from Unity GameObjects and MonoBehaviours to enable
    /// headless simulation, save/load serialization, and off-screen execution.
    /// </summary>
    public class AgentState : ISocialAgent
    {
        public const float DefaultBaseSpeed = 2.0f;
        public const float FixedTickDeltaTime = 0.1f;

        // Identity & Transform
        public GUID AgentId { get; set; }
        public Vector2 Position { get; set; }
        public Vector2 Velocity { get; set; }
        public float BaseSpeed { get; set; } = DefaultBaseSpeed;

        // Biological Drives & Needs
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

        /// <summary>What this agent has done this session (a field, so the simulation records into it in place).</summary>
        public AgentActivity Activity = AgentActivity.Empty;

        // Inventory & Economic Primitives
        public AgentInventory Inventory { get; set; }

        public bool HasFoodInInventory
        {
            get => Inventory != null && Inventory.HasItem(ItemType.Ration);
            set
            {
                if (Inventory == null) return;
                if (value)
                {
                    if (!Inventory.HasItem(ItemType.Ration))
                    {
                        Inventory.TryAddItem(new ItemInstance(1, ItemType.Ration, 1, 5), out _);
                    }
                }
                else
                {
                    Inventory.TryRemoveItem(ItemType.Ration, 1);
                }
            }
        }

        // Epistemic & Relational Memory
        public AgentMemory Memory { get; set; }

        // Sociopolitical Collective Standing
        public GroupMembership Membership { get; set; } = GroupMembership.None;
        public IdentityTag Identity { get; set; } = IdentityTag.Default;

        /// <summary>Stable traits (review §3.2). Neutral by default; set once at spawn.</summary>
        public Simpiens.Cognition.Contracts.AgentPersonality Personality { get; set; }

        /// <summary>Why the current intent was chosen (main-thread copy of AgentIntent.Decision).</summary>
        public Simpiens.Cognition.Contracts.DecisionSummary LastDecision { get; set; }

        // GOAP Deliberative Planning State
        public GoapPlan ActivePlan { get; } = new GoapPlan(16);
        public GoapPlan ScratchPlan { get; } = new GoapPlan(16);
        public GoapGoal ActiveGoal { get; set; }
        public bool HasActivePlan => !ActivePlan.IsEmpty && !ActivePlan.IsFinished;

        // Intent Execution Tracking
        public bool HasActiveIntent { get; set; }
        public AgentIntent CurrentIntent { get; set; }
        public bool IsEvaluating { get; set; }
        public int NavigationalStalls { get; set; }
        public bool IsExhaustionCollapsed { get; set; }

        // Visual / Psychological Emotion Timers
        public float TantrumTimer { get; set; }
        public float YieldingTimer { get; set; }
        public float GossipTimer { get; set; }
        public float SharingTimer { get; set; }
        public float RallyTimer { get; set; }
        public float DefiantTimer { get; set; }
        public uint LastEvaluatedDirectiveTick { get; set; }

        // Scout bounty progress: set on the main thread by ScoutIntentHandler once the claimant
        // physically enters the bounty's target sector; consumed by the Scout delivery at a stall.
        public ushort ScoutReportBountyId { get; set; }
        public uint ScoutReportTick { get; set; }

        public bool IsInTantrum => TantrumTimer > 0f;
        public bool IsYielding => YieldingTimer > 0f;
        public bool IsGossiping => GossipTimer > 0f;
        public bool IsSharing => SharingTimer > 0f;
        public bool IsRallying => RallyTimer > 0f;
        public bool IsDefiant => DefiantTimer > 0f;
        public bool IsStarvationPanicking => Needs.Hunger >= MentalBreakEvaluator.StarvationThreshold && !HasFoodInInventory && !HasKnownFood();

        public float LastGossipTimestamp { get; set; } = -10f;
        public float LastYieldTimestamp { get; set; } = -10f;
        public float LastTantrumTimestamp { get; set; } = -10f;

        private AgentVisualState _baseVisualState = AgentVisualState.Idle;
        public AgentVisualState VisualState
        {
            get
            {
                if (IsExhaustionCollapsed) return AgentVisualState.Resting;
                if (TantrumTimer > 0f || IsStarvationPanicking) return AgentVisualState.Panicking;
                // Asleep in a bunk: the brief social looks below don't sit the sleeper up
                if (_baseVisualState == AgentVisualState.Sleeping) return AgentVisualState.Sleeping;
                if (DefiantTimer > 0f) return AgentVisualState.Defiant;
                if (YieldingTimer > 0f) return AgentVisualState.Yielding;
                if (SharingTimer > 0f) return AgentVisualState.Sharing;
                if (GossipTimer > 0f) return AgentVisualState.Gossiping;
                if (RallyTimer > 0f) return AgentVisualState.Rallying;
                return _baseVisualState;
            }
            set => _baseVisualState = value;
        }

        public AgentState()
        {
            try
            {
                AgentId = GUID.Generate();
            }
            catch
            {
                AgentId = default;
            }

            Inventory = new AgentInventory(AgentInventory.DefaultCapacity, startingGold: Simpiens.Simulation.Economy.EconomyTuning.InventoryDefaultGold);
            Memory = new AgentMemory();
        }

        public AgentState(GUID id, AgentInventory inventory = null, AgentMemory memory = null)
        {
            AgentId = id;
            Inventory = inventory ?? new AgentInventory(AgentInventory.DefaultCapacity, startingGold: Simpiens.Simulation.Economy.EconomyTuning.InventoryDefaultGold);
            Memory = memory ?? new AgentMemory();
        }

        /// <summary>
        /// Advance biological drives and emotion timers on fixed logical tick delta.
        /// </summary>
        public void Tick(float dt)
        {
            if (YieldingTimer > 0f) YieldingTimer -= dt;
            if (SharingTimer > 0f) SharingTimer -= dt;
            if (GossipTimer > 0f) GossipTimer -= dt;
            if (RallyTimer > 0f) RallyTimer -= dt;
            if (DefiantTimer > 0f) DefiantTimer -= dt;

            if (TantrumTimer > 0f)
            {
                TantrumTimer -= dt;
                if (TantrumTimer <= 0f)
                {
                    TantrumTimer = 0f;
                    VisualState = AgentVisualState.Idle;
                }
                else
                {
                    VisualState = AgentVisualState.Panicking;
                    return;
                }
            }

            // Biological drive decay
            float hunger = Mathf.Min(100f, Needs.Hunger + dt * 1.5f);
            float energy = Needs.Energy;

            bool inBunk = HasActivePlan && ActivePlan.CurrentAction is Simpiens.Cognition.Planning.Actions.SleepAtBunkAction;
            if (IsExhaustionCollapsed || inBunk || (HasActivePlan && ActivePlan.CurrentAction is Simpiens.Cognition.Planning.Actions.RestAction))
            {
                // A bunk restores energy faster than resting where you stand (Epic MAP Phase 3 rest anchor)
                float rate = inBunk && !IsExhaustionCollapsed ? Simpiens.Cognition.Planning.Considerations.BunkRecoveryPerSecond : 15f;
                energy = Mathf.Min(100f, energy + dt * rate);
                if (IsExhaustionCollapsed && energy >= 30f)
                {
                    SimLog.Drives.ZLogInformation($"{SimLog.Agent(AgentId)} recovered from exhaustion collapse (energy {energy:F0})");
                    IsExhaustionCollapsed = false;
                    VisualState = AgentVisualState.Idle;
                }
            }
            else if (VisualState == AgentVisualState.Walking)
            {
                energy = Mathf.Max(0f, energy - dt * 1.0f);
            }
            else
            {
                energy = Mathf.Max(0f, energy - dt * 0.2f);
            }

            float social = Mathf.Max(0f, Needs.Social - dt * 0.5f);
            Needs = new AgentNeeds(hunger, energy, social);
            Frustration = Mathf.Max(0f, Frustration - dt * 1f);

            // Emergency preemption check
            bool emergencyExhaustion = energy <= MentalBreakEvaluator.ExhaustionThreshold;
            if (emergencyExhaustion)
            {
                if (!IsExhaustionCollapsed)
                {
                    SimLog.Drives.ZLogInformation($"{SimLog.Agent(AgentId)} collapsed from exhaustion (hunger {hunger:F0}, social {social:F0})");
                }
                IsExhaustionCollapsed = true;
                VisualState = AgentVisualState.Resting;
            }
        }

        // A social look replaces the base look, except a bunk sleeper's (only waking ends that)
        private void SetSocialVisual(AgentVisualState state)
        {
            if (_baseVisualState != AgentVisualState.Sleeping) _baseVisualState = state;
        }

        public void TriggerGossipVisual(float timestamp = 0f, float duration = 1.5f)
        {
            LastGossipTimestamp = timestamp;
            GossipTimer = duration > 0f ? duration : 1.5f;
            SetSocialVisual(AgentVisualState.Gossiping);
        }

        public void TriggerYieldVisual(float timestamp = 0f, float duration = 1.2f)
        {
            LastYieldTimestamp = timestamp;
            YieldingTimer = duration > 0f ? duration : 1.2f;
            SetSocialVisual(AgentVisualState.Yielding);
        }

        public void TriggerTantrumVisual(float timestamp = 0f, float duration = 2.5f)
        {
            SimLog.Drives.ZLogInformation($"{SimLog.Agent(AgentId)} tantrum (frustration {Frustration:F0})");
            LastTantrumTimestamp = timestamp;
            TantrumTimer = duration > 0f ? duration : 2.5f;
            VisualState = AgentVisualState.Panicking;
        }

        public void TriggerSharingVisual(float duration = 1.2f)
        {
            SharingTimer = duration > 0f ? duration : 1.2f;
            SetSocialVisual(AgentVisualState.Sharing);
        }

        public void TriggerRallyVisual(float duration = 2.0f)
        {
            RallyTimer = duration;
            SetSocialVisual(AgentVisualState.Rallying);
        }

        public void TriggerDefiantVisual(float duration = 1.5f)
        {
            DefiantTimer = duration;
            SetSocialVisual(AgentVisualState.Defiant);
        }

        public void TriggerReliefVisual()
        {
            if (IsExhaustionCollapsed) IsExhaustionCollapsed = false;
            if (TantrumTimer > 0f) TantrumTimer = 0f;
            VisualState = AgentVisualState.Idle;
        }

        public void CollapseFromExhaustion()
        {
            Energy = 0f;
            IsExhaustionCollapsed = true;
            VisualState = AgentVisualState.Resting;
        }

        public void RelieveFrustration(float amount)
        {
            Frustration = Mathf.Max(0f, Frustration - amount);
        }

        /// <param name="resetVisual">False when the same evaluation immediately issues a replacement intent
        /// (reflex pre-emption, invalid step): resetting to Idle there only flashes Thinking > Idle > next state.</param>
        public void AbortActivePlan(bool resetVisual = true)
        {
            ActivePlan.Clear();
            ActiveGoal = null;
            CurrentIntent = null;
            if (resetVisual && !IsExhaustionCollapsed && TantrumTimer <= 0f && SharingTimer <= 0f && !IsStarvationPanicking)
            {
                VisualState = AgentVisualState.Idle;
            }
        }

        /// <summary>
        /// Whether a ration could be bought at a reachable stall at the last evaluation (main thread). A starving
        /// crawler standing at the stall with the gold for a ration used to panic instead of buying one, because
        /// only remembered food plots counted as known food (Epic MAP Phase 3).
        /// </summary>
        public bool CanBuyFoodAtStall { get; set; }

        public bool HasKnownFood(uint currentTick = 0)
        {
            if (HasFoodInInventory) return true;
            if (CanBuyFoodAtStall) return true;
            if (Memory == null || Memory.SpatialMemoryMap == null) return false;

            foreach (var kvp in Memory.SpatialMemoryMap)
            {
                var record = kvp.Value;
                if (record.Type == EntityType.Resource && record.ResourceKind.IsFoodYield() && !Memory.IsBlacklisted(record.EntityId, currentTick))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// This crawler's plain state for a save (Milestone 1 save/load). The current intent, plan, goal, last decision
        /// and look are work in progress and are left out: the crawler decides afresh after a load.
        /// </summary>
        public CrawlerRecord CaptureRecord()
        {
            return new CrawlerRecord
            {
                Id = AgentId,
                Position = Position,
                Needs = Needs,
                Frustration = Frustration,
                Activity = Activity,
                Membership = Membership,
                Identity = Identity,
                Personality = Personality,
                NavigationalStalls = NavigationalStalls,
                TantrumTimer = TantrumTimer,
                YieldingTimer = YieldingTimer,
                GossipTimer = GossipTimer,
                SharingTimer = SharingTimer,
                RallyTimer = RallyTimer,
                DefiantTimer = DefiantTimer,
                LastGossipTimestamp = LastGossipTimestamp,
                LastYieldTimestamp = LastYieldTimestamp,
                LastTantrumTimestamp = LastTantrumTimestamp,
                LastEvaluatedDirectiveTick = LastEvaluatedDirectiveTick,
                ScoutReportTick = ScoutReportTick,
                ScoutReportBountyId = ScoutReportBountyId,
                IsExhaustionCollapsed = IsExhaustionCollapsed,
                CanBuyFoodAtStall = CanBuyFoodAtStall
            };
        }

        public AgentContext CreateContext(SharedWorldSnapshot snapshot, uint currentTick)
        {
            var memorySnapshot = Memory != null ? Memory.GetSnapshot(currentTick) : null;
            var inventorySnapshot = Inventory != null ? Inventory.GetSnapshot() : null;

            return new AgentContext(
                AgentId,
                Position,
                Needs,
                Frustration,
                snapshot,
                memorySnapshot,
                currentTick,
                Membership,
                Identity,
                inventorySnapshot,
                ScoutReportBountyId,
                Personality);
        }
    }
}
