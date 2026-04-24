using Simpiens.Entities;
using Simpiens.Simulation;
using Simpiens.Simulation.Spatial;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Simpiens.Core
{
    /// <summary>
    /// Serves as the main controller for the simulation state.
    /// Implements VContainer's IStartable to run after dependencies are resolved.
    /// </summary>
    public class SimulationStarter : IStartable
    {
        private readonly NodeFactory _nodeFactory;
        private readonly ISpatialPartition _spatialPartition;
        private readonly ISimulationManager _simulationManager;
        private readonly IObjectResolver _resolver;
        private readonly IWorldRegistry _registry;

        [Inject]
        public SimulationStarter(NodeFactory nodeFactory, ISpatialPartition spatialPartition, IObjectResolver resolver, IWorldRegistry registry, ISimulationManager simulationManager)
        {
            _nodeFactory = nodeFactory;
            _spatialPartition = spatialPartition;
            _resolver = resolver;
            _registry = registry;
            _simulationManager = simulationManager;
        }

        public void Start()
        {
            Debug.Log("Simulation Started Code-First!");

            // Initialize Spatial Partition
            Camera cam = Camera.main;
            float worldHeight = 10f; // fallback
            float worldWidth = 10f;  // fallback
            if (cam != null && cam.orthographic)
            {
                worldHeight = cam.orthographicSize * 2f;
                worldWidth = worldHeight * cam.aspect;
            }

            // Initialize with the calculated bounds and a reasonable cell size
            _spatialPartition.Initialize(worldWidth, worldHeight, 1.0f);

            // Attach GridVisualizer for Validation Testing
            var visualizerGo = new GameObject("[GridVisualizer]");
            var visualizer = visualizerGo.AddComponent<GridVisualizer>();
            _resolver.Inject(visualizer);

            SpawnResources();
        }

        private void SpawnResources()
        {
            GameObject root = new GameObject("[Resources]");
            for (int i = 0; i < 20; i++)
            {
                var resourceGo = new GameObject($"Resource_{i}");
                resourceGo.transform.SetParent(root.transform);
                resourceGo.transform.position = Random.insideUnitCircle * 8f; // Scatter around

                var sr = resourceGo.AddComponent<SpriteRenderer>();
                var tex = new Texture2D(1, 1);
                tex.SetPixel(0, 0, Color.green);
                tex.Apply();
                sr.sprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
                resourceGo.transform.localScale = Vector3.one * 0.3f; // Slightly larger than pawns

                var rb = resourceGo.AddComponent<Rigidbody2D>();
                rb.gravityScale = 0f;
                rb.bodyType = RigidbodyType2D.Static;

                var col = resourceGo.AddComponent<CircleCollider2D>();
                col.radius = 0.15f;

                var controller = resourceGo.AddComponent<NodeController>();
                controller.Type = EntityType.Resource;
                var resId = UnityEngine.GUID.Generate();
                controller.Initialize(resId, new NodeConfiguration(speed: 0f));

                _registry.RegisterNode(controller);
                _simulationManager.RegisterResource(new ResourceData { EntityId = resId, RemainingYield = 5 });
            }
        }
    }
}
