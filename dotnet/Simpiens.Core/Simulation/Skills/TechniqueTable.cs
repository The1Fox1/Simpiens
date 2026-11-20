using System.Collections.Generic;
using Simpiens.Cognition.Contracts;
using static System.FormattableString;

namespace Simpiens.Simulation.Skills
{
    /// <summary>
    /// A technique, by id (Epic 11, S4, A2, D13): a choice a crawler makes at a band reached. The id is what a crawler's traits
    /// record holds in the band's slot and its save: never reuse one. What a technique is (its skill and band, its lean, its
    /// effects and words) is its row in <see cref="TechniqueTable"/>; production code names none outside the table.
    /// </summary>
    public enum Technique : byte
    {
        None = 0,
        ShieldWall = 1,
        RecklessFury = 2,
        QuickBlade = 3,
        HeavyBlows = 4,
        Unyielding = 5,
        LastStand = 6,
        SteadyAim = 7,
        QuickDraw = 8,
        LongShot = 9,
        PointBlank = 10,
        Deadeye = 11,
        RapidFire = 12,
        SeamStudent = 13,
        HardDriver = 14,
        PatientPick = 15,
        RichVeins = 16,
        OldHandsLessons = 17,
        MotherLode = 18,
        GentleHands = 19,
        QuickHands = 20,
        HerbLore = 21,
        WideRanger = 22,
        GreenTeacher = 23,
        BountifulHarvest = 24,
        FineTemper = 25,
        FastForge = 26,
        StudiedHammer = 27,
        HotIron = 28,
        ForgeLessons = 29,
        Masterstroke = 30,
        CarefulMeasure = 31,
        QuickBrew = 32,
        PatientStill = 33,
        BigBatches = 34,
        StillLessons = 35,
        AlchemistsHaste = 36,
        SteadyHands = 37,
        StrongBack = 38,
        BedsideStudy = 39,
        Triage = 40,
        SurgeonsLessons = 41,
        MiracleWorker = 42
    }

    /// <summary>
    /// What a technique changes (Epic 11, S4, A4, D14): a number in its skill's work, or a rule flag. Each is read in one
    /// place, through <see cref="CrawlerTechniques"/>, and held within its bounds (<see cref="TechniqueRules"/>, A5). Ids are
    /// part of the table, not the save: a stat may be added, never renumbered while a row names it.
    /// </summary>
    public enum TechniqueStat : byte
    {
        None = 0,
        /// <summary>A blow's harm, in percent (a fight).</summary>
        Attack = 1,
        /// <summary>Defense against blows, flat (a fight; never below none).</summary>
        Defense = 2,
        /// <summary>The hit chance, in hundredths (a fight).</summary>
        Hit = 3,
        /// <summary>How fast the work goes, in percent: a swing's interval, a harvest, a make or a carry, whichever the skill's work is.</summary>
        Pace = 4,
        /// <summary>A shot's range, in percent (a fight from range).</summary>
        Range = 5,
        /// <summary>A blow's harm below half health, in percent (a fight).</summary>
        LastStand = 6,
        /// <summary>A rule flag: struck, the crawler fights back if armed, bold or not (a fight in reach).</summary>
        Unyielding = 7,
        /// <summary>Practice in the skill, in percent, beside its passion.</summary>
        Practice = 8,
        /// <summary>Levels counted toward the grade of what it makes (making).</summary>
        Grade = 9,
        /// <summary>A medicine given heals this much more, in percent (tending the fallen).</summary>
        Heal = 10,
        /// <summary>Points added to the share of an act a mentor's lesson teaches (half, by D9).</summary>
        Teach = 11,
        /// <summary>A lair's harm, as the crawler judges whether to brave it, in percent more (Epic 11, S6.3: Shaken).</summary>
        Dread = 12
    }

