using System;
using System.Collections.Generic;
using Simpiens.Cognition;
using Simpiens.Cognition.Evaluators;
using Simpiens.Cognition.Inventory;
using Simpiens.Cognition.Pathfinding;
using Simpiens.Cognition.Planning;
using Simpiens.Diagnostics;
using Simpiens.Simulation;
using Simpiens.Simulation.Economy;
using Simpiens.Simulation.Persistence;
using Simpiens.Simulation.Spatial;
using Simpiens.Simulation.World;
using ZLogger;

namespace Simpiens.Core.Session
{
    /// <summary>
    /// A running session, engine-free (Epic CORE C5): builds the world from the session's map (partition, walls, sectors,
    /// resource nodes, bunks, hearths), hands the rest (agents, relationships, groups, stalls, seeded bounties) to the
    /// session definition, ticks every crawler after each simulation tick, and captures and restores the whole session.
    /// Every respawn starts from tick 0 with a reset id allocator, so the same seed gives the same world. Views follow
    /// its events (<c>SwarmSpawner</c> in Unity); a headless host uses it as it is. Main thread.
    /// </summary>
    public sealed class SessionHost : ISessionSwitcher, ISessionPersistence, IDisposable
    {
        // Body radii and speed the simulation gives each kind of entity (they were the views' collider radii)
        public const float CrawlerRadius = 0.35f;
        public const float CrawlerSpeed = 2f;
        public const float ResourceRadius = 0.4f;
        public const float HearthRadius = 0.35f;
        public const float BunkRadius = 0.5f;

        private readonly ISpatialPartition _spatialPartition;
        private readonly IWorldRegistry _registry;
        private readonly ISimulationManager _simulationManager;
        private readonly ISimulationClock _clock;
        private readonly IGoapPlanner _planner;
        private readonly IEntityIdAllocator _idAllocator;
        private readonly SimulationSettings _settings;
        private readonly IWorldMap _worldMap;

        private readonly ActionGraph _actionGraph;
        private readonly List<GoapGoal> _goals;
        private readonly ICognitiveEvaluator _evaluator;
        private readonly IAgentCognitionService _cognitionService;
        private readonly ICognitiveEvaluator _mentalBreakEvaluator;
        private readonly ICognitiveEvaluator _altruismEvaluator;

        private readonly List<AgentRuntime> _crawlers = new List<AgentRuntime>(1000);
        private readonly List<AgentState> _states = new List<AgentState>(1000);   // parallel to _crawlers
        private readonly List<string> _names = new List<string>(1000);            // "SwarmAgent_3", parallel to _crawlers
        private readonly List<string> _labels = new List<string>(1000);           // log labels, parallel to _crawlers

        private readonly List<ISessionDefinition> _sessions = new List<ISessionDefinition>(2);
        private readonly ISessionDefinition _defaultSession;
        private ISessionDefinition _current;
        private LoadedWorld _world;

        public SessionHost(
            ISpatialPartition spatialPartition,
            IAsyncPathfinder pathfinder,
            IWorldRegistry registry,
            ISimulationManager simulationManager,
            ISimulationClock clock,
            IGoapPlanner planner,
            SimulationSettings settings,
            IEntityIdAllocator idAllocator,
            IWorldMap worldMap,
            IReadOnlyList<ISessionDefinition> sessions)
        {
            _idAllocator = idAllocator;
            _worldMap = worldMap ?? new WorldMap(spatialPartition, null);
            _settings = settings;
            _spatialPartition = spatialPartition;
            _registry = registry;
            _simulationManager = simulationManager;
            _clock = clock;
            _planner = planner;

            if (sessions != null)
            {
                for (int i = 0; i < sessions.Count; i++)
                {
                    if (sessions[i] != null) _sessions.Add(sessions[i]);
                }
            }
            if (_sessions.Count == 0) _sessions.Add(new OutpostSession());

            // The live game always boots the outpost; test sessions are only ever selected explicitly
            _defaultSession = Find(OutpostSession.SessionId) ?? _sessions[0];

            // Crawlers tick after the simulation's own tick: SimulationManager subscribed first (this host depends on it)
            if (_clock != null)
            {
                _clock.OnSimulationTick += TickCrawlers;
            }

            _evaluator = new SurvivalUtilityEvaluator(pathfinder);
            _cognitionService = new AgentCognitionService(null, new AmbientWanderEvaluator(pathfinder, settings));
            _mentalBreakEvaluator = new MentalBreakEvaluator(pathfinder, settings);
            _altruismEvaluator = new AltruismEvaluator(pathfinder);

            // Production GOAP domain (Cognition/Planning/Domain)
            _actionGraph = Simpiens.Cognition.Planning.Domain.OutpostGoapDomain.BuildActionGraph(pathfinder);
            _goals = Simpiens.Cognition.Planning.Domain.OutpostGoapDomain.BuildGoals();
        }

