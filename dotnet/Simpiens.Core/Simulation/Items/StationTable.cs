using System;

namespace Simpiens.Simulation.Items
{
    /// <summary>What a station costs to build, as a line of the session's <c>WorkshopTuning</c>: the numbers live there.</summary>
    public enum StationCost : byte
    {
        /// <summary>The workshop row's price (<c>WorkshopTuning.ConstructionCost</c>).</summary>
        Workshop = 0,
        /// <summary>The drill yard's (<c>WorkshopTuning.DrillYardCost</c>).</summary>
        Yard = 1,
    }

    /// <summary>
    /// One station kind's row (Epic LIFE-1 plan 2, T.1, D17; the epic's A4, "every new system is a table"): its words, what it
    /// is for, whether an artisan works it, whether it has recipes, its build cost's line, and its art key (the kind's name in
    /// kebab case, as an item's). Code asks the row, never the kind.
    /// </summary>
    public sealed class StationKindDefinition
    {
        public StationKindDefinition(StationKind kind, string word, string title, string description, bool takesArtisan, bool hasRecipes, StationCost cost, string artKey)
        {
            Kind = kind;
            Word = word;
            Title = title;
            Description = description;
            TakesArtisan = takesArtisan;
            HasRecipes = hasRecipes;
            Cost = cost;
            ArtKey = artKey;
        }

        public StationKind Kind { get; }

        /// <summary>The player's word inside a line: "forge", "drill yard".</summary>
        public string Word { get; }

        /// <summary>Its title: "Forge", "Alchemy Still".</summary>
        public string Title { get; }

        /// <summary>What it is for, in a line for the station window.</summary>
        public string Description { get; }

        /// <summary>Whether an artisan is hired to work it (the drill yard has none).</summary>
        public bool TakesArtisan { get; }

        /// <summary>Whether it makes things to order (<see cref="RecipeBook"/>).</summary>
        public bool HasRecipes { get; }

        public StationCost Cost { get; }

        /// <summary>Its art key's last part: the built station is drawn as "prop/" and this.</summary>
        public string ArtKey { get; }

        public override string ToString() => Kind.ToString();
    }

    /// <summary>
    /// The station kinds, as a table (Epic LIFE-1 plan 2, T.1, D17; finding F13: six places switched on a station's kind).
    /// Static and immutable, as the item catalogue is. A new kind is a <see cref="StationKind"/> value and a row here (AGENTS.md,
    /// "Adding a station kind"), its sites on the map, and its art.
    /// </summary>
    public static class StationTable
    {
        private static readonly StationKindDefinition Unknown = new StationKindDefinition(StationKind.None, "station", "Station", "A station worked by an artisan you hire.",
            takesArtisan: true, hasRecipes: true, StationCost.Workshop, "station-plot");

        private static readonly StationKindDefinition[] ById = Build();

        private static StationKindDefinition[] Build()
        {
            var rows = new[]
            {
                new StationKindDefinition(StationKind.Forge, "forge", "Forge", "Iron ore into tools and gear, worked by an artisan you hire.",
                    takesArtisan: true, hasRecipes: true, StationCost.Workshop, "forge"),
                new StationKindDefinition(StationKind.Still, "still", "Alchemy Still", "Herbs into draughts, worked by an artisan you hire.",
                    takesArtisan: true, hasRecipes: true, StationCost.Workshop, "still"),
                new StationKindDefinition(StationKind.DrillYard, "drill yard", "Drill Yard", "Where guild members drill in their free hours: no artisan, no orders.",
                    takesArtisan: false, hasRecipes: false, StationCost.Yard, "drill-yard"),
            };
            int capacity = 0;
            foreach (var row in rows) capacity = Math.Max(capacity, (int)row.Kind + 1);
            var byId = new StationKindDefinition[capacity];
            foreach (var row in rows)
            {
                if (row.Kind == StationKind.None) throw new ArgumentException("A station row needs a kind.");
                if (byId[(int)row.Kind] != null) throw new ArgumentException($"Two rows for {row.Kind}.");
                byId[(int)row.Kind] = row;
            }
            return byId;
        }

        /// <summary>The row of <paramref name="kind"/>; a plain station's words for a kind with no row.</summary>
        public static StationKindDefinition Get(StationKind kind) => (int)kind < ById.Length && ById[(int)kind] != null ? ById[(int)kind] : Unknown;

        /// <summary>Every row, in kind order.</summary>
        public static System.Collections.Generic.IReadOnlyList<StationKindDefinition> All { get; } = Array.FindAll(ById, r => r != null);
    }
}