    /// <summary>One effect of a technique: a stat and how much.</summary>
    public readonly struct TechniqueEffect
    {
        public TechniqueEffect(TechniqueStat stat, int value)
        {
            Stat = stat;
            Value = value;
        }

        public TechniqueStat Stat { get; }
        public int Value { get; }

        /// <summary>True for an effect the crawler gains by (a flag set, a number up); false for its cost.</summary>
        public bool IsUpside => Value > 0;

        public override string ToString() => TechniqueRules.Describe(this);
    }

    /// <summary>One technique: a row of <see cref="TechniqueTable"/>.</summary>
    public sealed class TechniqueDefinition
    {
        public TechniqueDefinition(Technique id, string displayName, Skill skill, SkillBand band, SkillLean lean, string vow, params TechniqueEffect[] effects)
        {
            Id = id;
            DisplayName = displayName;
            Skill = skill;
            Band = band;
            Lean = lean;
            Vow = vow;
            Effects = effects ?? System.Array.Empty<TechniqueEffect>();
        }

        public Technique Id { get; }

        /// <summary>The code name, for logs.</summary>
        public string Name => Id.ToString();

        /// <summary>The player's words: "Shield Wall".</summary>
        public string DisplayName { get; }

        /// <summary>The skill whose band offers it, and whose work its effects change.</summary>
        public Skill Skill { get; }

        /// <summary>The band that offers it: Apprentice, Journeyman or Master.</summary>
        public SkillBand Band { get; }

        /// <summary>The personality that leans the pick toward it (D15), as a talent's lean.</summary>
        public SkillLean Lean { get; }

        /// <summary>What the crawler says in the choice scene (D17): "Let them come. I'll stand."</summary>
        public string Vow { get; }

        /// <summary>Its effects, one to three: at least one upside and one cost.</summary>
        public IReadOnlyList<TechniqueEffect> Effects { get; }

        /// <summary>Its effects in the player's words: "+3 defense, 15% less damage".</summary>
        public string Words
        {
            get
            {
                var text = new System.Text.StringBuilder();
                for (int i = 0; i < Effects.Count; i++)
                {
                    if (i > 0) text.Append(", ");
                    text.Append(TechniqueRules.Describe(Effects[i]));
                }
                return text.ToString();
            }
        }

        /// <summary>This row's value for <paramref name="stat"/>; 0 when it has none.</summary>
        public int ValueOf(TechniqueStat stat)
        {
            for (int i = 0; i < Effects.Count; i++) if (Effects[i].Stat == stat) return Effects[i].Value;
            return 0;
        }

        public override string ToString() => Name;
    }

    /// <summary>
    /// Techniques, as a table (Epic 11, S4, A2, Q4, D13): two at each band above Novice of each skill that has them, each with
    /// an upside and a cost. Static and immutable, as the skills' table is. A new technique is a <see cref="Technique"/> value
    /// and a row; the pick, the effects, the save and the client follow.
    /// </summary>
    public static class TechniqueTable
    {
        private static readonly TechniqueDefinition[] ById = new TechniqueDefinition[256];
        private static readonly TechniqueDefinition[] AllRows;
        private static readonly TechniqueDefinition[][] ByBand = new TechniqueDefinition[SkillTable.Capacity * 4][];

        static TechniqueTable()
        {
            var rows = Definitions();
            var all = new List<TechniqueDefinition>(rows.Length);
            var bands = new List<TechniqueDefinition>[SkillTable.Capacity * 4];
            foreach (var row in rows)
            {
                int id = (byte)row.Id;
                if (id == 0) throw new System.ArgumentException($"Technique {row.Name} has id 0.");
                if (ById[id] != null) throw new System.ArgumentException($"Two techniques have id {id}.");
                if (row.Band == SkillBand.Novice) throw new System.ArgumentException($"Technique {row.Name} is offered at Novice; techniques start at Apprentice.");
                ById[id] = row;
                all.Add(row);
                int at = BandIndex(row.Skill, row.Band);
                if (at < 0) throw new System.ArgumentException($"Technique {row.Name} names no skill.");
                (bands[at] ??= new List<TechniqueDefinition>(2)).Add(row);
            }
            AllRows = all.ToArray();
            for (int i = 0; i < bands.Length; i++) ByBand[i] = bands[i]?.ToArray() ?? System.Array.Empty<TechniqueDefinition>();
        }

