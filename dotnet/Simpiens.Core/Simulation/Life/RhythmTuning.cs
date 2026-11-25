namespace Simpiens.Simulation.Life
{
    /// <summary>
    /// The rhythm's numbers (Epic LIFE-1 plan 3; the epic's A11), inside <see cref="LifeTuning"/>: how a full purse and weariness
    /// weigh work (R.6, D32, D33), and the day's intention's odds and weights (R.7, D34). A zeroed tuning (<c>default</c>) weighs
    /// nothing and draws only work days: every factor it sets reads exactly 1.
    /// Set through <c>with</c> (<c>RhythmTuning.Default with { SatiationFloor = 0.5f }</c>) and <see cref="LifeTuning.WithRhythm"/>.
    /// </summary>
    public readonly struct RhythmTuning
    {
        /// <summary>
        /// How far greed moves a crawler's purse target from the comfortable purse (D32): the target is the comfortable purse
        /// times 1 + greed x this (0.5: from half to one and a half times it), plus what its held wants ask for.
        /// </summary>
        public float PurseGreed { get; init; }

        /// <summary>
        /// Work's weight at twice the purse target and beyond (D32): it weighs 1 up to the target and falls linearly to this. 0 in
        /// a zeroed tuning, read as no satiation.
        /// </summary>
        public float SatiationFloor { get; init; }

        /// <summary>How much weariness at its height takes off work's weight (D33): work weighs 1 - this x weariness / 100.</summary>
        public float WearinessPull { get; init; }

        // --- The day's intention (R.7, D34): its odds ---

        /// <summary>A rest day's odds for anyone, and what weariness at its height, a windfall within the last game day and a purse above its target add.</summary>
        public float RestBase { get; init; }
        public float RestByWeariness { get; init; }
        public float RestAfterWindfall { get; init; }
        public float RestAbovePurse { get; init; }

        /// <summary>A training day's odds for a crawler with a passion; a want's day's for one holding a want.</summary>
        public float TrainingWithPassion { get; init; }
        public float WantDayWithWant { get; init; }

        // --- Its weights (0 reads as 1, unset) ---

        /// <summary>A rest day's weights on work, leisure and company (D34).</summary>
        public float RestDayWork { get; init; }
        public float RestDayLeisure { get; init; }
        public float RestDaySocial { get; init; }

        /// <summary>
        /// A rest day's weight on duty (D36): orders weigh less on a day off, so compliance decides whether a member answers; one
        /// who keeps to them is called in, its day a work day again. The routine itself never weighs duty.
        /// </summary>
        public float RestDayDuty { get; init; }

        /// <summary>
        /// Q11's switch (D37): every seventh game day is a rest day for every resident, whatever its odds; off in the live game.
        /// Artisans' shifts that day are the player's to keep or clear.
        /// </summary>
        public bool OutpostRestDay { get; init; }

        /// <summary>A training day's weights on growth and work; a want's day's on its want (D34).</summary>
        public float TrainingDayGrowth { get; init; }
        public float TrainingDayWork { get; init; }
        public float WantDayWant { get; init; }

        /// <summary>
        /// The day's intention's weight on a goal of <paramref name="family"/> and <paramref name="motive"/> (D34): 1 on a work day,
        /// for the unweighted family (but duty on a rest day, D36), and for any weight left unset.
        /// </summary>
        public float IntentionWeight(DayIntention intention, Simpiens.Cognition.Planning.ActivityFamily family, Simpiens.Cognition.Planning.Motive motive)
        {
            if (intention == DayIntention.Rest && motive == Simpiens.Cognition.Planning.Motive.Duty) return Set(RestDayDuty); // D36
            if (family == Simpiens.Cognition.Planning.ActivityFamily.Unweighted) return 1f;
            switch (intention)
            {
                case DayIntention.Rest:
                    return family switch
                    {
                        Simpiens.Cognition.Planning.ActivityFamily.Work => Set(RestDayWork),
                        Simpiens.Cognition.Planning.ActivityFamily.Leisure => Set(RestDayLeisure),
                        Simpiens.Cognition.Planning.ActivityFamily.Social => Set(RestDaySocial),
                        _ => 1f,
                    };
                case DayIntention.Training:
                    if (motive == Simpiens.Cognition.Planning.Motive.Growth) return Set(TrainingDayGrowth);
                    return family == Simpiens.Cognition.Planning.ActivityFamily.Work ? Set(TrainingDayWork) : 1f;
                case DayIntention.Want:
                    return motive == Simpiens.Cognition.Planning.Motive.Want ? Set(WantDayWant) : 1f;
                default:
                    return 1f;
            }
        }

        private static float Set(float weight) => weight == 0f ? 1f : weight;

        /// <summary>
        /// Plan 3's numbers for the live game: greed moves the purse target by half; work weighs 0.6 at twice its target (D32);
        /// weariness at its height weighs work 0.7 (D33); the day's intention (D34): a rest day 5%, plus 40% at weariness's height,
        /// 50% after a windfall in the last game day and 15% with the purse above its target; a training day 15% with a passion;
        /// a want's day 25% with a want held; a rest day weighs work 0.4 and leisure 1.3, a training day growth 1.5 and work 0.8,
        /// a want's day its want 1.5; a rest day weighs duty 0.8 (D36); no outpost rest day (D37). A rest day weighed company 1.2 at first (D34 amended by measurement, as D30 was): over six days
        /// on the gate seeds the freed hours went to the hearth (4.9 to 5.7 h a resident-day) and leisure fell.
        /// </summary>
        public static RhythmTuning Default => new RhythmTuning
        {
            PurseGreed = 0.5f,
            SatiationFloor = 0.6f,
            WearinessPull = 0.3f,
            RestBase = 0.05f,
            RestByWeariness = 0.4f,
            RestAfterWindfall = 0.5f,
            RestAbovePurse = 0.15f,
            TrainingWithPassion = 0.15f,
            WantDayWithWant = 0.25f,
            RestDayWork = 0.4f,
            RestDayLeisure = 1.3f,
            RestDaySocial = 1f,
            RestDayDuty = 0.8f,
            TrainingDayGrowth = 1.5f,
            TrainingDayWork = 0.8f,
            WantDayWant = 1.5f,
        };
    }
}
