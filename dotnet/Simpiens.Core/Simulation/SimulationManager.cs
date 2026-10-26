using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Simpiens.Cognition;
using Simpiens.Cognition.Contracts;
using Simpiens.Cognition.Groups;
using Simpiens.Cognition.Pathfinding;
using Simpiens.Diagnostics;
using Simpiens.Intervention;
using Simpiens.Narrative;
using Simpiens.Simulation;
using Simpiens.Simulation.Economy;
using Simpiens.Simulation.Spatial;
using Simpiens.Simulation.World;
using ZLogger;

namespace Simpiens.Simulation
{
    public class ActiveIntentState
    {
        public AgentIntent Intent;
        public int CurrentWaypointIndex;
        public float ElapsedTime;

        // Motor Watchdog Tracking
        public Vec2 LastSampledPosition;
        public float TimeSinceLastProgress;
        public int StallCount;
        public bool UseRelaxedClearance;

        // Contention Tracking
        public EntityId ContestedCompetitorId;
    }

    public readonly struct AnchorData
    {
        public readonly EntityId EntityId;
        public readonly Vec2 Position;
        public readonly float Radius;

        public AnchorData(EntityId entityId, Vec2 position, float radius = 3.0f)
        {
            EntityId = entityId;
            Position = position;
            Radius = radius;
        }
    }

    /// <summary>
    /// Handles the real-time simulation tick, driven strictly by logical ticks (ISimulationClock at 10 Hz).
    /// Decoupled from Unity's render frames to guarantee multiplayer-ready determinism and consistent speeds.
    /// </summary>
    public class SimulationManager : ISimulationManager, IIntentExecutionContext, ISupplyHost, System.IDisposable
    {
        public const float FixedTickDeltaTime = 0.1f;
        float IIntentExecutionContext.FixedTickDeltaTime => FixedTickDeltaTime;
        private long _fallbackTick = 0;

        private readonly IWorldRegistry _worldRegistry;
        private readonly ISpatialPartition _spatialPartition;
        private readonly ISimulationClock _clock;
        private readonly IGroupRegistry _groupRegistry;
        private readonly INarrativeChronicleService _chronicleService;
        private readonly ISectorRegistry _sectors;
        private readonly IPlayerInterventionService _interventionService;
        private readonly IAgentRegistry _agentRegistry;
        private readonly IAgentConsequenceService _consequenceService;
        private readonly IEconomyLedger _economyLedger;
        private readonly IIntentHandlerRegistry _intentRegistry;
        private readonly ISupplyService _supplyService;
        private EconomyTuning _economy = EconomyTuning.Default;

        private readonly ConcurrentQueue<AgentIntent> _intentQueue = new ConcurrentQueue<AgentIntent>();
        
        public static readonly EntityId PlayerEntityId = Simpiens.Simulation.WorldRegistry.PlayerEntityId;

        // Main thread tracking stores
        private readonly Dictionary<EntityId, ResourceData> _resources = new Dictionary<EntityId, ResourceData>();
        private readonly Dictionary<EntityId, WorldEntity> _dormantNodes = new Dictionary<EntityId, WorldEntity>();
        private readonly Dictionary<EntityId, AnchorData> _anchors = new Dictionary<EntityId, AnchorData>();
        private readonly Dictionary<EntityId, TradeStallData> _stalls = new Dictionary<EntityId, TradeStallData>();
        private readonly List<TradeStallData> _activeStallsBuffer = new List<TradeStallData>(8);
        private readonly Dictionary<ushort, BountyContract> _bounties = new Dictionary<ushort, BountyContract>();
        // The next generated bounty id. Not saved: a load derives it as the highest saved id + 1, which is exact because
        // nothing removes a bounty from _bounties (finished ones stay, and are saved) and every id taken raises it (until
        // the 65,535 ids run out and generation wraps to fill gaps)
        private ushort _nextBountyId = 1;
        private readonly List<BountyContract> _activeBountiesBuffer = new List<BountyContract>(16);
        private readonly List<ushort> _expiredBountiesBuffer = new List<ushort>(16);
        private readonly Dictionary<EntityId, ActiveIntentState> _activeIntents = new Dictionary<EntityId, ActiveIntentState>();
        // Deterministic ordering (ROADMAP §1A.1 rule 3): active intents execute in agent-id order, never Dictionary order
        private readonly List<EntityId> _activeIntentOrder = new List<EntityId>(64);
        private readonly List<AgentIntent> _pendingIntents = new List<AgentIntent>(64);
        private readonly List<EntityId> _keysToRemove = new List<EntityId>(64);
        private readonly List<WorldEntity> _hearthPawnsBuffer = new List<WorldEntity>(32);
        private readonly List<ISocialAgent> _hearthAgentsBuffer = new List<ISocialAgent>(32);
        private readonly List<AnchorData> _activeAnchorsBuffer = new List<AnchorData>(16);

        public bool IsPaused { get; private set; }
        public IWorldRegistry WorldRegistry => _worldRegistry;
        public IAgentRegistry Agents => _agentRegistry;
        public IGroupRegistry GroupRegistry => _groupRegistry;
        public IGroupRegistry Groups => _groupRegistry;
        public INarrativeChronicleService Chronicle => _chronicleService;
        public INarrativeChronicleService ChronicleService => _chronicleService;
        public IEconomyLedger EconomyLedger => _economyLedger;
        public ISupplyService Supply => _supplyService;
        public EconomyTuning Economy => _economy;
        public uint CurrentTick => _clock != null ? (uint)_clock.CurrentTick : (uint)_fallbackTick;
        public int IntentFaults { get; private set; }
        public string FirstIntentFault { get; private set; }
        public IReadOnlyList<TradeStallData> ActiveStalls => _activeStallsBuffer;
        public IReadOnlyList<BountyContract> ActiveBounties => _activeBountiesBuffer;
        internal Dictionary<EntityId, ActiveIntentState> ActiveIntents => _activeIntents;
        IReadOnlyDictionary<EntityId, ActiveIntentState> IIntentExecutionContext.ActiveIntents => _activeIntents;
        EntityId IIntentExecutionContext.PlayerEntityId => PlayerEntityId;

        public bool TryGetResource(EntityId resourceId, out ResourceData resource) => _resources.TryGetValue(resourceId, out resource);
        public void RemoveResource(EntityId resourceId) => _resources.Remove(resourceId);

        public void DepleteResource(EntityId resourceId)
        {
            var node = _worldRegistry?.GetEntity(resourceId);

            if (_resources.TryGetValue(resourceId, out var resource) && resource.RegrowTicks > 0 && _supplyService != null)
            {
                // Dormant: hidden from perception and harvesting until the supply service regrows it
                uint tick = CurrentTick;
                resource.IsDormant = true;
                resource.RemainingYield = 0;
                resource.DepletedAtTick = tick;
                _supplyService.RegisterDormant(resourceId, tick + resource.RegrowTicks);
                SimLog.Simulation.ZLogDebug($"Resource {SimLog.Agent(resourceId)} ({resource.YieldItem}) depleted -> dormant until t{tick + resource.RegrowTicks}");

                if (node != null)
                {
                    _dormantNodes[resourceId] = node;
                    _worldRegistry.Unregister(node);
                }
                return;
            }

            _resources.Remove(resourceId);
            if (node != null)
            {
                _worldRegistry.Unregister(node);
            }
            SimLog.Simulation.ZLogDebug($"Resource {SimLog.Agent(resourceId)} depleted and removed permanently (no regrowth)");
        }

        public void ReactivateResource(EntityId resourceId)
        {
            if (_dormantNodes.TryGetValue(resourceId, out var node))
            {
                _dormantNodes.Remove(resourceId);
                if (node != null)
                {
                    _worldRegistry?.Register(node);
                }
                SimLog.Simulation.ZLogDebug($"Resource {SimLog.Agent(resourceId)} reactivated in the world");
            }
        }

        public void MarkSectorExplored(int sectorId)
        {
            _sectors?.SetExplored(sectorId, true);
            SimLog.Economy.ZLogInformation($"Sector {sectorId} marked explored");
        }

        private readonly List<Simpiens.Cognition.Memory.SpatialMemoryRecord> _surveyBuffer = new List<Simpiens.Cognition.Memory.SpatialMemoryRecord>(16);

        /// <summary>
        /// A scout's successful report publishes its survey of the sector: everything it noted inside the fog goes
        /// into every agent's spatial memory (a newer record is kept), in entity id order. Main thread.
        /// </summary>
        public int PublishSurvey(EntityId scoutId, int sectorId)
        {
            var scoutMemory = FindMemory(scoutId);
            if (scoutMemory == null) return 0;

            _surveyBuffer.Clear();
            scoutMemory.TakeSurveyNotes((byte)sectorId, _surveyBuffer);
            if (_surveyBuffer.Count == 0) return 0;
            SortStable(_surveyBuffer, (a, b) => a.EntityId.CompareTo(b.EntityId));

            int recipients = 0;
            if (_agentRegistry != null)
            {
                var agents = _agentRegistry.AllAgents;
                for (int i = 0; i < agents.Count; i++)
                {
                    if (agents[i]?.Memory == null) continue;
                    agents[i].Memory.ReceiveSurvey(_surveyBuffer);
                    recipients++;
                }
            }
            else
            {
                scoutMemory.ReceiveSurvey(_surveyBuffer);
                recipients = 1;
            }

            SimLog.Economy.ZLogInformation($"{SimLog.Agent(scoutId)} charted sector {sectorId}: {_surveyBuffer.Count} landmarks shared with {recipients} agents");
            return _surveyBuffer.Count;
        }

        // --- Rest anchor: bunks (Epic MAP Phase 3) ---
        /// <summary>Slack on top of BunkTargeting.ReachDistance for a sleeper's final step onto the bunk.</summary>
        public const float BunkClaimTolerance = 0.25f;

        /// <summary>A sleeper this close to the bunk's position is lying in it.</summary>
        public const float BunkSettleDistance = 0.02f;