        private static TechniqueEffect E(TechniqueStat stat, int value) => new TechniqueEffect(stat, value);

        private const TechniqueStat Attack = TechniqueStat.Attack, Defense = TechniqueStat.Defense, Hit = TechniqueStat.Hit, Pace = TechniqueStat.Pace,
            Range = TechniqueStat.Range, LastStandStat = TechniqueStat.LastStand, Flag = TechniqueStat.Unyielding, Practice = TechniqueStat.Practice,
            Grade = TechniqueStat.Grade, Heal = TechniqueStat.Heal, Teach = TechniqueStat.Teach;

        /// <summary>
        /// The game's techniques (D13): in a fight, a stance, then a rhythm, then a last resort; at work, producing against
        /// learning at Apprentice and Journeyman, teaching against producing at Master; smithing's grades; medicine's healing
        /// against carrying. First guesses, to tune.
        /// </summary>
        private static TechniqueDefinition[] Definitions() => new[]
        {
            // Melee
            new TechniqueDefinition(Technique.ShieldWall, "Shield Wall", Skill.Melee, SkillBand.Apprentice, SkillLean.Cautious, "Let them come. I'll stand.", E(Defense, 3), E(Attack, -15)),
            new TechniqueDefinition(Technique.RecklessFury, "Reckless Fury", Skill.Melee, SkillBand.Apprentice, SkillLean.Bold, "Hit first, and hit hardest.", E(Attack, 25), E(Defense, -2)),
            new TechniqueDefinition(Technique.QuickBlade, "Quick Blade", Skill.Melee, SkillBand.Journeyman, SkillLean.Diligent, "Two cuts for every one of theirs.", E(Pace, 20), E(Attack, -10)),
            new TechniqueDefinition(Technique.HeavyBlows, "Heavy Blows", Skill.Melee, SkillBand.Journeyman, SkillLean.Bold, "One blow, if it's the right one.", E(Attack, 20), E(Pace, -15)),
            new TechniqueDefinition(Technique.Unyielding, "Unyielding", Skill.Melee, SkillBand.Master, SkillLean.Loyal, "I don't run. Not any more.", E(Flag, 1), E(Defense, 2), E(Pace, -10)),
            new TechniqueDefinition(Technique.LastStand, "Last Stand", Skill.Melee, SkillBand.Master, SkillLean.Bold, "Hurt me and see what happens.", E(LastStandStat, 40), E(Defense, -2)),
            // Ranged
            new TechniqueDefinition(Technique.SteadyAim, "Steady Aim", Skill.Ranged, SkillBand.Apprentice, SkillLean.Cautious, "Breathe out. Then loose.", E(Hit, 8), E(Pace, -15)),
            new TechniqueDefinition(Technique.QuickDraw, "Quick Draw", Skill.Ranged, SkillBand.Apprentice, SkillLean.Bold, "Something's bound to hit.", E(Pace, 20), E(Hit, -5)),
            new TechniqueDefinition(Technique.LongShot, "Long Shot", Skill.Ranged, SkillBand.Journeyman, SkillLean.Diligent, "They'll never get near me.", E(Range, 25), E(Attack, -10)),
            new TechniqueDefinition(Technique.PointBlank, "Point Blank", Skill.Ranged, SkillBand.Journeyman, SkillLean.Bold, "Close enough to see their eyes.", E(Attack, 20), E(Range, -20)),
            new TechniqueDefinition(Technique.Deadeye, "Deadeye", Skill.Ranged, SkillBand.Master, SkillLean.Cautious, "One shot. Make it count.", E(Hit, 10), E(Attack, 10), E(Pace, -20)),
            new TechniqueDefinition(Technique.RapidFire, "Rapid Fire", Skill.Ranged, SkillBand.Master, SkillLean.Bold, "Keep their heads down.", E(Pace, 30), E(Hit, -8)),
            // Mining
            new TechniqueDefinition(Technique.SeamStudent, "Seam Student", Skill.Mining, SkillBand.Apprentice, SkillLean.Diligent, "Every seam has something to teach.", E(Practice, 25), E(Pace, -10)),
            new TechniqueDefinition(Technique.HardDriver, "Hard Driver", Skill.Mining, SkillBand.Apprentice, SkillLean.Greedy, "Ore in the cart is ore in the purse.", E(Pace, 15), E(Practice, -20)),
            new TechniqueDefinition(Technique.PatientPick, "Patient Pick", Skill.Mining, SkillBand.Journeyman, SkillLean.Cautious, "Slow, and learn the rock.", E(Practice, 30), E(Pace, -5)),
            new TechniqueDefinition(Technique.RichVeins, "Rich Veins", Skill.Mining, SkillBand.Journeyman, SkillLean.Greedy, "Follow the vein, and swing.", E(Pace, 20), E(Practice, -30)),
            new TechniqueDefinition(Technique.OldHandsLessons, "Old Hand's Lessons", Skill.Mining, SkillBand.Master, SkillLean.Sociable, "Watch my hands, not the pick.", E(Teach, 25), E(Pace, -10)),
            new TechniqueDefinition(Technique.MotherLode, "Mother Lode", Skill.Mining, SkillBand.Master, SkillLean.Greedy, "The deep seam is mine.", E(Pace, 30), E(Practice, -40)),
            // Foraging
            new TechniqueDefinition(Technique.GentleHands, "Gentle Hands", Skill.Foraging, SkillBand.Apprentice, SkillLean.Cautious, "Pick it like it matters.", E(Practice, 25), E(Pace, -10)),
            new TechniqueDefinition(Technique.QuickHands, "Quick Hands", Skill.Foraging, SkillBand.Apprentice, SkillLean.Bold, "Fill the basket. Sort it later.", E(Pace, 15), E(Practice, -20)),
            new TechniqueDefinition(Technique.HerbLore, "Herb Lore", Skill.Foraging, SkillBand.Journeyman, SkillLean.Diligent, "Every leaf has a name.", E(Practice, 30), E(Pace, -5)),
            new TechniqueDefinition(Technique.WideRanger, "Wide Ranger", Skill.Foraging, SkillBand.Journeyman, SkillLean.Greedy, "There's always another patch.", E(Pace, 20), E(Practice, -30)),
            new TechniqueDefinition(Technique.GreenTeacher, "Green Teacher", Skill.Foraging, SkillBand.Master, SkillLean.Sociable, "Come, I'll show you where they grow.", E(Teach, 25), E(Pace, -10)),
            new TechniqueDefinition(Technique.BountifulHarvest, "Bountiful Harvest", Skill.Foraging, SkillBand.Master, SkillLean.Greedy, "The caves feed whoever's quickest.", E(Pace, 30), E(Practice, -40)),
            // Smithing
            new TechniqueDefinition(Technique.FineTemper, "Fine Temper", Skill.Smithing, SkillBand.Apprentice, SkillLean.Diligent, "It's done when it's right.", E(Grade, 2), E(Pace, -20)),
            new TechniqueDefinition(Technique.FastForge, "Fast Forge", Skill.Smithing, SkillBand.Apprentice, SkillLean.Greedy, "Good enough, and on the rack.", E(Pace, 20), E(Grade, -2)),
            new TechniqueDefinition(Technique.StudiedHammer, "Studied Hammer", Skill.Smithing, SkillBand.Journeyman, SkillLean.Cautious, "Every blow teaches the next.", E(Practice, 30), E(Pace, -10)),
            new TechniqueDefinition(Technique.HotIron, "Hot Iron", Skill.Smithing, SkillBand.Journeyman, SkillLean.Bold, "Strike while it's hot.", E(Pace, 25), E(Practice, -30)),
            new TechniqueDefinition(Technique.ForgeLessons, "Forge Lessons", Skill.Smithing, SkillBand.Master, SkillLean.Sociable, "Hold the tongs. No, like this.", E(Teach, 25), E(Pace, -10)),
            new TechniqueDefinition(Technique.Masterstroke, "Masterstroke", Skill.Smithing, SkillBand.Master, SkillLean.Diligent, "This one they'll remember.", E(Grade, 3), E(Pace, -25)),
            // Alchemy
            new TechniqueDefinition(Technique.CarefulMeasure, "Careful Measure", Skill.Alchemy, SkillBand.Apprentice, SkillLean.Cautious, "A grain too much and it's poison.", E(Practice, 25), E(Pace, -10)),
            new TechniqueDefinition(Technique.QuickBrew, "Quick Brew", Skill.Alchemy, SkillBand.Apprentice, SkillLean.Bold, "Boil it, bottle it, sell it.", E(Pace, 15), E(Practice, -20)),
            new TechniqueDefinition(Technique.PatientStill, "Patient Still", Skill.Alchemy, SkillBand.Journeyman, SkillLean.Diligent, "The still can't be hurried.", E(Practice, 30), E(Pace, -5)),
            new TechniqueDefinition(Technique.BigBatches, "Big Batches", Skill.Alchemy, SkillBand.Journeyman, SkillLean.Greedy, "Why brew one when you can brew ten?", E(Pace, 25), E(Practice, -30)),
            new TechniqueDefinition(Technique.StillLessons, "Still Lessons", Skill.Alchemy, SkillBand.Master, SkillLean.Sociable, "Smell that? Now you know.", E(Teach, 25), E(Pace, -10)),
            new TechniqueDefinition(Technique.AlchemistsHaste, "Alchemist's Haste", Skill.Alchemy, SkillBand.Master, SkillLean.Bold, "Fire high, and stand back.", E(Pace, 30), E(Practice, -40)),
            // Medicine
            new TechniqueDefinition(Technique.SteadyHands, "Steady Hands", Skill.Medicine, SkillBand.Apprentice, SkillLean.Sociable, "Hold still. This will hurt.", E(Heal, 40), E(Pace, -15)),
            new TechniqueDefinition(Technique.StrongBack, "Strong Back", Skill.Medicine, SkillBand.Apprentice, SkillLean.Bold, "Up you get. I've got you.", E(Pace, 25), E(Heal, -25)),
            new TechniqueDefinition(Technique.BedsideStudy, "Bedside Study", Skill.Medicine, SkillBand.Journeyman, SkillLean.Diligent, "Every wound teaches me something.", E(Practice, 40), E(Heal, -10)),
            new TechniqueDefinition(Technique.Triage, "Triage", Skill.Medicine, SkillBand.Journeyman, SkillLean.Cautious, "The worst first. Always.", E(Heal, 30), E(Practice, -30)),
            new TechniqueDefinition(Technique.SurgeonsLessons, "Surgeon's Lessons", Skill.Medicine, SkillBand.Master, SkillLean.Sociable, "Press here. Harder. Good.", E(Teach, 25), E(Pace, -10)),
            new TechniqueDefinition(Technique.MiracleWorker, "Miracle Worker", Skill.Medicine, SkillBand.Master, SkillLean.Loyal, "Nobody dies on my watch.", E(Heal, 60), E(Pace, -20)),
        };

