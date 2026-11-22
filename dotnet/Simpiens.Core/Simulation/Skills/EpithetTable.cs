using System.Collections.Generic;
using Simpiens.Simulation.Creatures;

namespace Simpiens.Simulation.Skills
{
    /// <summary>
    /// An epithet, by id (Epic 11, S6.1, A2, D27): what the outpost calls a crawler for its deeds, "Corrin Beetlebane". The id is
    /// what a crawler's traits record holds and its save: never reuse one. What an epithet is (its words, the deed and count that
    /// earn it, its rank and form) is its row in <see cref="EpithetTable"/>; production code names none outside the table.
    /// </summary>
    public enum Epithet : ushort
    {
        None = 0,
        KindSlayer = 1,
        BossSlayer = 2,
        Steadfast = 3,
        Unbroken = 4,
        Lairbreaker = 5,
        Huntsman = 6,
        Teacher = 7,
        Maker = 8,
        Avenger = 9
    }

    /// <summary>How an epithet's words are made (D27).</summary>
    public enum EpithetForm : byte
    {
        /// <summary>Its own words: "the Steadfast".</summary>
        Plain = 0,
        /// <summary>The slain kind's word (<see cref="CreatureDefinition.SlayerEpithet"/>): "Beetlebane".</summary>
        KindWord = 1,
        /// <summary>Its words around a crawler's name: "{0}'s Avenger".</summary>
        CrawlerName = 2
    }

    /// <summary>One epithet: a row of <see cref="EpithetTable"/>.</summary>
    public sealed class EpithetDefinition
    {
        public EpithetDefinition(Epithet id, string words, Deed deed, int count, int rank, EpithetForm form = EpithetForm.Plain)
        {
            Id = id;
            Words = words;
            Deed = deed;
            Count = count;
            Rank = rank;
            Form = form;
        }

        public Epithet Id { get; }

        /// <summary>The code name, for logs.</summary>
        public string Name => Id.ToString();

        /// <summary>Its words: "the Steadfast"; for a crawler's name, a format with its name as {0}; none for a kind's word.</summary>
        public string Words { get; }

        /// <summary>The deed it reads, and the count of it that earns it: earned at the deed that brings the count there.</summary>
        public Deed Deed { get; }
        public int Count { get; }

        /// <summary>Its rank: a crawler holds the highest it has earned, and one of the same rank doesn't replace it.</summary>
        public int Rank { get; }

        public EpithetForm Form { get; }

        public override string ToString() => Name;
    }

    /// <summary>
    /// Epithets, as a table (Epic 11, S6.1, A2, D27). Static and immutable, as the skills' table is. A new epithet is an
    /// <see cref="Epithet"/> value and a row; the rule, the save, the chronicle and the client follow. The counts were set from
    /// the deeds measured on the gate seeds (<c>docs/skills-model.md</c> §5.9).
    /// </summary>
    public static class EpithetTable
    {
        private static readonly Dictionary<Epithet, EpithetDefinition> ById = new Dictionary<Epithet, EpithetDefinition>();
        private static readonly EpithetDefinition[] AllRows = Build();

        private static EpithetDefinition[] Build()
        {
            var rows = new[]
            {
                new EpithetDefinition(Epithet.KindSlayer, null, Deed.CreatureSlain, 3, 2, EpithetForm.KindWord),
                new EpithetDefinition(Epithet.BossSlayer, null, Deed.BossSlain, 1, 5, EpithetForm.KindWord),
                new EpithetDefinition(Epithet.Steadfast, "the Steadfast", Deed.Rescue, 2, 3),
                new EpithetDefinition(Epithet.Unbroken, "the Unbroken", Deed.DowningSurvived, 2, 3),
                new EpithetDefinition(Epithet.Lairbreaker, "Lairbreaker", Deed.ClearWon, 5, 3),
                new EpithetDefinition(Epithet.Huntsman, "the Huntsman", Deed.HuntPaid, 3, 2),
                new EpithetDefinition(Epithet.Teacher, "the Teacher", Deed.Taught, 10, 2),
                new EpithetDefinition(Epithet.Maker, "the Maker", Deed.Make, 40, 1),
                // The avenger (Epic 11, S7.5, D35): whoever slays a named creature carries its first victim's name, the highest rank
                new EpithetDefinition(Epithet.Avenger, "{0}'s Avenger", Deed.NamedCreatureSlain, 1, 6, EpithetForm.CrawlerName),
            };
            foreach (var row in rows)
            {
                if (row.Id == Epithet.None) throw new System.ArgumentException("An epithet has id 0.");
                if (!ById.TryAdd(row.Id, row)) throw new System.ArgumentException($"Two epithets have id {(ushort)row.Id}.");
            }
            return rows;
        }