        private readonly Dictionary<EntityId, Vec2> _bunks = new Dictionary<EntityId, Vec2>(16);
        private readonly Dictionary<EntityId, EntityId> _bunkOccupants = new Dictionary<EntityId, EntityId>(16);
        private readonly List<EntityId> _occupiedBunks = new List<EntityId>(16);
        private readonly Dictionary<EntityId, int> _bunkSleeps = new Dictionary<EntityId, int>(16);
        private readonly List<EntityId> _bunkScratch = new List<EntityId>(16);

        public void RegisterBunk(EntityId id, Vec2 position) => _bunks[id] = position;

        /// <summary>Bunk id to sleeper id, for the bunks taken right now.</summary>
        public IReadOnlyDictionary<EntityId, EntityId> BunkOccupants => _bunkOccupants;

        /// <summary>Completed bunk sleeps per agent this session.</summary>
        public IReadOnlyDictionary<EntityId, int> BunkSleeps => _bunkSleeps;

        public bool TryClaimBunk(EntityId bunkId, EntityId agentId, Vec2 agentPosition)
        {
            if (!_bunks.TryGetValue(bunkId, out var bunkPosition)) return false;
            float reach = Simpiens.Cognition.Planning.BunkTargeting.ReachDistance + BunkClaimTolerance;
            if ((bunkPosition - agentPosition).SqrMagnitude > reach * reach) return false;
            if (_bunkOccupants.TryGetValue(bunkId, out var occupant) && occupant != agentId) return false;
            _bunkOccupants[bunkId] = agentId;
            RebuildOccupiedBunks();
            return true;
        }

        public IntentResult StepOntoBunk(WorldEntity node, EntityId bunkId)
        {
            if (node == null || !_bunks.TryGetValue(bunkId, out var bunkPosition)) return IntentResult.TargetMissing;
            if (_bunkOccupants.TryGetValue(bunkId, out var occupant) && occupant != node.Id) return IntentResult.TargetMissing;

            // Walk to the bunk's exact position (at walking speed, so the view never sees a jump): the sleeper lies in it
            if ((bunkPosition - (Vec2)node.Position).SqrMagnitude <= BunkSettleDistance * BunkSettleDistance) return IntentResult.Success;

            Vec2 candidate = Vec2.MoveTowards(node.Position, bunkPosition, node.BaseSpeed * FixedTickDeltaTime);
            // Someone still standing in it (a sleeper just up): wait, within the handler's bounded approach, rather than
            // lie down on them (Epic 10 G0: walkers reach occupied bunks now, and stacked there)
            if (IsCrowdedMove(node, candidate)) return IntentResult.InProgress;
            if (!TryDisplacePawn(node, candidate, respectPawns: false)) return IntentResult.PathBlocked;
            return (bunkPosition - candidate).SqrMagnitude <= BunkSettleDistance * BunkSettleDistance ? IntentResult.Success : IntentResult.InProgress;
        }

        private readonly List<ushort> _departingClaims = new List<ushort>(2);
        private readonly List<EntityId> _departingBunks = new List<EntityId>(1);

        public void DepartOutpost(WorldEntity node)
        {
            var agent = node?.Agent;
            if (agent == null || agent.HasLeft) return;
            uint tick = _clock != null ? (uint)_clock.CurrentTick : 0;

            var inventory = agent.Inventory;
            int gold = inventory != null ? inventory.Gold : 0;
            if (gold > 0)
            {
                _economyLedger?.RecordSink((uint)gold, "DrifterDepartures");
                inventory.Gold = 0;
            }

            _departingClaims.Clear();
            for (int i = 0; i < _activeBountiesBuffer.Count; i++)
            {
                var bounty = _activeBountiesBuffer[i];
                if (bounty.Status == BountyStatus.Claimed && bounty.ClaimantId == node.Id) _departingClaims.Add(bounty.BountyId);
            }
            for (int i = 0; i < _departingClaims.Count; i++)
            {
                if (_bounties.TryGetValue(_departingClaims[i], out var claimed)) UpdateBounty(claimed.WithClaimant(default, BountyStatus.Open));
            }

            _departingBunks.Clear();
            foreach (var kvp in _bunkOccupants)
            {
                if (kvp.Value == node.Id) _departingBunks.Add(kvp.Key);
            }
            for (int i = 0; i < _departingBunks.Count; i++) ReleaseBunk(_departingBunks[i], node.Id);

            agent.HasLeft = true;
            SimLog.Economy.ZLogInformation($"{SimLog.Agent(node.Id)} left by the Road Gate with {gold}g ({_departingClaims.Count} claim(s) released)");
            _chronicleService?.RecordEvent(new ChronicleEvent(ChronicleEventType.DrifterLeft, node.Id, default, GetSectorForPosition(node.Position), tick));
        }

        public void ReleaseBunk(EntityId bunkId, EntityId agentId)
        {
            if (_bunkOccupants.TryGetValue(bunkId, out var occupant) && occupant == agentId)
            {
                _bunkOccupants.Remove(bunkId);
                RebuildOccupiedBunks();
            }
        }

        private readonly Dictionary<ItemType, int> _harvested = new Dictionary<ItemType, int>(4);

        public void RecordHarvest(ItemType item)
        {
            _harvested.TryGetValue(item, out int n);
            _harvested[item] = n + 1;
        }

        /// <summary>Units of the item harvested this session.</summary>
        public int HarvestedUnits(ItemType item) => _harvested.TryGetValue(item, out int n) ? n : 0;

        public void RecordBunkSleep(EntityId agentId)
        {
            _bunkSleeps.TryGetValue(agentId, out int n);
            _bunkSleeps[agentId] = n + 1;
        }

        /// <summary>
        /// Drops every claim whose sleeper is no longer sleeping in that bunk (preempted, cancelled or removed), so a
        /// bunk can never stay taken by someone who left it. Runs every tick before the snapshot.
        /// </summary>
        internal void ValidateBunkOccupancy()
        {
            if (_bunkOccupants.Count == 0) return;
            _bunkScratch.Clear();
            foreach (var kvp in _bunkOccupants)
            {
                bool sleeping = _activeIntents.TryGetValue(kvp.Value, out var state)
                    && state.Intent is SleepAtBunkIntent sleep && sleep.BunkId == kvp.Key;
                if (!sleeping) _bunkScratch.Add(kvp.Key);
            }
            if (_bunkScratch.Count == 0) return;
            for (int i = 0; i < _bunkScratch.Count; i++)
            {
                // A sleeper pulled out of bed (preempted) gets up, unless its new intent already set its own look
                if (_agentRegistry != null && _agentRegistry.TryGetAgent(_bunkOccupants[_bunkScratch[i]], out var sleeper)
                    && sleeper.VisualState == Simpiens.Entities.AgentVisualState.Sleeping)
                {
                    sleeper.VisualState = Simpiens.Entities.AgentVisualState.Idle;
                }
                _bunkOccupants.Remove(_bunkScratch[i]);
            }
            RebuildOccupiedBunks();
        }

        private void RebuildOccupiedBunks()
        {
            _occupiedBunks.Clear();
            foreach (var kvp in _bunkOccupants) _occupiedBunks.Add(kvp.Key);
        }

        private Simpiens.Cognition.Memory.AgentMemory FindMemory(EntityId agentId)
        {
            if (_agentRegistry != null && _agentRegistry.TryGetAgent(agentId, out var state)) return state.Memory;
            var node = _worldRegistry?.GetEntity(agentId);
            if (node == null) return null;
            if (node.Agent != null) return node.Agent.Memory;
            return node.Agent != null ? node.Agent.Memory : null;
        }
        public bool TryGetStall(EntityId stallId, out TradeStallData stall) => _stalls.TryGetValue(stallId, out stall);
        public void UpdateStall(TradeStallData stall)
        {
            _stalls[stall.StallEntityId] = stall;
            UpdateActiveStallsBuffer();
        }
        public void UpdateBounty(BountyContract bounty)
        {
            _bounties[bounty.BountyId] = bounty;
            UpdateActiveBountiesBuffer();
        }
        int IIntentExecutionContext.GetSectorForPosition(Vec2 position) => GetSectorForPosition(position);

        public SimulationManager(
            IWorldRegistry worldRegistry,
            ISpatialPartition spatialPartition,
            ISimulationClock clock = null,
            IGroupRegistry groupRegistry = null,
            INarrativeChronicleService chronicleService = null,
            ISectorRegistry sectors = null,
            IPlayerInterventionService interventionService = null,
            IAgentRegistry agentRegistry = null,
            IAgentConsequenceService consequenceService = null,
            IEconomyLedger economyLedger = null,
            IIntentHandlerRegistry intentRegistry = null,
            ISupplyService supplyService = null)
        {
            _worldRegistry = worldRegistry;
            _spatialPartition = spatialPartition;
            _clock = clock;
            _groupRegistry = groupRegistry;
            _chronicleService = chronicleService;
            _sectors = sectors;
            _interventionService = interventionService;
            _agentRegistry = agentRegistry ?? new Simpiens.Simulation.AgentRegistry();
            _consequenceService = consequenceService ?? new Simpiens.Cognition.AgentConsequenceService(_groupRegistry, _spatialPartition, _chronicleService);
            _economyLedger = economyLedger ?? new EconomyLedger();
            _intentRegistry = intentRegistry ?? IntentHandlerRegistry.CreateDefault();
            _supplyService = supplyService ?? new SupplyService(); // keeps its own tuning until a session applies one

            if (_clock != null)
            {
                _clock.OnSimulationTick += HandleSimulationTick;
            }
        }

        public void Dispose()
        {
            if (_clock != null)
            {
                _clock.OnSimulationTick -= HandleSimulationTick;
            }
        }

        private void HandleSimulationTick(long currentTick)
        {
            SimulateTick(currentTick);
        }

        public void RecordStoryEvent(in ChronicleEvent evt)
        {
            _chronicleService?.RecordEvent(evt);
        }

