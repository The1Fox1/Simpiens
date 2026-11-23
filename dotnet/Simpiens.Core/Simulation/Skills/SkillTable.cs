using System.Collections.Generic;
using Simpiens.Simulation.Items;

namespace Simpiens.Simulation.Skills
{
    /// <summary>
    /// A skill, by id (Epic 10 Part 3, K9.1, Q17). The id is the skill's place in a crawler's <see cref="SkillSet"/> and in
    /// its save: never reuse one, and keep it under <see cref="SkillTable.Capacity"/>. What a skill is (its words, the work
    /// that practises it, what leans its draw) is its row in <see cref="SkillTable"/>; code names the work it does
    /// (<see cref="SkillWork"/>), not a skill.
    /// </summary>
    public enum Skill : byte
    {
        None = 0,
        Melee = 1,
        Ranged = 2,
        Mining = 3,
        Foraging = 4,
        Smithing = 5,
        Alchemy = 6,
        Medicine = 7,
        Leadership = 8
    }

    /// <summary>The kind of work that practises a skill and that the skill makes better (K9.1): each row of the table names one.</summary>
    public enum SkillUse : byte
    {
        None = 0,
        /// <summary>Fighting in reach: a blow landed is practice; the hit chance and a little damage.</summary>
        MeleeFight = 1,
        /// <summary>Fighting from range: a shot is practice; the hit chance and a little damage.</summary>
        RangedFight = 2,
        /// <summary>Harvesting salvage of the row's <see cref="SkillDefinition.Harvests"/>: a unit is practice; the harvest's speed.</summary>
        Harvest = 3,
        /// <summary>Making at the row's <see cref="SkillDefinition.Station"/>: a make is practice; the make's grade and speed.</summary>
        Make = 4,
        /// <summary>Tending the fallen: a rescue is practice; carrying faster, and a medicine given heals more.</summary>
        Tend = 5,
        /// <summary>Leading a party (Epic 11, S5.1, D19): each mate brought home is practice; the party holds longer under its retreat rule.</summary>
        Lead = 6
    }

    /// <summary>Which way a personality leans a skill's draw as a starting talent (Q19: "the bold lean toward melee").</summary>
    public enum SkillLean : byte
    {
        None = 0,
        Bold = 1,
        Cautious = 2,
        Diligent = 3,
        Sociable = 4,
        Loyal = 5,
        Greedy = 6
    }

    /// <summary>The named bands of a skill's levels (Q18): Novice 0–2, Apprentice 3–5, Journeyman 6–8, Master 9–10.</summary>
    public enum SkillBand : byte
    {
        Novice = 0,
        Apprentice = 1,
        Journeyman = 2,
        Master = 3
    }

    /// <summary>Why the mastery budget holds a skill (Epic 11, S2.1, D7; <see cref="SkillRules.BudgetCeiling(in SkillSet, Skill, out BudgetHold)"/>).</summary>
    public enum BudgetHold : byte
    {
        /// <summary>Not held: the skill may climb to the last level.</summary>
        None = 0,
        /// <summary>Two other skills stand past Apprentice: held at the top of Apprentice.</summary>
        PastApprentice = 1,
        /// <summary>Another skill stands at Master: held at the top of Journeyman.</summary>
        Master = 2
    }

    /// <summary>One skill: a row of <see cref="SkillTable"/>.</summary>
    public sealed class SkillDefinition
    {
        public SkillDefinition(Skill id, string name, string displayName, SkillUse use)
        {
            Id = id;
            Name = name;
            DisplayName = displayName;
            Use = use;
        }

        public Skill Id { get; }

        /// <summary>The code name, for logs.</summary>
        public string Name { get; }

        /// <summary>The player's word: "Melee", "Smithing".</summary>
        public string DisplayName { get; }

        /// <summary>The work that practises it and that it makes better.</summary>
        public SkillUse Use { get; }

        /// <summary>For <see cref="SkillUse.Harvest"/>: the group of salvage it harvests (minerals, botanicals).</summary>
        public ItemCategory Harvests { get; init; }

        /// <summary>For <see cref="SkillUse.Make"/>: the station it works.</summary>
        public StationKind Station { get; init; }

        /// <summary>Which personality leans toward it as a starting talent.</summary>
        public SkillLean Lean { get; init; }

        /// <summary>One who practises it, in the chronicle's words: "fighter", "miner".</summary>
        public string Practitioner { get; init; } = "hand";

        /// <summary>Its place in lists; ties by id.</summary>
        public int Order { get; init; }

        /// <summary>How the chronicle speaks of a crawler in it (Epic 11, S6.4, D30); null speaks plainly.</summary>
        public SkillVoice Voice { get; init; }

