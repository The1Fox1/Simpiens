using System;

namespace Simpiens.Simulation
{
    public interface ISimulationClock
    {
        long CurrentTick { get; }
        float TimeMultiplier { get; }
        float TickDeltaTime { get; }
        float InterpolationAlpha { get; }
        bool IsPaused { get; }
        
        event Action<long> OnSimulationTick;
        event Action<float> OnTimeMultiplierChanged;

        /// <summary>
        /// Publicly accessible API method to adjust the simulation speed.
        /// Can be called by player interaction (e.g., UI speed controls).
        /// </summary>
        void SetTimeMultiplier(float multiplier);

        /// <summary>
        /// Explicitly advances the simulation by a fixed number of ticks.
        /// Useful for headless testing, deterministic simulation stepping, and catch-up ticks.
        /// </summary>
        void AdvanceTicks(long ticks);
    }
}