        /// <summary>The world is about to be torn down (a respawn or a load): views destroy what they drew.</summary>
        public event Action WorldTearingDown;

        /// <summary>A map was loaded (partition, walls and sectors are ready): views draw it.</summary>
        public event Action<MapDefinition> MapLoaded;

        /// <summary>A map entity was spawned (a resource node, a bunk, a hearth), before it is registered.</summary>
        public event Action<WorldEntity> PropSpawned;

        /// <summary>A crawler was spawned or loaded: its tick, its entity and its name.</summary>
        public event Action<AgentRuntime, WorldEntity, string> CrawlerSpawned;

        public event Action<ISessionDefinition> SessionChanged;

        /// <summary>
        /// A tick has finished: the simulation's own, then every crawler's. Nothing changes the world again until the
        /// next tick, so views capture what they draw here (Epic CLIENT, review A3); a server would send its state here.
        /// </summary>
        public event Action<long> TickCompleted;

        /// <summary>Written into saves (the Unity host sets the player build's version).</summary>
        public string BuildVersion { get; set; } = "headless";

        /// <summary>The map loaded by the last respawn, with its spawned entity ids.</summary>
        public LoadedWorld World => _world;

        public ISectorRegistry Sectors => _worldMap?.Sectors;

        /// <summary>Every crawler of the session, in spawn order.</summary>
        public IReadOnlyList<AgentRuntime> Crawlers => _crawlers;

        /// <summary>Every crawler's state, in spawn order.</summary>
        public IReadOnlyList<AgentState> CrawlerStates => _states;

        /// <summary>Every crawler's name ("SwarmAgent_3"), in spawn order.</summary>
        public IReadOnlyList<string> CrawlerNames => _names;

        public IReadOnlyList<ISessionDefinition> Sessions => _sessions;
        public ISessionDefinition Default => _defaultSession;
        public ISessionDefinition Current => _current ?? _defaultSession;

        internal EntityId NextEntityId() => _idAllocator != null ? _idAllocator.Next() : EntityId.Generate();

        /// <summary>The registered session with <paramref name="sessionId"/>, or null.</summary>
        public ISessionDefinition Find(string sessionId)
        {
            for (int i = 0; i < _sessions.Count; i++)
            {
                if (_sessions[i].Id == sessionId) return _sessions[i];
            }
            return null;
        }

        public void SwitchTo(ISessionDefinition session)
        {
            Respawn(session);
        }

        /// <summary>Respawns the current session (the default session before the first respawn).</summary>
        public void Respawn()
        {
            Respawn(Current);
        }

        public void Respawn(ISessionDefinition session)
        {
            session ??= _defaultSession;
            var map = BuildStaticWorld(session);

            // 3. Session content: the economy first (stall terms, starting gold), then agents, relationships, groups,
            // stalls, staged bounties
            _simulationManager?.SetEconomy(session.Economy);
            var builder = new SessionBuilder(this, map, _world, _simulationManager, _settings);
            session.Populate(builder);

            // 4. Ledger baseline: all gold present at session start enters through one named faucet, so the
            // conservation invariant (circulating == faucets - sinks) holds from tick 0. Seeded bounties escrow
            // afterwards, moving gold the baseline already counted.
            uint seededGold = 0;
            if (_simulationManager != null)
            {
                seededGold = builder.SeededStallGold;
                for (int i = 0; i < _states.Count; i++)
                {
                    var inv = _states[i].Inventory;
                    if (inv != null) seededGold += (uint)inv.Gold;
                }
                _simulationManager.EconomyLedger?.RecordFaucet(seededGold, "InitialSeed");

                var bounties = builder.SeedBounties;
                for (int b = 0; b < bounties.Count; b++)
                {
                    _simulationManager.RegisterBounty(bounties[b]);
                }
            }

            SimLog.Bootstrap.ZLogInformation($"Session '{session.Id}' spawned: {_crawlers.Count} agents, map '{map.Name}', {builder.StallCount} stall(s), seeded gold {seededGold}g, {builder.SeedBounties.Count} seeded bounties");
            SessionChanged?.Invoke(session);
        }

