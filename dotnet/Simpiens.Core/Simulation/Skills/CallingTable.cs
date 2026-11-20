using System.Collections.Generic;

namespace Simpiens.Simulation.Skills
{
    /// <summary>
    /// A calling, by id (Epic 11, S5, A2, Q11, D20): one word for who a crawler has become, earned from its best skill and the
    /// technique it took at Apprentice there. The id is what a crawler's traits record holds and its save: never reuse one. What
    /// a calling is (its words, skill, technique, role, Master title and capstone) is its row in <see cref="CallingTable"/>.
    /// </summary>
    public enum Calling : byte
    {
        None = 0,
        Warden = 1,
        Berserker = 2,
        Marksman = 3,
        Skirmisher = 4,
        Prospector = 5,
        Rockbreaker = 6,
        Herbalist = 7,
        Gleaner = 8,
        Smith = 9,
        Forgehand = 10,
        Apothecary = 11,
        Brewer = 12,
        FieldSurgeon = 13,
        Bearer = 14,
        Captain = 15
    }

    /// <summary>
    /// What a calling does in a party the guild sends (Epic 11, S5.4, D22): the guild's orders read it, never a calling's name.
    /// Ids are part of the table, not the save.
    /// </summary>
    public enum PartyRole : byte
    {
        None = 0,
        /// <summary>Holds the front: at a clear's fight it draws the creatures onto itself.</summary>
        Front = 1,
        /// <summary>Shoots from behind: the clear's arms give it a weapon with a range first.</summary>
        Back = 2,
        /// <summary>Carries the fallen: the guild sends it to a fallen mate before the healthiest.</summary>
        Carrier = 3,
        /// <summary>Leads: the party's captain, by Leadership (D19), a Captain first.</summary>
        Leader = 4
    }

    /// <summary>One calling: a row of <see cref="CallingTable"/>.</summary>
    public sealed class CallingDefinition
    {
        public CallingDefinition(Calling id, string displayName, Skill skill, Technique earnedBy, PartyRole role, string masterName, TechniqueEffect capstone = default)
        {
            Id = id;
            DisplayName = displayName;
            Skill = skill;
            EarnedBy = earnedBy;
            Role = role;
            MasterName = masterName;
            Capstone = capstone;
        }

        public Calling Id { get; }

        /// <summary>The code name, for logs.</summary>
        public string Name => Id.ToString();

        /// <summary>The player's word: "Warden".</summary>
        public string DisplayName { get; }

        /// <summary>The skill it is a calling of.</summary>
        public Skill Skill { get; }

        /// <summary>The Apprentice technique of <see cref="Skill"/> that earns it; none when any Apprentice does (a skill without techniques).</summary>
        public Technique EarnedBy { get; }

        /// <summary>What it does in a party (D22).</summary>
        public PartyRole Role { get; }

        /// <summary>Its title at Master in its skill (D23): "Master Warden", "Forgemaster".</summary>
        public string MasterName { get; }

        /// <summary>What it gains at Master in its skill (D23), an effect through the techniques' door; none for no stat.</summary>
        public TechniqueEffect Capstone { get; }

        /// <summary>Its title for a crawler at <paramref name="band"/> in its skill: the Master title at Master, its word below.</summary>
        public string TitleAt(SkillBand band) => band >= SkillBand.Master ? MasterName : DisplayName;

        public override string ToString() => Name;
    }

    /// <summary>
    /// Callings, as a table (Epic 11, S5.2, A2, Q11, D20): a calling for each Apprentice technique of each skill that has them,
    /// and one for a skill without (Leadership's Captain). Static and immutable, as the techniques' table is. A new calling is a
    /// <see cref="Calling"/> value and a row; the earning, the roles, the capstone, the save and the client follow.
    /// </summary>
    public static class CallingTable
    {
        private static readonly CallingDefinition[] ById = new CallingDefinition[256];
        private static readonly CallingDefinition[] AllRows;

        static CallingTable()
        {
            var rows = Definitions();
            var all = new List<CallingDefinition>(rows.Length);
            foreach (var row in rows)
            {
                int id = (byte)row.Id;
                if (id == 0) throw new System.ArgumentException($"Calling {row.Name} has id 0.");
                if (ById[id] != null) throw new System.ArgumentException($"Two callings have id {id}.");
                ById[id] = row;
                all.Add(row);
            }
            AllRows = all.ToArray();
        }

        private static TechniqueEffect E(TechniqueStat stat, int value) => new TechniqueEffect(stat, value);

