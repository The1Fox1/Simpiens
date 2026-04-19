using UnityEngine;
using VContainer;
using VContainer.Unity;
using Simpiens.Entities;

namespace Simpiens.Core
{
    /// <summary>
    /// Serves as the main controller for the simulation state.
    /// Implements VContainer's IStartable to run after dependencies are resolved.
    /// </summary>
    public class SimulationStarter : IStartable
    {
        private readonly NodeFactory _nodeFactory;

        [Inject]
        public SimulationStarter(NodeFactory nodeFactory)
        {
            _nodeFactory = nodeFactory;
        }

        public void Start()
        {
            Debug.Log("Simulation Started Code-First!");

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
