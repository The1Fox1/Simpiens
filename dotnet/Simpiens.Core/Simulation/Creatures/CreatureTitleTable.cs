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

    /// <summary>A named creature's title at one rank (Epic 11, S7.1, D31, D32): the word in its name ("Ama's Bane").</summary>
    public sealed class CreatureTitle
    {
        public CreatureTitle(byte rank, string word)
        {
            Rank = rank;
            Word = word;
        }

        /// <summary>Its rank, 1 to <see cref="CreatureTitleTable.MaxRank"/>: the crawlers it has downed, to the most.</summary>
        public byte Rank { get; }

        /// <summary>The word after its victim's name: "Bane".</summary>
        public string Word { get; }
    }

    /// <summary>
    /// The titles a named creature grows through (Epic 11, S7.1, D31, D32), a row per rank: a creature that downs a crawler takes
    /// the first ("{the crawler}'s Bane"), and each further crawler it downs raises it, to <see cref="MaxRank"/>. A new rank is a
    /// row. The creatures' rule reads a creature's rank, never its name.
    /// </summary>
    public static class CreatureTitleTable
    {
        /// <summary>The highest rank: a creature that has downed this many is as grown as it gets.</summary>
        public const int MaxRank = 3;

        private static readonly CreatureTitle[] Rows =
        {
            new CreatureTitle(1, "Bane"),
            new CreatureTitle(2, "Scourge"),
            new CreatureTitle(3, "Terror"),
        };

        /// <summary>Every title, rank by rank.</summary>
        public static IReadOnlyList<CreatureTitle> All => Rows;

        /// <summary>The title at <paramref name="rank"/>; null for none (0, an unnamed creature) or past the table.</summary>
        public static CreatureTitle Get(int rank) => rank >= 1 && rank <= Rows.Length ? Rows[rank - 1] : null;

        /// <summary>The word at <paramref name="rank"/>: "Bane"; empty for none.</summary>
        public static string WordOf(int rank) => Get(rank)?.Word ?? string.Empty;

        /// <summary>A named creature's name: "{<paramref name="victim"/>}'s {its word}" ("Ama's Bane"); empty for an unnamed one.</summary>
        public static string NameOf(string victim, int rank) => Get(rank) is CreatureTitle title ? $"{victim}'s {title.Word}" : string.Empty;
    }
}
