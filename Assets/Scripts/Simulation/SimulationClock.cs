using System;
using UnityEngine;
using VContainer.Unity;

namespace Simpiens.Simulation
{
    public class SimulationClock : ISimulationClock, ITickable
    {
        public long CurrentTick { get; private set; }
        public float TimeMultiplier { get; private set; } = 1f;

        public const float FixedTickDeltaTime = 0.1f;
        public float TickDeltaTime => FixedTickDeltaTime;
        public float InterpolationAlpha => Mathf.Clamp01(_accumulator / TimePerTick);
        public bool IsPaused => TimeMultiplier <= 0f || Mathf.Approximately(TimeMultiplier, 0f);

        public event Action<long> OnSimulationTick;
        public event Action<float> OnTimeMultiplierChanged;

        // Represents the frequency of logical ticks per real-world second (at 1x speed).
        private const float TicksPerSecond = 10f;
        private const float TimePerTick = 1f / TicksPerSecond;

        private float _accumulator;

        public void SetTimeMultiplier(float multiplier)
        {
            float newMultiplier = Mathf.Max(0f, multiplier);
            if (Mathf.Approximately(TimeMultiplier, newMultiplier)) return;

            TimeMultiplier = newMultiplier;
            OnTimeMultiplierChanged?.Invoke(TimeMultiplier);
        }

        public void Tick()
        {
            // Accumulate real-time passed, modified by the speed multiplier.
            _accumulator += Time.deltaTime * TimeMultiplier;

            while (_accumulator >= TimePerTick)
            {
                _accumulator -= TimePerTick;
                CurrentTick++;
                OnSimulationTick?.Invoke(CurrentTick);
            }
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