        /// <summary>
        /// The first half of every respawn and load: tears the old world down, resets the registries, the id allocator,
        /// the simulation and the clock, loads the map and spawns its resource nodes, bunks and hearths in definition
        /// order, so they get the same ids every time (same seed + same spawn order).
        /// </summary>
        private MapDefinition BuildStaticWorld(ISessionDefinition session)
        {
            _current = session;

            WorldTearingDown?.Invoke();
            for (int i = 0; i < _crawlers.Count; i++) _crawlers[i].Dispose();

            _crawlers.Clear();
            _states.Clear();
            _names.Clear();
            _labels.Clear();
            _registry?.Clear();
            _idAllocator?.Reset(); // same seed + same spawn order => same entity ids on every respawn
            SimLog.ClearAgentNames();
            _simulationManager?.ResetState();

            // A session starts at tick 0 with no history: seeded expiries (bounties, directives) are session-relative,
            // seeded randomness (keyed on the tick) does not depend on uptime, and chronicle events never point at
            // ids the reset allocator hands out again
            _clock?.Reset();
            SimLog.CurrentTick = 0;
            _simulationManager?.Chronicle?.Clear();

            // 1. Static geography: partition, walls, sectors
            var map = session.Map;
            _world = _worldMap.Load(map);
            MapLoaded?.Invoke(map);

            // 2. Resource nodes and hearths from the map definition (keys resolve to ids for the session scripts)
            for (int n = 0; n < map.ResourceNodes.Count; n++)
            {
                var node = map.ResourceNodes[n];
                var nodeId = SpawnResourceNode(node.Position, node.Yield, node.Item, node.RegrowTicks);
                _world.RegisterEntity(node.Key, nodeId);
            }
            for (int p = 0; p < map.Pois.Count; p++)
            {
                var poi = map.Pois[p];
                if (poi.Kind == PoiKind.Bunk)
                {
                    _world.RegisterEntity(poi.Key, SpawnBunk(poi.Position));
                    continue;
                }
                if (poi.Kind != PoiKind.Hearth) continue;
                _world.RegisterEntity(poi.Key, SpawnHearth(poi.Position, poi.Radius));
            }
            return map;
        }

        /// <summary>
        /// Loads a captured session (Milestone 1 save/load). Rebuilds the map exactly as a respawn does, so its entities
        /// get the same ids; then, in place of the session's Populate, puts back every crawler and the simulation's state,
        /// and resumes the clock and the id sequence at the saved tick. Crawlers decide afresh on the next tick (D3).
        /// A save made with another format, for another session or seed, or on another map is refused with a reason,
        /// and nothing changes (D4). Main thread, between ticks.
        /// </summary>
        public bool TryRestore(SessionSave save, out string refusal)
        {
            refusal = CheckRestorable(save, out var session);
            if (refusal != null)
            {
                SimLog.Bootstrap.ZLogWarning($"Load refused: {refusal}");
                return false;
            }

            BuildStaticWorld(session);
            _simulationManager?.SetEconomy(session.Economy);

            for (int i = 0; i < save.Crawlers.Length; i++) SpawnRestoredCrawler(save.Crawlers[i]);
            _simulationManager?.RestoreFrom(save);

            var world = save.World;
            if (world.EntityKeys != null && world.EntityIds != null)
            {
                for (int i = 0; i < world.EntityKeys.Length && i < world.EntityIds.Length; i++) _world.RegisterEntity(world.EntityKeys[i], world.EntityIds[i]);
            }

            var header = save.Header;
            _clock?.ResumeAt(header.Tick);
            SimLog.CurrentTick = header.Tick;
            _idAllocator?.ResumeAt(header.EntityIdCounter);

            SimLog.Bootstrap.ZLogInformation($"Session '{session.Id}' loaded at t{header.Tick}: {_crawlers.Count} agents, map '{session.Map.Name}', saved by build {header.BuildVersion}");
            SessionChanged?.Invoke(session);
            return true;
        }

