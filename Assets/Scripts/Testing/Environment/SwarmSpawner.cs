using System.Collections.Generic;
using Simpiens.Cognition;
using Simpiens.Cognition.Pathfinding;
using Simpiens.Diagnostics;
using Simpiens.Entities;
using Simpiens.Simulation;
using Simpiens.Simulation.Economy;
using Simpiens.Simulation.Spatial;
using UnityEngine;
using VContainer;
using VContainer.Unity;
using ZLogger;

namespace Simpiens.Testing.Environment
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

        // Outpost Commons layout (open yard north of the pens)
        public static readonly Vector2 OutpostStallPosition = new Vector2(0f, 9f);
        public const ushort SeedGatherBountyId = 1;
        public const ushort SeedScoutBountyId = 2;

        // Regrowth is on everywhere (pens included); durations in logical ticks
        public const uint FoodRegrowTicks = 450;
        public const uint HerbRegrowTicks = 600;
        public const uint OreRegrowTicks = 900;

        private readonly IEntityIdAllocator _idAllocator;

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
            IEntityIdAllocator idAllocator)
        {
            _idAllocator = idAllocator;
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

            _actionGraph = new Simpiens.Cognition.Planning.ActionGraph();
            _actionGraph.RegisterAction(new Simpiens.Cognition.Planning.Actions.SearchResourceAction(pathfinder: _pathfinder));
            _actionGraph.RegisterAction(new Simpiens.Cognition.Planning.Actions.TravelToResourceAction(pathfinder: _pathfinder));
            _actionGraph.RegisterAction(new Simpiens.Cognition.Planning.Actions.HarvestResourceAction(pathfinder: _pathfinder));
            _actionGraph.RegisterAction(new Simpiens.Cognition.Planning.Actions.EatCarriedFoodAction());
            _actionGraph.RegisterAction(new Simpiens.Cognition.Planning.Actions.RestAction());
            _actionGraph.RegisterAction(new Simpiens.Cognition.Planning.Actions.TravelToPeerAction());
            _actionGraph.RegisterAction(new Simpiens.Cognition.Planning.Actions.ShareFoodAction(pathfinder: _pathfinder));
            _actionGraph.RegisterAction(new Simpiens.Cognition.Planning.Actions.TravelToAnchorAction(_pathfinder));
            _actionGraph.RegisterAction(new Simpiens.Cognition.Planning.Actions.RelaxAtAnchorAction());
            _actionGraph.RegisterAction(new Simpiens.Cognition.Planning.Actions.TravelToDirectiveAction(_pathfinder));
            _actionGraph.RegisterAction(new Simpiens.Cognition.Planning.Actions.ExecuteDirectiveAction());
            _actionGraph.RegisterAction(new Simpiens.Cognition.Planning.Actions.TravelToStallAction(_pathfinder));
            _actionGraph.RegisterAction(new Simpiens.Cognition.Planning.Actions.BuyProvisionsAction());
            _actionGraph.RegisterAction(new Simpiens.Cognition.Planning.Actions.SellSalvageAction());
            _actionGraph.RegisterAction(new Simpiens.Cognition.Planning.Actions.ClaimGatherBountyAction());
            _actionGraph.RegisterAction(new Simpiens.Cognition.Planning.Actions.ClaimScoutBountyAction());
            _actionGraph.RegisterAction(new Simpiens.Cognition.Planning.Actions.DeliverBountyAction());
            _actionGraph.RegisterAction(new Simpiens.Cognition.Planning.Actions.SearchSalvageAction(_pathfinder));
            _actionGraph.RegisterAction(new Simpiens.Cognition.Planning.Actions.GatherSalvageAction(_pathfinder));
            _actionGraph.RegisterAction(new Simpiens.Cognition.Planning.Actions.ScoutSectorAction(_pathfinder));
            _actionGraph.RegisterAction(new Simpiens.Cognition.Planning.Actions.ReportScoutBountyAction());

            _goals = new List<Simpiens.Cognition.Planning.GoapGoal>
            {
                new Simpiens.Cognition.Planning.Goals.SatiateHungerGoal(),
                new Simpiens.Cognition.Planning.Goals.RestGoal(),
                new Simpiens.Cognition.Planning.Goals.SocializeGoal(),
                new Simpiens.Cognition.Planning.Goals.AidCompanionGoal(),
                new Simpiens.Cognition.Planning.Goals.GatherAtAnchorGoal(),
                new Simpiens.Cognition.Planning.Goals.ComplyDirectiveGoal(),
                new Simpiens.Cognition.Planning.Goals.PurchaseProvisionsGoal(),
                new Simpiens.Cognition.Planning.Goals.LiquidateSalvageGoal(),
                new Simpiens.Cognition.Planning.Goals.FulfillBountyGoal(),
                new Simpiens.Cognition.Planning.Goals.EarnIncomeGoal()
            };
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

            // Create roots for organization
            GameObject root = new GameObject("[SwarmAgents]");
            GameObject bushRoot = new GameObject("[DemoContestedBushes]");
            GameObject campfireRoot = new GameObject("[DemoCampfires]");
            GameObject pensRoot = new GameObject("[PlayPens]");
            GameObject commonsRoot = new GameObject("[OutpostCommons]");

            // 1. Construct the 5 bordered Play Pens
            SpawnPlayPens(pensRoot);

            // 2. Pre-spawn demonstration bushes inside each pen
            // Pen 1 (Fear Yield): Center bush (1 yield) and secondary foraging bush (3 yields)
            SpawnResourceNode(bushRoot, new Vector2(-8f, 0f), 1, ItemType.Ration, FoodRegrowTicks, out var bushFearId);
            SpawnResourceNode(bushRoot, new Vector2(-8f, -2.2f), 3, ItemType.Ration, FoodRegrowTicks, out _);

            // Pen 2 (Respect Defer): Center bush (1 yield) and secondary foraging bush (3 yields)
            SpawnResourceNode(bushRoot, new Vector2(-4f, 0f), 1, ItemType.Ration, FoodRegrowTicks, out var bushRespectId);
            SpawnResourceNode(bushRoot, new Vector2(-4f, -2.2f), 3, ItemType.Ration, FoodRegrowTicks, out _);

            // Pen 3 (Rivalry Contest): Center contested bush (1 yield, no secondary bush)
            SpawnResourceNode(bushRoot, new Vector2(0f, 0f), 1, ItemType.Ration, FoodRegrowTicks, out var bushRivalryId);

            // 3. Pre-spawn demonstration campfires inside Pens 4 and 5
            SpawnDemoCampfire(campfireRoot, new Vector2(4f, 0f), out var campfirePen4Id);
            SpawnDemoCampfire(campfireRoot, new Vector2(8f, 0f), out var campfirePen5Id);

            // 4. Pre-spawn demonstration props inside Pens 7, 8, 9, 10 (Row 2: Epic 8)
            SpawnResourceNode(bushRoot, new Vector2(-4f, -11.0f), 3, ItemType.Ration, FoodRegrowTicks, out var bushPen7Id);
            SpawnDemoCampfire(campfireRoot, new Vector2(0f, -9.0f), out var campfirePen8Id);
            SpawnResourceNode(bushRoot, new Vector2(4f, -11.0f), 5, ItemType.Ration, FoodRegrowTicks, out var bushPen9Id);
            SpawnResourceNode(bushRoot, new Vector2(8f, -11.0f), 5, ItemType.Ration, FoodRegrowTicks, out var bushPen10Id);

            // 5. Outpost Commons (open yard north of the pens): the live Milestone 1 supply loop arena
            SpawnOutpostCommons(commonsRoot);

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
                var outpostStall = new TradeStallData(
                    stallId,
                    position: OutpostStallPosition,
                    rationSellPrice: 15,
                    rationStock: 50,
                    oreBuyPrice: 20,
                    oreStock: 10,
                    herbBuyPrice: 25,
                    herbStock: 10,
                    treasuryGold: 250,
                    interactionRadius: 3.0f);
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
                    targetSectorId: (byte)Simpiens.UI.Zones.FacilityZoneService.OreRidgeZoneId,
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
                    targetSectorId: (byte)Simpiens.UI.Zones.FacilityZoneService.HerbHollowZoneId,
                    targetItemType: ItemType.None,
                    targetQuantity: 1,
                    rewardGold: 15,
                    issuedTick: 0,
                    expirationTick: 6000,
                    creatorStallId: stallId,
                    status: BountyStatus.Open);
                _simulationManager.RegisterBounty(scoutBounty);

                SimLog.RegisterAgentName(stallId, "OutpostStall");
                SimLog.Bootstrap.ZLogInformation($"Session spawned: {_agents.Count} agents ({Mathf.Max(0, _agents.Count - ScriptedPenAgentCount)} free crawlers), stall at {OutpostStallPosition}, seeded gold {seededGold}g, bounties #{SeedGatherBountyId} (Gather) and #{SeedScoutBountyId} (Scout)");
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

        private void SpawnOutpostCommons(GameObject root)
        {
            // Ore Ridge (west): iron veins
            SpawnResourceNode(root, new Vector2(-9f, 12f), 3, ItemType.IronOre, OreRegrowTicks, out _);
            SpawnResourceNode(root, new Vector2(-8f, 9.5f), 3, ItemType.IronOre, OreRegrowTicks, out _);
            SpawnResourceNode(root, new Vector2(-10f, 8f), 3, ItemType.IronOre, OreRegrowTicks, out _);

            // Herb Hollow (east): medicinal herb patches
            SpawnResourceNode(root, new Vector2(9f, 12f), 3, ItemType.MedicinalHerb, HerbRegrowTicks, out _);
            SpawnResourceNode(root, new Vector2(8f, 9.5f), 3, ItemType.MedicinalHerb, HerbRegrowTicks, out _);
            SpawnResourceNode(root, new Vector2(10f, 8f), 3, ItemType.MedicinalHerb, HerbRegrowTicks, out _);

            // Food bushes beside the market stall
            SpawnResourceNode(root, new Vector2(-2f, 6.5f), 3, ItemType.Ration, FoodRegrowTicks, out _);
            SpawnResourceNode(root, new Vector2(2f, 6.5f), 3, ItemType.Ration, FoodRegrowTicks, out _);

            SpawnPenTitle(root, -8.5f, 14.5f, "Ore Ridge", new Color(0.95f, 0.55f, 0.2f));
            SpawnPenTitle(root, 0f, 14.5f, "Outpost Market", new Color(0.95f, 0.85f, 0.4f));
            SpawnPenTitle(root, 8.5f, 14.5f, "Herb Hollow", new Color(0.45f, 0.9f, 0.8f));
        }

        private void SpawnPlayPens(GameObject root)
        {
            float[] dividerX = new float[] { -10f, -6f, -2f, 2f, 6f, 10f };

            // --- Row 1 (Pens 1–5: Epics 7 & Beta) ---
            float minY1 = -3.5f;
            float maxY1 = 3.5f;

            // Spawn top and bottom horizontal perimeter walls from -10.0 to +10.0 (step 1.0)
            for (float x = -10.0f; x <= 10.05f; x += 1.0f)
            {
                SpawnWallNode(root, new Vector2(x, maxY1));
                SpawnWallNode(root, new Vector2(x, minY1));
            }

            // Spawn vertical divider walls (interior Y from -2.5 to +2.5 step 1.0)
            for (int d = 0; d < dividerX.Length; d++)
            {
                float x = dividerX[d];
                for (float y = minY1 + 1.0f; y < maxY1 - 0.5f; y += 1.0f)
                {
                    SpawnWallNode(root, new Vector2(x, y));
                }
            }

            // Spawn distinct titles above Row 1 play pens
            SpawnPenTitle(root, -8.0f, 4.2f, "1. Fear Yield", new Color(1f, 0.75f, 0.2f));
            SpawnPenTitle(root, -4.0f, 4.2f, "2. Respect Defer", new Color(0.35f, 0.8f, 1f));
            SpawnPenTitle(root, 0.0f, 4.2f, "3. Rivalry Contest", new Color(1f, 0.4f, 0.4f));
            SpawnPenTitle(root, 4.0f, 4.2f, "4. Altruism & Hearth", new Color(0.3f, 0.95f, 0.5f));
            SpawnPenTitle(root, 8.0f, 4.2f, "5. Selfish Peer", new Color(0.75f, 0.8f, 0.85f));

            // --- Row 2 (Pens 6–10: Epic 8) ---
            float minY2 = -12.5f;
            float maxY2 = -5.5f;

            // Spawn top and bottom horizontal perimeter walls for Row 2 from -10.0 to +10.0 (step 1.0)
            for (float x = -10.0f; x <= 10.05f; x += 1.0f)
            {
                SpawnWallNode(root, new Vector2(x, maxY2));
                SpawnWallNode(root, new Vector2(x, minY2));
            }

            // Spawn vertical divider walls for Row 2 (interior Y from -11.5 to -6.5 step 1.0)
            for (int d = 0; d < dividerX.Length; d++)
            {
                float x = dividerX[d];
                for (float y = minY2 + 1.0f; y < maxY2 - 0.5f; y += 1.0f)
                {
                    SpawnWallNode(root, new Vector2(x, y));
                }
            }

            // Spawn distinct titles above Row 2 play pens
            SpawnPenTitle(root, -8.0f, -4.8f, "6. Directive Rally", new Color(1f, 0.85f, 0.25f));
            SpawnPenTitle(root, -4.0f, -4.8f, "7. Passive Defiance", new Color(0.95f, 0.3f, 0.3f));
            SpawnPenTitle(root, 0.0f, -4.8f, "8. Hearth Emergence", new Color(1f, 0.6f, 0.2f));
            SpawnPenTitle(root, 4.0f, -4.8f, "9. Dominance Trial", new Color(0.8f, 0.4f, 0.9f));
            SpawnPenTitle(root, 8.0f, -4.8f, "10. Factional Schism", new Color(0.2f, 0.85f, 0.85f));
        }

        private void SpawnWallNode(GameObject root, Vector2 pos)
        {
            var wallGo = new GameObject($"Wall_{pos.x:F1}_{pos.y:F1}");
            wallGo.transform.SetParent(root.transform);
            wallGo.transform.position = pos;

            var sr = wallGo.AddComponent<SpriteRenderer>();
            sr.sprite = AgentSpriteLibrary.WallSprite;
            sr.sortingOrder = 0;

            var rb = wallGo.AddComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            rb.bodyType = RigidbodyType2D.Static;

            var col = wallGo.AddComponent<CircleCollider2D>();
            col.radius = 0.55f;

            var box = wallGo.AddComponent<BoxCollider2D>();
            box.size = new Vector2(1.02f, 1.02f);

            var controller = wallGo.AddComponent<NodeController>();
            controller.Type = EntityType.Wall;
            controller.Initialize(NextEntityId(), new NodeConfiguration(speed: 0f));

            _registry.RegisterNode(controller);
        }

        private void SpawnPenTitle(GameObject root, float x, float y, string title, Color color)
        {
            var textGo = new GameObject($"PenTitle_{title}");
            textGo.transform.SetParent(root.transform);
            textGo.transform.position = new Vector3(x, y, 0f);

            var textMesh = textGo.AddComponent<TextMesh>();
            textMesh.text = title;
            textMesh.fontSize = 28;
            textMesh.characterSize = 0.08f;
            textMesh.alignment = TextAlignment.Center;
            textMesh.anchor = TextAnchor.MiddleCenter;
            textMesh.color = color;

            var mr = textGo.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                mr.sortingOrder = 10;
            }
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

        private void SpawnDemoCampfire(GameObject root, Vector2 pos, out UnityEngine.GUID anchorId)
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
            _simulationManager.RegisterAnchor(anchorId, pos, radius: 3.0f);
        }

        private void CleanupExistingWorld()
        {
            string[] rootNames = new[] { "[SwarmAgents]", "[DemoContestedBushes]", "[DemoCampfires]", "[PlayPens]", "[OutpostCommons]", "[DynamicObstacles]" };
            for (int i = 0; i < rootNames.Length; i++)
            {
                var existing = GameObject.Find(rootNames[i]);
                if (existing != null)
                {
                    Object.DestroyImmediate(existing);
                }
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
