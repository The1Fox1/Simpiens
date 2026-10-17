using System.Collections.Generic;
using Simpiens.Cognition;
using Simpiens.Cognition.Inventory;
using Simpiens.Cognition.Pathfinding;
using Simpiens.Diagnostics;
using Simpiens.Entities;
using Simpiens.Simulation;
using Simpiens.Simulation.Economy;
using Simpiens.Simulation.Persistence;
using Simpiens.Simulation.Spatial;
using Simpiens.Simulation.World;
using UnityEngine;
using VContainer;
using VContainer.Unity;
using ZLogger;

namespace Simpiens.Core.Session
{
    /// <summary>
    /// Spawns a session: builds the world from the session's map (partition, walls, sectors, resource nodes, hearths)
    /// and hands the rest (agents, relationships, groups, stalls, seeded bounties) to the session definition.
    /// Every respawn starts from tick 0 with a reset id allocator, so the same seed gives the same world.
    /// </summary>
    public class SwarmSpawner : IStartable, System.IDisposable, ISessionSwitcher, ISessionPersistence
    {
        private readonly ISpatialPartition _spatialPartition;
        private readonly IAsyncPathfinder _pathfinder;
        private readonly IWorldRegistry _registry;

        private readonly List<AutonomousAgent> _agents = new List<AutonomousAgent>(1000);
        private readonly List<string> _agentLabels = new List<string>(1000); // log labels, parallel to _agents

        private readonly Simpiens.Cognition.Planning.IGoapPlanner _planner;
        private readonly Simpiens.Cognition.Planning.ActionGraph _actionGraph;
        private readonly List<Simpiens.Cognition.Planning.GoapGoal> _goals;

        private readonly IEntityIdAllocator _idAllocator;
        private readonly SimulationSettings _settings;
        private readonly IWorldMap _worldMap;
        private readonly List<ISessionDefinition> _sessions = new List<ISessionDefinition>(2);
        private readonly ISessionDefinition _defaultSession;
        private ISessionDefinition _current;
        private LoadedWorld _world;
        private GameObject _agentsRoot;
        private readonly WorldSelection _selection;

        /// <summary>The map loaded by the last respawn, with its spawned entity ids.</summary>
        public LoadedWorld World => _world;

        public IReadOnlyList<ISessionDefinition> Sessions => _sessions;
        public ISessionDefinition Default => _defaultSession;
        public ISessionDefinition Current => _current ?? _defaultSession;
        public event System.Action<ISessionDefinition> SessionChanged;

        internal EntityId NextEntityId() => _idAllocator != null ? _idAllocator.Next() : EntityId.Generate();

        [Inject]
        public SwarmSpawner(
            ISpatialPartition spatialPartition, 
            IAsyncPathfinder pathfinder, 
            IWorldRegistry registry, 
            ISimulationManager simulationManager, 
            ISimulationClock clock,
            Simpiens.Cognition.Planning.IGoapPlanner planner,
            SimulationSettings settings,
            IEntityIdAllocator idAllocator,
            IWorldMap worldMap,
            IReadOnlyList<ISessionDefinition> sessions,
            WorldSelection selection)
        {
            _selection = selection;
            _idAllocator = idAllocator;
            _worldMap = worldMap ?? new WorldMap(spatialPartition, null);
            _settings = settings;
            _spatialPartition = spatialPartition;
            _pathfinder = pathfinder;
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

            if (_clock != null)
            {
                _clock.OnSimulationTick += HandleSimulationTick;
            }

            _evaluator = new Simpiens.Cognition.Evaluators.SurvivalUtilityEvaluator(_pathfinder);
            _cognitionService = new AgentCognitionService(null, new Simpiens.Cognition.Evaluators.AmbientWanderEvaluator(_pathfinder, settings));
            _mentalBreakEvaluator = new Simpiens.Cognition.Evaluators.MentalBreakEvaluator(_pathfinder, settings);
            _altruismEvaluator = new Simpiens.Cognition.Evaluators.AltruismEvaluator(_pathfinder);

            // Production GOAP domain (Cognition/Planning/Domain), no longer assembled here
            _actionGraph = Simpiens.Cognition.Planning.Domain.OutpostGoapDomain.BuildActionGraph(_pathfinder);
            _goals = Simpiens.Cognition.Planning.Domain.OutpostGoapDomain.BuildGoals();
        }

