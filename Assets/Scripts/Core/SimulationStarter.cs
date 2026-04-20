using UnityEngine;
using VContainer;
using VContainer.Unity;
using Simpiens.Entities;
using Simpiens.Simulation.Spatial;

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

        [Inject]
        public SimulationStarter(NodeFactory nodeFactory, ISpatialPartition spatialPartition)
        {
            _nodeFactory = nodeFactory;
            _spatialPartition = spatialPartition;
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

            // Test spawning a few nodes
            for (int i = 0; i < 10; i++)
            {
                // We'll spawn them near the center
                Vector2 randomPos = Random.insideUnitCircle * 2f;
                _nodeFactory.CreateNode(randomPos);
            }
        }
    }
}
