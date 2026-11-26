using Simpiens.Simulation.Economy;
using Simpiens.Simulation.World;

namespace Simpiens.Testing.Sessions
{
    /// <summary>
    /// The Dev Map: the 10 demonstration play pens (Epics 7, Beta, 8), test-only since Epic MAP Phase 2. Walls,
    /// sectors and props are transcribed cell for cell from the former live pen arena (parity proven in Phase 1);
    /// the Outpost Commons yard became the open-yard test fixture CommonsYardFixture. Below the pens, the building yard (Epic
    /// PLACES plan 1, B.2): a closed zone holding a small standing building and an empty lot, for the validators and the renders.
    /// </summary>
    public static class DevMapLayout
    {
        // Regrowth durations in logical ticks
        public const uint FoodRegrowTicks = 450;

        // Entity keys the pen scripts look up
        public const string Pen1ContestedBush = "pen1.contested";
        public const string Pen2ContestedBush = "pen2.contested";
        public const string Pen3ContestedBush = "pen3.contested";
        public const string Pen4Campfire = "pen4.campfire";
        public const string Pen5Campfire = "pen5.campfire";
        public const string Pen7Bush = "pen7.bush";
        public const string Pen8Campfire = "pen8.campfire";
        public const string Pen9Bush = "pen9.bush";
        public const string Pen10Bush = "pen10.bush";

        /// <summary>The building yard's zone (Epic PLACES plan 1, B.2): below the pens.</summary>
        public const byte BuildingYardSectorId = 11;

        /// <summary>The yard's small standing building, a barracks, its door on its north edge.</summary>
        public const string YardBarracks = "yard.barracks";

        /// <summary>The yard's empty lot, its door on its north edge.</summary>
        public const string YardLot = "yard.lot";

        /// <summary>Where the camera frames the pens: the arena before the building yard was added below them.</summary>
        public static readonly RectF PenFrame = new RectF(-12f, -14.5f, 24f, 20f);

        // Pen grid: dividers between and around the 5 pens of each row
        private static readonly float[] DividerX = { -10f, -6f, -2f, 2f, 6f, 10f };

        private static MapDefinition _definition;

        /// <summary>Built once; definitions are immutable.</summary>
        public static MapDefinition Definition => _definition ??= Build();