        private readonly Simpiens.Cognition.Evaluators.ICognitiveEvaluator _evaluator;
        private readonly IAgentCognitionService _cognitionService;
        private readonly Simpiens.Cognition.Evaluators.ICognitiveEvaluator _mentalBreakEvaluator;
        private readonly Simpiens.Cognition.Evaluators.ICognitiveEvaluator _altruismEvaluator;
        private readonly ISimulationManager _simulationManager;
        private readonly ISimulationClock _clock;

        public IReadOnlyList<AutonomousAgent> Agents => _agents;

        public void Start()
        {
            Respawn();
        }

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
                for (int i = 0; i < _agents.Count; i++)
                {
                    var inv = _agents[i].State?.Inventory;
                    if (inv != null) seededGold += (uint)inv.Gold;
                }
                _simulationManager.EconomyLedger?.RecordFaucet(seededGold, "InitialSeed");

                var bounties = builder.SeedBounties;
                for (int b = 0; b < bounties.Count; b++)
                {
                    _simulationManager.RegisterBounty(bounties[b]);
                }
            }

            SimLog.Bootstrap.ZLogInformation($"Session '{session.Id}' spawned: {_agents.Count} agents, map '{map.Name}', {builder.StallCount} stall(s), seeded gold {seededGold}g, {builder.SeedBounties.Count} seeded bounties");
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

            CleanupExistingWorld();

            _agents.Clear();
            _agentLabels.Clear();
            _registry?.Clear();
            _idAllocator?.Reset(); // same seed + same spawn order => same entity ids on every respawn
            SimLog.ClearAgentNames();
            _simulationManager?.ResetState();

            // A session starts at tick 0 with no history: seeded expiries (bounties, directives) are session-relative,
            // seeded randomness (keyed on the tick) does not depend on uptime, and chronicle events never point at
            // GUIDs the reset allocator hands out again
            _clock?.Reset();
            SimLog.CurrentTick = 0;
            _simulationManager?.Chronicle?.Clear();

            // 1. Static geography: partition, walls, sectors (simulation) and the map view
            var map = session.Map;
            _world = _worldMap.Load(map);
            _selection?.Clear(); // the selected crawler belonged to the old world
            MapView.Show(map, _worldMap.Sectors, _selection);

            // Create roots for organization
            _agentsRoot = new GameObject("[SwarmAgents]");
            GameObject propsRoot = new GameObject("[MapEntities]");

            // 2. Resource nodes and hearths from the map definition (keys resolve to ids for the session scripts)
            for (int n = 0; n < map.ResourceNodes.Count; n++)
            {
                var node = map.ResourceNodes[n];
                SpawnResourceNode(propsRoot, node.Position, node.Yield, node.Item, node.RegrowTicks, out var nodeId);
                _world.RegisterEntity(node.Key, nodeId);
            }
            for (int p = 0; p < map.Pois.Count; p++)
            {
                var poi = map.Pois[p];
                if (poi.Kind == PoiKind.Bunk)
                {
                    SpawnBunk(propsRoot, poi.Position, out var bunkId);
                    _world.RegisterEntity(poi.Key, bunkId);
                    continue;
                }
                if (poi.Kind != PoiKind.Hearth) continue;
                SpawnDemoCampfire(propsRoot, poi.Position, poi.Radius, out var anchorId);
                _world.RegisterEntity(poi.Key, anchorId);
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

            for (int i = 0; i < save.Crawlers.Length; i++) SpawnRestoredAgent(save.Crawlers[i]);
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

            SimLog.Bootstrap.ZLogInformation($"Session '{session.Id}' loaded at t{header.Tick}: {_agents.Count} agents, map '{session.Map.Name}', saved by build {header.BuildVersion}");
            SessionChanged?.Invoke(session);
            return true;
        }

