using System;
using System.Runtime.InteropServices;

namespace Simpiens.Simulation.Economy
{
    /// <summary>
    /// A hunt bounty's party (Epic 10 Part 3, K6.1, Q7), beside its contract: the lair it is posted on, and up to
    /// <see cref="MaxClaimants"/> claimants with the damage each has dealt to the lair's creatures, which shares the reward.
    /// The contract stays open with no claimant of its own, so the single-claim bounty chain leaves hunts alone. Saved in the
    /// market's block as raw memory: 64 bytes, no padding.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct HuntRecord
    {
        public const int MaxClaimants = 3;

        public EntityId Claimant0, Claimant1, Claimant2;
        public float Damage0, Damage1, Damage2;
        public ushort BountyId;
        public byte LairIndex;
        public byte Count;

        public HuntRecord(ushort bountyId, byte lairIndex)
        {
            this = default;
            BountyId = bountyId;
            LairIndex = lairIndex;
        }

        public readonly EntityId ClaimantAt(int index) => index switch { 0 => Claimant0, 1 => Claimant1, 2 => Claimant2, _ => default };
        public readonly float DamageAt(int index) => index switch { 0 => Damage0, 1 => Damage1, 2 => Damage2, _ => 0f };

        public readonly bool HasClaimant(EntityId crawler)
        {
            for (int i = 0; i < Count; i++) if (ClaimantAt(i) == crawler) return true;
            return false;
        }

        public readonly bool IsFull => Count >= MaxClaimants;

        public readonly float TotalDamage => Damage0 + Damage1 + Damage2;

        /// <summary>The same with <paramref name="crawler"/> joined (unchanged when full or already in).</summary>
        public readonly HuntRecord WithClaimant(EntityId crawler)
        {
            if (crawler == default || IsFull || HasClaimant(crawler)) return this;
            var next = this;
            switch (Count)
            {
                case 0: next.Claimant0 = crawler; next.Damage0 = 0f; break;
                case 1: next.Claimant1 = crawler; next.Damage1 = 0f; break;
                default: next.Claimant2 = crawler; next.Damage2 = 0f; break;
            }
            next.Count++;
            return next;
        }

        /// <summary>The same with <paramref name="crawler"/> gone (its share with it), the rest kept in order.</summary>
        public readonly HuntRecord WithoutClaimant(EntityId crawler)
        {
            var next = new HuntRecord(BountyId, LairIndex);
            for (int i = 0; i < Count; i++)
            {
                if (ClaimantAt(i) == crawler) continue;
                next = next.WithClaimant(ClaimantAt(i)).WithDamage(ClaimantAt(i), DamageAt(i));
            }
            return next;
        }

        /// <summary>The same with <paramref name="damage"/> added to <paramref name="crawler"/>'s share (unchanged if it isn't a claimant).</summary>
        public readonly HuntRecord WithDamage(EntityId crawler, float damage)
        {
            var next = this;
            if (Count > 0 && Claimant0 == crawler) next.Damage0 += damage;
            else if (Count > 1 && Claimant1 == crawler) next.Damage1 += damage;
            else if (Count > 2 && Claimant2 == crawler) next.Damage2 += damage;
            return next;
        }

        /// <summary>
        /// The reward split by damage dealt (Q7) into <paramref name="shares"/> (one per claimant, in order): floors of each
        /// claimant's part, the rest to the claimant who dealt the most (the first on a tie); an equal split, the rest to the
        /// first, when nobody dealt any.
        /// </summary>
        public readonly void Split(uint reward, Span<uint> shares)
        {
            if (Count == 0) return;
            float total = TotalDamage;
            uint given = 0;
            int top = 0;
            for (int i = 0; i < Count; i++)
            {
                uint share = total > 0f ? (uint)(reward * (DamageAt(i) / total)) : reward / (uint)Count;
                shares[i] = share;
                given += share;
                if (DamageAt(i) > DamageAt(top)) top = i;
            }
            shares[top] += reward - given;
        }
    }

    /// <summary>Hunt bounties' numbers (Epic 10 Part 3, K6, Q7): how many may claim one, its reward and life when the board proposes one.</summary>
    public readonly struct HuntTuning
    {
        public readonly ushort DefaultReward;
        public readonly int LifeHours;

        public HuntTuning(ushort defaultReward, int lifeHours)
        {
            DefaultReward = defaultReward;
            LifeHours = lifeHours;
        }

        public static HuntTuning Default => new HuntTuning(defaultReward: 60, lifeHours: 48);
    }
}