        /// <summary>Every technique, in id order.</summary>
        public static IReadOnlyList<TechniqueDefinition> All => AllRows;

        /// <summary>The technique's row, or null for none and undefined ids.</summary>
        public static TechniqueDefinition Get(Technique id) => ById[(byte)id];

        /// <summary>A code name for logs: the row's, or the raw id.</summary>
        public static string NameOf(Technique id) => Get(id)?.Name ?? $"Technique{(byte)id}";

        /// <summary>The techniques <paramref name="band"/> of <paramref name="skill"/> offers, in id order; none for Novice, a skill without techniques or no skill.</summary>
        public static IReadOnlyList<TechniqueDefinition> Options(Skill skill, SkillBand band)
        {
            int at = BandIndex(skill, band);
            return at < 0 ? System.Array.Empty<TechniqueDefinition>() : ByBand[at];
        }

        /// <summary>The band's other technique (two at each band): what a sponsor offers in place of <paramref name="held"/>; none when there is no other.</summary>
        public static Technique OtherThan(Technique held)
        {
            var row = Get(held);
            if (row == null) return Technique.None;
            var options = Options(row.Skill, row.Band);
            for (int i = 0; i < options.Count; i++) if (options[i].Id != held) return options[i].Id;
            return Technique.None;
        }

