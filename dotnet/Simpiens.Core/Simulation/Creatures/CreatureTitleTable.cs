using System.Collections.Generic;

namespace Simpiens.Simulation.Creatures
{
    /// <summary>What a downing did for a creature's name (Epic 11, S7.1, D31; <see cref="CreatureService.Credit"/>).</summary>
    public enum Renown : byte
    {
        /// <summary>Nothing: a boss, a creature gone, or a death after the downing counted.</summary>
        None = 0,
        /// <summary>It took a name from the crawler it downed.</summary>
        Named = 1,
        /// <summary>Named already, it downed another, and its rank rose.</summary>
        RankRose = 2,
        /// <summary>Named and at its highest rank, it downed another: counted, its rank as it was.</summary>
        Downed = 3,
    }

    /// <summary>
    /// A named creature's title at one rank (Epic 11, S7.1, D31, D32): the word in its name ("Ama's Bane"), and how much it has
    /// grown, its health and its attack in percent over its kind's.
    /// </summary>
    public sealed class CreatureTitle
    {
        public CreatureTitle(byte rank, string word, int healthPercent, int attackPercent)
        {
            Rank = rank;
            Word = word;
            HealthPercent = healthPercent;
            AttackPercent = attackPercent;
        }

        /// <summary>Its health over its kind's, in percent (S7.2, D32), within <see cref="CreatureTitleTable.MaxHealthPercent"/>.</summary>
        public int HealthPercent { get; }

        /// <summary>Its attack over its kind's, in percent (S7.2, D32), within <see cref="CreatureTitleTable.MaxAttackPercent"/>.</summary>
        public int AttackPercent { get; }

        /// <summary>Its rank, 1 to <see cref="CreatureTitleTable.MaxRank"/>: the crawlers it has downed, to the most.</summary>
        public byte Rank { get; }

        /// <summary>The word after its victim's name: "Bane".</summary>
        public string Word { get; }
    }

    /// <summary>
    /// The titles a named creature grows through (Epic 11, S7.1, D31, D32), a row per rank: a creature that downs a crawler takes
    /// the first ("{the crawler}'s Bane"), and each further crawler it downs raises it, to <see cref="MaxRank"/>; each rank grows
    /// its health and attack (S7.2, D32: +20% and +10% a rank, first guesses). A new rank is a row. The creatures' rule reads a
    /// creature's rank, never its name: its fighter (<see cref="Simpiens.Simulation.Combat.Combatant.ForCreature"/>), its health
    /// (<see cref="CreatureState.MaxHealth"/>) and its lair's danger (<see cref="LairReach"/>).
    /// </summary>
    public static class CreatureTitleTable
    {
        /// <summary>The highest rank: a creature that has downed this many is as grown as it gets.</summary>
        public const int MaxRank = 3;

        /// <summary>The most a rank grows a creature's health and attack, in percent over its kind's (D32's bounds).</summary>
        public const int MaxHealthPercent = 100, MaxAttackPercent = 50;

        private static readonly CreatureTitle[] Rows =
        {
            new CreatureTitle(1, "Bane", healthPercent: 20, attackPercent: 10),
            new CreatureTitle(2, "Scourge", healthPercent: 40, attackPercent: 20),
            new CreatureTitle(3, "Terror", healthPercent: 60, attackPercent: 30),
        };

        /// <summary>Every title, rank by rank.</summary>
        public static IReadOnlyList<CreatureTitle> All => Rows;

        /// <summary>The title at <paramref name="rank"/>; null for none (0, an unnamed creature) or past the table.</summary>
        public static CreatureTitle Get(int rank) => rank >= 1 && rank <= Rows.Length ? Rows[rank - 1] : null;

        /// <summary>The word at <paramref name="rank"/>: "Bane"; empty for none.</summary>
        public static string WordOf(int rank) => Get(rank)?.Word ?? string.Empty;

        /// <summary>What <paramref name="rank"/> multiplies a creature's health by: 1 unnamed, 1.2 a Bane.</summary>
        public static float HealthFactor(int rank) => Get(rank) is CreatureTitle title ? 1f + SimMath.Min(title.HealthPercent, MaxHealthPercent) / 100f : 1f;

        /// <summary>What <paramref name="rank"/> multiplies a creature's attack by: 1 unnamed, 1.1 a Bane.</summary>
        public static float AttackFactor(int rank) => Get(rank) is CreatureTitle title ? 1f + SimMath.Min(title.AttackPercent, MaxAttackPercent) / 100f : 1f;

        /// <summary>A named creature's name: "{<paramref name="victim"/>}'s {its word}" ("Ama's Bane"); empty for an unnamed one.</summary>
        public static string NameOf(string victim, int rank) => Get(rank) is CreatureTitle title ? $"{victim}'s {title.Word}" : string.Empty;
    }
}
