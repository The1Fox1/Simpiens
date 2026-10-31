using System.Collections.Generic;

namespace Simpiens.Simulation.People
{
    /// <summary>
    /// Crawlers' given names (Epic UI-Beta B1, Q7): feminine, masculine and neutral lists, drawn by body frame. Latin-1
    /// only, because the client's baked fonts hold ASCII and Latin-1, and at most <see cref="MaxLength"/> characters, the
    /// width the dossier, bubbles and labels are laid out for (plan D7). A name in two lists, or twice in one, is refused by
    /// the validator. Every lineage draws from these lists until the producer writes its own (Q4).
    /// </summary>
    public static class GivenNames
    {
        public const int MaxLength = 10;

        public static IReadOnlyList<string> Feminine { get; } = new[]
        {
            "Maya", "Hazel", "Freya", "Rosa", "Amara", "Leila", "Ingrid", "Noor", "Saoirse", "Ayesha",
            "Mei", "Yuki", "Zara", "Thandi", "Inès", "Lucía", "Anya", "Mira", "Ottilie", "Briony",
            "Esme", "Ivy", "Nadia", "Priya", "Sigrid", "Tove", "Ione", "Liesl", "Marisol", "Nia",
            "Oona", "Paloma", "Rhea", "Selin", "Talia", "Vesna", "Ximena", "Zuri", "Adaeze", "Dagny",
            "Elif", "Fatou", "Greta", "Hana", "Ilse", "Kiri", "Lowri", "Maren", "Nell", "Orla",
            "Beatrix", "Céline", "Dalia", "Pernille",
        };

        public static IReadOnlyList<string> Masculine { get; } = new[]
        {
            "Bob", "Gideon", "Niles", "Orson", "Björn", "Tariq", "Kofi", "Hamid", "Ivo", "Jasper",
            "Kenji", "Lars", "Mateo", "Nikolai", "Obi", "Pavel", "Rafael", "Søren", "Tomas", "Ulf",
            "Viktor", "Wendell", "Yusuf", "Zoltan", "Anders", "Bram", "Cormac", "Dmitri", "Emil", "Farid",
            "Gunnar", "Hugo", "Idris", "Jonah", "Kwame", "Leif", "Magnus", "Nasir", "Olek", "Piet",
            "Rustam", "Silas", "Tenzin", "Umar", "Vidar", "Wim", "Xavi", "Yoren", "Zeb", "Arjun",
            "Caspar", "Dario", "Eamon", "Felix", "Joaquín",
        };

        public static IReadOnlyList<string> Neutral { get; } = new[]
        {
            "Ash", "Avery", "Bay", "Blair", "Cass", "Dale", "Eden", "Ellis", "Emery", "Finch",
            "Gray", "Harper", "Indy", "Jules", "Kai", "Kit", "Lane", "Lark", "Marlo", "Merit",
            "Nico", "Noa", "Oakley", "Onyx", "Perry", "Quill", "Quinn", "Reed", "Remy", "Rio",
            "Robin", "Rowan", "Rune", "Sage", "Sasha", "Shay", "Sol", "Tam", "Tobin", "Vale",
            "Wren", "Yael", "Zephyr", "Alex", "Brook", "Corin", "Darcy", "Ember", "Fen", "Hollis",
            "Jory", "Morgan", "Sky",
        };

        /// <summary>Random picks tried before the draw walks the list in order.</summary>
        private const int Tries = 32;

        /// <summary>
        /// A name for <paramref name="entityId"/> with body frame <paramref name="frame"/>, from the frame's list and the
        /// neutral one, on its own salt (<see cref="SimRandomSalt.Name"/>). A name in <paramref name="seen"/> (everyone the
        /// session has seen, plan D5) is skipped while any is left; then only names in <paramref name="living"/> are.
        /// </summary>
        public static string Draw(ulong seed, EntityId entityId, byte frame, ICollection<string> seen, ICollection<string> living)
        {
            var rng = SimRandom.For(seed, entityId, 0, SimRandomSalt.Name);
            string key = LookCatalog.KeyOf(LookGene.Frame, frame);
            var own = key == "female" ? Feminine : key == "male" ? Masculine : null;
            int ownCount = own != null ? own.Count : Feminine.Count + Masculine.Count;
            int count = ownCount + Neutral.Count;

            for (int t = 0; t < Tries; t++)
            {
                string name = Candidate(own, (int)(rng.NextULong() % (ulong)count));
                if (seen == null || !seen.Contains(name)) return name;
            }
            for (int i = 0; i < count; i++)
            {
                string name = Candidate(own, i);
                if (seen == null || !seen.Contains(name)) return name;
            }
            for (int i = 0; i < count; i++)
            {
                string name = Candidate(own, i);
                if (living == null || !living.Contains(name)) return name;
            }
            return Candidate(own, 0);
        }

        // The i-th candidate: the frame's list, then the neutral one (both gendered lists when the frame is unset)
        private static string Candidate(IReadOnlyList<string> own, int i)
        {
            if (own == null)
            {
                if (i < Feminine.Count) return Feminine[i];
                i -= Feminine.Count;
                if (i < Masculine.Count) return Masculine[i];
                return Neutral[i - Masculine.Count];
            }
            return i < own.Count ? own[i] : Neutral[i - own.Count];
        }
    }
}
