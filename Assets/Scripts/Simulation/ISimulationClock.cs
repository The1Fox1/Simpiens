using System;

namespace Simpiens.Simulation
{
    public interface ISimulationClock
    {
        long CurrentTick { get; }
        float TimeMultiplier { get; }
        
        event Action<long> OnSimulationTick;
        event Action<float> OnTimeMultiplierChanged;

        /// <summary>
        /// Publicly accessible API method to adjust the simulation speed.
        /// Can be called by player interaction (e.g., UI speed controls).
        /// </summary>
        void SetTimeMultiplier(float multiplier);
    }
}
