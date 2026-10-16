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

        /// <summary>
        /// Rewinds the clock to tick 0 for a fresh session (respawn). The time multiplier is kept, so a paused game
        /// stays paused and a scenario's speed is untouched.
        /// </summary>
        void Reset();

        /// <summary>Sets the clock to a saved tick (a load). Like a reset, the time multiplier is kept.</summary>
        void ResumeAt(long tick);
    }
}
