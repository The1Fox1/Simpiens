using Simpiens.Simulation;
using Simpiens.Simulation.Spatial;
using Simpiens.Cognition.Pathfinding;
using Simpiens.Cognition.Contracts;
using VContainer.Unity;
using System.Collections.Concurrent;

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

        private readonly ConcurrentQueue<AgentIntent> _intentQueue = new ConcurrentQueue<AgentIntent>();

        public bool IsPaused { get; private set; }

        public SimulationManager(IWorldRegistry worldRegistry, ISpatialPartition spatialPartition)
        {
            _worldRegistry = worldRegistry;
            _spatialPartition = spatialPartition;
        }

        public void Pause() => IsPaused = true;
        public void Resume() => IsPaused = false;

        public void EnqueueIntent(AgentIntent intent)
        {
            _intentQueue.Enqueue(intent);
        }

        public void Tick()
        {
            if (IsPaused) return;

            // Fast loop: Physics, rendering updates, and immediate game rules.
            // DO NOT process agent cognition here.

            // Process queued intents from background cognitive threads
            while (_intentQueue.TryDequeue(out var intent))
            {
                // In Epic 3, we simply route the intent to the active Simulation state.
                // Depending on the intent, we can apply pathfinding movement or state mutation here.
                switch (intent)
                {
                    case HarvestResourceIntent harvestIntent:
                        // Find the agent's node in the registry to apply movement
                        // For example: var node = _worldRegistry.GetNode(intent.AgentId);
                        // node.SetActivePath(harvestIntent.Path);
                        break;
                        
                    case IdleIntent idleIntent:
                        // Idle logic
                        break;
                }
            }
            
            // Generate the thread-safe global snapshot for any cognitive agents that poll this frame
            _spatialPartition.UpdateFromRegistry(_worldRegistry);
        }
    }
}