        private int GetSectorForPosition(Vec2 position)
        {
            return _sectors != null ? _sectors.GetSectorAt(position).Id : 0;
        }

        public void Pause()
        {
            IsPaused = true;
            _clock?.SetTimeMultiplier(0f);
        }

        public void Resume()
        {
            IsPaused = false;
            if (_clock != null && _clock.TimeMultiplier <= 0f)
            {
                _clock.SetTimeMultiplier(1f);
            }
        }

        public void EnqueueIntent(AgentIntent intent)
        {
            _intentQueue.Enqueue(intent);
        }

        public uint SessionEpoch { get; private set; } = 1;

        public Simpiens.Cognition.CognitionScheduler Cognition { get; } = new Simpiens.Cognition.CognitionScheduler();

        public void ProcessQueuedIntents()
        {
            // Cognition finishes in thread-pool order; apply this tick's arrivals in agent-id order instead
            _pendingIntents.Clear();
            while (_intentQueue.TryDequeue(out var queued))
            {
                if (queued == null) continue;
                if (queued.SessionEpoch != 0 && queued.SessionEpoch != SessionEpoch)
                {
                    // Decided before the last respawn: its agent is gone, and the id now belongs to a new one
                    SimLog.Cognition.ZLogDebug($"Dropped a {queued.GetType().Name} decided in session {queued.SessionEpoch} (now {SessionEpoch}) for {SimLog.Agent(queued.AgentId)}");
                    continue;
                }
                _pendingIntents.Add(queued);
            }
            SortStable(_pendingIntents, (a, b) => a.AgentId.CompareTo(b.AgentId));

            for (int p = 0; p < _pendingIntents.Count; p++)
            {
                var intent = _pendingIntents[p];
                Vec2 initialPos = Vec2.Zero;
                if (_worldRegistry != null)
                {
                    var node = _worldRegistry.GetEntity(intent.AgentId);
                    if (node != null) initialPos = node.Position;
                }

                // Main-thread copy of why this intent was chosen (single writer for AgentState)
                if (_agentRegistry != null && _agentRegistry.TryGetAgent(intent.AgentId, out var decidingAgent))
                {
                    decidingAgent.LastDecision = intent.Decision;
                }

                // Register as the current active intent for this agent
                if (!_activeIntents.ContainsKey(intent.AgentId)) InsertSorted(_activeIntentOrder, intent.AgentId);
                _activeIntents[intent.AgentId] = new ActiveIntentState
                {
                    Intent = intent,
                    CurrentWaypointIndex = 0,
                    LastSampledPosition = initialPos
                };
            }
            _pendingIntents.Clear();
        }

        // Stable insertion sort: per-tick lists are small, it allocates nothing, and equal keys keep arrival order
        private static void SortStable<T>(List<T> list, System.Comparison<T> compare)
        {
            for (int i = 1; i < list.Count; i++)
            {
                var item = list[i];
                int j = i - 1;
                while (j >= 0 && compare(list[j], item) > 0)
                {
                    list[j + 1] = list[j];
                    j--;
                }
                list[j + 1] = item;
            }
        }

        private sealed class EntityIdComparer : IComparer<EntityId>
        {
            public static readonly EntityIdComparer Instance = new EntityIdComparer();
            public int Compare(EntityId x, EntityId y) => x.CompareTo(y);
        }

        private static void InsertSorted(List<EntityId> sorted, EntityId id)
        {
            int index = sorted.BinarySearch(id, EntityIdComparer.Instance);
            if (index < 0) sorted.Insert(~index, id);
        }

        private static void RemoveSorted(List<EntityId> sorted, EntityId id)
        {
            int index = sorted.BinarySearch(id, EntityIdComparer.Instance);
            if (index >= 0) sorted.RemoveAt(index);
        }

        public bool AbortIntent(EntityId agentId)
        {
            ProcessQueuedIntents();

            if (_activeIntents.TryGetValue(agentId, out var state))
            {
                _activeIntents.Remove(agentId);
                RemoveSorted(_activeIntentOrder, agentId);
                CompleteIntent(state.Intent, IntentResult.Aborted);
                return true;
            }
            return false;
        }

        public void RegisterResource(ResourceData data)
        {
            if (data.MaxYield <= 0) data.MaxYield = data.RemainingYield;
            _resources[data.EntityId] = data;
        }

        public void RegisterAnchor(EntityId id, Vec2 position, float radius = 3.0f)
        {
            _anchors[id] = new AnchorData(id, position, radius);
        }

        public void RegisterTradeStall(TradeStallData stallData)
        {
            _stalls[stallData.StallEntityId] = stallData;
            UpdateActiveStallsBuffer();
        }

        public bool TryGetTradeStall(EntityId stallId, out TradeStallData stallData)
        {
            return _stalls.TryGetValue(stallId, out stallData);
        }

        private void UpdateActiveStallsBuffer()
        {
            _activeStallsBuffer.Clear();
            foreach (var kvp in _stalls)
            {
                _activeStallsBuffer.Add(kvp.Value);
            }
            SortStable(_activeStallsBuffer, (a, b) => a.StallEntityId.CompareTo(b.StallEntityId));
        }

        /// <summary>
        /// Registers a bounty, locking its reward in escrow from the creator stall first.
        /// Bounties whose reward cannot be escrowed are rejected so delivery can never mint gold.
        /// </summary>
        public void RegisterBounty(BountyContract bounty)
        {
            TryRegisterBounty(bounty);
        }

        public bool TryRegisterBounty(BountyContract bounty)
        {
            if (bounty.BountyId == 0)
            {
                SimLog.Intervention.ZLogWarning($"Bounty rejected: BountyId cannot be 0");
                return false;
            }

            // A live bounty's id is never taken over (ROADMAP §1A.2 #19): its escrow and claimant would be lost. A finished
            // bounty's record may be replaced by an explicit id (scripted sessions and validators reuse fixed ids);
            // generated ids never reuse one
            if (_bounties.TryGetValue(bounty.BountyId, out var existing) &&
                (existing.Status == BountyStatus.Open || existing.Status == BountyStatus.Claimed))
            {
                SimLog.Intervention.ZLogWarning($"Bounty #{bounty.BountyId} rejected: the id is held by a live bounty (status {existing.Status})");
                return false;
            }

            bool isLive = bounty.Status == BountyStatus.Open || bounty.Status == BountyStatus.Claimed;

            // The target must make sense on the loaded map (unknown sectors, scouting a charted one), before any escrow
            var target = isLive ? BountyRules.CheckTarget(bounty.Type, bounty.TargetSectorId, _sectors) : BountyRules.TargetCheck.Ok;
            if (target != BountyRules.TargetCheck.Ok)
            {
                SimLog.Intervention.ZLogWarning($"Bounty #{bounty.BountyId} rejected: {BountyRules.Describe(target, bounty.TargetSectorId, _sectors)}");
                return false;
            }
            if (isLive && bounty.RewardGold > 0 && _economyLedger != null && !_economyLedger.HasEscrow(bounty.BountyId))
            {
                var stallId = bounty.CreatorStallId;
                if (stallId == default && _activeStallsBuffer.Count > 0)
                {
                    stallId = _activeStallsBuffer[0].StallEntityId;
                }

                if (stallId == default || !_stalls.TryGetValue(stallId, out var stall))
                {
                    SimLog.Intervention.ZLogWarning($"Bounty #{bounty.BountyId} rejected: no creator stall to escrow its {bounty.RewardGold}g reward");
                    return false;
                }

                if (!_economyLedger.TryLockEscrow(stallId, bounty.BountyId, bounty.RewardGold, ref stall))
                {
                    SimLog.Intervention.ZLogWarning($"Bounty #{bounty.BountyId} rejected: stall treasury {stall.TreasuryGold}g cannot escrow {bounty.RewardGold}g reward");
                    return false;
                }

                _stalls[stallId] = stall;
                UpdateActiveStallsBuffer();

                if (bounty.CreatorStallId != stallId)
                {
                    bounty = new BountyContract(
                        bounty.BountyId, bounty.Type, bounty.TargetSectorId, bounty.TargetItemType, bounty.TargetQuantity,
                        bounty.RewardGold, bounty.IssuedTick, bounty.ExpirationTick, bounty.TargetLocation,
                        bounty.ClaimantId, stallId, bounty.Status);
                }
            }

            if (bounty.TargetLocation == default)
            {
                bounty = bounty.WithTargetLocation(ResolveSectorTarget(bounty.TargetSectorId));
            }

            _bounties[bounty.BountyId] = bounty;
            if (bounty.BountyId >= _nextBountyId)
            {
                _nextBountyId = (ushort)(bounty.BountyId + 1);
            }
            UpdateActiveBountiesBuffer();
            SimLog.Intervention.ZLogInformation($"Bounty #{bounty.BountyId} registered: {bounty.Type} {bounty.TargetItemType}x{bounty.TargetQuantity} sector {bounty.TargetSectorId} at {bounty.TargetLocation}, reward {bounty.RewardGold}g, status {bounty.Status}, expires t{bounty.ExpirationTick}");
            return true;
        }

        /// <summary>
        /// Walkable target point for a sector: its anchor when defined, otherwise its bounds centre.
        /// The A* pathfinder falls back to the nearest walkable cell if the point is obstructed.
        /// </summary>
        private Vec2 ResolveSectorTarget(byte sectorId)
        {
            if (_sectors != null && sectorId != 0 && _sectors.TryGetSector(sectorId, out var sector))
            {
                return sector.TargetPoint;
            }
            return default;
        }

        public bool TryGetBounty(ushort bountyId, out BountyContract bounty)
        {
            return _bounties.TryGetValue(bountyId, out bounty);
        }

        private void UpdateActiveBountiesBuffer()
        {
            _activeBountiesBuffer.Clear();
            foreach (var kvp in _bounties)
            {
                if (kvp.Value.Status == BountyStatus.Open || kvp.Value.Status == BountyStatus.Claimed)
                {
                    _activeBountiesBuffer.Add(kvp.Value);
                }
            }
            SortStable(_activeBountiesBuffer, (a, b) => a.BountyId.CompareTo(b.BountyId));
        }