        /// <summary>Why a save can't be loaded into this game (player-facing text), or null when it can.</summary>
        public string CheckRestorable(SessionSave save, out ISessionDefinition session)
        {
            session = null;
            var header = save?.Header;
            if (header == null || save.Crawlers == null || Array.IndexOf(save.Crawlers, null) >= 0 || save.Economy == null || save.World == null || save.Groups == null || save.Chronicle == null)
            {
                return "The save is incomplete or damaged.";
            }
            if (header.FormatVersion != SessionSaveHeader.CurrentFormatVersion)
            {
                return $"The save was made by another version of the game (save format {header.FormatVersion}; this game reads format {SessionSaveHeader.CurrentFormatVersion}).";
            }
            session = Find(header.SessionId);
            if (session == null)
            {
                return $"The save is of a session this game doesn't have ('{header.SessionId}').";
            }
            ulong seed = _settings != null ? _settings.Seed : SimulationSettings.DefaultSeed;
            if (header.Seed != seed)
            {
                session = null;
                return $"The save was made with world seed {header.Seed}; this game runs seed {seed}.";
            }
            if (header.MapFingerprint != SaveFingerprint.Of(session.Map))
            {
                var map = session.Map;
                session = null;
                return $"The save was made on a different version of the map '{map.Name}'.";
            }
            return null;
        }

        private void SpawnRestoredCrawler(CrawlerSave saved)
        {
            var record = saved.Record;
            string name = string.IsNullOrEmpty(saved.Name) ? $"SwarmAgent_{_crawlers.Count}" : saved.Name;
            var state = SpawnCrawler(record.Position, record.Id, name, saved.Label ?? name).State;
            state.RestoreRecord(record);

            if (saved.Inventory?.Slots != null)
            {
                if (state.Inventory == null || state.Inventory.Capacity != saved.Inventory.Slots.Length)
                {
                    state.Inventory = new AgentInventory(saved.Inventory.Slots.Length, startingGold: 0);
                }
                state.Inventory.RestoreFrom(saved.Inventory);
            }
            if (saved.Memory != null) state.Memory?.RestoreFrom(saved.Memory);
        }

        /// <summary>Spawns one crawler (called by <see cref="SessionBuilder.SpawnAgent"/>).</summary>
        internal AgentState SpawnAgent(Vec2 position, string label, bool seededPersonality)
        {
            var state = SpawnCrawler(position, NextEntityId(), $"SwarmAgent_{_crawlers.Count}", label).State;

            if (state.Inventory != null && _simulationManager != null)
            {
                state.Inventory.Gold = _simulationManager.Economy.StartingGold; // before the ledger's InitialSeed baseline
            }

            if (seededPersonality)
            {
                ulong seed = _settings != null ? _settings.Seed : SimulationSettings.DefaultSeed;
                state.Personality = Simpiens.Cognition.Contracts.AgentPersonality.Generate(seed, state.AgentId);
                SimLog.Cognition.ZLogInformation($"{SimLog.Agent(state.AgentId)} personality: {state.Personality.ToString()}");
            }
            return state;
        }

        /// <summary>A crawler with a given id and names: its state, its entity in the world and its tick.</summary>
        private AgentRuntime SpawnCrawler(Vec2 position, EntityId id, string name, string label)
        {
            var state = new AgentState(id);
            var entity = new WorldEntity(id, EntityType.Pawn, position, CrawlerRadius, baseSpeed: CrawlerSpeed, agent: state, name: name);
            _registry?.Register(entity);
            SimLog.RegisterAgentName(id, label);
            var runtime = new AgentRuntime(state, _spatialPartition, new[] { _mentalBreakEvaluator, _altruismEvaluator, _evaluator }, _simulationManager, _clock, _planner, _actionGraph, _goals, _cognitionService);

            _crawlers.Add(runtime);
            _states.Add(state);
            _names.Add(name);
            _labels.Add(label);
            CrawlerSpawned?.Invoke(runtime, entity, name);
            return runtime;
        }

