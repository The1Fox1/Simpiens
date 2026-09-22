using System;
using UnityEngine;

namespace Simpiens.Cognition.Social
{
    /// <summary>
    /// A zero-allocation, 16-byte unmanaged stack struct representing an agent's bilateral opinion of another entity.
    /// Operates across four orthogonal psychological axes:
    /// - Warmth (-100 to +100): Affection, camaraderie, and altruism vs. hatred, animosity, and disgust.
    /// - Trust (-100 to +100): Reliability, honesty, and predictability vs. treachery, suspicion, and deceit.
    /// - Fear (0 to 100): Physical dread, intimidation, and terror vs. confidence, equality, or dominance.
    /// - Respect (-100 to +100): Admiration, prestige, and recognized competence vs. contempt, derision, and pity.
    /// </summary>
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    public readonly struct AffinityRecord : IEquatable<AffinityRecord>
    {
        public readonly sbyte Warmth;
        public readonly sbyte Trust;
        public readonly sbyte Fear;
        public readonly sbyte Respect;
        public readonly byte InteractionCount;
        private readonly byte _padding0;
        private readonly byte _padding1;
        private readonly byte _padding2;
        public readonly uint LastInteractionTick;

        public static readonly AffinityRecord Neutral = new AffinityRecord(0, 0, 0, 0, 0, 0);

        public AffinityRecord(sbyte warmth, sbyte trust, sbyte fear, sbyte respect, byte interactionCount, uint lastInteractionTick)
        {
            Warmth = (sbyte)Mathf.Clamp(warmth, -100, 100);
            Trust = (sbyte)Mathf.Clamp(trust, -100, 100);
            Fear = (sbyte)Mathf.Clamp(fear, 0, 100);
            Respect = (sbyte)Mathf.Clamp(respect, -100, 100);
            InteractionCount = interactionCount;
            _padding0 = 0;
            _padding1 = 0;
            _padding2 = 0;
            LastInteractionTick = lastInteractionTick;
        }

        public bool IsTrusted => Trust >= 20;
        public bool IsDistrusted => Trust <= -20;
        public bool IsFeared => Fear >= 40;
        public bool IsWarm => Warmth >= 20;
        public bool IsHostile => Warmth <= -20;
        public bool IsRespected => Respect >= 30;
        public bool IsDisrespected => Respect <= -20;

        /// <summary>
        /// Produces a mutated copy reflecting an interaction event with clamped bounded axes.
        /// </summary>
        public AffinityRecord WithInteraction(sbyte deltaWarmth, sbyte deltaTrust, sbyte deltaFear, sbyte deltaRespect, uint currentTick)
        {
            int newWarmth = Mathf.Clamp(Warmth + deltaWarmth, -100, 100);
            int newTrust = Mathf.Clamp(Trust + deltaTrust, -100, 100);
            int newFear = Mathf.Clamp(Fear + deltaFear, 0, 100);
            int newRespect = Mathf.Clamp(Respect + deltaRespect, -100, 100);
            byte newCount = InteractionCount < byte.MaxValue ? (byte)(InteractionCount + 1) : byte.MaxValue;

            return new AffinityRecord((sbyte)newWarmth, (sbyte)newTrust, (sbyte)newFear, (sbyte)newRespect, newCount, currentTick);
        }

        /// <summary>
        /// Slowly decays extreme emotional valences back toward the baseline neutral state over elapsed simulation ticks.
        /// </summary>
        public AffinityRecord RegressTowardNeutral(uint currentTick, uint regressionPeriod = 1000)
        {
            if (currentTick <= LastInteractionTick || regressionPeriod == 0)
            {
                return this;
            }

            uint elapsedTicks = currentTick - LastInteractionTick;
            uint intervals = elapsedTicks / regressionPeriod;
            if (intervals == 0)
            {
                return this;
            }

            int decayAmount = (int)intervals * 5;

            int newWarmth = RegressValue(Warmth, decayAmount);
            int newTrust = RegressValue(Trust, decayAmount);
            int newFear = Mathf.Max(0, Fear - decayAmount);
            int newRespect = RegressValue(Respect, decayAmount);

            uint remainder = elapsedTicks % regressionPeriod;
            uint newTick = currentTick - remainder;

            return new AffinityRecord((sbyte)newWarmth, (sbyte)newTrust, (sbyte)newFear, (sbyte)newRespect, InteractionCount, newTick);
        }

        private static int RegressValue(sbyte value, int amount)
        {
            if (value > 0)
            {
                return Mathf.Max(0, value - amount);
            }
            if (value < 0)
            {
                return Mathf.Min(0, value + amount);
            }
            return 0;
        }

        public bool Equals(AffinityRecord other)
        {
            return Warmth == other.Warmth &&
                   Trust == other.Trust &&
                   Fear == other.Fear &&
                   Respect == other.Respect &&
                   InteractionCount == other.InteractionCount &&
                   LastInteractionTick == other.LastInteractionTick;
        }

        public override bool Equals(object obj) => obj is AffinityRecord other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = (int)Warmth;
                hash = (hash * 397) ^ (int)Trust;
                hash = (hash * 397) ^ (int)Fear;
                hash = (hash * 397) ^ (int)Respect;
                hash = (hash * 397) ^ (int)InteractionCount;
                hash = (hash * 397) ^ (int)LastInteractionTick;
                return hash;
            }
        }

        public static bool operator ==(AffinityRecord left, AffinityRecord right) => left.Equals(right);
        public static bool operator !=(AffinityRecord left, AffinityRecord right) => !left.Equals(right);

        public override string ToString()
        {
            return $"[Affinity: Warmth={Warmth}, Trust={Trust}, Fear={Fear}, Respect={Respect} | Count={InteractionCount}, Tick={LastInteractionTick}]";
        }
    }
}