        private void ProcessInfluenceEvent(in InfluenceEvent influence, uint currentTick)
        {
            SimLog.Intervention.ZLogDebug($"Applying player command {influence.CommandType}");
            switch (influence.CommandType)
            {
                case InfluenceCommandType.PostBounty:
                    ExecutePostBountyCommand(influence.PostBounty, currentTick);
                    break;

                case InfluenceCommandType.CancelBounty:
                    ExecuteCancelBountyCommand(influence.CancelBounty, currentTick);
                    break;

                case InfluenceCommandType.UpdateStallPrices:
                    ExecuteUpdateStallPricesCommand(influence.UpdateStallPrices);
                    break;

                case InfluenceCommandType.CalmCrawler:
                    ExecuteCalmCrawlerCommand(influence.CalmCrawler);
                    break;
            }
        }

        /// <summary>The dossier's Calm Frustration, applied on the tick: what Unity's dashboard did directly to the crawler.</summary>
        private void ExecuteCalmCrawlerCommand(in CalmCrawlerCommand cmd)
        {
            var node = _worldRegistry?.GetEntity(cmd.CrawlerId);
            var agent = node?.Agent;
            if (agent == null)
            {
                SimLog.Intervention.ZLogWarning($"Calm rejected: no crawler {SimLog.Agent(cmd.CrawlerId)}");
                return;
            }
            agent.Frustration = 0f;
            agent.RelieveFrustration(100f);
            agent.TriggerReliefVisual();
            SimLog.Intervention.ZLogInformation($"Player calmed {SimLog.Agent(node.Id)} (frustration -> 0)");
        }

        private void ExecutePostBountyCommand(in PostBountyCommand cmd, uint currentTick)
        {
            ushort bountyId = cmd.BountyId;
            if (bountyId == 0 && !TryTakeNextFreeBountyId(out bountyId))
            {
                SimLog.Intervention.ZLogWarning($"Bounty rejected: every bounty id is taken");
                return;
            }
            PostBountyInternal(
                bountyId,
                cmd.Type,
                cmd.SectorId,
                cmd.ItemType,
                cmd.Quantity,
                cmd.RewardGold,
                cmd.DurationTicks,
                cmd.TargetLocation,
                cmd.CreatorStallId,
                currentTick);
        }

        // The next free id from _nextBountyId on, skipping 0 and every id on the board; false when all 65,535 are taken
        private bool TryTakeNextFreeBountyId(out ushort id)
        {
            for (int tried = 0; tried <= ushort.MaxValue; tried++)
            {
                ushort candidate = _nextBountyId++;
                if (candidate != 0 && !_bounties.ContainsKey(candidate))
                {
                    id = candidate;
                    return true;
                }
            }
            id = 0;
            return false;
        }

        private void ExecuteCancelBountyCommand(in CancelBountyCommand cmd, uint currentTick)
        {
            CancelBountyInternal(cmd.BountyId, currentTick);
        }

        private void ExecuteUpdateStallPricesCommand(in UpdateStallPricesCommand cmd)
        {
            EntityId targetStallId = cmd.StallId;
            if (targetStallId == default && _activeStallsBuffer.Count > 0)
            {
                targetStallId = _activeStallsBuffer[0].StallEntityId;
            }

            if (targetStallId != default && _stalls.TryGetValue(targetStallId, out var stall))
            {
                _stalls[targetStallId] = stall.WithPrices(
                    cmd.RationSellPrice > 0 ? cmd.RationSellPrice : stall.RationSellPrice,
                    cmd.OreBuyPrice > 0 ? cmd.OreBuyPrice : stall.OreBuyPrice,
                    cmd.HerbBuyPrice > 0 ? cmd.HerbBuyPrice : stall.HerbBuyPrice);
                if (cmd.GlowstoneBuyPrice > 0) _stalls[targetStallId] = _stalls[targetStallId].WithGlowstone(cmd.GlowstoneBuyPrice, stall.GlowstoneStock);
                if (cmd.HazardGearSellPrice > 0) _stalls[targetStallId] = _stalls[targetStallId].WithHazardGear(cmd.HazardGearSellPrice, stall.HazardGearStock);
                UpdateActiveStallsBuffer();
                var updated = _stalls[targetStallId];
                SimLog.Intervention.ZLogInformation($"Stall {SimLog.Agent(targetStallId)} prices updated: ration {updated.RationSellPrice}g, ore {updated.OreBuyPrice}g, herb {updated.HerbBuyPrice}g, glowstone {updated.GlowstoneBuyPrice}g, hazard gear {updated.HazardGearSellPrice}g");
            }
        }

        private void PostBountyInternal(
            ushort bountyId,
            BountyType type,
            byte sectorId,
            ItemType itemType,
            byte qty,
            ushort rewardGold,
            uint durationTicks,
            Vec2 targetLocation,
            EntityId creatorStallId,
            uint currentTick)
        {
            if (creatorStallId == default && _activeStallsBuffer.Count > 0)
            {
                creatorStallId = _activeStallsBuffer[0].StallEntityId;
            }

            var resolvedType = type == BountyType.None ? BountyType.GatherResource : type;
            var contract = new BountyContract(
                bountyId,
                resolvedType,
                sectorId,
                itemType == ItemType.None && resolvedType == BountyType.GatherResource ? ItemType.IronOre : itemType,
                qty == 0 ? (byte)1 : qty,
                rewardGold,
                currentTick,
                currentTick + durationTicks,
                targetLocation,
                default,
                creatorStallId,
                BountyStatus.Open);

            // Escrow is locked (or the bounty rejected) inside TryRegisterBounty
            if (!TryRegisterBounty(contract))
            {
                return;
            }

            _chronicleService?.RecordEvent(new ChronicleEvent(
                ChronicleEventType.BountyPosted,
                PlayerEntityId,
                default,
                sectorId,
                currentTick,
                ChronicleIntensity.Notable,
                rewardGold));
        }

        private void CancelBountyInternal(ushort bountyId, uint currentTick)
        {
            if (_bounties.TryGetValue(bountyId, out var bounty))
            {
                if (bounty.Status == BountyStatus.Open || bounty.Status == BountyStatus.Claimed)
                {
                    // Refund escrow gold to stall if recorded in ledger or legacy fallback
                    if (bounty.CreatorStallId != default && _stalls.TryGetValue(bounty.CreatorStallId, out var stall))
                    {
                        if (_economyLedger != null)
                        {
                            if (_economyLedger.TryRefundEscrow(bountyId, bounty.CreatorStallId, ref stall, out _))
                            {
                                _stalls[bounty.CreatorStallId] = stall;
                                UpdateActiveStallsBuffer();
                            }
                        }
                        else
                        {
                            _stalls[bounty.CreatorStallId] = stall.WithTreasury(stall.TreasuryGold + bounty.RewardGold);
                            UpdateActiveStallsBuffer();
                        }
                    }

                    _bounties[bountyId] = bounty.WithStatus(BountyStatus.Cancelled);
                    UpdateActiveBountiesBuffer();
                    SimLog.Intervention.ZLogInformation($"Bounty #{bountyId} cancelled (was {bounty.Status}, claimant {SimLog.Agent(bounty.ClaimantId)}); escrow refunded");

                    _chronicleService?.RecordEvent(new ChronicleEvent(
                        ChronicleEventType.BountyExpired,
                        PlayerEntityId,
                        bounty.ClaimantId,
                        bounty.TargetSectorId,
                        currentTick,
                        ChronicleIntensity.Trivial));
                }
            }
        }

        /// <summary>
        /// Applies a session's economy tuning (SwarmSpawner, on every respawn). The tuning is immutable, so the snapshot
        /// shares it by reference with background cognition. A null tuning falls back to the default.
        /// </summary>
        public void SetEconomy(EconomyTuning economy)
        {
            _economy = economy ?? EconomyTuning.Default;
            _supplyService?.Configure(_economy.Supply);
            _spatialPartition?.SetEconomy(_economy);
        }

        /// <summary>
        /// Writes the simulation's state into a save (Milestone 1 save/load): the stalls, every bounty and the ledger;
        /// resource nodes, the regrowth queue, the fog and the session's tallies; groups; the chronicle. Records are
        /// sorted by id. Active intents, the bunk claims and contention that depend on them, and the snapshot are not
        /// saved. Main thread, between ticks.
        /// </summary>
        public void CaptureTo(Simpiens.Simulation.Persistence.SessionSave save)
        {
            if (save == null) throw new System.ArgumentNullException(nameof(save));

            var stalls = new TradeStallData[_stalls.Count];
            int n = 0;
            foreach (var kvp in _stalls) stalls[n++] = kvp.Value;
            System.Array.Sort(stalls, (a, b) => a.StallEntityId.CompareTo(b.StallEntityId));

            var bounties = new BountyContract[_bounties.Count];
            n = 0;
            foreach (var kvp in _bounties) bounties[n++] = kvp.Value;
            System.Array.Sort(bounties, (a, b) => a.BountyId.CompareTo(b.BountyId));

            var ledger = new Simpiens.Simulation.Persistence.LedgerSave();
            _economyLedger?.CaptureTo(ledger);
            save.Economy = new Simpiens.Simulation.Persistence.EconomySave { Stalls = stalls, Bounties = bounties, Ledger = ledger };

            var resources = new Simpiens.Simulation.Persistence.ResourceRecord[_resources.Count];
            n = 0;
            foreach (var kvp in _resources) resources[n++] = new Simpiens.Simulation.Persistence.ResourceRecord(kvp.Value);
            System.Array.Sort(resources, (a, b) => a.EntityId.CompareTo(b.EntityId));

            var supply = new Simpiens.Simulation.Persistence.SupplySave();
            _supplyService?.CaptureTo(supply);

            var sleeps = new Simpiens.Simulation.Persistence.IdCountRecord[_bunkSleeps.Count];
            n = 0;
            foreach (var kvp in _bunkSleeps) sleeps[n++] = new Simpiens.Simulation.Persistence.IdCountRecord { Id = kvp.Key, Count = kvp.Value };
            System.Array.Sort(sleeps, (a, b) => a.Id.CompareTo(b.Id));

            var harvested = new Simpiens.Simulation.Persistence.ItemCountRecord[_harvested.Count];
            n = 0;
            foreach (var kvp in _harvested) harvested[n++] = new Simpiens.Simulation.Persistence.ItemCountRecord(kvp.Key, kvp.Value);
            System.Array.Sort(harvested, (a, b) => ((byte)a.Item).CompareTo((byte)b.Item));

            save.World ??= new Simpiens.Simulation.Persistence.WorldSave();
            save.World.Resources = resources;
            save.World.Supply = supply;
            save.World.ExploredSectors = _sectors?.CaptureExplored();
            save.World.BunkSleeps = sleeps;
            save.World.Harvested = harvested;

            var groups = new Simpiens.Simulation.Persistence.GroupSave();
            _groupRegistry?.CaptureTo(groups);
            save.Groups = groups;

            var chronicle = new Simpiens.Simulation.Persistence.ChronicleSave();
            _chronicleService?.CaptureTo(chronicle);
            save.Chronicle = chronicle;
        }

