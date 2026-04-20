using System;
using UnityEngine;
using VContainer.Unity;

namespace Simpiens.Simulation
{
    public class SimulationClock : ISimulationClock, ITickable
    {
        public long CurrentTick { get; private set; }
        public float TimeMultiplier { get; private set; } = 1f;

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
    }
}
