namespace Simpiens.Simulation.Economy
{
    /// <summary>
    /// The workshops' numbers (Epic 10 Part 2): what a station costs to build (D10), and who may be hired to work one, on
    /// what wage, and when an artisan quits (Q3, D9; a member's terms, Part 1's D2–D4, are the model). Part of the
    /// session's <see cref="EconomyTuning"/>, never literals. Durations are in ticks (10 a second).
    /// </summary>
    public readonly struct WorkshopTuning
    {
        /// <summary>What building a station takes from the stall's treasury, once (Q2): the <c>Construction</c> sink.</summary>
        public readonly ushort ConstructionCost;

        /// <summary>A resident is hired at this trust and warmth toward the player or more (Q3).</summary>
        public readonly sbyte HireTrust;
        public readonly sbyte HireWarmth;

        /// <summary>An artisan's risk tolerance is below this (Q3): one who would not go into danger alone.</summary>
        public readonly byte MaxRiskTolerancePercent;

        /// <summary>A neutral crawler's least wage per caravan visit; a crawler's own is this times its greed factor.</summary>
        public readonly ushort ReservationWage;

        /// <summary>The wage the station window offers first, and the most a wage may be.</summary>
        public readonly ushort DefaultWage;
        public readonly ushort MaxWage;

        /// <summary>After quitting or being let go, a crawler can't be hired again for this long (5 minutes).</summary>
        public readonly uint RehireCooldownTicks;

        /// <summary>A new artisan's allegiance to its station's crew (Epic 8's 0–100).</summary>
        public readonly byte InitialAllegiance;

        /// <summary>An artisan quits below this trust toward the player, or after this many unpaid visits in a row.</summary>
        public readonly sbyte QuitTrust;
        public readonly byte MaxUnpaidVisits;

        /// <summary>A paid artisan's affinity toward the player and allegiance to its crew, per visit.</summary>
        public readonly TrustDelta Paid;
        public readonly sbyte PaidAllegiance;

        /// <summary>An unpaid artisan's, per visit.</summary>
        public readonly TrustDelta Unpaid;
        public readonly sbyte UnpaidAllegiance;

        /// <summary>Trust toward the player an artisan who is let go loses.</summary>
        public readonly sbyte DismissTrust;

        /// <summary>
        /// Overwork (W1.3, D15, D16): an artisan on shift at or under this energy breaks down, at this cost in affinity
        /// toward the player, and works no shift until rested to <see cref="RestedEnergy"/>.
        /// </summary>
        public readonly float BreakEnergy;
        public readonly TrustDelta Break;
        public readonly float RestedEnergy;

        /// <summary>
        /// Hours on shift in a row that break an artisan whatever its energy (W1.3, D35): three blocks in a row deplete its
        /// needs, two don't. In ticks.
        /// </summary>
        public readonly uint BreakAfterTicks;

        public WorkshopTuning(ushort constructionCost, sbyte hireTrust, sbyte hireWarmth, byte maxRiskTolerancePercent, ushort reservationWage,
            ushort defaultWage, ushort maxWage, uint rehireCooldownTicks, byte initialAllegiance, sbyte quitTrust, byte maxUnpaidVisits,
            TrustDelta paid, sbyte paidAllegiance, TrustDelta unpaid, sbyte unpaidAllegiance, sbyte dismissTrust,
            float breakEnergy = 15f, TrustDelta breakCost = default, float restedEnergy = 60f, uint breakAfterTicks = 10 * DayClock.TicksPerHour)
        {
            BreakAfterTicks = breakAfterTicks;
            BreakEnergy = breakEnergy;
            Break = breakCost;
            RestedEnergy = restedEnergy;
            ConstructionCost = constructionCost;
            HireTrust = hireTrust;
            HireWarmth = hireWarmth;
            MaxRiskTolerancePercent = maxRiskTolerancePercent;
            ReservationWage = reservationWage;
            DefaultWage = defaultWage;
            MaxWage = maxWage;
            RehireCooldownTicks = rehireCooldownTicks;
            InitialAllegiance = initialAllegiance;
            QuitTrust = quitTrust;
            MaxUnpaidVisits = maxUnpaidVisits;
            Paid = paid;
            PaidAllegiance = paidAllegiance;
            Unpaid = unpaid;
            UnpaidAllegiance = unpaidAllegiance;
            DismissTrust = dismissTrust;
        }

        public static WorkshopTuning Default => new WorkshopTuning(
            constructionCost: 300,
            hireTrust: 50,
            hireWarmth: 30,
            maxRiskTolerancePercent: 30,
            reservationWage: 5,
            defaultWage: 5,
            maxWage: 50,
            rehireCooldownTicks: 3000,
            initialAllegiance: 70,
            quitTrust: 30,
            maxUnpaidVisits: 2,
            paid: new TrustDelta(warmth: 1, trust: 2, respect: 0),
            paidAllegiance: 1,
            unpaid: new TrustDelta(warmth: -5, trust: -10, respect: 0),
            unpaidAllegiance: -5,
            dismissTrust: -5,
            breakEnergy: 15f,
            breakCost: new TrustDelta(warmth: -5, trust: -10, respect: 0),
            restedEnergy: 60f,
            breakAfterTicks: DayClock.Hours(10));
    }
}