        /// <summary>
        /// Puts a save's simulation state back (a load), after the spawner has rebuilt the map and the crawlers: the
        /// stalls, every bounty and the ledger as saved (no escrow is locked again); resource nodes, hiding the views of
        /// dormant nodes and of nodes used up for good; the regrowth queue, the fog, the tallies, groups and the
        /// chronicle. Main thread.
        /// </summary>
        public void RestoreFrom(Simpiens.Simulation.Persistence.SessionSave save)
        {
            if (save == null) throw new System.ArgumentNullException(nameof(save));

            var economy = save.Economy;
            _stalls.Clear();
            if (economy?.Stalls != null)
            {
                for (int i = 0; i < economy.Stalls.Length; i++) _stalls[economy.Stalls[i].StallEntityId] = economy.Stalls[i];
            }
            UpdateActiveStallsBuffer();

            _bounties.Clear();
            _nextBountyId = 1;
            if (economy?.Bounties != null)
            {
                for (int i = 0; i < economy.Bounties.Length; i++)
                {
                    var b = economy.Bounties[i];
                    _bounties[b.BountyId] = b;
                    if (b.BountyId >= _nextBountyId)
                    {
                        _nextBountyId = (ushort)(b.BountyId + 1);
                    }
                }
            }
            UpdateActiveBountiesBuffer();
            if (economy?.Ledger != null) _economyLedger?.RestoreFrom(economy.Ledger);

            var world = save.World;
            RestoreResources(world?.Resources);
            if (world?.Supply != null) _supplyService?.RestoreFrom(world.Supply);
            if (world?.ExploredSectors != null) _sectors?.RestoreExplored(world.ExploredSectors);

            _bunkSleeps.Clear();
            if (world?.BunkSleeps != null)
            {
                for (int i = 0; i < world.BunkSleeps.Length; i++) _bunkSleeps[world.BunkSleeps[i].Id] = world.BunkSleeps[i].Count;
            }
            _harvested.Clear();
            if (world?.Harvested != null)
            {
                for (int i = 0; i < world.Harvested.Length; i++) _harvested[world.Harvested[i].Item] = world.Harvested[i].Count;
            }

            if (save.Groups != null) _groupRegistry?.RestoreFrom(save.Groups);
            if (save.Chronicle != null) _chronicleService?.RestoreFrom(save.Chronicle);
        }

        /// <summary>
        /// The map's spawn registered every node full. Put back each saved node's yield and regrowth, hide a dormant node's
        /// view until it regrows, and remove a node missing from the save: it was used up for good (as DepleteResource does).
        /// </summary>
        private void RestoreResources(Simpiens.Simulation.Persistence.ResourceRecord[] records)
        {
            _keysToRemove.Clear();
            foreach (var kvp in _resources) _keysToRemove.Add(kvp.Key);

            if (records != null)
            {
                for (int i = 0; i < records.Length; i++)
                {
                    var r = records[i];
                    _keysToRemove.Remove(r.EntityId);
                    if (!_resources.TryGetValue(r.EntityId, out var data))
                    {
                        data = new ResourceData { EntityId = r.EntityId };
                        _resources[r.EntityId] = data;
                    }
                    data.RemainingYield = r.RemainingYield;
                    data.MaxYield = r.MaxYield;
                    data.YieldItem = r.YieldItem;
                    data.RegrowTicks = r.RegrowTicks;
                    data.DepletedAtTick = r.DepletedAtTick;
                    data.IsDormant = r.IsDormant;
                    if (r.IsDormant) HideResourceNode(r.EntityId, dormant: true);
                }
            }

            for (int i = 0; i < _keysToRemove.Count; i++)
            {
                _resources.Remove(_keysToRemove[i]);
                HideResourceNode(_keysToRemove[i], dormant: false);
            }
            _keysToRemove.Clear();
        }

        private void HideResourceNode(EntityId resourceId, bool dormant)
        {
            var node = _worldRegistry?.GetEntity(resourceId);
            if (node == null) return;
            if (dormant) _dormantNodes[resourceId] = node;
            _worldRegistry.Unregister(node);
        }

        public void ResetState()
        {
            SessionEpoch++;

            // Decisions in flight belong to the old session: wait for them, release their snapshots, drop them
            Cognition.Reset();
            while (_intentQueue.TryDequeue(out _)) { }

            // Player commands queued before a respawn or a load belong to the old session
            if (_interventionService != null)
            {
                while (_interventionService.TryDequeueInfluence(out _)) { }
            }
            _activeIntents.Clear();
            _activeIntentOrder.Clear();
            _resources.Clear();
            _dormantNodes.Clear();
            _supplyService?.Reset();
            _economyLedger?.Reset();
            _anchors.Clear();
            _bunks.Clear();
            _bunkOccupants.Clear();
            _occupiedBunks.Clear();
            _bunkSleeps.Clear();
            _harvested.Clear();
            _stalls.Clear();
            _activeStallsBuffer.Clear();
            _bounties.Clear();
            _nextBountyId = 1;
            _activeBountiesBuffer.Clear();
            _keysToRemove.Clear();
            _hearthPawnsBuffer.Clear();
            _activeAnchorsBuffer.Clear();
            if (_groupRegistry != null)
            {
                _groupRegistry.Clear();
            }

            // Walls from the torn-down world (including injected chaos walls) must not outlive it
            _spatialPartition?.ResetStatic();
        }

        public void Tick()
        {
            if (_clock != null)
            {
                SimulateTick(_clock.CurrentTick);
            }
            else
            {
                _fallbackTick++;
                SimulateTick(_fallbackTick);
            }
        }

        public void Tick(long currentTick)
        {
            SimulateTick(currentTick);
        }

        public void SimulateTick(long tick)
        {
            if (IsPaused || (_clock != null && _clock.IsPaused)) return;

            SimLog.CurrentTick = tick;

            // Decisions dispatched on earlier ticks land now, in agent-id order, whatever order they finished in
            Cognition.ApplyDue(tick);

            // Register queued intents (cognition's decisions just applied, plus any enqueued directly)
            ProcessQueuedIntents();

            uint currentTick = (uint)tick;

            // Drain and process player interventions
            if (_interventionService != null)
            {
                while (_interventionService.TryDequeueInfluence(out var influence))
                {
                    ProcessInfluenceEvent(influence, currentTick);
                }
            }

            // Check bounty expirations
            if (_bounties.Count > 0)
            {
                _expiredBountiesBuffer.Clear();
                foreach (var kvp in _bounties)
                {
                    if (kvp.Value.IsExpired(currentTick))
                    {
                        _expiredBountiesBuffer.Add(kvp.Key);
                    }
                }

                if (_expiredBountiesBuffer.Count > 0)
                {
                    SortStable(_expiredBountiesBuffer, (a, b) => a.CompareTo(b));
                    for (int i = 0; i < _expiredBountiesBuffer.Count; i++)
                    {
                        ushort expiredId = _expiredBountiesBuffer[i];
                        var b = _bounties[expiredId];

                        if (b.CreatorStallId != default && _stalls.TryGetValue(b.CreatorStallId, out var stall))
                        {
                            if (_economyLedger != null)
                            {
                                if (_economyLedger.TryRefundEscrow(expiredId, b.CreatorStallId, ref stall, out _))
                                {
                                    _stalls[b.CreatorStallId] = stall;
                                }
                            }
                            else
                            {
                                _stalls[b.CreatorStallId] = stall.WithTreasury(stall.TreasuryGold + b.RewardGold);
                            }
                        }

                        _bounties[expiredId] = b.WithStatus(BountyStatus.Expired);
                        SimLog.Intervention.ZLogInformation($"Bounty #{expiredId} expired unfulfilled (was {b.Status}, claimant {SimLog.Agent(b.ClaimantId)}); escrow refunded");

                        _chronicleService?.RecordEvent(new ChronicleEvent(
                            ChronicleEventType.BountyExpired,
                            PlayerEntityId,
                            b.ClaimantId,
                            b.TargetSectorId,
                            currentTick,
                            ChronicleIntensity.Trivial));
                    }

                    UpdateActiveBountiesBuffer();
                    UpdateActiveStallsBuffer();
                }
            }

            // Tick group registry to handle directive expirations
            if (_groupRegistry != null)
            {
                _groupRegistry.Tick(currentTick);
            }

            // Execute active intents if registry is present. A handler that throws aborts its own intent only (ROADMAP
            // §1A.2 #17): the agents after it still run, every finished intent is removed once, and the fault is counted
            if (_worldRegistry != null)
            {
                _keysToRemove.Clear();

                try
                {
                    for (int k = 0; k < _activeIntentOrder.Count; k++)
                    {
                        var agentId = _activeIntentOrder[k];
                        if (!_activeIntents.TryGetValue(agentId, out var state)) continue;

                        // An agent whose node no longer exists is aborted
                        var node = _worldRegistry.GetEntity(agentId);
                        IntentResult result = IntentResult.Aborted;
                        if (node != null)
                        {
                            try
                            {
                                result = ExecuteIntent(node, state);
                            }
                            catch (Exception ex)
                            {
                                RecordIntentFault(ex, "executing", agentId, state.Intent, currentTick);
                                result = IntentResult.Aborted;
                            }
                        }

                        if (result != IntentResult.InProgress)
                        {
                            _keysToRemove.Add(agentId);
                            try
                            {
                                CompleteIntent(state.Intent, result);
                            }
                            catch (Exception ex)
                            {
                                RecordIntentFault(ex, "completing", agentId, state.Intent, currentTick);
                            }
                        }
                    }
                }
                finally
                {
                    int removeCount = _keysToRemove.Count;
                    for (int i = 0; i < removeCount; i++)
                    {
                        _activeIntents.Remove(_keysToRemove[i]);
                        RemoveSorted(_activeIntentOrder, _keysToRemove[i]);
                    }
                }
            }

            // Supply faucets (resource regrowth, stall caravan) in a fixed slot after intent execution
            _supplyService?.Tick(currentTick, this);

            // Evaluate Hearth Dynamics, Proximity Bonding & Succession every 50 ticks
            if (currentTick % 50 == 0)
            {
                EvaluateHearthProximity(currentTick);
                EvaluateLeadershipSuccession(currentTick);
            }

            // Generate the thread-safe global snapshot for any cognitive agents that poll this frame
            if (_spatialPartition != null && _worldRegistry != null)
            {
                if (_sectors != null) _spatialPartition.SetExploredSectors(_sectors.ExploredMask);
                ValidateBunkOccupancy();
                _spatialPartition.SetOccupiedBunks(_occupiedBunks);
                _spatialPartition.UpdateFromRegistry(_worldRegistry, _groupRegistry, _activeStallsBuffer, _activeBountiesBuffer);
            }
        }

