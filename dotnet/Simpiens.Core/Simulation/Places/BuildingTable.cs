using System;

namespace Simpiens.Simulation.Places
{
    /// <summary>
    /// A kind of building (Epic PLACES, A1, plan 1's D4). Never reuse a value: plan 2 saves a building's state by its kind. None is
    /// an empty lot (D5).
    /// </summary>
    public enum BuildingKind : byte
    {
        None = 0,
        Bunkhouse = 1,
        Barracks = 2,
    }

    /// <summary>How big a building's lot is, from its footprint's area (D2): a kind stands only on a lot at least its row's size.</summary>
    public enum LotSize : byte
    {
        Small = 1,
        Medium = 2,
        Large = 3,
    }

    /// <summary>What a building's walls are drawn as (D9). A zone's walls are stone.</summary>
    public enum WallMaterial : byte
    {
        Stone = 0,
        Timber = 1,
        /// <summary>A lot's: a low course of stone where the walls will stand.</summary>
        Foundations = 2,
    }

    /// <summary>What a building's floor is drawn as (D9). A zone's floor is flagstone.</summary>
    public enum FloorMaterial : byte
    {
        Flagstone = 0,
        Planks = 1,
        /// <summary>A lot's: bare ground.</summary>
        Bare = 2,
    }

    /// <summary>
    /// One building kind's row (Epic PLACES, plan 1's D4; the epic's "every new system is a table"): its words, what it is for, the
    /// lot it needs, its walls and floor, and its art key (the kind's name in kebab case). Code asks the row, never the kind.
    /// </summary>
    public sealed class BuildingKindDefinition
    {
        public BuildingKindDefinition(BuildingKind kind, string word, string title, string description, LotSize size, WallMaterial walls, FloorMaterial floor, string artKey)
        {
            Kind = kind;
            Word = word;
            Title = title;
            Description = description;
            Size = size;
            Walls = walls;
            Floor = floor;
            ArtKey = artKey;
        }

        public BuildingKind Kind { get; }

        /// <summary>The player's word inside a line: "bunkhouse", "empty lot".</summary>
        public string Word { get; }

        /// <summary>Its title, as its sign and the map's label say it: "Bunkhouse".</summary>
        public string Title { get; }

        /// <summary>What it is for, in a line.</summary>
        public string Description { get; }

        /// <summary>The smallest lot it stands on.</summary>
        public LotSize Size { get; }

        public WallMaterial Walls { get; }
        public FloorMaterial Floor { get; }

        /// <summary>Its art key's last part.</summary>
        public string ArtKey { get; }

        public override string ToString() => Kind.ToString();
    }

    /// <summary>
    /// The building kinds, as a table (Epic PLACES, plan 1's D4). Static and immutable, as the station table is. A new kind is a
    /// <see cref="BuildingKind"/> value and a row here; its lots on the map; its look by its walls and floor.
    /// </summary>
    public static class BuildingTable
    {
        /// <summary>A footprint of this area or more is a medium lot (wall centre lines: 8 × 8).</summary>
        public const float MediumFrom = 64f;

        /// <summary>A footprint of this area or more is a large lot (16 × 8).</summary>
        public const float LargeFrom = 128f;

        private static readonly BuildingKindDefinition[] ById = Build();

        private static BuildingKindDefinition[] Build()
        {
            var rows = new[]
            {
                new BuildingKindDefinition(BuildingKind.None, "empty lot", "Empty Lot", "Foundations where a building may stand.",
                    LotSize.Small, WallMaterial.Foundations, FloorMaterial.Bare, "lot"),
                new BuildingKindDefinition(BuildingKind.Bunkhouse, "bunkhouse", "Bunkhouse", "Bunks where the outpost's residents sleep.",
                    LotSize.Medium, WallMaterial.Timber, FloorMaterial.Planks, "bunkhouse"),
                new BuildingKindDefinition(BuildingKind.Barracks, "barracks", "Barracks", "The guild's bunks, one kept for each member.",
                    LotSize.Small, WallMaterial.Timber, FloorMaterial.Planks, "barracks"),
            };
            int capacity = 0;
            foreach (var row in rows) capacity = Math.Max(capacity, (int)row.Kind + 1);
            var byId = new BuildingKindDefinition[capacity];
            foreach (var row in rows)
            {
                if (byId[(int)row.Kind] != null) throw new ArgumentException($"Two rows for {row.Kind}.");
                byId[(int)row.Kind] = row;
            }
            return byId;
        }

        /// <summary>The row of <paramref name="kind"/>; the empty lot's for a kind with no row.</summary>
        public static BuildingKindDefinition Get(BuildingKind kind) => (int)kind < ById.Length && ById[(int)kind] != null ? ById[(int)kind] : ById[0];

        /// <summary>Every row, in kind order (the empty lot first).</summary>
        public static System.Collections.Generic.IReadOnlyList<BuildingKindDefinition> All { get; } = Array.FindAll(ById, r => r != null);

        /// <summary>The lot a footprint makes, by its area (wall centre lines).</summary>
        public static LotSize SizeOf(RectF footprint)
        {
            float area = footprint.Width * footprint.Height;
            return area >= LargeFrom ? LotSize.Large : area >= MediumFrom ? LotSize.Medium : LotSize.Small;
        }
    }
}
