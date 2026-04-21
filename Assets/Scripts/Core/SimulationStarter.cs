using Simpiens.Entities;
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
        private readonly IObjectResolver _resolver;

        [Inject]
        public SimulationStarter(NodeFactory nodeFactory, ISpatialPartition spatialPartition, IObjectResolver resolver)
        {
            _nodeFactory = nodeFactory;
            _spatialPartition = spatialPartition;
            _resolver = resolver;
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
        }
    }
}
