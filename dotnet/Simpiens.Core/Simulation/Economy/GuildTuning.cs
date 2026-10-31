namespace Simpiens.Simulation.Economy
{
    /// <summary>
    /// The guild's numbers (Epic 10 G4, decisions D2–D4): who may sign on, the fee and wages, and when a member quits.
    /// Part of the session's <see cref="EconomyTuning"/>, never literals. Durations are in ticks (10 a second).
    /// </summary>
    public readonly struct GuildTuning
    {
        /// <summary>Members at most (Q4: four, so the whole guild can be one party).</summary>
        public readonly byte MaxMembers;

        /// <summary>A crawler signs on at this trust and warmth toward the player or more (Q5).</summary>
        public readonly sbyte RecruitTrust;
        public readonly sbyte RecruitWarmth;

        /// <summary>What the stall pays a recruit when it signs (D2).</summary>
        public readonly ushort SigningFee;

        /// <summary>A neutral crawler's least wage per caravan visit; a crawler's own is this times its greed factor (D2).</summary>
        public readonly ushort ReservationWage;

        /// <summary>The wage the dossier offers first, and the most a wage may be.</summary>
        public readonly ushort DefaultWage;
        public readonly ushort MaxWage;

        /// <summary>After quitting or being dismissed, a crawler can't sign again for this long (5 minutes).</summary>
        public readonly uint RejoinCooldownTicks;

        /// <summary>A new member's allegiance to the guild (Epic 8's 0–100).</summary>
        public readonly byte InitialAllegiance;

        /// <summary>A member quits below this trust toward the player, or after this many unpaid visits in a row (D4).</summary>
        public readonly sbyte QuitTrust;
        public readonly byte MaxUnpaidVisits;

        /// <summary>A paid member's affinity toward the player and allegiance to the guild, per visit (D3).</summary>
        public readonly TrustDelta Paid;
        public readonly sbyte PaidAllegiance;

        /// <summary>An unpaid member's, per visit (D3).</summary>
        public readonly TrustDelta Unpaid;
        public readonly sbyte UnpaidAllegiance;

        /// <summary>Trust toward the player a dismissed member loses (D4).</summary>
        public readonly sbyte DismissTrust;

        public GuildTuning(byte maxMembers, sbyte recruitTrust, sbyte recruitWarmth, ushort signingFee, ushort reservationWage,
            ushort defaultWage, ushort maxWage, uint rejoinCooldownTicks, byte initialAllegiance, sbyte quitTrust, byte maxUnpaidVisits,
            TrustDelta paid, sbyte paidAllegiance, TrustDelta unpaid, sbyte unpaidAllegiance, sbyte dismissTrust)
        {
            MaxMembers = maxMembers;
            RecruitTrust = recruitTrust;
            RecruitWarmth = recruitWarmth;
            SigningFee = signingFee;
            ReservationWage = reservationWage;
            DefaultWage = defaultWage;
            MaxWage = maxWage;
            RejoinCooldownTicks = rejoinCooldownTicks;
            InitialAllegiance = initialAllegiance;
            QuitTrust = quitTrust;
            MaxUnpaidVisits = maxUnpaidVisits;
            Paid = paid;
            PaidAllegiance = paidAllegiance;
            Unpaid = unpaid;
            UnpaidAllegiance = unpaidAllegiance;
            DismissTrust = dismissTrust;
        }

        public static GuildTuning Default => new GuildTuning(
            maxMembers: 4,
            recruitTrust: 60,
            recruitWarmth: 40,
            signingFee: 20,
            reservationWage: 5,
            defaultWage: 5,
            maxWage: 50,
            rejoinCooldownTicks: 3000,
            initialAllegiance: 70,
            quitTrust: 30,
            maxUnpaidVisits: 2,
            paid: new TrustDelta(warmth: 1, trust: 2, respect: 0),
            paidAllegiance: 1,
            unpaid: new TrustDelta(warmth: -5, trust: -10, respect: 0),
            unpaidAllegiance: -5,
            dismissTrust: -5);
    }
}
