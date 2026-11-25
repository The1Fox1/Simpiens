
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

        public static SimRandom For(ulong seed, EntityId entityId, long tick, uint salt)
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
        public const uint Personality = 4;
        public const uint DrifterSchedule = 5;
        public const uint DrifterTraits = 6;
        public const uint Loot = 7; // loot tables (item system I7)
        public const uint Look = 8; // a crawler's look (Epic UI-Beta B1)
        public const uint Name = 9; // a crawler's given name (Epic UI-Beta B1)
        public const uint Combat = 10; // a swing's hit roll (Epic 10 Part 3, K0.3)
        public const uint Lair = 11; // a lair's pack size (K2.1)
        public const uint CreatureId = 12; // a creature's id, its own sequence apart from the entities' (K2.1)
        public const uint CreatureRest = 13; // how long a creature rests before it roams (K2.2)
        public const uint CreatureRoam = 14; // where a creature roams (K2.2)
        public const uint PileId = 15; // a pile's id, its own sequence apart from the entities' (K4.1)
        public const uint Skills = 16; // a crawler's starting skills (K9.1)
        public const uint Passions = 17; // a crawler's passions (Epic 11, S1.1, D3)
        public const uint Techniques = 18; // a crawler's pick of a technique at a band reached (Epic 11, S4.2, D15)
        public const uint Inspiration = 19; // whether a Masterwork make is a masterpiece, and its name's words (Epic 11, S8.2, D41)
        public const uint Curiosity = 20; // a crawler's curiosity, a sixth trait drawn at spawn (Epic LIFE-1 plan 2, L2.7, Q8)
        public const uint Wants = 21; // whether a want arises on waking, and which (Epic LIFE-1 plan 2, W.2, D20)
        public const uint Chronotype = 22; // a crawler's chronotype at spawn, and its waking hour's shift each day (Epic LIFE-1 plan 3, R.1, D28, D29)
        public const uint Intention = 23; // a crawler's intention for its day, drawn once a routine day (Epic LIFE-1 plan 3, R.7, D34)
    }
}