        private static MapDefinition Build()
        {
            // The two pen rows (walls span x -10.55..10.55, y -13.05..4.05) and the building yard below them (y -26.5..-14.5), plus a
            // margin. The pens and the yard are closed, so the bounds only size the partition; the camera frames the pens.
            var arena = new RectF(-12f, -28f, 24f, 33.5f);
            var b = new MapDefinitionBuilder("DevMap", arena).CameraFrame(PenFrame);

            // --- Row 1 (Pens 1-5: Epics 7 & Beta), walls on y = ±3.5 ---
            b.WallRun(new Vec2(-10f, 3.5f), new Vec2(10f, 3.5f));
            b.WallRun(new Vec2(-10f, -3.5f), new Vec2(10f, -3.5f));
            for (int d = 0; d < DividerX.Length; d++)
            {
                b.WallRun(new Vec2(DividerX[d], -2.5f), new Vec2(DividerX[d], 2.5f));
            }

            // --- Row 2 (Pens 6-10: Epic 8), walls on y = -5.5 and -12.5 ---
            b.WallRun(new Vec2(-10f, -5.5f), new Vec2(10f, -5.5f));
            b.WallRun(new Vec2(-10f, -12.5f), new Vec2(10f, -12.5f));
            for (int d = 0; d < DividerX.Length; d++)
            {
                b.WallRun(new Vec2(DividerX[d], -11.5f), new Vec2(DividerX[d], -6.5f));
            }

            // --- Sectors (former facility zones; first match wins, so order matters) ---
            b.Sector(1, "Perimeter West", "Security perimeter, outer observation post, and quarantine gate.",
                new RectF(-10f, -3.5f, 4f, 7f), new Vec2(-8f, 0f), 2.5f);
            b.Sector(2, "Berry Orchard", "Hydroponics foraging rows and biological resource cultivation.",
                new RectF(-6f, -3.5f, 4f, 7f), new Vec2(-4f, 0f), 2.5f);
            b.Sector(3, "Central Corridor", "High-traffic central nexus connecting east and west wings.",
                new RectF(-2f, -3.5f, 4f, 7f), new Vec2(0f, 0f), 2.5f);
            b.Sector(4, "Common Hearth", "Campfire circle, relaxation lounge, and communal gossip site.",
                new RectF(2f, -3.5f, 4f, 7f), new Vec2(4f, 0f), 3.0f);
            b.Sector(5, "East Quarters", "Living bunks, private retreat chambers, and resting quarters.",
                new RectF(6f, -3.5f, 4f, 7f), new Vec2(8f, 0f), 3.0f);
            // Every pen starts charted: the Dev Map has no scouts, and under the fog of war (Epic MAP Phase 3) an
            // uncharted pen would hide its own props from the agents inside it
            b.Sector(6, "Directive Sector", "Collective rallying point, military standard, and discipline drills.",
                new RectF(-10f, -12.5f, 4f, 7f), new Vec2(-8f, -9f), 2.0f);
            b.Sector(7, "Defiance Sector", "Starvation defiance, passive desertion, and grievance post.",
                new RectF(-6f, -12.5f, 4f, 7f), new Vec2(-4f, -9f), 2.0f);
            b.Sector(8, "Emergence Hearth", "Organic hearth emergence, campfire consensus, and settlement founding.",
                new RectF(-2f, -12.5f, 4f, 7f), new Vec2(0f, -9f), 2.0f);
            b.Sector(9, "Succession Arena", "Leadership succession contest, dominance trial, and power transition.",
                new RectF(2f, -12.5f, 4f, 7f), new Vec2(4f, -9f), 2.0f);
            b.Sector(10, "Schism Ward", "Crisis of legitimacy, factional fracture, and splinter bands.",
                new RectF(6f, -12.5f, 4f, 7f), new Vec2(8f, -9f), 2.0f);

            // --- The building yard (Epic PLACES plan 1, B.2): a closed zone, a barracks and an empty lot side by side, on the pens'
            // wall grid (half units in y) ---
            b.WallRun(new Vec2(-10f, -14.5f), new Vec2(10f, -14.5f));
            b.WallRun(new Vec2(-10f, -26.5f), new Vec2(10f, -26.5f));
            b.WallRun(new Vec2(-10f, -26.5f), new Vec2(-10f, -14.5f));
            b.WallRun(new Vec2(10f, -26.5f), new Vec2(10f, -14.5f));
            b.Sector(BuildingYardSectorId, "Building Yard", "A barracks and an empty lot, for buildings' checks and looks.",
                new RectF(-10f, -26.5f, 20f, 12f), new Vec2(0f, -16.5f), 1.5f);
            b.Building(YardBarracks, Simpiens.Simulation.Places.BuildingKind.Barracks, new RectF(-9f, -24.5f, 8f, 6f), new Vec2(-5f, -18.5f));
            b.Lot(YardLot, new RectF(1f, -24.5f, 8f, 6f), new Vec2(5f, -18.5f));

            // --- Pen demonstration props ---
            b.Node(Pen1ContestedBush, new Vec2(-8f, 0f), ItemType.Ration, 1, FoodRegrowTicks);
            b.Node("pen1.forage", new Vec2(-8f, -2.2f), ItemType.Ration, 3, FoodRegrowTicks);
            b.Node(Pen2ContestedBush, new Vec2(-4f, 0f), ItemType.Ration, 1, FoodRegrowTicks);
            b.Node("pen2.forage", new Vec2(-4f, -2.2f), ItemType.Ration, 3, FoodRegrowTicks);
            b.Node(Pen3ContestedBush, new Vec2(0f, 0f), ItemType.Ration, 1, FoodRegrowTicks);
            b.Node(Pen7Bush, new Vec2(-4f, -11.0f), ItemType.Ration, 3, FoodRegrowTicks);
            b.Node(Pen9Bush, new Vec2(4f, -11.0f), ItemType.Ration, 5, FoodRegrowTicks);
            b.Node(Pen10Bush, new Vec2(8f, -11.0f), ItemType.Ration, 5, FoodRegrowTicks);
            b.Poi(Pen4Campfire, PoiKind.Hearth, new Vec2(4f, 0f), 3.0f);
            b.Poi(Pen5Campfire, PoiKind.Hearth, new Vec2(8f, 0f), 3.0f);
            b.Poi(Pen8Campfire, PoiKind.Hearth, new Vec2(0f, -9.0f), 3.0f);

            // --- Labels ---
            b.Label("1. Fear Yield", new Vec2(-8.0f, 4.2f), new Rgba(1f, 0.75f, 0.2f));
            b.Label("2. Respect Defer", new Vec2(-4.0f, 4.2f), new Rgba(0.35f, 0.8f, 1f));
            b.Label("3. Rivalry Contest", new Vec2(0.0f, 4.2f), new Rgba(1f, 0.4f, 0.4f));
            b.Label("4. Altruism & Hearth", new Vec2(4.0f, 4.2f), new Rgba(0.3f, 0.95f, 0.5f));
            b.Label("5. Selfish Peer", new Vec2(8.0f, 4.2f), new Rgba(0.75f, 0.8f, 0.85f));
            b.Label("6. Directive Rally", new Vec2(-8.0f, -4.8f), new Rgba(1f, 0.85f, 0.25f));
            b.Label("7. Passive Defiance", new Vec2(-4.0f, -4.8f), new Rgba(0.95f, 0.3f, 0.3f));
            b.Label("8. Hearth Emergence", new Vec2(0.0f, -4.8f), new Rgba(1f, 0.6f, 0.2f));
            b.Label("9. Dominance Trial", new Vec2(4.0f, -4.8f), new Rgba(0.8f, 0.4f, 0.9f));
            b.Label("10. Factional Schism", new Vec2(8.0f, -4.8f), new Rgba(0.2f, 0.85f, 0.85f));

            return b.Build();
        }
    }
}
