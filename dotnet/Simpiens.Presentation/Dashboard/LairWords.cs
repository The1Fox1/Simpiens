using System.Collections.Generic;
using Simpiens.Simulation.Creatures;

namespace Simpiens.Presentation.Dashboard
{
    /// <summary>
    /// A lair in the player's words (Epic 10 Part 3, K10.5d), shared by the bounty board and the guild's builder and tracker:
    /// its pack in the plural ("the burrow rats"), a boss alone by its name ("the Gatekeeper"), a pack with a boss at its head
    /// as hers ("the Matriarch's brood": short enough for the tracker's title). Lower case at the start; the caller capitalises
    /// a line's first word.
    /// </summary>
    public static class LairWords
    {
        public static string Pack(IReadOnlyList<LairState> lairs, int lairIndex)
        {
            if (lairs == null || lairIndex < 0 || lairIndex >= lairs.Count) return "the creatures";
            var lair = lairs[lairIndex].Definition;
            var kind = CreatureCatalog.Get(lair.Kind);
            if (kind == null) return "the creatures";
            if (kind.IsBoss) return Named(kind.DisplayName);
            string brood = "the " + kind.DisplayName.ToLowerInvariant() + "s";
            var boss = lair.HasBoss ? CreatureCatalog.Get(lair.Boss) : null;
            return boss != null ? Named(boss.DisplayName) + "'s brood" : brood;
        }

        // A boss's name keeps its capital, its article doesn't: "The Matriarch" -> "the Matriarch"
        private static string Named(string displayName) =>
            displayName.StartsWith("The ", System.StringComparison.Ordinal) ? "the " + displayName.Substring(4) : displayName;
    }
}