        private static int BandIndex(Skill skill, SkillBand band)
        {
            if (skill == Skill.None || (byte)skill >= SkillTable.Capacity || band == SkillBand.Novice || band > SkillBand.Master) return -1;
            return (byte)skill * 4 + (byte)band;
        }
    }

    /// <summary>
    /// The techniques' rules (Epic 11, S4, A4, A5, D14): each stat's bounds, the work it applies to, and its words.
    /// </summary>
    public static class TechniqueRules
    {
        /// <summary>The least and the most a stat may sum to over every technique (and capstone) a crawler holds: no combination runs away (A5).</summary>
        public static (int Min, int Max) Bounds(TechniqueStat stat) => stat switch
        {
            TechniqueStat.Attack => (-50, 100),
            TechniqueStat.Defense => (-5, 10),
            TechniqueStat.Hit => (-20, 20),
            TechniqueStat.Pace => (-50, 100),
            TechniqueStat.Range => (-50, 50),
            TechniqueStat.LastStand => (0, 100),
            TechniqueStat.Unyielding => (0, 1),
            TechniqueStat.Practice => (-50, 100),
            TechniqueStat.Grade => (-3, 3),
            TechniqueStat.Heal => (-50, 100),
            TechniqueStat.Teach => (0, 50),
            TechniqueStat.Dread => (0, 100),
            _ => (0, 0)
        };

