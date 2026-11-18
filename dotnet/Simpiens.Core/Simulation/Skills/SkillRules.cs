using Simpiens.Simulation.Items;

namespace Simpiens.Simulation.Skills
{
    /// <summary>
    /// The skills' rule (Epic 10 Part 3, K9, Q18–Q20): levels from experience, the bands, what each kind of practice is worth,
    /// and what a level does for its kind of work. Pure functions and constants, as the combat rule's are. At level 0 a
    /// crawler fights, makes and tends by the numbers the game had before skills; the harvest alone is centred on a Novice 2
    /// (<see cref="HarvestBase"/>).
    /// </summary>
    public static class SkillRules
    {
        /// <summary>Levels run 0 to this (Q18).</summary>
        public const int MaxLevel = 10;

        /// <summary>A level costs this times its number: 100 for the first, 1,000 for the tenth, 5,500 in all ("each level costs more than the last").</summary>
        public const int LevelCost = 100;

        /// <summary>The experience that reaches <paramref name="level"/> from nothing.</summary>
        public static int ExperienceFor(int level)
        {
            if (level <= 0) return 0;
            if (level > MaxLevel) level = MaxLevel;
            return LevelCost * level * (level + 1) / 2;
        }

        /// <summary>The most experience a skill holds: the last level's. Practice past it is lost.</summary>
        public static int MaxExperience => ExperienceFor(MaxLevel);

        /// <summary>The level <paramref name="experience"/> has reached.</summary>
        public static int LevelOf(int experience)
        {
            int level = 0;
            while (level < MaxLevel && experience >= ExperienceFor(level + 1)) level++;
            return level;
        }

        /// <summary>A level's band (Q18): Novice 0–2, Apprentice 3–5, Journeyman 6–8, Master 9–10.</summary>
        public static SkillBand BandOf(int level) => level >= 9 ? SkillBand.Master : level >= 6 ? SkillBand.Journeyman : level >= 3 ? SkillBand.Apprentice : SkillBand.Novice;

        public static string NameOf(SkillBand band) => band switch
        {
            SkillBand.Apprentice => "Apprentice",
            SkillBand.Journeyman => "Journeyman",
            SkillBand.Master => "Master",
            _ => "Novice"
        };

        // --- Practice (K9.2, Q18): what each act is worth ---

        /// <summary>A blow landed in reach.</summary>
        public const int HitExperience = 10;

        /// <summary>A shot loosed, hit or miss (a shot takes longer than a swing, and half of them miss at range).</summary>
        public const int ShotExperience = 8;

        /// <summary>A unit of salvage mined or foraged.</summary>
        public const int UnitExperience = 10;

        /// <summary>A make finished at a station: ten makes take a new hand to Apprentice, as W4's ten makes took it to Fine.</summary>
        public const int MakeExperience = 60;

        /// <summary>A fallen crawler carried out of danger, or stood up with a medicine.</summary>
        public const int RescueExperience = 100;

        // --- Practice by challenge (Epic 11, S1.3, Q8, D5) ---

        /// <summary>
        /// What share of an act's practice a crawler takes, in hundredths, by how far its level stands above the act's
        /// challenge: all of it at or below, then 75, 55, 40, 25, 20, 15, and a tenth at seven levels or more above. A rat
        /// (2) teaches a Novice fully and a Journeyman 6 a quarter; a beetle (7) teaches a Journeyman fully.
        /// </summary>
        private static readonly int[] ChallengeShares = { 100, 75, 55, 40, 25, 20, 15, 10 };

        public static int ChallengePercent(int level, int challenge)
        {
            int above = Clamp(level) - Clamp(challenge);
            return above <= 0 ? 100 : ChallengeShares[above < ChallengeShares.Length ? above : ChallengeShares.Length - 1];
        }

        // --- The mastery budget (Epic 11, S2.1, Q6, D7) ---

        /// <summary>How many skills may stand past Apprentice (Journeyman or Master) at once.</summary>
        public const int PastApprenticeSlots = 2;

        /// <summary>How many skills may stand at Master at once.</summary>
        public const int MasterSlots = 1;

        /// <summary>Where a skill stops while every slot past Apprentice is taken: the top of Apprentice.</summary>
        public const int ApprenticeTop = 5;

        /// <summary>Where a skill stops while the Master's slot is taken: the top of Journeyman.</summary>
        public const int JourneymanTop = 8;