        /// <summary>
        /// The whole session at the current tick, as plain data (Milestone 1 save/load): the header, every crawler in
        /// spawn order, the keyed entities and the simulation's blocks. Main thread, between ticks. Nothing background
        /// cognition writes is read: plans and intents are not saved.
        /// </summary>
        public SessionSave Capture()
        {
            var session = Current;
            var save = new SessionSave
            {
                Header = new SessionSaveHeader
                {
                    BuildVersion = BuildVersion,
                    SessionId = session.Id,
                    MapFingerprint = SaveFingerprint.Of(session.Map),
                    Seed = _settings != null ? _settings.Seed : SimulationSettings.DefaultSeed,
                    Tick = _clock != null ? _clock.CurrentTick : 0,
                    EntityIdCounter = _idAllocator != null ? _idAllocator.Counter : 0
                }
            };

            var crawlers = new List<CrawlerSave>(_crawlers.Count);
            for (int i = 0; i < _crawlers.Count; i++)
            {
                var runtime = _crawlers[i];
                if (runtime.IsDisposed) continue; // stopped by a test

                var state = runtime.State;
                var crawler = new CrawlerSave
                {
                    Name = _names[i],
                    Label = _labels[i],
                    Record = state.CaptureRecord(),
                    Inventory = new InventorySave(),
                    Memory = new MemorySave()
                };
                state.Inventory?.CaptureTo(crawler.Inventory);
                state.Memory?.CaptureTo(crawler.Memory);
                crawlers.Add(crawler);
            }
            save.Crawlers = crawlers.ToArray();

            _simulationManager?.CaptureTo(save);
            save.World ??= new WorldSave();
            if (_world != null)
            {
                _world.CaptureEntities(out var keys, out var ids);
                save.World.EntityKeys = keys;
                save.World.EntityIds = ids;
            }
            return save;
        }

        private EntityId SpawnResourceNode(Vec2 pos, int yield, ItemType yieldItem, uint regrowTicks)
        {
            var resId = NextEntityId();
            var entity = new WorldEntity(resId, EntityType.Resource, pos, ResourceRadius, name: $"{yieldItem}Node_{pos.X}_{pos.Y}") { ResourceKind = yieldItem };
            PropSpawned?.Invoke(entity);
            _registry?.Register(entity);
            SimLog.RegisterAgentName(resId, $"{yieldItem}@({pos.X:0.#},{pos.Y:0.#})");
            _simulationManager?.RegisterResource(new ResourceData
            {
                EntityId = resId,
                RemainingYield = yield,
                MaxYield = yield,
                YieldItem = yieldItem,
                RegrowTicks = regrowTicks
            });
            return resId;
        }

        private EntityId SpawnHearth(Vec2 pos, float radius)
        {
            var anchorId = NextEntityId();
            var entity = new WorldEntity(anchorId, EntityType.Anchor, pos, HearthRadius, name: $"DemoCampfire_{pos.X}_{pos.Y}");
            PropSpawned?.Invoke(entity);
            _registry?.Register(entity);
            _simulationManager?.RegisterAnchor(anchorId, pos, radius: radius);
            return anchorId;
        }

        /// <summary>A bunk: walkable, one sleeper at a time (Epic MAP Phase 3 rest anchor).</summary>
        private EntityId SpawnBunk(Vec2 pos)
        {
            var bunkId = NextEntityId();
            var entity = new WorldEntity(bunkId, EntityType.Bunk, pos, BunkRadius, name: $"Bunk_{pos.X}_{pos.Y}");
            PropSpawned?.Invoke(entity);
            _registry?.Register(entity);
            _simulationManager?.RegisterBunk(bunkId, pos);
            return bunkId;
        }

        /// <summary>Every crawler's tick, in spawn order, after the simulation's own tick.</summary>
        private void TickCrawlers(long currentTick)
        {
            int count = _crawlers.Count;
            for (int i = 0; i < count; i++)
            {
                var crawler = _crawlers[i];
                if (!crawler.IsDisposed) crawler.Tick(currentTick);
            }
            TickCompleted?.Invoke(currentTick);
        }

        public void Dispose()
        {
            if (_clock != null)
            {
                _clock.OnSimulationTick -= TickCrawlers;
            }
        }
    }
}
