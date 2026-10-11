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
    public class SwarmSpawner : IStartable, System.IDisposable
    {
        private readonly ISpatialPartition _spatialPartition;
        private readonly IAsyncPathfinder _pathfinder;
        private readonly IWorldRegistry _registry;

        private readonly List<AutonomousAgent> _agents = new List<AutonomousAgent>(1000);

        private readonly Simpiens.Cognition.Planning.IGoapPlanner _planner;
        private readonly Simpiens.Cognition.Planning.ActionGraph _actionGraph;
        private readonly List<Simpiens.Cognition.Planning.GoapGoal> _goals;

        // 24 scripted pen agents + 6 free "crawlers" in the Outpost Commons (no seeded relationships or memory)
        public int SpawnCount = 30;
        public const int ScriptedPenAgentCount = 24;

        // Geography (walls, sectors, nodes, campfires, stall) comes from the map definition (Epic MAP Phase 1)
        public static Vector2 OutpostStallPosition => DevMapLayout.OutpostStallPosition;
        public const ushort SeedGatherBountyId = 1;
        public const ushort SeedScoutBountyId = 2;

        private readonly IEntityIdAllocator _idAllocator;
        private readonly SimulationSettings _settings;
        private readonly IWorldMap _worldMap;
        private LoadedWorld _world;

        /// <summary>The map loaded by the last respawn, with its spawned entity ids.</summary>
        public LoadedWorld World => _world;

        private UnityEngine.GUID NextEntityId() => _idAllocator != null ? _idAllocator.Next() : UnityEngine.GUID.Generate();

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
            IWorldMap worldMap)
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

        public void Respawn()
        {
            CleanupExistingWorld();

            _agents.Clear();
            _registry?.Clear();
            _idAllocator?.Reset(); // same seed + same spawn order => same entity ids on every respawn
            SimLog.ClearAgentNames();
            _simulationManager?.ResetState();

            // 1. Static geography: partition, walls, sectors (simulation) and the map view
            var map = DevMapLayout.Definition;
            _world = _worldMap.Load(map);
            MapView.Show(map);

            // Create roots for organization
            GameObject root = new GameObject("[SwarmAgents]");
            GameObject propsRoot = new GameObject("[MapEntities]");

            // 2. Resource nodes and campfires from the map definition (keys resolve to ids for the pen scripts)
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

            var bushFearId = _world.GetEntityId(DevMapLayout.Pen1ContestedBush);
            var bushRespectId = _world.GetEntityId(DevMapLayout.Pen2ContestedBush);
            var bushRivalryId = _world.GetEntityId(DevMapLayout.Pen3ContestedBush);
            var campfirePen4Id = _world.GetEntityId(DevMapLayout.Pen4Campfire);
            var campfirePen5Id = _world.GetEntityId(DevMapLayout.Pen5Campfire);
            var bushPen7Id = _world.GetEntityId(DevMapLayout.Pen7Bush);
            var campfirePen8Id = _world.GetEntityId(DevMapLayout.Pen8Campfire);
            var bushPen9Id = _world.GetEntityId(DevMapLayout.Pen9Bush);
            var bushPen10Id = _world.GetEntityId(DevMapLayout.Pen10Bush);

            for (int i = 0; i < SpawnCount; i++)
            {
                GameObject agentGo = new GameObject($"SwarmAgent_{i}");
                agentGo.transform.SetParent(root.transform);

                // Configure predetermined positions for the 10 demonstration scenarios within their respective play pens
                if (i == 0) agentGo.transform.position = new Vector2(-8f, 1.8f);       // Pen 1: Dominant
                else if (i == 1) agentGo.transform.position = new Vector2(-8f, -1.0f); // Pen 1: Submissive
                else if (i == 2) agentGo.transform.position = new Vector2(-4f, 1.8f);  // Pen 2: Respected
                else if (i == 3) agentGo.transform.position = new Vector2(-4f, -1.0f); // Pen 2: Deferent
                else if (i == 4) agentGo.transform.position = new Vector2(0f, 1.5f);   // Pen 3: Rival A
                else if (i == 5) agentGo.transform.position = new Vector2(0f, -1.5f);  // Pen 3: Rival B
                else if (i == 6) agentGo.transform.position = new Vector2(4f, 1.8f);   // Pen 4: Altruistic Donor
                else if (i == 7) agentGo.transform.position = new Vector2(4f, -1.8f);  // Pen 4: Starving Companion
                else if (i == 8) agentGo.transform.position = new Vector2(8f, 1.8f);   // Pen 5: Selfish Peer
                else if (i == 9) agentGo.transform.position = new Vector2(8f, -1.8f);  // Pen 5: Starving Stranger
                // Row 2 (Pens 6–10, Epic 8):
                else if (i == 10) agentGo.transform.position = new Vector2(-8f, -7.5f);  // Pen 6: Group 1 Leader
                else if (i == 11) agentGo.transform.position = new Vector2(-8f, -10.5f); // Pen 6: Compliant Subordinate
                else if (i == 12) agentGo.transform.position = new Vector2(-4f, -7.2f);  // Pen 7: Group 2 Leader
                else if (i == 13) agentGo.transform.position = new Vector2(-4f, -9.0f);  // Pen 7: Starving Defiant Subordinate
                else if (i == 14) agentGo.transform.position = new Vector2(0f, -8.0f);   // Pen 8: Respected Candidate
                else if (i == 15) agentGo.transform.position = new Vector2(-0.8f, -9.8f);// Pen 8: Elector Peer A
                else if (i == 16) agentGo.transform.position = new Vector2(0.8f, -9.8f); // Pen 8: Elector Peer B
                else if (i == 17) agentGo.transform.position = new Vector2(4f, -8.0f);   // Pen 9: Collapsed Leader
                else if (i == 18) agentGo.transform.position = new Vector2(4f, -9.5f);   // Pen 9: Dominance Challenger
                else if (i == 19) agentGo.transform.position = new Vector2(4.8f, -9.0f); // Pen 9: Group 3 Member
                else if (i == 20) agentGo.transform.position = new Vector2(7.2f, -8.0f); // Pen 10: Discredited Leader
                else if (i == 21) agentGo.transform.position = new Vector2(7.2f, -9.5f); // Pen 10: Loyalist
                else if (i == 22) agentGo.transform.position = new Vector2(8.8f, -8.0f); // Pen 10: Dissident Contender
                else if (i == 23) agentGo.transform.position = new Vector2(8.8f, -9.5f); // Pen 10: Dissident Follower
                else
                {
                    // Free crawlers: spread across the north edge of the Outpost Commons
                    int c = i - ScriptedPenAgentCount;
                    agentGo.transform.position = new Vector2(-3f + (c % 4) * 2f, 13f + (c / 4) * 1.5f);
                }

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
                SimLog.RegisterAgentName(controller.Id, AgentLabel(i));

                var agent = agentGo.AddComponent<AutonomousAgent>();
                agent.Initialize(controller.Id, _spatialPartition, new Simpiens.Cognition.Evaluators.ICognitiveEvaluator[] { _mentalBreakEvaluator, _altruismEvaluator, _evaluator }, _simulationManager, _clock, _planner, _actionGraph, _goals, _cognitionService);

                // Free crawlers get seeded personalities (review §3.2); scripted pen agents stay neutral so the
                // Epic 7 / Beta / 8 demonstrations keep the behaviour they were tuned with (producer decision 2026-10-09)
                if (i >= ScriptedPenAgentCount && agent.State != null)
                {
                    ulong seed = _settings != null ? _settings.Seed : SimulationSettings.DefaultSeed;
                    agent.State.Personality = Simpiens.Cognition.Contracts.AgentPersonality.Generate(seed, controller.Id);
                    SimLog.Cognition.ZLogInformation($"{SimLog.Agent(controller.Id)} personality: {agent.State.Personality.ToString()}");
                }

                // Attach AgentView for animated visual feedback
                agentGo.AddComponent<AgentView>();

                _agents.Add(agent);
            }

            // Seed relational dyads for visual demonstration
            if (_agents.Count >= 6)
            {
                // Dyad 1 (Fear Asymmetry): Agent 1 fears Agent 0 (Fear = 75). Agent 1 will yield Bush 0 to Agent 0.
                _agents[0].Hunger = 60f;
                _agents[1].Hunger = 60f;
                _agents[1].Memory.RecordInteraction(_agents[0].AgentId, deltaWarmth: 0, deltaTrust: 0, deltaFear: 75, deltaRespect: 0, 0);
                _agents[0].Memory.SpatialMemoryMap[bushFearId] = new Simpiens.Cognition.Memory.SpatialMemoryRecord(bushFearId, EntityType.Resource, new Vector2Int(-8, 0), 0);
                _agents[1].Memory.SpatialMemoryMap[bushFearId] = new Simpiens.Cognition.Memory.SpatialMemoryRecord(bushFearId, EntityType.Resource, new Vector2Int(-8, 0), 0);

                // Dyad 2 (Respect Asymmetry): Agent 3 respects Agent 2 (Respect = 80). Agent 3 will defer Bush 1 to Agent 2.
                _agents[2].Hunger = 60f;
                _agents[3].Hunger = 60f;
                _agents[3].Memory.RecordInteraction(_agents[2].AgentId, deltaWarmth: 20, deltaTrust: 20, deltaFear: 0, deltaRespect: 80, 0);
                _agents[2].Memory.SpatialMemoryMap[bushRespectId] = new Simpiens.Cognition.Memory.SpatialMemoryRecord(bushRespectId, EntityType.Resource, new Vector2Int(-4, 0), 0);
                _agents[3].Memory.SpatialMemoryMap[bushRespectId] = new Simpiens.Cognition.Memory.SpatialMemoryRecord(bushRespectId, EntityType.Resource, new Vector2Int(-4, 0), 0);

                // Dyad 3 (Symmetrical Rivalry): Agent 4 and Agent 5 are mutual rivals (Warmth -40, Respect -20).
                _agents[4].Hunger = 60f;
                _agents[5].Hunger = 60f;
                _agents[4].Memory.RecordInteraction(_agents[5].AgentId, deltaWarmth: -40, deltaTrust: -30, deltaFear: 0, deltaRespect: -20, 0);
                _agents[5].Memory.RecordInteraction(_agents[4].AgentId, deltaWarmth: -40, deltaTrust: -30, deltaFear: 0, deltaRespect: -20, 0);
                _agents[4].Memory.SpatialMemoryMap[bushRivalryId] = new Simpiens.Cognition.Memory.SpatialMemoryRecord(bushRivalryId, EntityType.Resource, new Vector2Int(0, 0), 0);
                _agents[5].Memory.SpatialMemoryMap[bushRivalryId] = new Simpiens.Cognition.Memory.SpatialMemoryRecord(bushRivalryId, EntityType.Resource, new Vector2Int(0, 0), 0);
            }

            if (_agents.Count >= 10)
            {
                // Dyad 4 (Altruistic Companions & Hearth): Agent 6 loves Agent 7 (Warmth = 85, Trust = 80).
                // Agent 6 is sated (Hunger = 20), Agent 7 is starving (Hunger = 90).
                // Agent 6 will approach Agent 7, feed Agent 7, and once sated, gather around the Pen 4 campfire!
                _agents[6].Hunger = 20f;
                _agents[7].Hunger = 90f;
                _agents[6].Memory.RecordInteraction(_agents[7].AgentId, deltaWarmth: 85, deltaTrust: 80, deltaFear: 0, deltaRespect: 20, 0);
                _agents[7].Memory.RecordInteraction(_agents[6].AgentId, deltaWarmth: 85, deltaTrust: 80, deltaFear: 0, deltaRespect: 20, 0);
                _agents[6].Memory.SpatialMemoryMap[campfirePen4Id] = new Simpiens.Cognition.Memory.SpatialMemoryRecord(campfirePen4Id, EntityType.Anchor, new Vector2Int(4, 0), 0);
                _agents[7].Memory.SpatialMemoryMap[campfirePen4Id] = new Simpiens.Cognition.Memory.SpatialMemoryRecord(campfirePen4Id, EntityType.Anchor, new Vector2Int(4, 0), 0);

                // Dyad 5 (Selfish Indifference & Hearth Isolation): Agent 8 is estranged/hostile to Agent 9 (Warmth = -20, Trust = -10).
                // Agent 8 is sated (Hunger = 20), Agent 9 is starving (Hunger = 90).
                // Agent 8 ignores Agent 9 and relaxes alone by the Pen 5 campfire; starving Agent 9 receives no aid.
                _agents[8].Hunger = 20f;
                _agents[9].Hunger = 90f;
                _agents[8].Memory.RecordInteraction(_agents[9].AgentId, deltaWarmth: -20, deltaTrust: -10, deltaFear: 0, deltaRespect: -10, 0);
                _agents[9].Memory.RecordInteraction(_agents[8].AgentId, deltaWarmth: -20, deltaTrust: -10, deltaFear: 0, deltaRespect: -10, 0);
                _agents[8].Memory.SpatialMemoryMap[campfirePen5Id] = new Simpiens.Cognition.Memory.SpatialMemoryRecord(campfirePen5Id, EntityType.Anchor, new Vector2Int(8, 0), 0);
                _agents[9].Memory.SpatialMemoryMap[campfirePen5Id] = new Simpiens.Cognition.Memory.SpatialMemoryRecord(campfirePen5Id, EntityType.Anchor, new Vector2Int(8, 0), 0);
            }

            if (_agents.Count >= 24 && _simulationManager?.Groups != null)
            {
                var groupRegistry = _simulationManager.Groups;

                // Pen 6 (Collective Directives & Compliance):
                // Agent 10 is Leader. Agent 11 is Follower with high Allegiance (85) and Respect (70).
                _agents[10].Hunger = 20f;
                _agents[11].Hunger = 20f;
                _agents[11].Memory.RecordInteraction(_agents[10].AgentId, deltaWarmth: 50, deltaTrust: 50, deltaFear: 0, deltaRespect: 70, 0);

                byte g6 = groupRegistry.CreateGroup(Simpiens.Cognition.Groups.GroupType.Settlement, _agents[10].AgentId, new Vector2(-8f, -9f), initialCohesion: 80);
                groupRegistry.AddMember(g6, _agents[11].AgentId, Simpiens.Cognition.Groups.RoleRank.Follower, initialAllegiance: 85);
                _agents[10].Membership = new Simpiens.Cognition.Groups.GroupMembership(g6, Simpiens.Cognition.Groups.RoleRank.Leader, 100, 0);
                _agents[11].Membership = new Simpiens.Cognition.Groups.GroupMembership(g6, Simpiens.Cognition.Groups.RoleRank.Follower, 85, 0);

                var dir6 = new Simpiens.Cognition.Groups.GroupDirective(
                    Simpiens.Cognition.Groups.DirectiveType.RallyAtLocation,
                    groupId: g6,
                    leaderId: _agents[10].AgentId,
                    targetLocation: new Vector2(-8f, -7.5f),
                    targetEntityId: default,
                    issuedTick: 0,
                    expirationTick: 5000,
                    targetRadius: 2.0f);
                groupRegistry.SetDirective(g6, in dir6);

                // Pen 7 (Directives & Passive Defiance):
                // Agent 12 is Leader. Agent 13 is Follower with low Allegiance (15), low Respect (-30), and starving (Hunger = 85).
                // Agent 13 knows of the bush at (-4, -11). Agent 13 defies rally directive to forage food.
                _agents[12].Hunger = 20f;
                _agents[13].Hunger = 85f;
                _agents[13].Memory.RecordInteraction(_agents[12].AgentId, deltaWarmth: -20, deltaTrust: -20, deltaFear: 0, deltaRespect: -30, 0);
                _agents[13].Memory.SpatialMemoryMap[bushPen7Id] = new Simpiens.Cognition.Memory.SpatialMemoryRecord(bushPen7Id, EntityType.Resource, new Vector2Int(-4, -11), 0);

                byte g7 = groupRegistry.CreateGroup(Simpiens.Cognition.Groups.GroupType.TemporaryParty, _agents[12].AgentId, new Vector2(-4f, -9f), initialCohesion: 40);
                groupRegistry.AddMember(g7, _agents[13].AgentId, Simpiens.Cognition.Groups.RoleRank.Follower, initialAllegiance: 15);
                _agents[12].Membership = new Simpiens.Cognition.Groups.GroupMembership(g7, Simpiens.Cognition.Groups.RoleRank.Leader, 100, 0);
                _agents[13].Membership = new Simpiens.Cognition.Groups.GroupMembership(g7, Simpiens.Cognition.Groups.RoleRank.Follower, 15, 0);

                var dir7 = new Simpiens.Cognition.Groups.GroupDirective(
                    Simpiens.Cognition.Groups.DirectiveType.RallyAtLocation,
                    groupId: g7,
                    leaderId: _agents[12].AgentId,
                    targetLocation: new Vector2(-4f, -7.2f),
                    targetEntityId: default,
                    issuedTick: 0,
                    expirationTick: 5000,
                    targetRadius: 2.0f);
                groupRegistry.SetDirective(g7, in dir7);

                // Pen 8 (Organic Hearth Emergence):
                // Agents 14, 15, 16 are unaligned and gather at the campfire anchor.
                // High mutual warmth/trust; high respect for Agent 14. Campfire consensus elects Agent 14.
                _agents[14].Hunger = 20f;
                _agents[15].Hunger = 20f;
                _agents[16].Hunger = 20f;

                _agents[14].Membership = Simpiens.Cognition.Groups.GroupMembership.None;
                _agents[15].Membership = Simpiens.Cognition.Groups.GroupMembership.None;
                _agents[16].Membership = Simpiens.Cognition.Groups.GroupMembership.None;

                _agents[14].Memory.SpatialMemoryMap[campfirePen8Id] = new Simpiens.Cognition.Memory.SpatialMemoryRecord(campfirePen8Id, EntityType.Anchor, new Vector2Int(0, -9), 0);
                _agents[15].Memory.SpatialMemoryMap[campfirePen8Id] = new Simpiens.Cognition.Memory.SpatialMemoryRecord(campfirePen8Id, EntityType.Anchor, new Vector2Int(0, -9), 0);
                _agents[16].Memory.SpatialMemoryMap[campfirePen8Id] = new Simpiens.Cognition.Memory.SpatialMemoryRecord(campfirePen8Id, EntityType.Anchor, new Vector2Int(0, -9), 0);

                _agents[14].Memory.RecordInteraction(_agents[15].AgentId, deltaWarmth: 60, deltaTrust: 60, deltaFear: 0, deltaRespect: 40, 0);
                _agents[15].Memory.RecordInteraction(_agents[14].AgentId, deltaWarmth: 60, deltaTrust: 60, deltaFear: 0, deltaRespect: 75, 0);

                _agents[14].Memory.RecordInteraction(_agents[16].AgentId, deltaWarmth: 65, deltaTrust: 65, deltaFear: 0, deltaRespect: 40, 0);
                _agents[16].Memory.RecordInteraction(_agents[14].AgentId, deltaWarmth: 65, deltaTrust: 65, deltaFear: 0, deltaRespect: 80, 0);

                _agents[15].Memory.RecordInteraction(_agents[16].AgentId, deltaWarmth: 50, deltaTrust: 50, deltaFear: 0, deltaRespect: 20, 0);
                _agents[16].Memory.RecordInteraction(_agents[15].AgentId, deltaWarmth: 50, deltaTrust: 50, deltaFear: 0, deltaRespect: 20, 0);

                // Pen 9 (Leadership Succession Contest - Dominance Challenge):
                // Group 3 with Cohesion 35. Incumbent leader Agent 17 collapsed from exhaustion.
                // Challenger Agent 18 challenges for dominance and wins.
                _agents[17].CollapseFromExhaustion();
                _agents[17].Hunger = 50f;
                _agents[18].Hunger = 20f;
                _agents[19].Hunger = 20f;

                _agents[18].Memory.SpatialMemoryMap[bushPen9Id] = new Simpiens.Cognition.Memory.SpatialMemoryRecord(bushPen9Id, EntityType.Resource, new Vector2Int(4, -11), 0);
                _agents[19].Memory.SpatialMemoryMap[bushPen9Id] = new Simpiens.Cognition.Memory.SpatialMemoryRecord(bushPen9Id, EntityType.Resource, new Vector2Int(4, -11), 0);

                byte g9 = groupRegistry.CreateGroup(Simpiens.Cognition.Groups.GroupType.TemporaryParty, _agents[17].AgentId, new Vector2(4f, -9f), initialCohesion: 35);
                groupRegistry.AddMember(g9, _agents[18].AgentId, Simpiens.Cognition.Groups.RoleRank.Follower, initialAllegiance: 60);
                groupRegistry.AddMember(g9, _agents[19].AgentId, Simpiens.Cognition.Groups.RoleRank.Follower, initialAllegiance: 65);
                _agents[17].Membership = new Simpiens.Cognition.Groups.GroupMembership(g9, Simpiens.Cognition.Groups.RoleRank.Leader, 100, 0);
                _agents[18].Membership = new Simpiens.Cognition.Groups.GroupMembership(g9, Simpiens.Cognition.Groups.RoleRank.Follower, 60, 0);
                _agents[19].Membership = new Simpiens.Cognition.Groups.GroupMembership(g9, Simpiens.Cognition.Groups.RoleRank.Follower, 65, 0);

                _agents[18].Memory.RecordInteraction(_agents[17].AgentId, deltaWarmth: -20, deltaTrust: -10, deltaFear: 0, deltaRespect: -20, 0);
                _agents[17].Memory.RecordInteraction(_agents[18].AgentId, deltaWarmth: -10, deltaTrust: -10, deltaFear: 65, deltaRespect: 0, 0);
                _agents[19].Memory.RecordInteraction(_agents[18].AgentId, deltaWarmth: 10, deltaTrust: 20, deltaFear: 30, deltaRespect: 60, 0);

                // Pen 10 (Factional Schism):
                // Group 4 with critical Cohesion 15. Leader Agent 20 is discredited (negative follower respect).
                // Group fractures into two splinter bands: 20+21 (Loyalists) and 22+23 (Dissidents).
                _agents[20].Hunger = 20f;
                _agents[21].Hunger = 20f;
                _agents[22].Hunger = 20f;
                _agents[23].Hunger = 20f;

                _agents[20].Memory.SpatialMemoryMap[bushPen10Id] = new Simpiens.Cognition.Memory.SpatialMemoryRecord(bushPen10Id, EntityType.Resource, new Vector2Int(8, -11), 0);
                _agents[21].Memory.SpatialMemoryMap[bushPen10Id] = new Simpiens.Cognition.Memory.SpatialMemoryRecord(bushPen10Id, EntityType.Resource, new Vector2Int(8, -11), 0);
                _agents[22].Memory.SpatialMemoryMap[bushPen10Id] = new Simpiens.Cognition.Memory.SpatialMemoryRecord(bushPen10Id, EntityType.Resource, new Vector2Int(8, -11), 0);
                _agents[23].Memory.SpatialMemoryMap[bushPen10Id] = new Simpiens.Cognition.Memory.SpatialMemoryRecord(bushPen10Id, EntityType.Resource, new Vector2Int(8, -11), 0);

                byte g10 = groupRegistry.CreateGroup(Simpiens.Cognition.Groups.GroupType.TemporaryParty, _agents[20].AgentId, new Vector2(8f, -9f), initialCohesion: 15);
                groupRegistry.AddMember(g10, _agents[21].AgentId, Simpiens.Cognition.Groups.RoleRank.Follower, initialAllegiance: 75);
                groupRegistry.AddMember(g10, _agents[22].AgentId, Simpiens.Cognition.Groups.RoleRank.Follower, initialAllegiance: 30);
                groupRegistry.AddMember(g10, _agents[23].AgentId, Simpiens.Cognition.Groups.RoleRank.Follower, initialAllegiance: 30);
                _agents[20].Membership = new Simpiens.Cognition.Groups.GroupMembership(g10, Simpiens.Cognition.Groups.RoleRank.Leader, 100, 0);
                _agents[21].Membership = new Simpiens.Cognition.Groups.GroupMembership(g10, Simpiens.Cognition.Groups.RoleRank.Follower, 75, 0);
                _agents[22].Membership = new Simpiens.Cognition.Groups.GroupMembership(g10, Simpiens.Cognition.Groups.RoleRank.Follower, 30, 0);
                _agents[23].Membership = new Simpiens.Cognition.Groups.GroupMembership(g10, Simpiens.Cognition.Groups.RoleRank.Follower, 30, 0);

                // Agent 21 is loyal to Agent 20
                _agents[21].Memory.RecordInteraction(_agents[20].AgentId, deltaWarmth: 80, deltaTrust: 80, deltaFear: 0, deltaRespect: 70, 0);

                // Agents 22 and 23 hate Agent 20 (causing negative average follower respect)
                _agents[22].Memory.RecordInteraction(_agents[20].AgentId, deltaWarmth: -60, deltaTrust: -60, deltaFear: 0, deltaRespect: -70, 0);
                _agents[23].Memory.RecordInteraction(_agents[20].AgentId, deltaWarmth: -60, deltaTrust: -60, deltaFear: 0, deltaRespect: -70, 0);

                // Agents 22 and 23 have high mutual loyalty
                _agents[22].Memory.RecordInteraction(_agents[23].AgentId, deltaWarmth: 80, deltaTrust: 80, deltaFear: 0, deltaRespect: 60, 0);
                _agents[23].Memory.RecordInteraction(_agents[22].AgentId, deltaWarmth: 80, deltaTrust: 80, deltaFear: 0, deltaRespect: 60, 0);
            }

            // Seed the Outpost Commons trade stall and initial bounties (Epic UI-2 / §3.1 supply loop)
            if (_simulationManager != null)
            {
                var stallId = NextEntityId();
                map.TryGetPoi(DevMapLayout.OutpostStall, out var stallPoi);
                _world.RegisterEntity(DevMapLayout.OutpostStall, stallId);
                var outpostStall = new TradeStallData(
                    stallId,
                    position: stallPoi.Position,
                    rationSellPrice: 15,
                    rationStock: 50,
                    oreBuyPrice: 20,
                    oreStock: 10,
                    herbBuyPrice: 25,
                    herbStock: 10,
                    treasuryGold: 250,
                    interactionRadius: stallPoi.Radius);
                _simulationManager.RegisterTradeStall(outpostStall);

                // Ledger baseline: all gold present at session start enters through one named faucet,
                // so the conservation invariant (circulating == faucets - sinks) holds from tick 0
                uint seededGold = outpostStall.TreasuryGold;
                for (int i = 0; i < _agents.Count; i++)
                {
                    var inv = _agents[i].State?.Inventory;
                    if (inv != null) seededGold += (uint)inv.Gold;
                }
                _simulationManager.EconomyLedger?.RecordFaucet(seededGold, "InitialSeed");

                // Both seeded bounties go through the same escrow path as player-posted ones.
                // TargetLocation is left default so the simulation derives it from the target sector.
                var gatherBounty = new BountyContract(
                    bountyId: SeedGatherBountyId,
                    type: BountyType.GatherResource,
                    targetSectorId: DevMapLayout.OreRidgeSectorId,
                    targetItemType: ItemType.IronOre,
                    targetQuantity: 1,
                    rewardGold: 25,
                    issuedTick: 0,
                    expirationTick: 6000,
                    creatorStallId: stallId,
                    status: BountyStatus.Open);
                _simulationManager.RegisterBounty(gatherBounty);

                var scoutBounty = new BountyContract(
                    bountyId: SeedScoutBountyId,
                    type: BountyType.ScoutSector,
                    targetSectorId: DevMapLayout.HerbHollowSectorId,
                    targetItemType: ItemType.None,
                    targetQuantity: 1,
                    rewardGold: 15,
                    issuedTick: 0,
                    expirationTick: 6000,
                    creatorStallId: stallId,
                    status: BountyStatus.Open);
                _simulationManager.RegisterBounty(scoutBounty);

                SimLog.RegisterAgentName(stallId, "OutpostStall");
                SimLog.Bootstrap.ZLogInformation($"Session spawned: {_agents.Count} agents ({Mathf.Max(0, _agents.Count - ScriptedPenAgentCount)} free crawlers), map '{map.Name}', stall at {stallPoi.Position}, seeded gold {seededGold}g, bounties #{SeedGatherBountyId} (Gather) and #{SeedScoutBountyId} (Scout)");
            }
        }

        /// <summary>
        /// Readable log label: scripted pen agents are "PenN.AgentI", free agents are "CrawlerC".
        /// </summary>
        public static string AgentLabel(int index)
        {
            if (index >= ScriptedPenAgentCount) return $"Crawler{index - ScriptedPenAgentCount}";
            int pen = index < 10 ? index / 2 + 1
                : index < 14 ? (index - 10) / 2 + 6
                : index < 17 ? 8
                : index < 20 ? 9
                : 10;
            return $"Pen{pen}.Agent{index}";
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