        public override string ToString() => Name;
    }

    /// <summary>
    /// A skill's voice (Epic 11, S6.4, D30; Q12: "the chronicle speaks in a crawler's strongest skill"): a few authored words
    /// for the events that are a crawler's own. {0} is the crawler's name. Latin-1, as the fonts are.
    /// </summary>
    public sealed class SkillVoice
    {
        public SkillVoice(string rising, string named, string blooded, string firstKill)
        {
            Rising = rising;
            Named = named;
            Blooded = blooded;
            FirstKill = firstKill;
        }

        /// <summary>A band reached in this skill: "{0}'s pick rings truer", before ": a journeyman miner now."</summary>
        public string Rising { get; }

        /// <summary>An epithet earned by one whose best skill this is: "{0}'s pick has won a name", before ": Bram Beetlebane, ...".</summary>
        public string Named { get; }

        /// <summary>First blood for one whose best skill this is: "{0}'s blade drew first blood", before ": a stone beetle, in ...".</summary>
        public string Blooded { get; }

        /// <summary>What the crawler says in its first blood's scene.</summary>
        public string FirstKill { get; }
    }

    /// <summary>
    /// The skills, as a table (Epic 10 Part 3, K9.1, Q17: "appendable, like the slots"). Static and immutable, as the slots'
    /// and the items' tables are. A new skill is a <see cref="Skill"/> value and a row: the draw at spawn, practice, what it
    /// does for its kind of work, the save and the client's lists follow.
    /// </summary>
    public static class SkillTable
    {
        /// <summary>The most skills there can be: the room a crawler's <see cref="SkillSet"/> and its save keep. An id at or above it changes the save format.</summary>
        public const int Capacity = 16;

        private sealed class Tables
        {
            public readonly SkillDefinition[] ById = new SkillDefinition[Capacity];
            public readonly SkillDefinition[] All;
            public readonly SkillDefinition[] InDisplayOrder;

            public Tables(IEnumerable<SkillDefinition> definitions)
            {
                var all = new List<SkillDefinition>(Capacity);
                foreach (var definition in definitions)
                {
                    int id = (byte)definition.Id;
                    if (id == 0 || id >= Capacity) throw new System.ArgumentException($"Skill {definition.Name} has id {id}; skills have ids from 1 to {Capacity - 1}.");
                    if (definition.Use == SkillUse.None) throw new System.ArgumentException($"Skill {definition.Name} is practised by no kind of work.");
                    if (ById[id] != null) throw new System.ArgumentException($"Two skills have id {id}.");
                    ById[id] = definition;
                }
                for (int id = 0; id < Capacity; id++)
                {
                    if (ById[id] != null) all.Add(ById[id]);
                }
                All = all.ToArray();
                var shown = new List<SkillDefinition>(all);
                shown.Sort((a, b) => a.Order != b.Order ? a.Order.CompareTo(b.Order) : ((byte)a.Id).CompareTo((byte)b.Id));
                InDisplayOrder = shown.ToArray();
            }
        }

        private static volatile Tables _tables = new Tables(Definitions());

