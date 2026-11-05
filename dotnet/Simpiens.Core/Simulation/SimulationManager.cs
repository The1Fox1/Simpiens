using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Simpiens.Cognition;
using Simpiens.Cognition.Contracts;
using Simpiens.Cognition.Groups;
using Simpiens.Diagnostics;
using Simpiens.Intervention;
using Simpiens.Narrative;
using Simpiens.Simulation.Economy;
using Simpiens.Simulation.Movement;
using Simpiens.Simulation.Social;
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
    public partial class SimulationManager : ISimulationManager, IIntentExecutionContext, ISupplyHost, Simpiens.Simulation.Guild.IGuildHost, Simpiens.Simulation.Workshops.IWorkshopHost, System.IDisposable
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
        private readonly Locomotion _locomotion;
        private readonly Market _market;
        private readonly Lodging _lodging;
        private readonly SocialDynamics _social;
        private EconomyTuning _economy = EconomyTuning.Default;

        private readonly ConcurrentQueue<AgentIntent> _intentQueue = new ConcurrentQueue<AgentIntent>();
        
        public static readonly EntityId PlayerEntityId = Simpiens.Simulation.WorldRegistry.PlayerEntityId;

        private readonly Dictionary<EntityId, ActiveIntentState> _activeIntents = new Dictionary<EntityId, ActiveIntentState>();
        // Deterministic ordering (ROADMAP §1A.1 rule 3): active intents execute in agent-id order, never Dictionary order
        private readonly List<EntityId> _activeIntentOrder = new List<EntityId>(64);
        private readonly List<AgentIntent> _pendingIntents = new List<AgentIntent>(64);
        private readonly List<EntityId> _keysToRemove = new List<EntityId>(64);

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

        /// <summary>The player's guild (Epic 10 G4): its group, members' contracts and cooldowns.</summary>
        public Simpiens.Simulation.Guild.GuildService Guild { get; } = new Simpiens.Simulation.Guild.GuildService();

        private readonly Simpiens.Simulation.Workshops.WorkshopService _workshops = new Simpiens.Simulation.Workshops.WorkshopService();

        /// <summary>The loaded map's stations (Epic 10 Part 2): built or not, their orders and makes.</summary>
        public Simpiens.Simulation.Workshops.WorkshopService Workshops => _workshops.Follow(_sectors?.Map);

        bool Simpiens.Simulation.Guild.IGuildHost.TryGetCrawler(EntityId crawlerId, out AgentState crawler)
        {
            crawler = null;
            if (_agentRegistry != null && _agentRegistry.TryGetAgent(crawlerId, out crawler)) return crawler != null;
            crawler = _worldRegistry?.GetEntity(crawlerId)?.Agent;
            return crawler != null;
        }

        int Simpiens.Simulation.Guild.IGuildHost.GetSectorForPosition(Vec2 position) => GetSectorForPosition(position);
        int Simpiens.Simulation.Workshops.IWorkshopHost.GetSectorForPosition(Vec2 position) => GetSectorForPosition(position);
        bool Simpiens.Simulation.Workshops.IWorkshopHost.TryGetCrawler(EntityId crawlerId, out AgentState crawler) => ((Simpiens.Simulation.Guild.IGuildHost)this).TryGetCrawler(crawlerId, out crawler);

        bool Simpiens.Simulation.Guild.IGuildHost.TryGetSector(byte sectorId, out SectorDefinition sector)
        {
            sector = default;
            return _sectors != null && _sectors.TryGetSector(sectorId, out sector);
        }

        bool Simpiens.Simulation.Guild.IGuildHost.IsSectorCharted(byte sectorId) => _sectors != null && _sectors.IsExplored(sectorId);

        bool Simpiens.Simulation.Guild.IGuildHost.TryGetNodePosition(EntityId resourceId, out Vec2 position)
        {
            var node = _worldRegistry?.GetEntity(resourceId);
            if (node == null) _dormantNodes.TryGetValue(resourceId, out node);
            position = node != null ? node.Position : default;
            return node != null;
        }

        /// <summary>The resource nodes in a sector, ready (in the registry) or dormant (set aside until they regrow), by id.</summary>
        void Simpiens.Simulation.Guild.IGuildHost.NodesIn(byte sectorId, List<EntityId> into)
        {
            into.Clear();
            foreach (var kvp in _resources)
            {
                var node = _worldRegistry?.GetEntity(kvp.Key);
                if (node == null) _dormantNodes.TryGetValue(kvp.Key, out node);
                if (node != null && GetSectorForPosition(node.Position) == sectorId) into.Add(kvp.Key);
            }
            into.Sort((a, b) => a.CompareTo(b)); // the dictionary's order is not the simulation's
        }

        TradeStallData ISupplyHost.PayWages(TradeStallData stall, uint currentTick) => Workshops.PayWages(this, Guild.PayWages(this, stall, currentTick), currentTick);
        Simpiens.Simulation.Workshops.CraftStep IIntentExecutionContext.WorkStation(int stationIndex) => Workshops.Work(this, stationIndex);

        public uint CurrentTick => _clock != null ? (uint)_clock.CurrentTick : (uint)_fallbackTick;
        public int IntentFaults { get; private set; }
        public string FirstIntentFault { get; private set; }
        internal Dictionary<EntityId, ActiveIntentState> ActiveIntents => _activeIntents;
        IReadOnlyDictionary<EntityId, ActiveIntentState> IIntentExecutionContext.ActiveIntents => _activeIntents;
        EntityId IIntentExecutionContext.PlayerEntityId => PlayerEntityId;

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
            _locomotion = new Locomotion(_spatialPartition, _worldRegistry, _clock);
            _groupRegistry = groupRegistry;
            _chronicleService = chronicleService;
            _sectors = sectors;
            _social = new SocialDynamics(_worldRegistry, _groupRegistry, _sectors, _chronicleService, _clock);
            _interventionService = interventionService;
            _agentRegistry = agentRegistry ?? new Simpiens.Simulation.AgentRegistry();
            _consequenceService = consequenceService ?? new Simpiens.Cognition.AgentConsequenceService(_groupRegistry, _spatialPartition, _chronicleService);
            _economyLedger = economyLedger ?? new EconomyLedger();
            _market = new Market(_economyLedger, _sectors, _chronicleService);
            _lodging = new Lodging(_locomotion);
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
            StableSort.Sort(_pendingIntents, (a, b) => a.AgentId.CompareTo(b.AgentId));

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

                // The guild (Epic 10 G4): its service decides each by GuildRules and logs a refusal
                case InfluenceCommandType.Recruit:
                    Guild.Recruit(this, influence.Recruit.CrawlerId, influence.Recruit.Wage, currentTick);
                    break;

                // A crawler holds one role at a time (Epic 10 Part 2, D6, D8): a member's or an artisan's terms
                case InfluenceCommandType.Dismiss:
                    if (Workshops.IsArtisan(influence.Dismiss.CrawlerId)) Workshops.Dismiss(this, influence.Dismiss.CrawlerId, currentTick);
                    else Guild.Dismiss(this, influence.Dismiss.CrawlerId, currentTick);
                    break;

                case InfluenceCommandType.SetWage:
                    if (Workshops.IsArtisan(influence.SetWage.CrawlerId)) Workshops.SetWage(this, influence.SetWage.CrawlerId, influence.SetWage.Wage, currentTick);
                    else Guild.SetWage(this, influence.SetWage.CrawlerId, influence.SetWage.Wage, currentTick);
                    break;

                // Expeditions (Epic 10 G5)
                case InfluenceCommandType.DispatchExpedition:
                    Guild.Dispatch(this, influence.DispatchExpedition, currentTick);
                    break;

                case InfluenceCommandType.RecallExpedition:
                    Guild.Recall(this, currentTick);
                    break;

                case InfluenceCommandType.GiveGear:
                    Guild.GiveGear(this, influence.GiveGear.CrawlerId, influence.GiveGear.Item, currentTick);
                    break;

                // The workshops (Epic 10 Part 2)
                case InfluenceCommandType.BuildStation:
                    Workshops.Build(this, influence.BuildStation.StationIndex, currentTick);
                    break;

                case InfluenceCommandType.HireArtisan:
                    Workshops.Hire(this, influence.HireArtisan.CrawlerId, influence.HireArtisan.StationIndex, influence.HireArtisan.Wage, currentTick);
                    break;

                case InfluenceCommandType.OrderStation:
                    Workshops.Order(this, influence.OrderStation.StationIndex, influence.OrderStation.Item, influence.OrderStation.Count);
                    break;

                case InfluenceCommandType.SetShifts:
                    Workshops.SetShifts(this, influence.SetShifts.CrawlerId, influence.SetShifts.Shifts);
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

        /// <summary>
        /// Applies a session's economy tuning (SwarmSpawner, on every respawn). The tuning is immutable, so the snapshot
        /// shares it by reference with background cognition. A null tuning falls back to the default.
        /// </summary>
        public void SetEconomy(EconomyTuning economy)
        {
            _economy = economy ?? EconomyTuning.Default;
            _supplyService?.Configure(_economy.Supply);
            _spatialPartition?.SetEconomy(_economy);
            _consequenceService?.SetPace(_economy.Day);
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
            _market.ExpireBounties(currentTick);

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

            // The guild's expedition moves on from where the intents left its members (Epic 10 G5)
            Guild.TickExpedition(this, currentTick);

            // The stations' crews: each one's order follows its artisan's shift (Epic 10 Part 2, W1.3)
            Workshops.TickShifts(this, currentTick);

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
                _spatialPartition.SetOccupiedBunks(_lodging.OccupiedBunks);
                _spatialPartition.UpdateFromRegistry(_worldRegistry, _groupRegistry, _market.ActiveStalls, _market.ActiveBounties);
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
