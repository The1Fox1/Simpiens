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

        /// <summary>The guild's expeditions (Epic 10 G5).</summary>
        public readonly ExpeditionTuning Expedition;

        public GuildTuning(byte maxMembers, sbyte recruitTrust, sbyte recruitWarmth, ushort signingFee, ushort reservationWage,
            ushort defaultWage, ushort maxWage, uint rejoinCooldownTicks, byte initialAllegiance, sbyte quitTrust, byte maxUnpaidVisits,
            TrustDelta paid, sbyte paidAllegiance, TrustDelta unpaid, sbyte unpaidAllegiance, sbyte dismissTrust, ExpeditionTuning? expedition = null)
        {
            Expedition = expedition ?? ExpeditionTuning.Default;
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

    /// <summary>
    /// An expedition's numbers (Epic 10 G5, decisions D8–D13), part of <see cref="GuildTuning"/>. Durations are in ticks
    /// (10 a second); the stages' limits are safety nets, so a party that can't get on comes home.
    /// </summary>
    public readonly struct ExpeditionTuning
    {
        /// <summary>The muster (D9): within this of the stall, hunger below this and health this or more, or the muster's time runs out (90 s).</summary>
        public readonly float MusterRadius;
        public readonly float MusterHunger;
        public readonly float MusterHealth;
        public readonly uint MusterTicks;

        /// <summary>The longest a march, the work and the way home may take before the party turns back or delivers what it has.</summary>
        public readonly uint MarchTicks;
        public readonly uint WorkTicks;
        public readonly uint ReturnTicks;

        /// <summary>The retreat rule's health floors (D10): Cautious, Steady, Bold.</summary>
        public readonly float CautiousHealth;
        public readonly float SteadyHealth;
        public readonly float BoldHealth;

        /// <summary>The members' share of the haul's caravan value, in percent (D13).</summary>
        public readonly byte SharePercent;

        /// <summary>Trust a member downed under orders loses (D10); a member back with a haul gains trust and allegiance (D8).</summary>
        public readonly sbyte DownedTrust;
        public readonly sbyte ReturnedTrust;
        public readonly sbyte ReturnedAllegiance;

        /// <summary>The most an order may ask for.</summary>
        public readonly byte MaxGoal;

        public ExpeditionTuning(float musterRadius, float musterHunger, float musterHealth, uint musterTicks, uint marchTicks, uint workTicks,
            uint returnTicks, float cautiousHealth, float steadyHealth, float boldHealth, byte sharePercent, sbyte downedTrust,
            sbyte returnedTrust, sbyte returnedAllegiance, byte maxGoal)
        {
            MusterRadius = musterRadius;
            MusterHunger = musterHunger;
            MusterHealth = musterHealth;
            MusterTicks = musterTicks;
            MarchTicks = marchTicks;
            WorkTicks = workTicks;
            ReturnTicks = returnTicks;
            CautiousHealth = cautiousHealth;
            SteadyHealth = steadyHealth;
            BoldHealth = boldHealth;
            SharePercent = sharePercent;
            DownedTrust = downedTrust;
            ReturnedTrust = returnedTrust;
            ReturnedAllegiance = returnedAllegiance;
            MaxGoal = maxGoal;
        }

        public static ExpeditionTuning Default => new ExpeditionTuning(
            musterRadius: 4f,
            musterHunger: 50f,
            musterHealth: 80f,
            musterTicks: 900,
            marchTicks: 1800,
            workTicks: 1800,
            returnTicks: 2400,
            cautiousHealth: 70f,
            steadyHealth: 55f,
            boldHealth: 40f,
            sharePercent: 30,
            downedTrust: -10,
            returnedTrust: 3,
            returnedAllegiance: 5,
            maxGoal: 20);

        /// <summary>The health below which a member turns the party back, under <paramref name="rule"/>.</summary>
        public float RetreatHealth(Simpiens.Simulation.Guild.RetreatRule rule) => rule switch
        {
            Simpiens.Simulation.Guild.RetreatRule.Cautious => CautiousHealth,
            Simpiens.Simulation.Guild.RetreatRule.Bold => BoldHealth,
            _ => SteadyHealth,
        };
    }
}
