using Simpiens.Simulation.Economy;
using Simpiens.Simulation.World;

namespace Simpiens.Testing.World
{
    /// <summary>
    /// Test fixture: the open Outpost Commons yard, the live map of Epic MAP Phase 2 (retired from production when
    /// Floor 1 went live in Phase 3). Validators that hand-place agents and need sectors with no walls between
    /// them use it: three sectors (one explored, two shrouded) and a stall in an open field.
    ///
    ///          x: -12      -5  -3    3  5       12
    ///   y 15   +--------+   +------+   +--------+
    ///          |  Ore   |   |Market|   |  Herb  |   open yard, no interior walls
    ///          |  Ridge |   | stall|   | Hollow |   Ore Ridge and Herb Hollow start shrouded
    ///   y  5   +--------+   +------+   +--------+
    /// </summary>
    public static class CommonsYardFixture
    {
        // Sector ids (bounty targets and scenarios refer to these)
        public const byte OutpostMarketSectorId = 11;
        public const byte OreRidgeSectorId = 12;
        public const byte HerbHollowSectorId = 13;

        // Regrowth durations in logical ticks
        public const uint FoodRegrowTicks = 450;
        public const uint HerbRegrowTicks = 600;
        public const uint OreRegrowTicks = 900;

        public const string OutpostStall = "outpost.stall";

        public static readonly Vec2 OutpostStallPosition = new Vec2(0f, 9f);

        private static MapDefinition _definition;

        /// <summary>Built once; definitions are immutable.</summary>
        public static MapDefinition Definition => _definition ??= Build();

        private static MapDefinition Build()
        {
            // The yard (sectors span x -12..12, y 5..15) plus a 2-unit margin; the map perimeter is the world edge
            var yard = new RectF(-14f, 3f, 28f, 14f);
            var b = new MapDefinitionBuilder("OutpostCommons", yard).CameraFrame(yard);

            // --- Sectors (first match wins, so order matters) ---
            b.Sector(OutpostMarketSectorId, "Outpost Market", "Trade stall, bounty board, and the commons where crawlers gather between runs.",
                new RectF(-3f, 5f, 6f, 10f), new Vec2(0f, 9f), 3.0f);
            b.Sector(OreRidgeSectorId, "Ore Ridge", "Exposed iron veins along the western ridge; regrow slowly after mining.",
                new RectF(-12f, 5f, 7f, 10f), new Vec2(-8.5f, 10f), 2.5f, startsExplored: false);
            b.Sector(HerbHollowSectorId, "Herb Hollow", "Sheltered eastern hollow where medicinal herbs grow back between harvests.",
                new RectF(5f, 5f, 7f, 10f), new Vec2(8.5f, 10f), 2.5f, startsExplored: false);

            // --- Resources ---
            b.Node("ore.1", new Vec2(-9f, 12f), ItemType.IronOre, 3, OreRegrowTicks);
            b.Node("ore.2", new Vec2(-8f, 9.5f), ItemType.IronOre, 3, OreRegrowTicks);
            b.Node("ore.3", new Vec2(-10f, 8f), ItemType.IronOre, 3, OreRegrowTicks);
            b.Node("herb.1", new Vec2(9f, 12f), ItemType.MedicinalHerb, 3, HerbRegrowTicks);
            b.Node("herb.2", new Vec2(8f, 9.5f), ItemType.MedicinalHerb, 3, HerbRegrowTicks);
            b.Node("herb.3", new Vec2(10f, 8f), ItemType.MedicinalHerb, 3, HerbRegrowTicks);
            b.Node("food.1", new Vec2(-2f, 6.5f), ItemType.Ration, 3, FoodRegrowTicks);
            b.Node("food.2", new Vec2(2f, 6.5f), ItemType.Ration, 3, FoodRegrowTicks);
            b.Poi(OutpostStall, PoiKind.Stall, OutpostStallPosition, 3.0f);

            // --- Labels ---
            b.Label("Ore Ridge", new Vec2(-8.5f, 14.5f), new Rgba(0.95f, 0.55f, 0.2f));
            b.Label("Outpost Market", new Vec2(0f, 14.5f), new Rgba(0.95f, 0.85f, 0.4f));
            b.Label("Herb Hollow", new Vec2(8.5f, 14.5f), new Rgba(0.45f, 0.9f, 0.8f));

            return b.Build();
        }
    }
}
