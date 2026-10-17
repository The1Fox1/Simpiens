using System;

namespace Simpiens.Cognition.Planning
{
    /// <summary>
    /// A zero-allocation, 16-byte stack struct representing agent or world state conditions
    /// using a 64-bit bitmask. Capable of evaluating preconditions and applying effects
    /// in nanoseconds with zero GC pressure.
    /// </summary>
    public readonly struct WorldState : IEquatable<WorldState>
    {
        public readonly ulong Values;
        public readonly ulong Mask;

        public static readonly WorldState Empty = new WorldState(0, 0);

        public WorldState(ulong values, ulong mask)
        {
            Values = values;
            Mask = mask;
        }

        /// <summary>
        /// Checks if this state satisfies all facts specified by the required state.
        /// Unmasked facts in the required state are ignored.
        /// </summary>
        public bool Satisfies(in WorldState required)
        {
            return (Values & required.Mask) == (required.Values & required.Mask);
        }

        /// <summary>
        /// Applies the effects of an action to this state, returning a new modified WorldState.
        /// Only facts masked by the effects state are updated; all other facts are preserved.
        /// </summary>
        public WorldState ApplyEffects(in WorldState effects)
        {
            ulong newValues = (Values & ~effects.Mask) | (effects.Values & effects.Mask);
            ulong newMask = Mask | effects.Mask;
            return new WorldState(newValues, newMask);
        }

        /// <summary>
        /// Returns a copy of this state with a specific fact set to true or false.
        /// </summary>
        public WorldState With(StateFact fact, bool value)
        {
            ulong bit = 1UL << (int)fact;
            ulong newValues = value ? (Values | bit) : (Values & ~bit);
            ulong newMask = Mask | bit;
            return new WorldState(newValues, newMask);
        }

        /// <summary>
        /// Returns a copy of this state with a specific fact cleared from the active mask.
        /// </summary>
        public WorldState Without(StateFact fact)
        {
            ulong bit = 1UL << (int)fact;
            return new WorldState(Values & ~bit, Mask & ~bit);
        }

        /// <summary>
        /// Gets the boolean value of a fact in this state. Returns false if unmasked.
        /// </summary>
        public bool Get(StateFact fact)
        {
            ulong bit = 1UL << (int)fact;
            return (Values & bit) != 0;
        }

        /// <summary>
        /// Checks whether a fact is explicitly tracked/masked in this state.
        /// </summary>
        public bool IsSet(StateFact fact)
        {
            ulong bit = 1UL << (int)fact;
            return (Mask & bit) != 0;
        }

        /// <summary>
        /// Calculates the number of facts in the required state that are either missing or mismatching in this state.
        /// Uses branchless Hamming weight (popcount) for single-digit nanosecond execution with zero allocations.
        /// </summary>
        public int CountUnsatisfiedFacts(in WorldState required)
        {
            // A fact is unsatisfied if it is in required.Mask, but this state does not match required.Values
            // Mismatch: (Values ^ required.Values) & required.Mask
            ulong mismatch = (Values ^ required.Values) & required.Mask;
            return PopCount(mismatch);
        }

        /// <summary>
        /// Branchless 64-bit Hamming weight (popcount) calculation.
        /// </summary>
        public static int PopCount(ulong v)
        {
            v = v - ((v >> 1) & 0x5555555555555555UL);
            v = (v & 0x3333333333333333UL) + ((v >> 2) & 0x3333333333333333UL);
            return (int)((((v + (v >> 4)) & 0xF0F0F0F0F0F0F0FUL) * 0x101010101010101UL) >> 56);
        }

        public bool Equals(WorldState other)
        {
            return Values == other.Values && Mask == other.Mask;
        }

        public override bool Equals(object obj)
        {
            return obj is WorldState other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((int)Values * 397) ^ (int)Mask;
            }
        }

        public static bool operator ==(WorldState a, WorldState b) => a.Equals(b);
        public static bool operator !=(WorldState a, WorldState b) => !a.Equals(b);

        public override string ToString()
        {
            var sb = new System.Text.StringBuilder();
            sb.Append("[WorldState: ");
            bool first = true;
            for (int i = 0; i < 64; i++)
            {
                ulong bit = 1UL << i;
                if ((Mask & bit) != 0)
                {
                    if (!first) sb.Append(", ");
                    first = false;
                    sb.Append((StateFact)i);
                    sb.Append("=");
                    sb.Append((Values & bit) != 0);
                }
            }
            if (first) sb.Append("Empty");
            sb.Append("]");
            return sb.ToString();
        }
    }
}
