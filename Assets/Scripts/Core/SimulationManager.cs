using Simpiens.Simulation;
using VContainer.Unity;

namespace Simpiens.Core
{
    /// <summary>
    /// Handles the real-time Unity tick, keeping it decoupled from slow agent cognition.
    /// Implements ITickable from VContainer to run on Unity's Update loop without being a MonoBehaviour.
    /// </summary>
    public class SimulationManager : ISimulationManager, ITickable
    {
        public bool IsPaused { get; private set; }

        public void Pause() => IsPaused = true;
        public void Resume() => IsPaused = false;

        public void Tick()
        {
            if (IsPaused) return;

            // Fast loop: Physics, rendering updates, and immediate game rules.
            // DO NOT process agent cognition here.
        }
    }
}