        private void RecordIntentFault(Exception ex, string phase, EntityId agentId, AgentIntent intent, long tick)
        {
            IntentFaults++;
            FirstIntentFault ??= $"t{tick}: {SimLog.Agent(agentId)} {intent?.GetType().Name} threw while {phase}: {ex.GetType().Name}: {ex.Message}";
            SimLog.Planning.ZLogError(ex, $"{SimLog.Agent(agentId)} {intent?.GetType().Name} threw while {phase}; its intent is removed");
        }

        internal IntentResult ExecuteIntent(WorldEntity node, ActiveIntentState state)
        {
            if (_intentRegistry != null)
            {
                return _intentRegistry.ExecuteIntent(node, state, this);
            }
            return IntentResult.Success;
        }

        private const float GossipRadius = 2.5f;

        public void CheckGossipOpportunity(WorldEntity node)
        {
            var agentA = node.Agent;
            if (agentA == null || agentA.Memory == null || agentA.IsExhaustionCollapsed) return;
            if (_worldRegistry == null) return;

            uint currentTick = _clock != null ? (uint)_clock.CurrentTick : 0;

            var activeNodes = _worldRegistry.ActiveEntities;
            int count = activeNodes.Count;
            for (int i = 0; i < count; i++)
            {
                var otherNode = activeNodes[i];
                if (otherNode == node || otherNode.Type != EntityType.Pawn) continue;

                float distSqr = (otherNode.Position - node.Position).SqrMagnitude;
                if (distSqr <= GossipRadius * GossipRadius)
                {
                    var agentB = otherNode.Agent;
                    if (agentB != null && agentB.Memory != null && !agentB.IsExhaustionCollapsed)
                    {
                        if (agentA.Memory.CanGossipWith(agentB.AgentId, currentTick))
                        {
                            if (agentA.Memory.TryGossip(agentB.Memory, agentB.AgentId, agentA.AgentId, currentTick))
                            {
                                agentA.RelieveFrustration(15f);
                                agentB.RelieveFrustration(15f);

                                // Fulfill biological social need
                                agentA.Social = SimMath.Min(100f, agentA.Social + 25f);
                                agentB.Social = SimMath.Min(100f, agentB.Social + 25f);

                                agentA.TriggerGossipVisual(currentTick);
                                agentB.TriggerGossipVisual(currentTick);

                                _chronicleService?.RecordEvent(new ChronicleEvent(
                                    ChronicleEventType.CampfireGossip,
                                    agentA.AgentId,
                                    agentB.AgentId,
                                    GetSectorForPosition(node.Position),
                                    currentTick,
                                    ChronicleIntensity.Notable));

                                SimLog.Social.ZLogDebug($"Gossip: {SimLog.Agent(agentA.AgentId)} and {SimLog.Agent(agentB.AgentId)} exchanged knowledge");
                                break;
                            }
                        }
                    }
                }
            }
        }

        internal void EvaluateHearthProximity(uint currentTick)
        {
            if (_worldRegistry == null) return;

            // Collect all anchors (from registered anchors and active world nodes)
            _activeAnchorsBuffer.Clear();
            foreach (var kvp in _anchors)
            {
                _activeAnchorsBuffer.Add(kvp.Value);
            }
            SortStable(_activeAnchorsBuffer, (a, b) => a.EntityId.CompareTo(b.EntityId));

            var activeNodes = _worldRegistry.ActiveEntities;
            int totalNodes = activeNodes.Count;

            for (int i = 0; i < totalNodes; i++)
            {
                var n = activeNodes[i];
                if (n.Type == EntityType.Anchor && !_anchors.ContainsKey(n.Id))
                {
                    _activeAnchorsBuffer.Add(new AnchorData(n.Id, n.Position, 3.0f));
                }
            }

            int anchorCount = _activeAnchorsBuffer.Count;
            if (anchorCount == 0) return;

            for (int a = 0; a < anchorCount; a++)
            {
                var anchor = _activeAnchorsBuffer[a];
                _hearthPawnsBuffer.Clear();

                for (int n = 0; n < totalNodes; n++)
                {
                    var node = activeNodes[n];
                    if (node.Type == EntityType.Pawn && node.Agent != null)
                    {
                        if (Vec2.Distance(node.Position, anchor.Position) <= anchor.Radius)
                        {
                            _hearthPawnsBuffer.Add(node);
                        }
                    }
                }

                int pawnCount = _hearthPawnsBuffer.Count;
                if (pawnCount == 0) continue;

                // 1. Environmental Comfort: Quench frustration and satisfy social drive
                for (int p = 0; p < pawnCount; p++)
                {
                    var agent = _hearthPawnsBuffer[p].Agent;
                    // Double frustration decay while relaxing near the fire (relieve 1.0 extra per 50 ticks)
                    agent.RelieveFrustration(1.0f);

                    // When 2 or more pawns are gathered, replenish social drive (+1.0/sec => ~1.0 per 50 ticks)
                    if (pawnCount >= 2)
                    {
                        agent.Social = SimMath.Min(100f, agent.Social + 1.0f);
                        if (agent != null) agent.Activity.RecordHearthGathering(currentTick);
                    }
                }

                // 2. Accelerated Gossip & Passive Propinquity Bonding between pairs
                for (int i = 0; i < pawnCount; i++)
                {
                    var nodeA = _hearthPawnsBuffer[i];
                    var agentA = nodeA.Agent;
                    if (agentA.Memory == null || agentA.IsExhaustionCollapsed) continue;

                    for (int j = i + 1; j < pawnCount; j++)
                    {
                        var nodeB = _hearthPawnsBuffer[j];
                        var agentB = nodeB.Agent;
                        if (agentB.Memory == null || agentB.IsExhaustionCollapsed) continue;

                        // A. Accelerated Gossip: cooldown 100 ticks instead of 300
                        if (agentA.Memory.CanGossipWith(agentB.AgentId, currentTick, cooldownTicks: 100))
                        {
                            if (agentA.Memory.TryGossip(agentB.Memory, agentB.AgentId, agentA.AgentId, currentTick, cooldownTicks: 100))
                            {
                                agentA.RelieveFrustration(15f);
                                agentB.RelieveFrustration(15f);
                                agentA.Social = SimMath.Min(100f, agentA.Social + 25f);
                                agentB.Social = SimMath.Min(100f, agentB.Social + 25f);
                                agentA.TriggerGossipVisual(currentTick);
                                agentB.TriggerGossipVisual(currentTick);

                                _chronicleService?.RecordEvent(new ChronicleEvent(
                                    ChronicleEventType.CampfireGossip,
                                    agentA.AgentId,
                                    agentB.AgentId,
                                    GetSectorForPosition(nodeA.Position),
                                    currentTick,
                                    ChronicleIntensity.Notable));

                                SimLog.Social.ZLogDebug($"Campfire gossip: {SimLog.Agent(agentA.AgentId)} and {SimLog.Agent(agentB.AgentId)} at the hearth");
                            }
                        }

                        // B. Passive Hearth Bonding: Every 100 ticks co-present around hearth
                        if (currentTick % 100 == 0)
                        {
                            agentA.Memory.RecordInteraction(agentB.AgentId, deltaWarmth: 2, deltaTrust: 1, deltaFear: -2, deltaRespect: 0, currentTick);
                            agentB.Memory.RecordInteraction(agentA.AgentId, deltaWarmth: 2, deltaTrust: 1, deltaFear: -2, deltaRespect: 0, currentTick);

                            agentA.Memory.EventLedger?.AddEvent(new Simpiens.Cognition.Memory.MemoryEvent(
                                Simpiens.Cognition.Memory.MemoryEventType.CampfireCompanionship,
                                agentB.AgentId,
                                GridPoint.RoundToInt(nodeA.Position),
                                currentTick));

                            agentB.Memory.EventLedger?.AddEvent(new Simpiens.Cognition.Memory.MemoryEvent(
                                Simpiens.Cognition.Memory.MemoryEventType.CampfireCompanionship,
                                agentA.AgentId,
                                GridPoint.RoundToInt(nodeB.Position),
                                currentTick));

                            if (currentTick % 200 == 0)
                            {
                                _chronicleService?.RecordEvent(new ChronicleEvent(
                                    ChronicleEventType.CampfireCompanionship,
                                    agentA.AgentId,
                                    agentB.AgentId,
                                    GetSectorForPosition(nodeA.Position),
                                    currentTick,
                                    ChronicleIntensity.Trivial));
                            }
                        }
                    }
                }

                // 3. Emergent Group Formation: Check for unaligned clustering every 100 ticks
                if (_groupRegistry != null && pawnCount >= 3 && currentTick % 100 == 0)
                {
                    _hearthAgentsBuffer.Clear();
                    for (int p = 0; p < pawnCount; p++)
                    {
                        var pawn = _hearthPawnsBuffer[p];
                        ISocialAgent ag = pawn.Agent != null ? (ISocialAgent)pawn.Agent : (ISocialAgent)pawn.Agent;
                        if (ag != null && !ag.Membership.HasGroup)
                        {
                            _hearthAgentsBuffer.Add(ag);
                        }
                    }

                    if (_hearthAgentsBuffer.Count >= 3)
                    {
                        if (SuccessionEngine.TryFormEmergentGroup(anchor.Position, _hearthAgentsBuffer, _groupRegistry, currentTick, out byte newGroupId))
                        {
                            _chronicleService?.RecordEvent(new ChronicleEvent(
                                ChronicleEventType.GroupFormed,
                                _hearthAgentsBuffer[0].AgentId,
                                _hearthAgentsBuffer.Count > 1 ? _hearthAgentsBuffer[1].AgentId : default,
                                GetSectorForPosition(anchor.Position),
                                currentTick,
                                ChronicleIntensity.Dramatic));

                            SimLog.Groups.ZLogInformation($"Group {newGroupId} (settlement) formed around hearth at {anchor.Position} with {_hearthAgentsBuffer.Count} members");
                        }
                    }
                }
            }
        }

