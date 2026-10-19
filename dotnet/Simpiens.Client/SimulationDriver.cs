using System;
using Simpiens.Simulation;

namespace Simpiens.Client
{
    /// <summary>
    /// Moves the simulation's clock on each frame (Epic CLIENT M0). Live, the clock runs the whole ticks that fit in the
    /// time that passed, as Unity's clock driver did; fixed, every update runs exactly N ticks (tests, fast-forward).
    /// Either way the core only ever steps whole ticks, so the world never depends on frame pacing.
    /// </summary>
    public sealed class SimulationDriver
    {
        private readonly ISimulationClock _clock;

        /// <param name="ticksPerUpdate">0: follow real time. N > 0: N ticks every update.</param>
        public SimulationDriver(ISimulationClock clock, int ticksPerUpdate)
        {
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            if (ticksPerUpdate < 0) throw new ArgumentOutOfRangeException(nameof(ticksPerUpdate), ticksPerUpdate, "Ticks per update can't be negative.");
            TicksPerUpdate = ticksPerUpdate;
        }

        public int TicksPerUpdate { get; }

        public void Advance(TimeSpan elapsed)
        {
            if (TicksPerUpdate > 0) _clock.AdvanceTicks(TicksPerUpdate);
            else if (elapsed > TimeSpan.Zero) _clock.Advance((float)elapsed.TotalSeconds); // never a negative frame (epic F5)
        }
    }
}
