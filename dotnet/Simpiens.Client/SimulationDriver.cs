using System;
using Simpiens.Diagnostics;
using Simpiens.Simulation;
using ZLogger;

namespace Simpiens.Client
{
    /// <summary>
    /// Moves the simulation's clock on each frame (Epic CLIENT M0). Live, the clock runs the whole ticks that fit in the
    /// time that passed, as Unity's clock driver did; fixed, every update runs exactly N ticks (tests, fast-forward).
    /// Either way the core only ever steps whole ticks, so the world never depends on frame pacing. A frame simulates at
    /// most <see cref="MaxFrameTime"/> of real time (review D2): after a stall (a debugger, a dragged window, a slow
    /// load) the world drops the rest rather than running a burst of ticks, each waiting on its cognition, in one frame.
    /// </summary>
    public sealed class SimulationDriver
    {
        /// <summary>The most real time one frame simulates: 4 ticks at 1x, 40 at 10x. KNI's own cap is 500 ms.</summary>
        public static readonly TimeSpan MaxFrameTime = TimeSpan.FromMilliseconds(400);

        private readonly ISimulationClock _clock;
        private bool _stalled;

        /// <param name="ticksPerUpdate">0: follow real time. N > 0: N ticks every update.</param>
        public SimulationDriver(ISimulationClock clock, int ticksPerUpdate)
        {
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            if (ticksPerUpdate < 0) throw new ArgumentOutOfRangeException(nameof(ticksPerUpdate), ticksPerUpdate, "Ticks per update can't be negative.");
            TicksPerUpdate = ticksPerUpdate;
        }

        public int TicksPerUpdate { get; }

        /// <summary>
        /// The player paused (a host command): no tick runs. Live, the paused clock runs none anyway; with whole ticks
        /// per update the driver must hold them itself, or a paused clock would count ticks it doesn't simulate (epic F6).
        /// </summary>
        public bool Held { get; set; }

        /// <summary>The real time stalled frames dropped instead of simulating, since this driver began.</summary>
        public TimeSpan Dropped { get; private set; }

        public void Advance(TimeSpan elapsed)
        {
            if (Held) return;
            if (TicksPerUpdate > 0)
            {
                _clock.AdvanceTicks(TicksPerUpdate);
                return;
            }
            if (elapsed <= TimeSpan.Zero) return; // never a negative frame (epic F5)
            if (elapsed > MaxFrameTime)
            {
                Dropped += elapsed - MaxFrameTime;
                if (!_stalled) SimLog.Simulation.ZLogWarning($"A {elapsed.TotalMilliseconds:F0} ms frame: the world runs {MaxFrameTime.TotalMilliseconds:F0} ms of it and drops the rest (logged once per stall).");
                _stalled = true;
                elapsed = MaxFrameTime;
            }
            else
            {
                _stalled = false;
            }
            _clock.Advance((float)elapsed.TotalSeconds);
        }
    }
}
