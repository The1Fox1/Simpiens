using System.Collections.Generic;
using Simpiens.Cognition;
using Simpiens.Cognition.Pathfinding;
using Simpiens.Diagnostics;
using Simpiens.Entities;
using Simpiens.Simulation;
using Simpiens.Simulation.Economy;
using Simpiens.Simulation.Spatial;
using Simpiens.Simulation.World;
using Simpiens.Core.Session.Layouts;
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
    public class SwarmSpawner : IStartable, System.IDisposable, ISessionSwitcher
    {
        private readonly ISpatialPartition _spatialPartition;
        private readonly IAsyncPathfinder _pathfinder;
        private readonly IWorldRegistry _registry;

        private readonly List<AutonomousAgent> _agents = new List<AutonomousAgent>(1000);

        private readonly Simpiens.Cognition.Planning.IGoapPlanner _planner;
        private readonly Simpiens.Cognition.Planning.ActionGraph _actionGraph;
        private readonly List<Simpiens.Cognition.Planning.GoapGoal> _goals;

        // TEMPORARY forwarders to the legacy session (Epic MAP Phase 2 step 2; removed with it in step 4)
        public const int ScriptedPenAgentCount = LegacySession.ScriptedPenAgentCount;
        public static Vector2 OutpostStallPosition => DevMapLayout.OutpostStallPosition;
        public const ushort SeedGatherBountyId = LegacySession.SeedGatherBountyId;
        public const ushort SeedScoutBountyId = LegacySession.SeedScoutBountyId;

        private readonly IEntityIdAllocator _idAllocator;
        private readonly SimulationSettings _settings;
        private readonly IWorldMap _worldMap;
        private readonly List<ISessionDefinition> _sessions = new List<ISessionDefinition>(2);
        private readonly ISessionDefinition _defaultSession;
        private ISessionDefinition _current;
        private LoadedWorld _world;
        private GameObject _agentsRoot;

        /// <summary>The map loaded by the last respawn, with its spawned entity ids.</summary>
        public LoadedWorld World => _world;

        public IReadOnlyList<ISessionDefinition> Sessions => _sessions;
        public ISessionDefinition Current => _current ?? _defaultSession;
        public event System.Action<ISessionDefinition> SessionChanged;

        internal UnityEngine.GUID NextEntityId() => _idAllocator != null ? _idAllocator.Next() : UnityEngine.GUID.Generate();

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
            IReadOnlyList<ISessionDefinition> sessions)
        {
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
            if (_sessions.Count == 0) _sessions.Add(new LegacySession());
            _defaultSession = Find(LegacySession.SessionId) ?? _sessions[0];

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
            _current = session;

            CleanupExistingWorld();

            _agents.Clear();
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
            MapView.Show(map);

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
                if (poi.Kind != PoiKind.Hearth) continue;
                SpawnDemoCampfire(propsRoot, poi.Position, poi.Radius, out var anchorId);
                _world.RegisterEntity(poi.Key, anchorId);
            }

            // 3. Session content: agents, relationships, groups, stalls, staged bounties
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

        /// <summary>Spawns one agent view + simulation state (called by <see cref="SessionBuilder.SpawnAgent"/>).</summary>
        internal AutonomousAgent SpawnAgent(Vector2 position, string label, bool seededPersonality)
        {
            int i = _agents.Count;
            GameObject agentGo = new GameObject($"SwarmAgent_{i}");
            if (_agentsRoot != null) agentGo.transform.SetParent(_agentsRoot.transform);
            agentGo.transform.position = position;

            var sr = agentGo.AddComponent<SpriteRenderer>();
            sr.sprite = AgentSpriteLibrary.GetAgentSprite(AgentVisualState.Idle, 0);
            agentGo.transform.localScale = Vector3.one * 0.7f; // Clear, readable icon size

            // Register them as pawns so they appear in the GridVisualizer
            var rb = agentGo.AddComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            rb.bodyType = RigidbodyType2D.Kinematic; // They move via interpolation, not physics

            var col = agentGo.AddComponent<CircleCollider2D>();
            col.radius = 0.35f;

            var controller = agentGo.AddComponent<NodeController>();
            controller.Type = EntityType.Pawn;
            controller.Initialize(NextEntityId(), new NodeConfiguration(speed: 2f));
            _registry.RegisterNode(controller);
            SimLog.RegisterAgentName(controller.Id, label);

            var agent = agentGo.AddComponent<AutonomousAgent>();
            agent.Initialize(controller.Id, _spatialPartition, new Simpiens.Cognition.Evaluators.ICognitiveEvaluator[] { _mentalBreakEvaluator, _altruismEvaluator, _evaluator }, _simulationManager, _clock, _planner, _actionGraph, _goals, _cognitionService);

            if (seededPersonality && agent.State != null)
            {
                ulong seed = _settings != null ? _settings.Seed : SimulationSettings.DefaultSeed;
                agent.State.Personality = Simpiens.Cognition.Contracts.AgentPersonality.Generate(seed, controller.Id);
                SimLog.Cognition.ZLogInformation($"{SimLog.Agent(controller.Id)} personality: {agent.State.Personality.ToString()}");
            }

            // Attach AgentView for animated visual feedback
            agentGo.AddComponent<AgentView>();

            _agents.Add(agent);
            return agent;
        }

        private void SpawnResourceNode(GameObject root, Vector2 pos, int yield, ItemType yieldItem, uint regrowTicks, out UnityEngine.GUID resId)
        {
            var resourceGo = new GameObject($"{yieldItem}Node_{pos.x}_{pos.y}");
            resourceGo.transform.SetParent(root.transform);
            resourceGo.transform.position = pos;

            var sr = resourceGo.AddComponent<SpriteRenderer>();
            sr.sprite = AgentSpriteLibrary.GetResourceSprite(yieldItem);
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

        private void SpawnDemoCampfire(GameObject root, Vector2 pos, float radius, out UnityEngine.GUID anchorId)
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