        /// <summary>The game's skills (Q17): two for a fight, two for the harvest, two for the stations, one for the fallen, and leading a party (Epic 11, S5.1).</summary>
        private static SkillDefinition[] Definitions() => new[]
        {
            new SkillDefinition(Skill.Melee, "Melee", "Melee", SkillUse.MeleeFight) { Lean = SkillLean.Bold, Practitioner = "fighter", Order = 0,
                Voice = new SkillVoice("{0}'s blade is surer", "{0}'s blade has won a name", "{0}'s blade drew first blood", "First blood. It won't be the last.") },
            new SkillDefinition(Skill.Ranged, "Ranged", "Ranged", SkillUse.RangedFight) { Lean = SkillLean.Cautious, Practitioner = "marksman", Order = 1,
                Voice = new SkillVoice("{0}'s aim is truer", "{0}'s aim has won a name", "{0}'s first shot to kill flew true", "One shot. Down it went.") },
            new SkillDefinition(Skill.Mining, "Mining", "Mining", SkillUse.Harvest) { Harvests = ItemCategory.Mineral, Lean = SkillLean.Diligent, Practitioner = "miner", Order = 2,
                Voice = new SkillVoice("{0}'s pick rings truer", "{0}'s pick has won a name", "{0} struck as at the rock, and drew first blood", "Tougher than rock. Not by much.") },
            new SkillDefinition(Skill.Foraging, "Foraging", "Foraging", SkillUse.Harvest) { Harvests = ItemCategory.Botanical, Practitioner = "forager", Order = 3,
                Voice = new SkillVoice("{0}'s hands know the caves better", "{0}'s wandering has won a name", "{0}, used to gentler work, drew first blood", "I only came for herbs.") },
            new SkillDefinition(Skill.Smithing, "Smithing", "Smithing", SkillUse.Make) { Station = StationKind.Forge, Lean = SkillLean.Diligent, Practitioner = "smith", Order = 4,
                Voice = new SkillVoice("{0}'s hammer rings truer", "{0}'s hammer has won a name", "{0} tried a blade from the forge, and drew first blood", "So that's what they're for.") },
            new SkillDefinition(Skill.Alchemy, "Alchemy", "Alchemy", SkillUse.Make) { Station = StationKind.Still, Practitioner = "alchemist", Order = 5,
                Voice = new SkillVoice("{0}'s still runs clearer", "{0}'s brews have won a name", "{0}, steadier with a flask than a blade, drew first blood", "Steady. Steady. There.") },
            new SkillDefinition(Skill.Medicine, "Medicine", "Medicine", SkillUse.Tend) { Lean = SkillLean.Sociable, Practitioner = "medic", Order = 6,
                Voice = new SkillVoice("{0}'s hands are steadier", "{0}'s mending has won a name", "{0}, who mends wounds, made one: first blood", "I'd rather mend than make wounds.") },
            new SkillDefinition(Skill.Leadership, "Leadership", "Leadership", SkillUse.Lead) { Lean = SkillLean.Loyal, Practitioner = "captain", Order = 7,
                Voice = new SkillVoice("{0}'s voice carries further", "{0}'s command has won a name", "{0} led from the front, and drew first blood", "Stay behind me. All of you.") }
        };

        /// <summary>
        /// Tests only: the table with <paramref name="extra"/> added, until the returned scope is disposed (K9.6, the proof that
        /// a skill needs no code beyond its row). The table is swapped whole, between ticks; tests that use it run serially.
        /// </summary>
        internal static System.IDisposable ExtendForTest(params SkillDefinition[] extra)
        {
            var previous = _tables;
            var definitions = new List<SkillDefinition>(previous.All);
            definitions.AddRange(extra);
            _tables = new Tables(definitions);
            return new Restore(previous);
        }

        private sealed class Restore : System.IDisposable
        {
            private readonly Tables _previous;
            public Restore(Tables previous) => _previous = previous;
            public void Dispose() => _tables = _previous;
        }

        /// <summary>Every skill, in id order: the order they are drawn and hashed in, so it never varies.</summary>
        public static IReadOnlyList<SkillDefinition> All => _tables.All;

        /// <summary>Every skill in the order lists show them (<see cref="SkillDefinition.Order"/>).</summary>
        public static IReadOnlyList<SkillDefinition> InDisplayOrder => _tables.InDisplayOrder;

        public static bool IsDefined(Skill id) => (byte)id < Capacity && _tables.ById[(byte)id] != null;

        /// <summary>The skill's row, or null for none and undefined ids.</summary>
        public static SkillDefinition Get(Skill id) => (byte)id < Capacity ? _tables.ById[(byte)id] : null;

        /// <summary>A code name for logs: the row's, or the raw id.</summary>
        public static string NameOf(Skill id) => Get(id)?.Name ?? $"Skill{(byte)id}";

        /// <summary>
        /// The skill that answers for <paramref name="work"/> (Epic 10 Part 3, K9.8a), the first matching row in id order;
        /// none if no row does:
        /// <list type="bullet">
        /// <item>a fight: the row of that use, in reach or from range;</item>
        /// <item>a harvest: the harvest row whose group the item is in (none for food and anything no row harvests);</item>
        /// <item>making: the row that works the station;</item>
        /// <item>tending the fallen: the row of that use.</item>
        /// </list>
        /// The skill practice raises and the dossier names. How good a crawler is at the work is
        /// <see cref="CrawlerSkills.Level(in SkillSet, Simpiens.Cognition.Inventory.IAgentInventory, in SkillWork)"/>.
        /// </summary>
        public static Skill SkillFor(in SkillWork work)
        {
            var all = _tables.All;
            for (int i = 0; i < all.Length; i++)
            {
                var row = all[i];
                if (row.Use != work.Use) continue;
                switch (work.Use)
                {
                    case SkillUse.Harvest:
                        if (row.Harvests != 0 && ItemCatalog.Is(work.Item, row.Harvests)) return row.Id;
                        break;
                    case SkillUse.Make:
                        if (work.Station != StationKind.None && row.Station == work.Station) return row.Id;
                        break;
                    default: // no row is of no use: the table refuses one
                        return row.Id;
                }
            }
            return Skill.None;
        }
    }
}