        internal void EvaluateLeadershipSuccession(uint currentTick)
        {
            if (_groupRegistry == null || _worldRegistry == null) return;

            for (byte g = 1; g <= Simpiens.Simulation.GroupRegistry.MaxGroups; g++)
            {
                if (!_groupRegistry.TryGetGroup(g, out var groupData)) continue;

                if (SuccessionEngine.IsLeaderIncapacitatedOrDiscredited(groupData.LeaderId, g, _worldRegistry, _groupRegistry, currentTick))
                {
                    var result = SuccessionEngine.ResolveSuccession(g, _groupRegistry, _worldRegistry, currentTick);
                    if (result.ChangesLeadership)
                    {
                        var eventType = result.Mode == SuccessionMode.FactionalSchism 
                            ? ChronicleEventType.GroupSchism 
                            : ChronicleEventType.LeadershipAssumed;

                        _chronicleService?.RecordEvent(new ChronicleEvent(
                            eventType,
                            result.NewLeaderId,
                            result.PreviousLeaderId,
                            GetSectorForPosition(groupData.AnchorPosition),
                            currentTick,
                            ChronicleIntensity.Crisis));

                        SimLog.Groups.ZLogInformation($"Group {g} succession via {result.Mode}: {SimLog.Agent(result.PreviousLeaderId)} -> {SimLog.Agent(result.NewLeaderId)}");
                    }
                    else if (result.Succeeded)
                    {
                        // Re-confirming the sitting leader is not news; chronicling it every 50 ticks flooded the buffer
                        SimLog.Groups.ZLogDebug($"Group {g} succession check via {result.Mode} re-confirmed leader {SimLog.Agent(result.NewLeaderId)}");
                    }
                }
            }
        }

        public IntentResult ExecutePanicMovement(WorldEntity node, PathResponse path, ref int currentIndex)
        {
            if (path == null || path.Waypoints == null || currentIndex >= path.Length)
            {
                return IntentResult.Success; // Reached end or empty
            }

            Vec2 targetPos = path.Waypoints[currentIndex];

            // Ignore occupied waypoints to physically break free from any deadlock, but never move into walls or onto pawns
            float speed = (node != null ? node.BaseSpeed : 2f) * 1.5f;
            float step = (float)(speed * FixedTickDeltaTime);
            if (!TryDisplacePawn(node, Vec2.MoveTowards(node.Position, targetPos, step)) && !TryStep(node, targetPos, step, SidestepTurns))
            {
                currentIndex++; // skip a blocked waypoint instead of grinding against the obstacle
                return currentIndex >= path.Length ? IntentResult.Success : IntentResult.InProgress;
            }

            // Check if reached the current waypoint
            if (Vec2.Distance(node.Position, targetPos) < 0.05f)
            {
                currentIndex++;
                // If this was the last waypoint, return true indicating completion
                if (currentIndex >= path.Length)
                {
                    return IntentResult.Success;
                }
            }

            return IntentResult.InProgress;
        }

        // Sidesteps for a panicking pawn whose way is blocked: 45 degrees either side, then 90, as (cos, sin) pairs
        private static readonly Vec2[] SidestepTurns =
        {
            new Vec2(0.70710677f, 0.70710677f), new Vec2(0.70710677f, -0.70710677f), new Vec2(0f, 1f), new Vec2(0f, -1f)
        };

        // A walker's way around a pawn: 45 degrees to the right first (keep right), then to the left; both gain ground
        private static readonly Vec2[] FlowTurns =
        {
            new Vec2(0.70710677f, -0.70710677f), new Vec2(0.70710677f, 0.70710677f)
        };

        /// <summary>
        /// Steps a pawn around whatever blocks its straight step toward <paramref name="targetPos"/>, trying the turns
        /// in order, each a (cos, sin) rotation of the heading; walls and pawn spacing are respected. A panicking crawler
        /// uses <see cref="SidestepTurns"/>: without it, in a crowd it skipped every waypoint and panicked again on the
        /// spot, starving there (Epic 10 G0, finding F2). A walker uses <see cref="FlowTurns"/>.
        /// </summary>
        private bool TryStep(WorldEntity node, Vec2 targetPos, float step, Vec2[] turns)
        {
            Vec2 toTarget = targetPos - (Vec2)node.Position;
            float dist = toTarget.Magnitude;
            if (dist < 0.001f) return false;
            Vec2 dir = toTarget / dist;
            for (int i = 0; i < turns.Length; i++)
            {
                Vec2 turn = turns[i];
                // Vec2 operators, not a scalar chain: each rounds when it stores, so Unity's Mono (which keeps a chain's
                // intermediates at double precision) and CoreCLR turn the same way (Epic CORE F1)
                Vec2 turned = dir * turn.X + new Vec2(-dir.Y, dir.X) * turn.Y;
                if (TryDisplacePawn(node, (Vec2)node.Position + turned * step)) return true;
            }
            return false;
        }

        public IntentResult ExecutePathMovement(WorldEntity node, PathResponse path, ActiveIntentState state)
        {
            if (path == null || path.Waypoints == null || state.CurrentWaypointIndex >= path.Length)
            {
                return IntentResult.Success; // Reached end or empty
            }

            Vec2 targetPos = path.Waypoints[state.CurrentWaypointIndex];

            // 1. Motor Watchdog Progress Tracking
            float distMoved = Vec2.Distance(node.Position, state.LastSampledPosition);
            if (distMoved >= 0.05f)
            {
                state.LastSampledPosition = node.Position;
                state.TimeSinceLastProgress = 0f;
                state.StallCount = 0;
                state.UseRelaxedClearance = false;
            }
            else
            {
                state.TimeSinceLastProgress += FixedTickDeltaTime;
                if (state.TimeSinceLastProgress >= 1.0f)
                {
                    state.StallCount++;
                    state.TimeSinceLastProgress = 0f;

                    var reflexResult = ApplyProgressiveUnstuckReflex(node, targetPos, state);
                    if (reflexResult != IntentResult.InProgress)
                    {
                        return reflexResult;
                    }
                }
            }

            bool isFinalWaypoint = state.CurrentWaypointIndex == path.Length - 1;
            bool nearWaypoint = Vec2.Distance(node.Position, targetPos) <= NavConstants.ArrivalTolerance;
            bool nearFinal = isFinalWaypoint && nearWaypoint;

            // 2. Path Invalidation: Check if next step is blocked. Only near the waypoint: a smoothed path's next
            // waypoint is often the destination, and a pawn standing there froze a crawler across the map (Epic 10 G0,
            // finding F2); further away, IsCrowdedMove below keeps pawns apart
            float clearance = state.UseRelaxedClearance ? 0.2f : 0.4f;
            if (nearWaypoint && IsPositionBlocked(targetPos, node.Id, clearance))
            {
                // Someone already stands on our destination and we're close: arrive here rather than stack on them
                if (nearFinal) return IntentResult.Success;

                // Do not advance position into the obstructed target this tick.
                // Stationary state will accumulate TimeSinceLastProgress in Section 1.
                // If blocked continuously for >= 1.0s, the progressive watchdog reflex will handle unsticking cleanly.
                return IntentResult.InProgress;
            }

            // 3. Move the pawn visually and logically, keeping clear of walls and other pawns; a downed crawler limps
            float speed = node != null ? node.BaseSpeed : 2f;
            if (node?.Agent != null && node.Agent.IsDowned) speed = (float)(speed * Vitality.DownedSpeedFactor);
            float step = (float)(speed * FixedTickDeltaTime);
            Vec2 candidate = Vec2.MoveTowards(node.Position, targetPos, step);
            if (IsCrowdedMove(node, candidate))
            {
                if (nearFinal) return IntentResult.Success;
                // Flow around: a step 45 degrees to the right, else to the left, still gains on the waypoint. Two crawlers
                // meeting head-on in a doorway both keep right and pass instead of standing nose to nose (Epic 10 G1:
                // a jam in the Garden door starved a crawler); the watchdog resolves what this can't
                if (!TryStep(node, targetPos, step, FlowTurns)) return IntentResult.InProgress;
            }
            else if (!TryDisplacePawn(node, candidate, respectPawns: false))
            {
                return IntentResult.InProgress; // static wall ahead; watchdog takes over
            }

            // Check if reached the current waypoint
            if (Vec2.Distance(node.Position, targetPos) < 0.05f)
            {
                state.CurrentWaypointIndex++;
                state.LastSampledPosition = node.Position;
                state.TimeSinceLastProgress = 0f;
                state.StallCount = 0;
                state.UseRelaxedClearance = false;

                // If this was the last waypoint, return true indicating completion
                if (state.CurrentWaypointIndex >= path.Length)
                {
                    return IntentResult.Success;
                }
            }

            return IntentResult.InProgress;
        }

