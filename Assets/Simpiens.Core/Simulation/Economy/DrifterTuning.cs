namespace Simpiens.Simulation.Economy
{
    /// <summary>
    /// Drifters (part of EconomyTuning; Epic 10 G1, producer decision Q7): strangers who walk in through the Road Gate
    /// with gold of their own, live as crawlers for a stay, and leave. Their purses are money from outside the outpost
    /// (the "DrifterPurses" faucet); what they still carry when they leave goes with them ("DrifterDepartures").
    /// Durations are in logical ticks (10 Hz). A session whose map has no spawn points never admits one.
    /// </summary>
    public readonly struct DrifterTuning
    {
        /// <summary>Tick of the first arrival (session-relative; every session starts at 0).</summary>
        public readonly uint FirstArrivalTick;

        /// <summary>Ticks between arrivals, each moved by up to <see cref="ArrivalJitterTicks"/> either way (seeded).</summary>
        public readonly uint ArrivalIntervalTicks;

        public readonly uint ArrivalJitterTicks;

        /// <summary>Most drifters in the outpost at once; an arrival due while it is full is skipped. 0 turns drifters off.</summary>
        public readonly byte MaxPresent;

        /// <summary>A drifter's stay, drawn between these (seeded); after it, it heads for the gate.</summary>
        public readonly uint StayMinTicks;

        public readonly uint StayMaxTicks;

        /// <summary>Gold a drifter brings, drawn between these (seeded).</summary>
        public readonly ushort PurseMin;

        public readonly ushort PurseMax;

        /// <summary>Hunger on arrival, drawn between these: they come off the road wanting a meal (outside buyers).</summary>
        public readonly float ArrivalHungerMin;

        public readonly float ArrivalHungerMax;

        public DrifterTuning(uint firstArrivalTick, uint arrivalIntervalTicks, uint arrivalJitterTicks, byte maxPresent,
            uint stayMinTicks, uint stayMaxTicks, ushort purseMin, ushort purseMax, float arrivalHungerMin, float arrivalHungerMax)
        {
            FirstArrivalTick = firstArrivalTick;
            ArrivalIntervalTicks = arrivalIntervalTicks;
            ArrivalJitterTicks = arrivalJitterTicks;
            MaxPresent = maxPresent;
            StayMinTicks = stayMinTicks;
            StayMaxTicks = stayMaxTicks;
            PurseMin = purseMin;
            PurseMax = purseMax;
            ArrivalHungerMin = arrivalHungerMin;
            ArrivalHungerMax = arrivalHungerMax;
        }

        /// <summary>
        /// A stranger about every three and a half minutes from minute two, at most three at once, staying three to five
        /// minutes, with 20-26g: a meal or so. Measured 2026-10-23 (headless soak): purses of 40-80g every two minutes
        /// nearly doubled the fair player's treasury at 30 minutes (1,189-1,273g); these keep it in its band (768-831g
        /// against 375-875g), and a slightly greedy player still ends richer than in a closed outpost over the soak seeds.
        /// </summary>
        public static DrifterTuning Default => new DrifterTuning(firstArrivalTick: 1200, arrivalIntervalTicks: 2100, arrivalJitterTicks: 300,
            maxPresent: 3, stayMinTicks: 1800, stayMaxTicks: 3000, purseMin: 20, purseMax: 26, arrivalHungerMin: 40f, arrivalHungerMax: 70f);

        /// <summary>No drifters: a closed economy (the economy soak's comparison, measurement sessions).</summary>
        public static DrifterTuning None => default;

        public bool IsEnabled => MaxPresent > 0 && ArrivalIntervalTicks > 0;
    }
}
