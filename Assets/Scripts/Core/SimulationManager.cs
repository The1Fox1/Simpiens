using Simpiens.Simulation;
using Simpiens.Simulation.Spatial;
using VContainer.Unity;

namespace Simpiens.Core
{
    /// <summary>
    /// Handles the real-time Unity tick, keeping it decoupled from slow agent cognition.
    /// Implements ITickable from VContainer to run on Unity's Update loop without being a MonoBehaviour.
    /// </summary>
    public class SimulationManager : ISimulationManager, ITickable
    {
        private readonly IWorldRegistry _worldRegistry;
        private readonly ISpatialPartition _spatialPartition;

        public bool IsPaused { get; private set; }

        public SimulationManager(IWorldRegistry worldRegistry, ISpatialPartition spatialPartition)
        {
            _worldRegistry = worldRegistry;
            _spatialPartition = spatialPartition;
        }

        public void Pause() => IsPaused = true;
        public void Resume() => IsPaused = false;

        public void Tick()
        {
            if (IsPaused) return;

            // Fast loop: Physics, rendering updates, and immediate game rules.
            // DO NOT process agent cognition here.
            
            // Generate the thread-safe global snapshot for any cognitive agents that poll this frame
            _spatialPartition.UpdateFromRegistry(_worldRegistry);
        }
    }
}
