using System.Collections.Generic;
using Simpiens.Cognition;
using Simpiens.Cognition.Pathfinding;
using Simpiens.Entities;
using Simpiens.Simulation;
using Simpiens.Simulation.Spatial;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Simpiens.Testing.Environment
{
    public class SwarmSpawner : ITickable, IStartable
    {
        private readonly ISpatialPartition _spatialPartition;
        private readonly IAsyncPathfinder _pathfinder;
        private readonly IWorldRegistry _registry;

        private readonly List<AutonomousAgent> _agents = new List<AutonomousAgent>(1000);

        private readonly Simpiens.Cognition.Planning.IGoapPlanner _planner;
        private readonly Simpiens.Cognition.Planning.ActionGraph _actionGraph;
        private readonly List<Simpiens.Cognition.Planning.GoapGoal> _goals;

        public int SpawnCount = 10;

        [Inject]
        public SwarmSpawner(
            ISpatialPartition spatialPartition, 
            IAsyncPathfinder pathfinder, 
            IWorldRegistry registry, 
            ISimulationManager simulationManager, 
            ISimulationClock clock,
            Simpiens.Cognition.Planning.IGoapPlanner planner)
        {
            _spatialPartition = spatialPartition;
            _pathfinder = pathfinder;
            _registry = registry;
            _simulationManager = simulationManager;
            _clock = clock;
            _planner = planner;

            _evaluator = new Simpiens.Cognition.Evaluators.SurvivalUtilityEvaluator(_pathfinder);
            _mentalBreakEvaluator = new Simpiens.Cognition.Evaluators.MentalBreakEvaluator(_pathfinder);
            _altruismEvaluator = new Simpiens.Cognition.Evaluators.AltruismEvaluator(_pathfinder);

            _actionGraph = new Simpiens.Cognition.Planning.ActionGraph();
            _actionGraph.RegisterAction(new Simpiens.Cognition.Planning.Actions.SearchResourceAction());
            _actionGraph.RegisterAction(new Simpiens.Cognition.Planning.Actions.TravelToResourceAction());
            _actionGraph.RegisterAction(new Simpiens.Cognition.Planning.Actions.HarvestResourceAction());
            _actionGraph.RegisterAction(new Simpiens.Cognition.Planning.Actions.EatCarriedFoodAction());
            _actionGraph.RegisterAction(new Simpiens.Cognition.Planning.Actions.RestAction());
            _actionGraph.RegisterAction(new Simpiens.Cognition.Planning.Actions.TravelToPeerAction());
            _actionGraph.RegisterAction(new Simpiens.Cognition.Planning.Actions.ShareFoodAction());
            _actionGraph.RegisterAction(new Simpiens.Cognition.Planning.Actions.TravelToAnchorAction(_pathfinder));
            _actionGraph.RegisterAction(new Simpiens.Cognition.Planning.Actions.RelaxAtAnchorAction());

            _goals = new List<Simpiens.Cognition.Planning.GoapGoal>
            {
                new Simpiens.Cognition.Planning.Goals.SatiateHungerGoal(),
                new Simpiens.Cognition.Planning.Goals.RestGoal(),
                new Simpiens.Cognition.Planning.Goals.SocializeGoal(),
                new Simpiens.Cognition.Planning.Goals.AidCompanionGoal(),
                new Simpiens.Cognition.Planning.Goals.GatherAtAnchorGoal()
            };
        }

        private readonly Simpiens.Cognition.Evaluators.ICognitiveEvaluator _evaluator;
        private readonly Simpiens.Cognition.Evaluators.ICognitiveEvaluator _mentalBreakEvaluator;
        private readonly Simpiens.Cognition.Evaluators.ICognitiveEvaluator _altruismEvaluator;
        private readonly ISimulationManager _simulationManager;
        private readonly ISimulationClock _clock;

        public void Start()
        {
            // Create roots for organization
            GameObject root = new GameObject("[SwarmAgents]");
            GameObject bushRoot = new GameObject("[DemoContestedBushes]");
            GameObject campfireRoot = new GameObject("[DemoCampfires]");
            GameObject pensRoot = new GameObject("[PlayPens]");

            // 1. Construct the 5 bordered Play Pens
            SpawnPlayPens(pensRoot);

            // 2. Pre-spawn demonstration bushes inside each pen
            // Pen 1 (Fear Yield): Center bush (1 yield) and secondary foraging bush (3 yields)
            SpawnDemoBush(bushRoot, new Vector2(-6f, 0f), 1, out var bushFearId);
            SpawnDemoBush(bushRoot, new Vector2(-6f, -2.5f), 3, out _);

            // Pen 2 (Respect Defer): Center bush (1 yield) and secondary foraging bush (3 yields)
            SpawnDemoBush(bushRoot, new Vector2(-3f, 0f), 1, out var bushRespectId);
            SpawnDemoBush(bushRoot, new Vector2(-3f, -2.5f), 3, out _);

            // Pen 3 (Rivalry Contest): Center contested bush (1 yield, no secondary bush)
            SpawnDemoBush(bushRoot, new Vector2(0f, 0f), 1, out var bushRivalryId);

            // 3. Pre-spawn demonstration campfires inside Pens 4 and 5
            SpawnDemoCampfire(campfireRoot, new Vector2(3f, 0f), out var campfirePen4Id);
            SpawnDemoCampfire(campfireRoot, new Vector2(6f, 0f), out var campfirePen5Id);

            for (int i = 0; i < SpawnCount; i++)
            {
                GameObject agentGo = new GameObject($"SwarmAgent_{i}");
                agentGo.transform.SetParent(root.transform);

                // Configure predetermined positions for the 5 demonstration dyads within their respective play pens
                if (i == 0) agentGo.transform.position = new Vector2(-6f, 1.8f);       // Pen 1: Dominant
                else if (i == 1) agentGo.transform.position = new Vector2(-6f, -1.2f); // Pen 1: Submissive
                else if (i == 2) agentGo.transform.position = new Vector2(-3f, 1.8f);  // Pen 2: Respected
                else if (i == 3) agentGo.transform.position = new Vector2(-3f, -1.2f); // Pen 2: Deferent
                else if (i == 4) agentGo.transform.position = new Vector2(0f, 1.5f);   // Pen 3: Rival A
                else if (i == 5) agentGo.transform.position = new Vector2(0f, -1.5f);  // Pen 3: Rival B
                else if (i == 6) agentGo.transform.position = new Vector2(3f, 1.2f);   // Pen 4: Altruistic Donor
                else if (i == 7) agentGo.transform.position = new Vector2(3f, -1.2f);  // Pen 4: Starving Companion
                else if (i == 8) agentGo.transform.position = new Vector2(6f, 1.2f);   // Pen 5: Selfish Peer
                else if (i == 9) agentGo.transform.position = new Vector2(6f, -1.2f);  // Pen 5: Starving Stranger
                else
                {
                    // Spread any extra background agents out in a clear open zone above the demonstration arena
                    agentGo.transform.position = new Vector2(-6f + (i - 10) * 2.5f, 6f);
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
                controller.Initialize(UnityEngine.GUID.Generate(), new NodeConfiguration(speed: 2f));
                _registry.RegisterNode(controller);

                var agent = agentGo.AddComponent<AutonomousAgent>();
                agent.Initialize(controller.Id, _spatialPartition, new Simpiens.Cognition.Evaluators.ICognitiveEvaluator[] { _mentalBreakEvaluator, _altruismEvaluator, _evaluator }, _simulationManager, _clock, _planner, _actionGraph, _goals);

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
                _agents[0].Memory.SpatialMemoryMap[bushFearId] = new Simpiens.Cognition.Memory.SpatialMemoryRecord(bushFearId, EntityType.Resource, new Vector2Int(-6, 0), 0);
                _agents[1].Memory.SpatialMemoryMap[bushFearId] = new Simpiens.Cognition.Memory.SpatialMemoryRecord(bushFearId, EntityType.Resource, new Vector2Int(-6, 0), 0);

                // Dyad 2 (Respect Asymmetry): Agent 3 respects Agent 2 (Respect = 80). Agent 3 will defer Bush 1 to Agent 2.
                _agents[2].Hunger = 60f;
                _agents[3].Hunger = 60f;
                _agents[3].Memory.RecordInteraction(_agents[2].AgentId, deltaWarmth: 20, deltaTrust: 20, deltaFear: 0, deltaRespect: 80, 0);
                _agents[2].Memory.SpatialMemoryMap[bushRespectId] = new Simpiens.Cognition.Memory.SpatialMemoryRecord(bushRespectId, EntityType.Resource, new Vector2Int(-3, 0), 0);
                _agents[3].Memory.SpatialMemoryMap[bushRespectId] = new Simpiens.Cognition.Memory.SpatialMemoryRecord(bushRespectId, EntityType.Resource, new Vector2Int(-3, 0), 0);

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
                _agents[6].Memory.SpatialMemoryMap[campfirePen4Id] = new Simpiens.Cognition.Memory.SpatialMemoryRecord(campfirePen4Id, EntityType.Anchor, new Vector2Int(3, 0), 0);
                _agents[7].Memory.SpatialMemoryMap[campfirePen4Id] = new Simpiens.Cognition.Memory.SpatialMemoryRecord(campfirePen4Id, EntityType.Anchor, new Vector2Int(3, 0), 0);

                // Dyad 5 (Selfish Indifference & Hearth Isolation): Agent 8 is estranged/hostile to Agent 9 (Warmth = -20, Trust = -10).
                // Agent 8 is sated (Hunger = 20), Agent 9 is starving (Hunger = 90).
                // Agent 8 ignores Agent 9 and relaxes alone by the Pen 5 campfire; starving Agent 9 receives no aid.
                _agents[8].Hunger = 20f;
                _agents[9].Hunger = 90f;
                _agents[8].Memory.RecordInteraction(_agents[9].AgentId, deltaWarmth: -20, deltaTrust: -10, deltaFear: 0, deltaRespect: -10, 0);
                _agents[9].Memory.RecordInteraction(_agents[8].AgentId, deltaWarmth: -20, deltaTrust: -10, deltaFear: 0, deltaRespect: -10, 0);
                _agents[8].Memory.SpatialMemoryMap[campfirePen5Id] = new Simpiens.Cognition.Memory.SpatialMemoryRecord(campfirePen5Id, EntityType.Anchor, new Vector2Int(6, 0), 0);
                _agents[9].Memory.SpatialMemoryMap[campfirePen5Id] = new Simpiens.Cognition.Memory.SpatialMemoryRecord(campfirePen5Id, EntityType.Anchor, new Vector2Int(6, 0), 0);
            }
        }

        private void SpawnPlayPens(GameObject root)
        {
            float[] dividerX = new float[] { -7.5f, -4.5f, -1.5f, 1.5f, 4.5f, 7.5f };
            float minY = -3.5f;
            float maxY = 3.5f;

            // Spawn top and bottom horizontal perimeter walls from -7.5 to +7.5 (step 1.0)
            for (float x = -7.5f; x <= 7.55f; x += 1.0f)
            {
                SpawnWallNode(root, new Vector2(x, maxY));
                SpawnWallNode(root, new Vector2(x, minY));
            }

            // Spawn vertical divider walls (interior Y from -2.5 to +2.5 step 1.0)
            for (int d = 0; d < dividerX.Length; d++)
            {
                float x = dividerX[d];
                for (float y = minY + 1.0f; y < maxY - 0.5f; y += 1.0f)
                {
                    SpawnWallNode(root, new Vector2(x, y));
                }
            }

            // Spawn distinct titles above each play pen
            SpawnPenTitle(root, -6.0f, 4.2f, "1. Fear Yield", new Color(1f, 0.75f, 0.2f));
            SpawnPenTitle(root, -3.0f, 4.2f, "2. Respect Defer", new Color(0.35f, 0.8f, 1f));
            SpawnPenTitle(root, 0.0f, 4.2f, "3. Rivalry Contest", new Color(1f, 0.4f, 0.4f));
            SpawnPenTitle(root, 3.0f, 4.2f, "4. Altruism & Hearth", new Color(0.3f, 0.95f, 0.5f));
            SpawnPenTitle(root, 6.0f, 4.2f, "5. Selfish Peer", new Color(0.75f, 0.8f, 0.85f));
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
            controller.Initialize(UnityEngine.GUID.Generate(), new NodeConfiguration(speed: 0f));

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

        private void SpawnDemoBush(GameObject root, Vector2 pos, int yield, out UnityEngine.GUID resId)
        {
            var resourceGo = new GameObject($"DemoBush_{pos.x}_{pos.y}");
            resourceGo.transform.SetParent(root.transform);
            resourceGo.transform.position = pos;

            var sr = resourceGo.AddComponent<SpriteRenderer>();
            sr.sprite = AgentSpriteLibrary.ResourceSprite;
            resourceGo.transform.localScale = Vector3.one * 0.8f;

            var rb = resourceGo.AddComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            rb.bodyType = RigidbodyType2D.Static;

            var col = resourceGo.AddComponent<CircleCollider2D>();
            col.radius = 0.4f;

            var controller = resourceGo.AddComponent<NodeController>();
            controller.Type = EntityType.Resource;
            resId = UnityEngine.GUID.Generate();
            controller.Initialize(resId, new NodeConfiguration(speed: 0f));

            _registry.RegisterNode(controller);
            _simulationManager.RegisterResource(new ResourceData { EntityId = resId, RemainingYield = yield });
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
            col.radius = 0.5f;

            var controller = campfireGo.AddComponent<NodeController>();
            controller.Type = EntityType.Anchor;
            anchorId = UnityEngine.GUID.Generate();
            controller.Initialize(anchorId, new NodeConfiguration(speed: 0f));

            campfireGo.AddComponent<CampfireView>();

            _registry.RegisterNode(controller);
            _simulationManager.RegisterAnchor(anchorId, pos, radius: 3.0f);
        }

        public void Tick()
        {
            // Centralized update loop for zero-allocation high-performance execution
            int count = _agents.Count;
            for (int i = 0; i < count; i++)
            {
                _agents[i].ManualUpdate();
            }
        }
    }
}
