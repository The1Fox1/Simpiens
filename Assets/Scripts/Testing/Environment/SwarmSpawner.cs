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
            _frustrationEvaluator = new Simpiens.Cognition.Evaluators.FrustrationEvaluator(_pathfinder);

            _actionGraph = new Simpiens.Cognition.Planning.ActionGraph();
            _actionGraph.RegisterAction(new Simpiens.Cognition.Planning.Actions.SearchResourceAction());
            _actionGraph.RegisterAction(new Simpiens.Cognition.Planning.Actions.TravelToResourceAction());
            _actionGraph.RegisterAction(new Simpiens.Cognition.Planning.Actions.HarvestResourceAction());
            _actionGraph.RegisterAction(new Simpiens.Cognition.Planning.Actions.EatCarriedFoodAction());

            _goals = new List<Simpiens.Cognition.Planning.GoapGoal>
            {
                new Simpiens.Cognition.Planning.Goals.SatiateHungerGoal()
            };
        }

        private readonly Simpiens.Cognition.Evaluators.ICognitiveEvaluator _evaluator;
        private readonly Simpiens.Cognition.Evaluators.ICognitiveEvaluator _frustrationEvaluator;
        private readonly ISimulationManager _simulationManager;
        private readonly ISimulationClock _clock;

        public void Start()
        {
            // Create a root for organization
            GameObject root = new GameObject("[SwarmAgents]");

            for (int i = 0; i < SpawnCount; i++)
            {
                GameObject agentGo = new GameObject($"SwarmAgent_{i}");
                agentGo.transform.SetParent(root.transform);

                // Spread them out initially
                agentGo.transform.position = Random.insideUnitCircle * 5f;

                var sr = agentGo.AddComponent<SpriteRenderer>();
                var tex = new Texture2D(1, 1);
                tex.SetPixel(0, 0, Color.cyan);
                tex.Apply();
                sr.sprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
                agentGo.transform.localScale = Vector3.one * 0.2f; // Small dots

                // Register them as pawns so they appear in the GridVisualizer
                var rb = agentGo.AddComponent<Rigidbody2D>();
                rb.gravityScale = 0f;
                rb.bodyType = RigidbodyType2D.Kinematic; // They move via interpolation, not physics

                var col = agentGo.AddComponent<CircleCollider2D>();
                col.radius = 0.1f;

                var controller = agentGo.AddComponent<NodeController>();
                controller.Type = EntityType.Pawn;
                controller.Initialize(UnityEngine.GUID.Generate(), new NodeConfiguration(speed: 2f));
                _registry.RegisterNode(controller);

                var agent = agentGo.AddComponent<AutonomousAgent>();
                agent.Initialize(controller.Id, _spatialPartition, new Simpiens.Cognition.Evaluators.ICognitiveEvaluator[] { _frustrationEvaluator, _evaluator }, _simulationManager, _clock, _planner, _actionGraph, _goals);

                _agents.Add(agent);
            }
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
