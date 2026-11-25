namespace Simpiens.Simulation.Life
{
    /// <summary>
    /// The rhythm's numbers (Epic LIFE-1 plan 3; the epic's A11), inside <see cref="LifeTuning"/>: how a full purse and weariness
    /// weigh work (R.6, D32, D33). A zeroed tuning (<c>default</c>) weighs nothing: every factor it sets reads exactly 1.
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

        /// <summary>
        /// Plan 3's numbers for the live game: greed moves the purse target by half; work weighs 0.6 at twice its target (D32);
        /// weariness at its height weighs work 0.7 (D33).
        /// </summary>
        public static RhythmTuning Default => new RhythmTuning
        {
            PurseGreed = 0.5f,
            SatiationFloor = 0.6f,
            WearinessPull = 0.3f,
        };
    }
}
