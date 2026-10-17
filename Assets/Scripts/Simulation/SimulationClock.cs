using System;

namespace Simpiens.Simulation
{
    /// <summary>
    /// The fixed logical clock (10 ticks per second at 1x). Engine-free: the host hands in the real time that passed
    /// (<see cref="Advance"/>: Unity's frame time; a headless host steps ticks with <see cref="AdvanceTicks"/>).
    /// </summary>
    public class SimulationClock : ISimulationClock
    {
        public long CurrentTick { get; private set; }
        public float TimeMultiplier { get; private set; } = 1f;

        public const float FixedTickDeltaTime = 0.1f;
        public float TickDeltaTime => FixedTickDeltaTime;
        public float InterpolationAlpha => SimMath.Clamp01(_accumulator / TimePerTick);
        public bool IsPaused => TimeMultiplier <= 0f || SimMath.Approximately(TimeMultiplier, 0f);

        public event Action<long> OnSimulationTick;
        public event Action<float> OnTimeMultiplierChanged;

        // Represents the frequency of logical ticks per real-world second (at 1x speed).
        private const float TicksPerSecond = 10f;
        private const float TimePerTick = 1f / TicksPerSecond;

        private float _accumulator;

        public void SetTimeMultiplier(float multiplier)
        {
            float newMultiplier = SimMath.Max(0f, multiplier);
            if (SimMath.Approximately(TimeMultiplier, newMultiplier)) return;

            TimeMultiplier = newMultiplier;
            OnTimeMultiplierChanged?.Invoke(TimeMultiplier);
        }

        public void Advance(float realSeconds)
        {
            // Accumulate real-time passed, modified by the speed multiplier.
            _accumulator += realSeconds * TimeMultiplier;

            while (_accumulator >= TimePerTick)
            {
                _accumulator -= TimePerTick;
                CurrentTick++;
                OnSimulationTick?.Invoke(CurrentTick);
            }
        }

        public void Reset()
        {
            CurrentTick = 0;
            _accumulator = 0f;
        }

        public void ResumeAt(long tick)
        {
            CurrentTick = tick < 0 ? 0 : tick;
            _accumulator = 0f;
        }

        /// <summary>
        /// Explicitly advances the simulation by a fixed number of ticks.
        /// Primarily used for deterministic testing, headless simulations, and fast-forward catchup.
        /// </summary>
        public void AdvanceTicks(long ticks)
        {
            for (long i = 0; i < ticks; i++)
            {
                CurrentTick++;
                OnSimulationTick?.Invoke(CurrentTick);
            }
        }
    }
}
