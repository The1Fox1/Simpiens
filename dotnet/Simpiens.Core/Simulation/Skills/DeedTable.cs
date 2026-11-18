using System.Collections.Generic;

namespace Simpiens.Simulation.Skills
{
    /// <summary>
    /// A deed, by id (Epic 11, A7, D2): something a crawler did that the outpost remembers, counted where it happens, on the
    /// main thread, in its <see cref="CrawlerTraits"/>. The id is the deed's counter in the record and in the save: never reuse
    /// one, and keep it under <see cref="DeedTable.Capacity"/>. Counted, never rebuilt from the chronicle: the chronicle tells,
    /// the counters remember. First blood is a count of one, derived, not a deed of its own.
    /// </summary>
    public enum Deed : byte
    {
        None = 0,
        /// <summary>It struck the blow that slew a creature.</summary>
        CreatureSlain = 1,
        /// <summary>It struck the blow that slew a boss (counted beside <see cref="CreatureSlain"/>).</summary>
        BossSlain = 2,
        /// <summary>It carried a fallen crawler out of danger, or stood one up with a medicine.</summary>
        Rescue = 3,
        /// <summary>It finished a make at a station.</summary>
        Make = 4,
        /// <summary>It collapsed in danger, its clock running, and lived: carried out, or back on its feet.</summary>
        DowningSurvived = 5,
        /// <summary>It was paid a share of a hunt's bounty, its lair emptied.</summary>
        HuntPaid = 6,
        /// <summary>It came home with its party from a clear that emptied its lair.</summary>
        ClearWon = 7,
        /// <summary>A crawler taught a skill, the first to teach it that skill (Epic 11, S3.1, D9): counted on the teacher.</summary>
        Taught = 8
    }

    /// <summary>One deed: a row of <see cref="DeedTable"/>.</summary>
    public sealed class DeedDefinition
    {
        public DeedDefinition(Deed id, string name, string displayName)
        {
            Id = id;
            Name = name;
            DisplayName = displayName;
        }

        public Deed Id { get; }

        /// <summary>The code name, for logs.</summary>
        public string Name { get; }

        /// <summary>The player's words for its count: "creatures slain".</summary>
        public string DisplayName { get; }

        public override string ToString() => Name;
    }

    /// <summary>The deeds, as a table (Epic 11, A2, D2). Static and immutable, as the skills' table is.</summary>
    public static class DeedTable
    {
        /// <summary>The most deeds there can be: the counters a crawler's traits record keeps. A 17th changes the save format.</summary>
        public const int Capacity = 16;

        private static readonly DeedDefinition[] ById = Build();

        private static DeedDefinition[] Build()
        {
            var rows = new[]
            {
                new DeedDefinition(Deed.CreatureSlain, "CreatureSlain", "creatures slain"),
                new DeedDefinition(Deed.BossSlain, "BossSlain", "bosses slain"),
                new DeedDefinition(Deed.Rescue, "Rescue", "rescues"),
                new DeedDefinition(Deed.Make, "Make", "makes"),
                new DeedDefinition(Deed.DowningSurvived, "DowningSurvived", "downings survived"),
                new DeedDefinition(Deed.HuntPaid, "HuntPaid", "hunts paid"),
                new DeedDefinition(Deed.ClearWon, "ClearWon", "clears won"),
                new DeedDefinition(Deed.Taught, "Taught", "crawlers taught")
            };
            var byId = new DeedDefinition[Capacity];
            foreach (var row in rows)
            {
                int id = (byte)row.Id;
                if (id == 0 || id >= Capacity) throw new System.ArgumentException($"Deed {row.Name} has id {id}; deeds have ids from 1 to {Capacity - 1}.");
                if (byId[id] != null) throw new System.ArgumentException($"Two deeds have id {id}.");
                byId[id] = row;
            }
            return byId;
        }

        private static readonly DeedDefinition[] AllRows = System.Array.FindAll(ById, row => row != null);

        /// <summary>Every deed, in id order.</summary>
        public static IReadOnlyList<DeedDefinition> All => AllRows;

        /// <summary>The deed's row, or null for none and undefined ids.</summary>
        public static DeedDefinition Get(Deed id) => (byte)id < Capacity ? ById[(byte)id] : null;

        /// <summary>A code name for logs: the row's, or the raw id.</summary>
        public static string NameOf(Deed id) => Get(id)?.Name ?? $"Deed{(byte)id}";
    }
}