        /// <summary>
        /// The mastery budget (Q6, D7): the highest level <paramref name="skill"/> may reach while the crawler's other skills
        /// stand where they do. It stops at 5 (<see cref="ApprenticeTop"/>) while two others stand past Apprentice, at 8
        /// (<see cref="JourneymanTop"/>) while another stands at Master, and otherwise at the last level. Slots go first
        /// come, first served: the skills that got there first hold them, and nobody gives a skill up. Practice's one entry
        /// holds a skill to this beside its source's ceiling; a skill already past it keeps what it has. With why it is held.
        /// </summary>
        public static int BudgetCeiling(in SkillSet skills, Skill skill, out BudgetHold hold)
        {
            int pastApprentice = 0, masters = 0;
            var all = SkillTable.All;
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i].Id == skill) continue;
                var band = skills.Band(all[i].Id);
                if (band >= SkillBand.Journeyman) pastApprentice++;
                if (band >= SkillBand.Master) masters++;
            }
            if (pastApprentice >= PastApprenticeSlots) { hold = BudgetHold.PastApprentice; return ApprenticeTop; }
            if (masters >= MasterSlots) { hold = BudgetHold.Master; return JourneymanTop; }
            hold = BudgetHold.None;
            return MaxLevel;
        }

        /// <inheritdoc cref="BudgetCeiling(in SkillSet, Skill, out BudgetHold)"/>
        public static int BudgetCeiling(in SkillSet skills, Skill skill) => BudgetCeiling(skills, skill, out _);

        /// <summary>Whether <paramref name="skill"/> stands at its budget's ceiling below the last level: practice in it is lost until nothing (it is never given up).</summary>
        public static bool HeldByBudget(in SkillSet skills, Skill skill, out int ceiling, out BudgetHold hold)
        {
            ceiling = BudgetCeiling(skills, skill, out hold);
            return hold != BudgetHold.None && skills.Level(skill) >= ceiling;
        }

        /// <summary>Whether <paramref name="skills"/> keep the budget: no more than two skills past Apprentice, no more than one at Master. What live scenarios assert at every tick.</summary>
        public static bool KeepsTheBudget(in SkillSet skills)
        {
            int pastApprentice = 0, masters = 0;
            var all = SkillTable.All;
            for (int i = 0; i < all.Count; i++)
            {
                var band = skills.Band(all[i].Id);
                if (band >= SkillBand.Journeyman) pastApprentice++;
                if (band >= SkillBand.Master) masters++;
            }
            return pastApprentice <= PastApprenticeSlots && masters <= MasterSlots;
        }

        // --- Mentors (Epic 11, S3.1, Q10, D9) ---

        /// <summary>A mentor stands at least this many levels above the learner at the work (its effective level).</summary>
        public const int MentorLead = 3;

        /// <summary>A lesson is this share of the act's experience, in percent, before the learner's passion.</summary>
        public const int MentorPercent = 50;

        /// <summary>A mentor teaches to its own level less this.</summary>
        public const int MentorCeilingBelow = 2;

        /// <summary>A mentor stands within this many units of the learner.</summary>
        public const float MentorRange = 6f;

        /// <summary>A mentor practised the same work within this many game hours.</summary>
        public const int MentorRecentHours = 1;

        // --- What a level does (K9.3, Q20) ---

        /// <summary>A fighter's hit chance rises by this a level: 70% unskilled, 90% at Master 10 (the combat rule caps it at 95%).</summary>
        public const float HitChancePerLevel = 0.02f;

        /// <summary>"A little damage": a blow's harm rises by this share a level, a fifth more at 10.</summary>
        public const float DamagePerLevel = 0.02f;

        /// <summary>
        /// A harvest's pace rises by this a level from <see cref="HarvestBase"/>, multiplied with a pickaxe's: a Novice 2 works
        /// at the pace before skills, an unskilled hand 8% slower, a Master 10 a third faster. Centred, unlike the other
        /// factors, because the harvest is the economy's inflow (Epic 10 Part 3, D51): the outpost's starting hands together bring in what
        /// they did before skills, and a talent's own trade pays it better.
        /// </summary>
        public const float HarvestPerLevel = 0.04f;

        /// <summary>An unskilled hand's harvest pace.</summary>
        public const float HarvestBase = 0.92f;

        /// <summary>A make goes this much faster a level.</summary>
        public const float MakePerLevel = 0.05f;

        /// <summary>A rescuer carries at half pace and this much more a level: four fifths at 10.</summary>
        public const float CarryPerLevel = 0.03f;

        /// <summary>A medicine given heals this much more a level.</summary>
        public const float HealPerLevel = 0.05f;

        public static float HitBonus(int level) => (float)(HitChancePerLevel * Clamp(level));
        public static float DamageFactor(int level) => (float)(1f + (float)(DamagePerLevel * Clamp(level)));
        public static float HarvestFactor(int level) => (float)(HarvestBase + (float)(HarvestPerLevel * Clamp(level)));
        public static float MakeFactor(int level) => (float)(1f + (float)(MakePerLevel * Clamp(level)));
        public static float CarryBonus(int level) => (float)(CarryPerLevel * Clamp(level));
        public static float HealFactor(int level) => (float)(1f + (float)(HealPerLevel * Clamp(level)));

        /// <summary>The grade of what an artisan makes (Q20: "an artisan's grade is now its skill's band"): Standard for a Novice, Fine for an Apprentice, Masterwork from Journeyman.</summary>
        public static byte GradeOf(int level) => BandOf(level) switch
        {
            SkillBand.Novice => Grades.Standard,
            SkillBand.Apprentice => Grades.Fine,
            _ => Grades.Masterwork
        };

        // --- The draw at spawn (K9.1b, Q19) ---

        /// <summary>A skill that isn't a talent starts at 0 (half of them), 1 or 2: a Novice.</summary>
        public const float StartsAtZero = 0.5f, StartsAtOneOrLess = 0.8f;

        /// <summary>One talent, or with this chance two.</summary>
        public const float SecondTalent = 0.5f;

        /// <summary>A talent starts an Apprentice: at 3, 4 or 5.</summary>
        public const int TalentLevel = 3, TalentSpread = 3;

        /// <summary>A personality at its extreme makes its skill this many times as likely a talent as one nothing leans (1 + this).</summary>
        public const float LeanWeight = 2f;

        private static int Clamp(int level) => level < 0 ? 0 : level > MaxLevel ? MaxLevel : level;
    }
}
