namespace Simpiens.Simulation.Economy
{
    /// <summary>
    /// The pace of a crawler's day (Epic 10 Part 2, milestone T; <c>docs/time-model.md</c>): how fast needs move, how
    /// sleep and rest restore energy, how long actions take, how health mends and how fast trust fades. Part of the
    /// session's <see cref="EconomyTuning"/>, never literals. Rates are per second of simulation time, as the needs are
    /// ticked, and written per game hour through <see cref="DayClock.PerSecond"/>.
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
        /// A tired crawler this hungry or more eats before it goes to sleep, unless it is too tired to walk (Epic 10 Part 2,
        /// D34): a night's sleep adds about 33 hunger, so from 50 it sleeps until rested rather than until
        /// <see cref="WakeHunger"/> cuts it short.
        /// </summary>
        public readonly float BedtimeHunger;

        /// <summary>
        /// The longest a rest where it stands lasts (sleeping rough: it ends at the same wake lines as a bunk sleep), and the
        /// energy added when it ends.
        /// </summary>
        public readonly float RestSeconds;
        public readonly float RestBonus;

        /// <summary>Energy a spell at the hearth adds (its social is the hearth's own).</summary>
        public readonly float RelaxEnergyBonus;

        /// <summary>Energy lost a second at work: harvesting (Epic 10 Part 2, T2.4).</summary>
        public readonly float EnergyWorkingPerSecond;

        /// <summary>
        /// Energy lost a second at a station's work (W1.3, D15): less than hauling and swinging at a seam, more than standing,
        /// so two blocks of a shift leave a rested artisan tired but whole and a third breaks it.
        /// </summary>
        public readonly float EnergyCraftingPerSecond;

        /// <summary>How long a crawler's actions take (D22).</summary>
        public readonly ActionTuning Actions;

        /// <summary>Health healed a second awake, and asleep in a bunk.</summary>
        public readonly float RestingHealPerSecond;
        public readonly float BunkHealPerSecond;

        /// <summary>Ticks per step of 5 a remembered affinity (trust toward the player included) fades toward neutral since the last dealing (T4.3).</summary>
        public readonly uint TrustFadeTicks;

        public DayTuning(float hungerPerSecond, float energyStandingPerSecond, float energyWalkingPerSecond, float socialPerSecond,
            float bunkRecoveryPerSecond, float restRecoveryPerSecond, float sleepSeconds, float wakeEnergy, float wakeHunger, float bedtimeHunger, float sleepBonus,
            float restSeconds, float restBonus, float relaxEnergyBonus, float energyWorkingPerSecond, float energyCraftingPerSecond, in ActionTuning actions,
            float restingHealPerSecond, float bunkHealPerSecond, uint trustFadeTicks)
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
            BedtimeHunger = bedtimeHunger;
            SleepBonus = sleepBonus;
            RestSeconds = restSeconds;
            RestBonus = restBonus;
            RelaxEnergyBonus = relaxEnergyBonus;
            EnergyWorkingPerSecond = energyWorkingPerSecond;
            EnergyCraftingPerSecond = energyCraftingPerSecond;
            Actions = actions;
            RestingHealPerSecond = restingHealPerSecond;
            BunkHealPerSecond = bunkHealPerSecond;
            TrustFadeTicks = trustFadeTicks;
        }

        /// <summary>Whether a sleeper (in a bunk or rough) is up: rested, or too hungry to sleep on.</summary>
        public bool Wakes(float energy, float hunger) => energy >= WakeEnergy || hunger >= WakeHunger;

        /// <summary>
        /// The 24-hour day (milestone T, RimWorld-paced; <c>docs/time-model.md</c>):
        /// <list type="bullet">
        /// <item>hunger fills over a day (D19) and social empties over 16 hours (T2.1);</item>
        /// <item>energy falls about 2.75 an hour standing and three times that walking, so a day of mostly standing reaches the
        /// tired line (40) after about 16 hours (D20, T2.2);</item>
        /// <item>a sleeper gains 8 an hour in a bunk and half that sleeping rough, for up to 12 hours, until rested (100) or
        /// starving (85): about 8 hours from the tired line;</item>
        /// <item>no bonus at a sleep's end, and the hearth restores company, not energy;</item>
        /// <item>work is stretched RimWorld-style, and tires as walking does (D22, T2.4: <see cref="ActionTuning.Default"/>);</item>
        /// <item>a bunk heals 10 an hour, a night 80, and awake about 50 a day (T3.2a; hazards' damage is the map's, gear's
        /// life the catalog's);</item>
        /// <item>trust and the other affinities fade 5 every 12 game hours since the last dealing, not every 2.4 (T4.3, D27: at 6
        /// trust rose for 8 of 12 residents on all but one gate seed; at 12, on all of them).</item>
        /// </list>
        /// </summary>
        public static DayTuning Default => new DayTuning(
            hungerPerSecond: DayClock.PerSecond(100f / DayClock.HoursPerDay),
            energyStandingPerSecond: DayClock.PerSecond(2.75f),
            energyWalkingPerSecond: DayClock.PerSecond(8.25f),
            socialPerSecond: DayClock.PerSecond(100f / 16f),
            bunkRecoveryPerSecond: DayClock.PerSecond(8f),
            restRecoveryPerSecond: DayClock.PerSecond(4f),
            sleepSeconds: 12 * DayClock.SecondsPerHour,
            wakeEnergy: 100f,
            wakeHunger: 85f,
            bedtimeHunger: 50f,
            sleepBonus: 0f,
            restSeconds: 12 * DayClock.SecondsPerHour,
            restBonus: 0f,
            relaxEnergyBonus: 0f,
            energyWorkingPerSecond: DayClock.PerSecond(8.25f),
            energyCraftingPerSecond: DayClock.PerSecond(6f),
            actions: ActionTuning.Default,
            restingHealPerSecond: 0.05f,
            bunkHealPerSecond: DayClock.PerSecond(10f),
            trustFadeTicks: DayClock.Hours(12));
    }

    /// <summary>
    /// How long a crawler's actions take, in seconds of simulation time (Epic 10 Part 2, T2.4, D22: work is stretched
    /// RimWorld-style, so a crawler does tens of tasks a day). An action of 0 seconds finishes within its tick.
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

        /// <summary>
        /// The 24-hour day's (<c>docs/time-model.md</c> §3.3): a unit of ore or glowstone 30 game minutes, herbs 20, a Garden
        /// ration 15; a meal 15; a trade, the board and a hand-over 10; a survey, treating wounds and the hearth 30.
        /// </summary>
        public static ActionTuning Default => new ActionTuning(
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
