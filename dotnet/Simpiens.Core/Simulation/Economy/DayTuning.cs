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
        /// The longest a bunk sleep lasts; it ends sooner once energy reaches <see cref="WakeEnergy"/>, or hunger
        /// <see cref="WakeHunger"/> (a sleeper doesn't starve in its bunk). Energy added when it ends.
        /// </summary>
        public readonly float SleepSeconds;
        public readonly float WakeEnergy;
        public readonly float WakeHunger;
        public readonly float SleepBonus;

        /// <summary>
        /// The longest a rest where it stands lasts (sleeping rough: it ends at the same wake lines as a bunk sleep), and the
        /// energy added when it ends.
        /// </summary>
        public readonly float RestSeconds;
        public readonly float RestBonus;

        /// <summary>Energy a spell at the hearth adds (its social is the hearth's own).</summary>
        public readonly float RelaxEnergyBonus;

        /// <summary>Energy lost a second at work: harvesting (Epic 10 Part 2, T2.4; a station's work, W1.3).</summary>
        public readonly float EnergyWorkingPerSecond;

        /// <summary>How long a crawler's actions take (D22).</summary>
        public readonly ActionTuning Actions;

        public DayTuning(float hungerPerSecond, float energyStandingPerSecond, float energyWalkingPerSecond, float socialPerSecond,
            float bunkRecoveryPerSecond, float restRecoveryPerSecond, float sleepSeconds, float wakeEnergy, float wakeHunger, float sleepBonus,
            float restSeconds, float restBonus, float relaxEnergyBonus, float energyWorkingPerSecond, in ActionTuning actions)
        {
            HungerPerSecond = hungerPerSecond;
            EnergyStandingPerSecond = energyStandingPerSecond;
            EnergyWalkingPerSecond = energyWalkingPerSecond;
            SocialPerSecond = socialPerSecond;
            BunkRecoveryPerSecond = bunkRecoveryPerSecond;
            RestRecoveryPerSecond = restRecoveryPerSecond;
            SleepSeconds = sleepSeconds;
            WakeEnergy = wakeEnergy;
            WakeHunger = wakeHunger;
            SleepBonus = sleepBonus;
            RestSeconds = restSeconds;
            RestBonus = restBonus;
            RelaxEnergyBonus = relaxEnergyBonus;
            EnergyWorkingPerSecond = energyWorkingPerSecond;
            Actions = actions;
        }

        /// <summary>Whether a sleeper (in a bunk or rough) is up: rested, or too hungry to sleep on.</summary>
        public bool Wakes(float energy, float hunger) => energy >= WakeEnergy || hunger >= WakeHunger;

        /// <summary>
        /// The pace before milestone T: hunger fills in 67 s, a bunk visit is a 3-second nap, resting a 2-second pause.
        /// Nobody wakes early (the wake lines are never reached).
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
            wakeHunger: float.MaxValue,
            sleepBonus: 40f,
            restSeconds: 2.0f,
            restBonus: 30f,
            relaxEnergyBonus: 10f,
            energyWorkingPerSecond: 0.2f,
            actions: ActionTuning.Legacy);

        /// <summary>
        /// The 24-hour day (milestone T, built a step at a time; the cut-over makes it the default; <c>docs/time-model.md</c>):
        /// <list type="bullet">
        /// <item>hunger fills over a day (D19) and social empties over 16 hours (T2.1);</item>
        /// <item>energy falls about 2.75 an hour standing and three times that walking, so a day of mostly standing reaches the
        /// tired line (40) after about 16 hours (D20, T2.2);</item>
        /// <item>a sleeper gains 8 an hour in a bunk and half that sleeping rough, for up to 12 hours, until rested (100) or
        /// starving (85): about 8 hours from the tired line;</item>
        /// <item>no bonus at a sleep's end, and the hearth restores company, not energy;</item>
        /// <item>work is stretched RimWorld-style, and tires as walking does (D22, T2.4: <see cref="ActionTuning.FullDay"/>).</item>
        /// </list>
        /// </summary>
        public static DayTuning FullDay => new DayTuning(
            hungerPerSecond: DayClock.PerSecond(100f / DayClock.HoursPerDay),
            energyStandingPerSecond: DayClock.PerSecond(2.75f),
            energyWalkingPerSecond: DayClock.PerSecond(8.25f),
            socialPerSecond: DayClock.PerSecond(100f / 16f),
            bunkRecoveryPerSecond: DayClock.PerSecond(8f),
            restRecoveryPerSecond: DayClock.PerSecond(4f),
            sleepSeconds: 12 * DayClock.SecondsPerHour,
            wakeEnergy: 100f,
            wakeHunger: 85f,
            sleepBonus: 0f,
            restSeconds: 12 * DayClock.SecondsPerHour,
            restBonus: 0f,
            relaxEnergyBonus: 0f,
            energyWorkingPerSecond: DayClock.PerSecond(8.25f),
            actions: ActionTuning.FullDay);

        public static DayTuning Default => Legacy;
    }

    /// <summary>
    /// How long a crawler's actions take, in seconds of simulation time (Epic 10 Part 2, T2.4, D22: work is stretched
    /// RimWorld-style, so a crawler does tens of tasks a day). An action of 0 seconds finishes within its tick, as the
    /// trades and the bounty board's did before the day.
    /// </summary>
    public readonly struct ActionTuning
    {
        /// <summary>A unit harvested: ore and glowstone, herbs, a Garden ration.</summary>
        public readonly float HarvestRockSeconds;
        public readonly float HarvestHerbSeconds;
        public readonly float HarvestFoodSeconds;

        /// <summary>Eating a ration from the pack; a starving crawler's wait at an empty Garden plot.</summary>
        public readonly float EatSeconds;
        public readonly float WaitAtPlotSeconds;

        /// <summary>A trade at the stall; a claim or a delivery at the bounty board; handing food to a peer.</summary>
        public readonly float TradeSeconds;
        public readonly float BoardSeconds;
        public readonly float ShareSeconds;

        /// <summary>A scout's survey at the sector's survey point; treating wounds with a herb; a spell at the hearth.</summary>
        public readonly float SurveySeconds;
        public readonly float TreatSeconds;
        public readonly float RelaxSeconds;

        public ActionTuning(float harvestRockSeconds, float harvestHerbSeconds, float harvestFoodSeconds, float eatSeconds, float waitAtPlotSeconds,
            float tradeSeconds, float boardSeconds, float shareSeconds, float surveySeconds, float treatSeconds, float relaxSeconds)
        {
            HarvestRockSeconds = harvestRockSeconds;
            HarvestHerbSeconds = harvestHerbSeconds;
            HarvestFoodSeconds = harvestFoodSeconds;
            EatSeconds = eatSeconds;
            WaitAtPlotSeconds = waitAtPlotSeconds;
            TradeSeconds = tradeSeconds;
            BoardSeconds = boardSeconds;
            ShareSeconds = shareSeconds;
            SurveySeconds = surveySeconds;
            TreatSeconds = treatSeconds;
            RelaxSeconds = relaxSeconds;
        }

        /// <summary>A unit of <paramref name="item"/> harvested: food from the Garden, herbs, or rock (ore, glowstone).</summary>
        public float HarvestSeconds(ItemType item)
        {
            if (item == ItemType.None || Simpiens.Simulation.Items.ItemCatalog.Is(item, Simpiens.Simulation.Items.ItemCategory.Food)) return HarvestFoodSeconds;
            if (Simpiens.Simulation.Items.ItemCatalog.Is(item, Simpiens.Simulation.Items.ItemCategory.Medicine)) return HarvestHerbSeconds;
            return HarvestRockSeconds;
        }

        /// <summary>Before the day: a harvest 1.5 s a unit, a meal 1 s, a hand-over 1 s, the hearth 3 s; trades, the board, a survey and treating wounds within the tick.</summary>
        public static ActionTuning Legacy => new ActionTuning(
            harvestRockSeconds: 1.5f,
            harvestHerbSeconds: 1.5f,
            harvestFoodSeconds: 1.5f,
            eatSeconds: 1.0f,
            waitAtPlotSeconds: 2f,
            tradeSeconds: 0f,
            boardSeconds: 0f,
            shareSeconds: 1.0f,
            surveySeconds: 0f,
            treatSeconds: 0f,
            relaxSeconds: 3.0f);

        /// <summary>
        /// The 24-hour day's (<c>docs/time-model.md</c> §3.3): a unit of ore or glowstone 30 game minutes, herbs 20, a Garden
        /// ration 15; a meal 15; a trade, the board and a hand-over 10; a survey, treating wounds and the hearth 30.
        /// </summary>
        public static ActionTuning FullDay => new ActionTuning(
            harvestRockSeconds: Minutes(30),
            harvestHerbSeconds: Minutes(20),
            harvestFoodSeconds: Minutes(15),
            eatSeconds: Minutes(15),
            waitAtPlotSeconds: Minutes(15),
            tradeSeconds: Minutes(10),
            boardSeconds: Minutes(10),
            shareSeconds: Minutes(10),
            surveySeconds: Minutes(30),
            treatSeconds: Minutes(30),
            relaxSeconds: Minutes(30));

        private static float Minutes(int minutes) => DayClock.Minutes(minutes) * SimulationClock.FixedTickDeltaTime;
    }
}