        internal IntentResult ApplyProgressiveUnstuckReflex(WorldEntity node, Vec2 targetPos, ActiveIntentState state)
        {
            Vec2 toTarget = targetPos - (Vec2)node.Position;
            float dist = toTarget.Magnitude;
            Vec2 dir = dist > 0.001f ? toTarget / dist : Vec2.Up;

            if (state.StallCount <= 2)
            {
                // Tier 1: Local Clearance Nudge (Orthogonal shift to break symmetrical collider face-offs)
                // StallCount 1: shifts +0.2f along perpendicular.
                // StallCount 2: shifts -0.4f to test the opposite flank (-0.2f relative to original centerline).
                float displacement = (state.StallCount == 1) ? 0.2f : -0.4f;
                Vec2 perpendicular = new Vec2(-dir.Y, dir.X) * displacement;
                if (SimLog.Traces(SimLog.Navigation, Microsoft.Extensions.Logging.LogLevel.Debug, node.Id))
                {
                    SimLog.Navigation.ZLogDebug($"Watchdog tier 1 ({state.StallCount}): {SimLog.Agent(node.Id)} stalled at {node.Position} toward {targetPos}; nudging {perpendicular}");
                }
                // Wall-checked: try the chosen flank, then the mirrored one, else stay put and escalate next stall
                if (!TryDisplacePawn(node, node.Position + perpendicular))
                {
                    TryDisplacePawn(node, (Vec2)node.Position - perpendicular);
                }
                state.LastSampledPosition = node.Position;
                return IntentResult.InProgress;
            }
            else if (state.StallCount == 3)
            {
                // Tier 2: Relaxed Clearance Repath
                state.UseRelaxedClearance = true;
                if (SimLog.Traces(SimLog.Navigation, Microsoft.Extensions.Logging.LogLevel.Debug, node.Id))
                {
                    SimLog.Navigation.ZLogDebug($"Watchdog tier 2: {SimLog.Agent(node.Id)} stalled at {node.Position}; relaxing pawn clearance");
                }
                return IntentResult.InProgress;
            }
            else
            {
                // Tier 3: Target Disengage & Blacklist Back-Off (StallCount >= 4)
                SimLog.Navigation.ZLogDebug($"Watchdog tier 3: {SimLog.Agent(node.Id)} disengaging from {targetPos} at {node.Position} ({state.Intent?.GetType().Name}); backing off 0.5m");
                TryDisplacePawn(node, (Vec2)node.Position - (dir * 0.5f));

                if (state.Intent is HarvestResourceIntent harvest)
                {
                    if (node.Agent != null && node.Agent.Memory != null)
                    {
                        uint currentTick = _clock != null ? (uint)_clock.CurrentTick : 0;
                        node.Agent.Memory.BlacklistEntity(harvest.TargetEntityId, currentTick + 300);
                    }
                }

                return IntentResult.PathBlocked;
            }
        }

        /// <summary>
        /// Moves a pawn to <paramref name="candidate"/> only if the straight move stays in statically walkable cells
        /// (exact segment traversal, no corner cutting) and, unless <paramref name="respectPawns"/> is false, does not
        /// crowd another pawn (see <see cref="IsCrowdedMove"/>). A pawn already inside a blocked cell may always move,
        /// so it can escape. Every simulation-side pawn displacement (path steps, panic, watchdog, yielding) goes through here.
        /// </summary>
        public bool TryDisplacePawn(WorldEntity node, Vec2 candidate, bool respectPawns = true)
        {
            if (node == null) return false;
            var snapshot = _spatialPartition?.GetActiveSnapshot();
            if (snapshot != null && snapshot.IsStaticWalkable(node.Position) && !snapshot.IsSegmentStaticClear(node.Position, candidate))
            {
                return false;
            }
            if (respectPawns && IsCrowdedMove(node, candidate))
            {
                return false;
            }
            node.SetPosition(candidate);
            return true;
        }

        // Snapshot positions are one tick old and every pawn moves at most ~0.3m per tick, so widen the candidate search
        private const float CrowdSearchMargin = 0.5f;

        /// <summary>
        /// True when moving <paramref name="node"/> to <paramref name="candidate"/> would bring it closer than
        /// <see cref="NavConstants.PawnSeparation"/> to another pawn without increasing the distance to it (stepping apart
        /// is always allowed). Nearby pawns come from the snapshot; their positions are read live from the registry,
        /// so pawns already moved earlier this tick are seen where they are now, not where they were.
        /// </summary>
        internal bool IsCrowdedMove(WorldEntity node, Vec2 candidate)
        {
            var snapshot = _spatialPartition?.GetActiveSnapshot();
            if (snapshot == null || node == null) return false;

            float search = NavConstants.PawnSeparation + CrowdSearchMargin;
            float offsetX = (snapshot.Width * snapshot.CellSize) * 0.5f;
            float offsetY = (snapshot.Height * snapshot.CellSize) * 0.5f;
            int minX = SimMath.Max(0, SimMath.FloorToInt((candidate.X - search + offsetX) / snapshot.CellSize));
            int maxX = SimMath.Min(snapshot.Width - 1, SimMath.FloorToInt((candidate.X + search + offsetX) / snapshot.CellSize));
            int minY = SimMath.Max(0, SimMath.FloorToInt((candidate.Y - search + offsetY) / snapshot.CellSize));
            int maxY = SimMath.Min(snapshot.Height - 1, SimMath.FloorToInt((candidate.Y + search + offsetY) / snapshot.CellSize));
            float sepSqr = NavConstants.PawnSeparation * NavConstants.PawnSeparation;
            Vec2 current = node.Position;

            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    int index = y * snapshot.Width + x;
                    int count = snapshot.CellCounts[index];
                    var cellArray = snapshot.Grid[index];
                    for (int i = 0; i < count; i++)
                    {
                        var entity = cellArray[i];
                        if (entity.Type != EntityType.Pawn || entity.Id == node.Id) continue;

                        var otherNode = _worldRegistry != null ? _worldRegistry.GetEntity(entity.Id) : null;
                        Vec2 other = otherNode != null ? otherNode.Position : entity.Position;
                        float candSqr = (candidate - other).SqrMagnitude;
                        if (candSqr >= sepSqr) continue;
                        if (candSqr > (current - other).SqrMagnitude) continue; // stepping apart is always allowed
                        return true;
                    }
                }
            }
            return false;
        }

        internal bool IsPositionBlocked(Vec2 targetPos, EntityId ignoreAgentId, float pawnClearanceRadius = 0.4f)
        {
            if (_spatialPartition == null) return false;
            var snapshot = _spatialPartition.GetActiveSnapshot();
            if (snapshot == null) return false;

            // Cast at each step: Mono keeps a scalar chain at double precision, CoreCLR rounds each step (Epic CORE F1)
            int cellX = SimMath.FloorToInt((float)((float)(targetPos.X + (float)(snapshot.Width * snapshot.CellSize * 0.5f)) / snapshot.CellSize));
            int cellY = SimMath.FloorToInt((float)((float)(targetPos.Y + (float)(snapshot.Height * snapshot.CellSize * 0.5f)) / snapshot.CellSize));

            if (cellX < 0 || cellX >= snapshot.Width || cellY < 0 || cellY >= snapshot.Height) return true; // Out of bounds

            if (snapshot.StaticGrid != null && !snapshot.StaticGrid.IsCellWalkable(cellX, cellY)) return true; // Static wall collision

            int index = cellY * snapshot.Width + cellX;
            int count = snapshot.CellCounts[index];
            var cellArray = snapshot.Grid[index];

            for (int i = 0; i < count; i++)
            {
                var entity = cellArray[i];
                if (entity.Id != ignoreAgentId)
                {
                    if (entity.Type == EntityType.Wall) return true;
                    if (entity.Type == EntityType.Anchor && Vec2.Distance(entity.Position, targetPos) < 0.45f) return true;
                    if (entity.Type == EntityType.Pawn)
                    {
                        // Dynamic validation: if another pawn is currently standing within clearance radius
                        if (Vec2.Distance(entity.Position, targetPos) < pawnClearanceRadius) return true;
                    }
                }
            }
            return false;
        }

        private void CompleteIntent(AgentIntent intent, IntentResult result)
        {
            if (intent is HarvestResourceIntent hri)
            {
                hri.Path?.ReturnToPool();
            }
            else if (intent is WanderIntent wi)
            {
                wi.Path?.ReturnToPool();
            }
            else if (intent is PanicIntent pi)
            {
                pi.Path?.ReturnToPool();
            }
            else if (intent is ScoutIntent si)
            {
                si.Path?.ReturnToPool();
            }
            else if (intent is ShareResourceIntent sri)
            {
                sri.Path?.ReturnToPool();
            }
            else if (intent is LeaveOutpostIntent loi)
            {
                loi.Path?.ReturnToPool();
            }
            else if (intent is RetreatIntent ri)
            {
                ri.Path?.ReturnToPool();
            }

            if (_consequenceService != null && _agentRegistry != null && _agentRegistry.TryGetAgent(intent.AgentId, out var agentState))
            {
                _consequenceService.ApplyConsequences(agentState, intent, result, _clock != null ? (uint)_clock.CurrentTick : 0);
            }

            intent?.OnComplete?.Invoke(result);
        }
    }
}
