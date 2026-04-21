using System.Collections.Generic;
using Simpiens.Cognition.Pathfinding;
using Simpiens.Entities;
using Simpiens.Simulation;
using Simpiens.Simulation.Spatial;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Simpiens.Cognition.Testing
{
    public class SwarmSpawner : ITickable, IStartable
    {
        private readonly ISpatialPartition _spatialPartition;
        private readonly IAsyncPathfinder _pathfinder;
        private readonly IWorldRegistry _registry;

        private readonly List<DumbSwarmAgent> _agents = new List<DumbSwarmAgent>(1000);

        public int SpawnCount = 1000;

        [Inject]
        public SwarmSpawner(ISpatialPartition spatialPartition, IAsyncPathfinder pathfinder, IWorldRegistry registry)
        {
            _spatialPartition = spatialPartition;
            _pathfinder = pathfinder;
            _registry = registry;
        }

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
                controller.Initialize(GUID.Generate(), new NodeConfiguration(speed: 2f));
                _registry.RegisterNode(controller);

                var agent = agentGo.AddComponent<DumbSwarmAgent>();
                agent.Initialize(_spatialPartition, _pathfinder);

                // Only draw path for the first few to avoid drawing 1000 lines
                agent.DrawPath = (i < 50);

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