        // --- The pick by nature (Epic 11, S4.2, Q3, D15) ---

        /// <summary>
        /// The technique <paramref name="entityId"/> picks at <paramref name="band"/> of <paramref name="skill"/>, by its
        /// nature (D15): one of the band's rows, each weighted 1 + 2 × the row's lean in <paramref name="personality"/> (as a
        /// talent's), on its own salt (<see cref="SimRandomSalt.Techniques"/>) keyed by the skill and the band, so the same
        /// crawler picks the same whenever it gets there. None where the band offers nothing.
        /// </summary>
        public static Technique Pick(ulong seed, EntityId entityId, in AgentPersonality personality, Skill skill, SkillBand band)
        {
            var options = TechniqueTable.Options(skill, band);
            if (options.Count == 0) return Technique.None;
            float total = 0f;
            System.Span<float> weights = stackalloc float[options.Count];
            for (int i = 0; i < options.Count; i++)
            {
                weights[i] = (float)(1f + (float)(SkillRules.LeanWeight * System.Math.Max(0f, SkillSet.Lean(options[i].Lean, personality))));
                total += weights[i];
            }
            var rng = SimRandom.For(seed, entityId, (byte)skill * 4 + (byte)band, SimRandomSalt.Techniques);
            float pick = (float)(rng.NextFloat() * total);
            for (int i = 0; i < options.Count; i++)
            {
                if (pick < weights[i]) return options[i].Id;
                pick -= weights[i];
            }
            return options[options.Count - 1].Id;
        }

        /// <summary>
        /// Fills every band <paramref name="skill"/> has reached in <paramref name="skills"/> whose slot is empty, by
        /// <see cref="Pick"/>; a slot already held (picked, or sponsored) is never picked over. Returns the last technique
        /// picked, the highest band's; none when nothing was empty.
        /// </summary>
        public static Technique FillReached(ulong seed, EntityId entityId, in AgentPersonality personality, in SkillSet skills, Skill skill, ref CrawlerTraits traits)
        {
            var reached = skills.Band(skill);
            var picked = Technique.None;
            for (var band = SkillBand.Apprentice; band <= reached && band <= SkillBand.Master; band++)
            {
                if (traits.TechniqueIn(skill, band) != Technique.None) continue;
                var technique = Pick(seed, entityId, personality, skill, band);
                if (technique == Technique.None) continue;
                traits.SetTechnique(skill, band, technique);
                picked = technique;
            }
            return picked;
        }

