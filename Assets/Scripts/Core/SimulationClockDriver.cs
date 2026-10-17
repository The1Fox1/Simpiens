using Simpiens.Simulation;
using UnityEngine;
using VContainer.Unity;

namespace Simpiens.Core.Bootstrapping
{
    /// <summary>
    /// Drives the engine-free <see cref="ISimulationClock"/> from Unity's frame time (Epic CORE C6): every frame the
    /// clock runs the logical ticks that fit in the time that passed. A headless host steps ticks itself instead.
    /// </summary>
    public sealed class SimulationClockDriver : ITickable
    {
        private readonly ISimulationClock _clock;

        public SimulationClockDriver(ISimulationClock clock)
        {
            _clock = clock;
        }

        public void Tick()
        {
            _clock?.Advance(Time.deltaTime);
        }
    }
}