        /// <summary>Every epithet, in the table's order.</summary>
        public static IReadOnlyList<EpithetDefinition> All => AllRows;

        /// <summary>The epithet's row, or null for none and undefined ids.</summary>
        public static EpithetDefinition Get(Epithet id) => ById.TryGetValue(id, out var row) ? row : null;

        /// <summary>A code name for logs: the row's, or the raw id.</summary>
        public static string NameOf(Epithet id) => Get(id)?.Name ?? $"Epithet{(ushort)id}";
    }

    /// <summary>
    /// Epithets' rule (Epic 11, S6.1, D27): judged where a deed is counted, on the main thread, never rebuilt from the chronicle.
    /// </summary>
    public static class EpithetRules
    {
        /// <summary>
        /// After <paramref name="deed"/> is counted in <paramref name="traits"/>: the epithet whose count it has just reached,
        /// if its rank is above the one held, is held now, naming <paramref name="kind"/> (the creature slain) and
        /// <paramref name="subject"/> (the crawler named) where its form does; and the record has news to tell. True when it
        /// changed.
        /// </summary>
        public static bool Judge(ref CrawlerTraits traits, Deed deed, byte kind, EntityId subject)
        {
            int count = traits.Count(deed), held = EpithetTable.Get(traits.Epithet)?.Rank ?? 0;
            EpithetDefinition best = null;
            var all = EpithetTable.All;
            for (int i = 0; i < all.Count; i++)
            {
                var row = all[i];
                if (row.Deed != deed || row.Count != count || row.Rank <= held || (best != null && row.Rank <= best.Rank)) continue;
                if (row.Form == EpithetForm.KindWord && CreatureCatalog.Get((CreatureKind)kind)?.SlayerEpithet == null) continue; // a kind with no word gives none
                if (row.Form == EpithetForm.CrawlerName && subject == default) continue;
                best = row;
            }
            if (best == null) return false;
            traits.SetEpithet(best.Id, best.Form == EpithetForm.KindWord ? kind : (byte)0, best.Form == EpithetForm.CrawlerName ? subject : default);
            traits.AddNews(LegendNews.Epithet);
            return true;
        }

        /// <summary>
        /// The epithet's words: "the Steadfast", "Beetlebane", "Ama's Avenger" (<paramref name="name"/> the crawler it names);
        /// empty for none.
        /// </summary>
        public static string WordsOf(Epithet epithet, byte kind, string name)
        {
            var row = EpithetTable.Get(epithet);
            if (row == null) return string.Empty;
            return row.Form switch
            {
                EpithetForm.KindWord => CreatureCatalog.Get((CreatureKind)kind)?.SlayerEpithet ?? string.Empty,
                EpithetForm.CrawlerName => string.Format(System.Globalization.CultureInfo.InvariantCulture, row.Words, name ?? "someone"),
                _ => row.Words ?? string.Empty,
            };
        }

        /// <summary>The words of the epithet <paramref name="traits"/> holds; <paramref name="nameOf"/> names the crawler it names.</summary>
        public static string Words(in CrawlerTraits traits, System.Func<EntityId, string> nameOf)
        {
            var row = EpithetTable.Get(traits.Epithet);
            if (row == null) return string.Empty;
            string name = row.Form == EpithetForm.CrawlerName ? nameOf?.Invoke(traits.EpithetSubject) : null;
            return WordsOf(traits.Epithet, traits.EpithetKind, name);
        }

        private static readonly string[] Numbers = { "no", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine", "ten" };

        /// <summary>A count in the chronicle's words: "three"; past ten, its digits.</summary>
        public static string CountWords(int count) => count >= 0 && count < Numbers.Length ? Numbers[count] : count.ToString(System.Globalization.CultureInfo.InvariantCulture);

        /// <summary>
        /// A name with its epithet: "Corrin Beetlebane", "Mara the Steadfast"; the name alone with none. With a title, "Corrin
        /// Beetlebane the Warden", or after an epithet that already has "the", "Mara the Steadfast, Warden".
        /// </summary>
        public static string NameWith(string name, string epithet, string title = null)
        {
            string named = string.IsNullOrEmpty(epithet) ? name : $"{name} {epithet}";
            if (string.IsNullOrEmpty(title)) return named;
            return epithet != null && epithet.StartsWith("the ", System.StringComparison.Ordinal) ? $"{named}, {title}" : $"{named} the {title}";
        }
    }
}