        /// <summary>
        /// The techniques a crawler holds at spawn (D15): every band its starting skills have reached, its talents an
        /// Apprentice's, picked by its nature. After the passions' draw, on its own salt, so no earlier draw moves.
        /// </summary>
        public static CrawlerTraits PickAtSpawn(ulong seed, EntityId entityId, in AgentPersonality personality, in SkillSet skills, CrawlerTraits traits)
        {
            var all = SkillTable.All;
            for (int i = 0; i < all.Count; i++) FillReached(seed, entityId, personality, skills, all[i].Id, ref traits);
            return traits;
        }

        /// <summary><paramref name="value"/> held within <paramref name="stat"/>'s bounds.</summary>
        public static int Clamp(TechniqueStat stat, int value)
        {
            var (min, max) = Bounds(stat);
            return value < min ? min : value > max ? max : value;
        }

        /// <summary>Whether <paramref name="stat"/> changes anything in <paramref name="use"/>'s work: a fight's stats in a fight, grade in making, healing in tending; pace, practice and teaching in any.</summary>
        public static bool AppliesTo(TechniqueStat stat, SkillUse use) => stat switch
        {
            TechniqueStat.Attack or TechniqueStat.Defense or TechniqueStat.Hit or TechniqueStat.LastStand or TechniqueStat.Dread => use == SkillUse.MeleeFight || use == SkillUse.RangedFight,
            TechniqueStat.Range => use == SkillUse.RangedFight,
            TechniqueStat.Unyielding => use == SkillUse.MeleeFight,
            TechniqueStat.Grade => use == SkillUse.Make,
            TechniqueStat.Heal => use == SkillUse.Tend,
            TechniqueStat.Pace or TechniqueStat.Practice or TechniqueStat.Teach => use != SkillUse.None,
            _ => false
        };

        /// <summary>An effect in the player's words: "+3 defense", "15% less damage", "20% faster". Latin-1, as the client's fonts are.</summary>
        public static string Describe(in TechniqueEffect effect)
        {
            int v = effect.Value, a = v < 0 ? -v : v;
            string sign = v < 0 ? "-" : "+"; // Latin-1 only: the baked fonts hold no other minus
            return effect.Stat switch
            {
                TechniqueStat.Attack => Invariant($"{a}% {(v < 0 ? "less" : "more")} damage"),
                TechniqueStat.Defense => Invariant($"{sign}{a} defense"),
                TechniqueStat.Hit => Invariant($"{sign}{a} to hit"),
                TechniqueStat.Pace => Invariant($"{a}% {(v < 0 ? "slower" : "faster")}"),
                TechniqueStat.Range => Invariant($"{a}% {(v < 0 ? "less" : "more")} range"),
                TechniqueStat.LastStand => Invariant($"{a}% more damage below half health"),
                TechniqueStat.Unyielding => "never flees a blow",
                TechniqueStat.Practice => Invariant($"learns {a}% {(v < 0 ? "slower" : "faster")}"),
                TechniqueStat.Grade => Invariant($"makes as if {a} level{(a == 1 ? "" : "s")} {(v < 0 ? "worse" : "better")}"),
                TechniqueStat.Heal => Invariant($"medicine heals {a}% {(v < 0 ? "less" : "more")}"),
                TechniqueStat.Teach => Invariant($"its lessons teach {SkillRules.MentorPercent + a}% of an act, not {SkillRules.MentorPercent}%"),
                TechniqueStat.Dread => Invariant($"judges a lair {a}% more dangerous"),
                _ => string.Empty
            };
        }
    }
}
