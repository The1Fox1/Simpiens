using UnityEngine;

namespace Simpiens.Simulation
{
    /// <summary>
    /// Seeded, stateless simulation randomness (ROADMAP §1A.1 rule 4). A stream is derived from
    /// (session seed, entity, tick, salt) with SplitMix64, so the same inputs always produce the same draws on any
    /// thread, in any evaluation order. No shared state: safe for off-thread cognition without [ThreadStatic].
    /// Use a distinct salt per decision so two decisions by one agent in one tick don't share a stream.
    /// </summary>
    public struct SimRandom
    {
        private ulong _state;

        private SimRandom(ulong state)
        {
            _state = state;
        }

        public static SimRandom For(ulong seed, GUID entityId, long tick, uint salt)
        {
            ulong h = Mix(seed ^ 0x9E3779B97F4A7C15UL);
            h = Mix(h ^ (uint)entityId.GetHashCode());
            h = Mix(h ^ (ulong)tick);
            h = Mix(h ^ salt);
            return new SimRandom(h);
        }

        /// <summary>Next uniformly distributed value in [0, 1).</summary>
        public float NextFloat()
        {
            return (NextULong() >> 40) * (1f / (1UL << 24));
        }

        /// <summary>Next uniformly distributed value in [min, max).</summary>
        public float Range(float min, float max)
        {
            return min + NextFloat() * (max - min);
        }

        public ulong NextULong()
        {
            _state += 0x9E3779B97F4A7C15UL;
            return Mix(_state);
        }

        private static ulong Mix(ulong z)
        {
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }
    }

    /// <summary>Salts for <see cref="SimRandom.For"/>; one per independent random decision.</summary>
    public static class SimRandomSalt
    {
        public const uint AmbientWander = 1;
        public const uint PanicDirection = 2;
        public const uint EntityId = 3;
    }
}