        /// <summary>The game's callings (D20, D23): a role for the fighters, the medics and the captain; a capstone at Master. First guesses, to tune.</summary>
        private static CallingDefinition[] Definitions() => new[]
        {
            new CallingDefinition(Calling.Warden, "Warden", Skill.Melee, Technique.ShieldWall, PartyRole.Front, "Master Warden", E(TechniqueStat.Defense, 3)),
            new CallingDefinition(Calling.Berserker, "Berserker", Skill.Melee, Technique.RecklessFury, PartyRole.Front, "Warlord", E(TechniqueStat.Attack, 20)),
            new CallingDefinition(Calling.Marksman, "Marksman", Skill.Ranged, Technique.SteadyAim, PartyRole.Back, "Master Marksman", E(TechniqueStat.Hit, 5)),
            new CallingDefinition(Calling.Skirmisher, "Skirmisher", Skill.Ranged, Technique.QuickDraw, PartyRole.Back, "Master Skirmisher", E(TechniqueStat.Pace, 15)),
            new CallingDefinition(Calling.Prospector, "Prospector", Skill.Mining, Technique.SeamStudent, PartyRole.None, "Master Prospector", E(TechniqueStat.Pace, 15)),
            new CallingDefinition(Calling.Rockbreaker, "Rockbreaker", Skill.Mining, Technique.HardDriver, PartyRole.None, "Master Rockbreaker", E(TechniqueStat.Pace, 15)),
            new CallingDefinition(Calling.Herbalist, "Herbalist", Skill.Foraging, Technique.GentleHands, PartyRole.None, "Master Herbalist", E(TechniqueStat.Pace, 15)),
            new CallingDefinition(Calling.Gleaner, "Gleaner", Skill.Foraging, Technique.QuickHands, PartyRole.None, "Master Gleaner", E(TechniqueStat.Pace, 15)),
            new CallingDefinition(Calling.Smith, "Smith", Skill.Smithing, Technique.FineTemper, PartyRole.None, "Master Smith", E(TechniqueStat.Pace, 15)),
            new CallingDefinition(Calling.Forgehand, "Forgehand", Skill.Smithing, Technique.FastForge, PartyRole.None, "Forgemaster", E(TechniqueStat.Pace, 15)),
            new CallingDefinition(Calling.Apothecary, "Apothecary", Skill.Alchemy, Technique.CarefulMeasure, PartyRole.None, "Master Apothecary", E(TechniqueStat.Pace, 15)),
            new CallingDefinition(Calling.Brewer, "Brewer", Skill.Alchemy, Technique.QuickBrew, PartyRole.None, "Master Brewer", E(TechniqueStat.Pace, 15)),
            new CallingDefinition(Calling.FieldSurgeon, "Field Surgeon", Skill.Medicine, Technique.SteadyHands, PartyRole.Carrier, "Master Surgeon", E(TechniqueStat.Heal, 20)),
            new CallingDefinition(Calling.Bearer, "Bearer", Skill.Medicine, Technique.StrongBack, PartyRole.Carrier, "Master Bearer", E(TechniqueStat.Pace, 20)),
            new CallingDefinition(Calling.Captain, "Captain", Skill.Leadership, Technique.None, PartyRole.Leader, "Commander"),
        };

        /// <summary>Every calling, in id order.</summary>
        public static IReadOnlyList<CallingDefinition> All => AllRows;

        /// <summary>The calling's row, or null for none and undefined ids.</summary>
        public static CallingDefinition Get(Calling id) => ById[(byte)id];

        /// <summary>A code name for logs: the row's, or the raw id.</summary>
        public static string NameOf(Calling id) => Get(id)?.Name ?? $"Calling{(byte)id}";

        /// <summary>
        /// The calling of <paramref name="skill"/> earned by <paramref name="apprenticeTechnique"/>: the row naming both, or for a
        /// skill whose row names no technique, that row; none when no row answers.
        /// </summary>
        public static Calling For(Skill skill, Technique apprenticeTechnique)
        {
            for (int i = 0; i < AllRows.Length; i++)
            {
                var row = AllRows[i];
                if (row.Skill != skill) continue;
                if (row.EarnedBy == Technique.None || row.EarnedBy == apprenticeTechnique) return row.Id;
            }
            return Calling.None;
        }

        /// <summary>The role a crawler holding <paramref name="calling"/> plays in a party; none for none.</summary>
        public static PartyRole RoleOf(Calling calling) => Get(calling)?.Role ?? PartyRole.None;
    }
}
