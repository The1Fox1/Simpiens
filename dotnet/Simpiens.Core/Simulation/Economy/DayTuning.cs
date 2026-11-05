namespace Simpiens.Simulation.Economy
{
    /// <summary>
    /// The pace of a crawler's day (Epic 10 Part 2, milestone T; <c>docs/time-model.md</c>): how fast needs move, and how
    /// sleep and rest restore energy. Part of the session's <see cref="EconomyTuning"/>, never literals. Rates are per
    /// second of simulation time, as the needs are ticked; the day's are written per game hour through
    /// <see cref="DayClock.PerSecond"/>.
    /// <para>
    /// <see cref="Legacy"/> is the pace before milestone T, kept, number for number, until the cut-over (T5) so that every
    /// step before it can show the golden hashes unchanged; the cut-over makes the 24-hour day the default and deletes it.
    /// </para>
    /// </summary>
    public readonly struct DayTuning
    {
        /// <summary>Hunger gained a second (0 fed, 100 starving).</summary>
        public readonly float HungerPerSecond;

        /// <summary>Energy lost a second standing, and walking.</summary>
        public readonly float EnergyStandingPerSecond;
        public readonly float EnergyWalkingPerSecond;

        /// <summary>Social lost a second.</summary>
        public readonly float SocialPerSecond;

        /// <summary>Energy gained a second asleep in a bunk, and resting where it stands (or collapsed).</summary>
        public readonly float BunkRecoveryPerSecond;
        public readonly float RestRecoveryPerSecond;

        /// <summary>
        /// The longest a bunk sleep lasts; it ends sooner once energy reaches <see cref="WakeEnergy"/>. Energy added when it
        /// ends.
        /// </summary>
        public readonly float SleepSeconds;
        public readonly float WakeEnergy;
        public readonly float SleepBonus;

        /// <summary>How long a rest where it stands lasts, and the energy added when it ends.</summary>
        public readonly float RestSeconds;
        public readonly float RestBonus;

        public DayTuning(float hungerPerSecond, float energyStandingPerSecond, float energyWalkingPerSecond, float socialPerSecond,
            float bunkRecoveryPerSecond, float restRecoveryPerSecond, float sleepSeconds, float wakeEnergy, float sleepBonus,
            float restSeconds, float restBonus)
        {
            HungerPerSecond = hungerPerSecond;
            EnergyStandingPerSecond = energyStandingPerSecond;
            EnergyWalkingPerSecond = energyWalkingPerSecond;
            SocialPerSecond = socialPerSecond;
            BunkRecoveryPerSecond = bunkRecoveryPerSecond;
            RestRecoveryPerSecond = restRecoveryPerSecond;
            SleepSeconds = sleepSeconds;
            WakeEnergy = wakeEnergy;
            SleepBonus = sleepBonus;
            RestSeconds = restSeconds;
            RestBonus = restBonus;
        }

        /// <summary>
        /// The pace before milestone T: hunger fills in 67 s, a bunk visit is a 3-second nap, resting a 2-second pause.
        /// Nobody wakes early (<see cref="WakeEnergy"/> is never reached).
        /// </summary>
        public static DayTuning Legacy => new DayTuning(
            hungerPerSecond: 1.5f,
            energyStandingPerSecond: 0.2f,
            energyWalkingPerSecond: 1.0f,
            socialPerSecond: 0.5f,
            bunkRecoveryPerSecond: 30f,
            restRecoveryPerSecond: 15f,
            sleepSeconds: 3.0f,
            wakeEnergy: float.MaxValue,
            sleepBonus: 40f,
            restSeconds: 2.0f,
            restBonus: 30f);

        public static DayTuning Default => Legacy;
    }
}
