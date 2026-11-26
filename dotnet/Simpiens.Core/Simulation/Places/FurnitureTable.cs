using System;

namespace Simpiens.Simulation.Places
{
    /// <summary>
    /// A kind of furniture (Epic PLACES, A4, plan 1's D11): an object crawlers use without working at it. Never reuse a value: a
    /// placement names its kind, and plan 2's building kinds list the furniture they come with.
    /// </summary>
    public enum FurnitureKind : byte
    {
        None = 0,
        Table = 1,
        Bench = 2,
        Chair = 3,
        Stool = 4,
        Bunk = 5,
        GuildBunk = 6,
    }

    /// <summary>Whom a piece's beds are for (D13): the outpost's residents, or the guild's members (a bunk kept for each).</summary>
    public enum BedFor : byte
    {
        None = 0,
        Residents = 1,
        Guild = 2,
    }

    /// <summary>
    /// One furniture kind's row (Epic PLACES plan 1, F.1, D11): its words; its footprint in world units (width along x, depth along
    /// y, swapped when a piece is turned); whether it blocks movement; its seats and beds, as offsets from its centre; whom its
    /// beds are for; its beauty and comfort (read from plan 3); and its art key (the kind's name in kebab case). Code asks the row.
    /// </summary>
    public sealed class FurnitureKindDefinition
    {
        public FurnitureKindDefinition(FurnitureKind kind, string word, string title, float width, float depth, bool blocks, Vec2[] seats, Vec2[] beds, BedFor bedFor,
            sbyte beauty, sbyte comfort, string artKey)
        {
            Kind = kind;
            Word = word;
            Title = title;
            Width = width;
            Depth = depth;
            Blocks = blocks;
            Seats = seats ?? Array.Empty<Vec2>();
            Beds = beds ?? Array.Empty<Vec2>();
            BedFor = bedFor;
            Beauty = beauty;
            Comfort = comfort;
            ArtKey = artKey;
        }

        public FurnitureKind Kind { get; }

        /// <summary>The player's word inside a line: "table", "stool".</summary>
        public string Word { get; }

        public string Title { get; }

        /// <summary>Its footprint along x, in world units, as placed unturned.</summary>
        public float Width { get; }

        /// <summary>Its footprint along y, in world units, as placed unturned.</summary>
        public float Depth { get; }

        /// <summary>Whether it stands in the way (D15): written into the static nav grid as a wall is. A table does; seats and bunks don't.</summary>
        public bool Blocks { get; }

        /// <summary>Where crawlers sit at it, offsets from its centre (unturned).</summary>
        public System.Collections.Generic.IReadOnlyList<Vec2> Seats { get; }

        /// <summary>Where crawlers sleep in it, offsets from its centre (unturned).</summary>
        public System.Collections.Generic.IReadOnlyList<Vec2> Beds { get; }

        public BedFor BedFor { get; }

        /// <summary>How it pleases the eye, -10 to 10 (Epic PLACES, A6; read from plan 3).</summary>
        public sbyte Beauty { get; }

        /// <summary>How restful it is to use, -10 to 10 (read from plan 3).</summary>
        public sbyte Comfort { get; }

        /// <summary>Its art key's last part: a placed piece is drawn as "furniture/" and this.</summary>
        public string ArtKey { get; }

        public override string ToString() => Kind.ToString();
    }

    /// <summary>
    /// The furniture kinds, as a table (Epic PLACES plan 1, F.1, D11). Static and immutable, as the building table is. A new kind is
    /// a <see cref="FurnitureKind"/> value and a row here; its placements on the map; its art under its key in both view modes.
    /// </summary>
    public static class FurnitureTable
    {
        private static readonly FurnitureKindDefinition[] ById = Build();

        private static FurnitureKindDefinition[] Build()
        {
            var centre = new[] { Vec2.Zero };
            var rows = new[]
            {
                new FurnitureKindDefinition(FurnitureKind.Table, "table", "Table", 1f, 1f, blocks: true, null, null, BedFor.None, beauty: 1, comfort: 0, "table"),
                new FurnitureKindDefinition(FurnitureKind.Bench, "bench", "Bench", 1.8f, 0.6f, blocks: false, new[] { new Vec2(-0.45f, 0f), new Vec2(0.45f, 0f) }, null, BedFor.None,
                    beauty: 0, comfort: 1, "bench"),
                new FurnitureKindDefinition(FurnitureKind.Chair, "chair", "Chair", 0.7f, 0.7f, blocks: false, centre, null, BedFor.None, beauty: 1, comfort: 2, "chair"),
                new FurnitureKindDefinition(FurnitureKind.Stool, "stool", "Stool", 0.6f, 0.6f, blocks: false, centre, null, BedFor.None, beauty: 0, comfort: 1, "stool"),
                new FurnitureKindDefinition(FurnitureKind.Bunk, "bunk", "Bunk", 1f, 1f, blocks: false, null, centre, BedFor.Residents, beauty: 0, comfort: 3, "bunk"),
                new FurnitureKindDefinition(FurnitureKind.GuildBunk, "guild bunk", "Guild Bunk", 1f, 1f, blocks: false, null, centre, BedFor.Guild, beauty: 0, comfort: 3, "guild-bunk"),
            };
            int capacity = 0;
            foreach (var row in rows) capacity = Math.Max(capacity, (int)row.Kind + 1);
            var byId = new FurnitureKindDefinition[capacity];
            foreach (var row in rows)
            {
                if (row.Kind == FurnitureKind.None) throw new ArgumentException("A furniture row needs a kind.");
                if (byId[(int)row.Kind] != null) throw new ArgumentException($"Two rows for {row.Kind}.");
                byId[(int)row.Kind] = row;
            }
            return byId;
        }

        /// <summary>The row of <paramref name="kind"/>, or null for a kind with no row.</summary>
        public static FurnitureKindDefinition Get(FurnitureKind kind) => (int)kind < ById.Length ? ById[(int)kind] : null;

        /// <summary>Every row, in kind order.</summary>
        public static System.Collections.Generic.IReadOnlyList<FurnitureKindDefinition> All { get; } = Array.FindAll(ById, r => r != null);
    }
}