        /// <summary>Why a save can't be loaded into this game (player-facing text), or null when it can.</summary>
        public string CheckRestorable(SessionSave save, out ISessionDefinition session)
        {
            session = null;
            var header = save?.Header;
            if (header == null || save.Crawlers == null || System.Array.IndexOf(save.Crawlers, null) >= 0 || save.Economy == null || save.World == null || save.Groups == null || save.Chronicle == null)
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

        private void SpawnRestoredAgent(CrawlerSave saved)
        {
            var record = saved.Record;
            string name = string.IsNullOrEmpty(saved.Name) ? $"SwarmAgent_{_agents.Count}" : saved.Name;
            var agent = SpawnAgentView(record.Position, record.Id, name, saved.Label ?? name);
            var state = agent.State;
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

        /// <summary>Spawns one agent view + simulation state (called by <see cref="SessionBuilder.SpawnAgent"/>).</summary>
        internal AutonomousAgent SpawnAgent(Vector2 position, string label, bool seededPersonality)
        {
            var agent = SpawnAgentView(position, NextEntityId(), $"SwarmAgent_{_agents.Count}", label);

            if (agent.State?.Inventory != null && _simulationManager != null)
            {
                agent.State.Inventory.Gold = _simulationManager.Economy.StartingGold; // before the ledger's InitialSeed baseline
            }

            if (seededPersonality && agent.State != null)
            {
                ulong seed = _settings != null ? _settings.Seed : SimulationSettings.DefaultSeed;
                agent.State.Personality = Simpiens.Cognition.Contracts.AgentPersonality.Generate(seed, agent.AgentId);
                SimLog.Cognition.ZLogInformation($"{SimLog.Agent(agent.AgentId)} personality: {agent.State.Personality.ToString()}");
            }
            return agent;
        }

        /// <summary>A crawler's view and simulation state with a given id and names: a new crawler, or a loaded one.</summary>
        private AutonomousAgent SpawnAgentView(Vector2 position, EntityId id, string name, string label)
        {
            GameObject agentGo = new GameObject(name);
            if (_agentsRoot != null) agentGo.transform.SetParent(_agentsRoot.transform);
            agentGo.transform.position = position;

            var sr = agentGo.AddComponent<SpriteRenderer>();
            sr.sprite = AgentSpriteLibrary.GetAgentSprite(AgentVisualState.Idle, 0);
            sr.sortingOrder = RenderOrder.Crawlers;
            agentGo.transform.localScale = Vector3.one * 0.7f; // Clear, readable icon size

            // Register them as pawns so they appear in the GridVisualizer
            var rb = agentGo.AddComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            rb.bodyType = RigidbodyType2D.Kinematic; // They move via interpolation, not physics

            var col = agentGo.AddComponent<CircleCollider2D>();
            col.radius = 0.35f;

            var controller = agentGo.AddComponent<NodeController>();
            controller.Type = EntityType.Pawn;
            controller.Initialize(id, new NodeConfiguration(speed: 2f));
            _registry.RegisterNode(controller);
            SimLog.RegisterAgentName(controller.Id, label);

            var agent = agentGo.AddComponent<AutonomousAgent>();
            agent.Initialize(controller.Id, _spatialPartition, new Simpiens.Cognition.Evaluators.ICognitiveEvaluator[] { _mentalBreakEvaluator, _altruismEvaluator, _evaluator }, _simulationManager, _clock, _planner, _actionGraph, _goals, _cognitionService);

            // Attach AgentView for animated visual feedback
            agentGo.AddComponent<AgentView>();

            _agents.Add(agent);
            _agentLabels.Add(label);
            return agent;
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
                    BuildVersion = Application.version,
                    SessionId = session.Id,
                    MapFingerprint = SaveFingerprint.Of(session.Map),
                    Seed = _settings != null ? _settings.Seed : SimulationSettings.DefaultSeed,
                    Tick = _clock != null ? _clock.CurrentTick : 0,
                    EntityIdCounter = _idAllocator != null ? _idAllocator.Counter : 0
                }
            };

            var crawlers = new List<CrawlerSave>(_agents.Count);
            for (int i = 0; i < _agents.Count; i++)
            {
                var agent = _agents[i];
                if (agent == null || agent.State == null) continue; // destroyed by a test

                var state = agent.State;
                var crawler = new CrawlerSave
                {
                    Name = agent.gameObject.name,
                    Label = _agentLabels[i],
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

        private void SpawnResourceNode(GameObject root, Vector2 pos, int yield, ItemType yieldItem, uint regrowTicks, out EntityId resId)
        {
            var resourceGo = new GameObject($"{yieldItem}Node_{pos.x}_{pos.y}");
            resourceGo.transform.SetParent(root.transform);
            resourceGo.transform.position = pos;

            var sr = resourceGo.AddComponent<SpriteRenderer>();
            sr.sprite = AgentSpriteLibrary.GetResourceSprite(yieldItem);
            sr.sortingOrder = RenderOrder.Props;
            resourceGo.transform.localScale = Vector3.one * 0.8f;

            var rb = resourceGo.AddComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            rb.bodyType = RigidbodyType2D.Static;

            var col = resourceGo.AddComponent<CircleCollider2D>();
            col.radius = 0.4f;

            var controller = resourceGo.AddComponent<NodeController>();
            controller.Type = EntityType.Resource;
            controller.ConfigureResourceKind(yieldItem);
            resId = NextEntityId();
            controller.Initialize(resId, new NodeConfiguration(speed: 0f));

            _registry.RegisterNode(controller);
            SimLog.RegisterAgentName(resId, $"{yieldItem}@({pos.x:0.#},{pos.y:0.#})");
            _simulationManager.RegisterResource(new ResourceData
            {
                EntityId = resId,
                RemainingYield = yield,
                MaxYield = yield,
                YieldItem = yieldItem,
                RegrowTicks = regrowTicks
            });
        }

        private void SpawnDemoCampfire(GameObject root, Vector2 pos, float radius, out EntityId anchorId)
        {
            var campfireGo = new GameObject($"DemoCampfire_{pos.x}_{pos.y}");
            campfireGo.transform.SetParent(root.transform);
            campfireGo.transform.position = pos;

            var sr = campfireGo.AddComponent<SpriteRenderer>();
            sr.sprite = AgentSpriteLibrary.GetCampfireSprite(0);
            campfireGo.transform.localScale = Vector3.one * 0.9f;

            var rb = campfireGo.AddComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            rb.bodyType = RigidbodyType2D.Static;

            var col = campfireGo.AddComponent<CircleCollider2D>();
            col.radius = 0.35f;

            var controller = campfireGo.AddComponent<NodeController>();
            controller.Type = EntityType.Anchor;
            anchorId = NextEntityId();
            controller.Initialize(anchorId, new NodeConfiguration(speed: 0f));

            campfireGo.AddComponent<CampfireView>();

            _registry.RegisterNode(controller);
            _simulationManager.RegisterAnchor(anchorId, pos, radius: radius);
        }

        /// <summary>A bunk: walkable (no collider), one sleeper at a time (Epic MAP Phase 3 rest anchor).</summary>
        private void SpawnBunk(GameObject root, Vector2 pos, out EntityId bunkId)
        {
            var bunkGo = new GameObject($"Bunk_{pos.x}_{pos.y}");
            bunkGo.transform.SetParent(root.transform);
            bunkGo.transform.position = pos;

            var sr = bunkGo.AddComponent<SpriteRenderer>();
            sr.sprite = AgentSpriteLibrary.BunkSprite;
            sr.sortingOrder = RenderOrder.Props; // under the sleeper
            bunkGo.transform.localScale = Vector3.one * 0.9f;

            // NodeController requires a body and a collider; without these lines it gets a dynamic body that crawlers
            // shove around. Static, and a trigger so the bunk stays walkable
            var rb = bunkGo.AddComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            rb.bodyType = RigidbodyType2D.Static;

            var col = bunkGo.AddComponent<CircleCollider2D>();
            col.isTrigger = true;

            var controller = bunkGo.AddComponent<NodeController>();
            controller.Type = EntityType.Bunk;
            bunkId = NextEntityId();
            controller.Initialize(bunkId, new NodeConfiguration(speed: 0f));

            _registry.RegisterNode(controller);
            _simulationManager?.RegisterBunk(bunkId, pos);
        }

        private void CleanupExistingWorld()
        {
            string[] rootNames = new[] { "[SwarmAgents]", "[MapEntities]", "[DynamicObstacles]" };
            for (int i = 0; i < rootNames.Length; i++)
            {
                var existing = GameObject.Find(rootNames[i]);
                if (existing != null)
                {
                    Object.DestroyImmediate(existing);
                }
            }
            MapView.Clear();
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
            // Centralized update loop for zero-allocation high-performance execution on logical simulation ticks
            int count = _agents.Count;
            for (int i = 0; i < count; i++)
            {
                var agent = _agents[i];
                if (agent != null)
                {
                    agent.ManualTick(currentTick);
                }
            }
        }

        public void Tick()
        {
            HandleSimulationTick(_clock != null ? _clock.CurrentTick : 0);
        }
    }
}
