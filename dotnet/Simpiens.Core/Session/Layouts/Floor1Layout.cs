using System.Collections.Generic;
using Simpiens.Simulation.Economy;
using Simpiens.Simulation.World;

namespace Simpiens.Core.Session.Layouts
{
    /// <summary>
    /// The Outpost Floor (Epic MAP Revision 2, §2.2): one connected floor where crawlers work, sell, eat, socialize
    /// and sleep. Near work sectors start explored; the two frontier sectors start shrouded, sit behind the near
    /// sectors, and hold richer nodes. Validated headlessly in Phase 1; spawned as the live map in Phase 3.
    ///
    ///          x: -24        -8         8         24
    ///   y 18   +----------+----------+----------+
    ///          | Deep Seam|  Garden  |Overgrowth|   frontier sectors start shrouded
    ///   y  6   +---D------+----D-----+------D---+
    ///          |  Ore     D  stall   D   Herb   |   quarry and grove doors sit beside the stall
    ///          |  Quarry  |fire      |   Grove  |   the hearth has the hub's quiet south-west corner
    ///   y -6   +---D------+-------D--+------D---+
    ///          | The Sump |bunks    D|Glow Caves|   one straight lane, gate to hub, along the east side;
    ///   y-12   | foul air +-------D--+ rockfall |   the Sump and the Glow Caves hurt whoever stays (Epic 10 G2)
    ///          |          | Road Gate|          |
    ///   y-18   +----D-----+----------+----------+
    ///          | Drowned  |   solid rock        |   the Drowned Gallery (Epic 10 G5, D11): low and long, below the
    ///   y-24   +-Gallery--+---------------------+   Sump; too dangerous to go alone, its glowstone seam at the far end
    ///
    /// Wall centre lines sit on whole units; every doorway leaves 4 walkable cells after body inflation. Doors are
    /// placed so that no through-route crosses the hearth's gathering ring or the bunks (Epic MAP Phase 3).
    /// </summary>
    public static class Floor1Layout
    {
        public const byte HubSectorId = 1;
        public const byte BunkhouseSectorId = 2;
        public const byte GardenSectorId = 3;
        public const byte OreQuarrySectorId = 4;
        public const byte HerbGroveSectorId = 5;
        public const byte DeepSeamSectorId = 6;
        public const byte OvergrowthSectorId = 7;
        public const byte RoadGateSectorId = 8;
        public const byte SumpSectorId = 9;
        public const byte GlowCavesSectorId = 10;
        public const byte DrownedGallerySectorId = 11;

        /// <summary>The hazard sectors' danger (Epic 10 G2): health lost per second by a crawler standing in them.</summary>
        public static readonly SectorHazard SumpHazard = new SectorHazard(HazardKind.FoulAir, 3f);
        public static readonly SectorHazard GlowCavesHazard = new SectorHazard(HazardKind.FallingRock, 4f);

        /// <summary>6 health a second, 3 with gear: past the 4 a second a crawler braves alone (Vitality.LoneHazardLimit), so only an expedition goes (Epic 10 G5, D11).</summary>
        public static readonly SectorHazard DrownedGalleryHazard = new SectorHazard(HazardKind.Flooding, 6f);

        /// <summary>The Gallery's one rich glowstone seam (D11), at its far west end: a party crosses the flood to reach it.</summary>
        public const string GallerySeam = "gallery.seam";
        public const int GallerySeamYield = 4;
        public const uint GlowstoneRegrowTicks = 6000; // ten minutes: rare (Epic 10 G2)

        public const uint FoodRegrowTicks = 300; // review §3.3 D6 (was 450)
        public const int FoodPlotYield = 4;      // rations per plot between regrowths (review §3.3 D6; was 3): 4 plots, about 32 a minute at most
        public const uint HerbRegrowTicks = 600;
        public const uint OreRegrowTicks = 900;

        public const string Stall = "hub.stall";
        public const string Hearth = "hub.hearth";

        public const int SpawnPointCount = 12;
        public const int BunkCount = 8;

        /// <summary>Doorway centres (on wall centre lines). Validators and scenarios read doors from here.</summary>
        public static readonly IReadOnlyList<Vec2> Doors = new[]
        {
            new Vec2(-16f, 6f),  // Deep Seam - Ore Quarry
            new Vec2(0f, 6f),    // Garden - Hub
            new Vec2(16f, 6f),   // Overgrowth - Herb Grove
            new Vec2(-8f, 3f),   // Ore Quarry - Hub
            new Vec2(8f, 3f),    // Hub - Herb Grove
            new Vec2(4f, -6f),   // Hub - Bunkhouse
            new Vec2(4f, -12f),  // Bunkhouse - Road Gate
            new Vec2(-16f, -6f), // Ore Quarry - The Sump
            new Vec2(16f, -6f),  // Herb Grove - Glow Caves
            new Vec2(-16f, -18f), // The Sump - The Drowned Gallery (Epic 10 G5)
        };

        public static Vec2 HubBunkhouseDoor => Doors[5];

        private static MapDefinition _definition;

        /// <summary>Built once; definitions are immutable.</summary>
        public static MapDefinition Definition => _definition ??= Build();

        private static MapDefinition Build()
        {
            var b = new MapDefinitionBuilder("Floor1", new RectF(-24f, -24f, 48f, 42f));

            // --- Outer walls of the 3x2 upper block ---
            b.WallRun(new Vec2(-24f, 18f), new Vec2(24f, 18f));
            b.WallRun(new Vec2(-24f, -24f), new Vec2(-24f, 17f));
            b.WallRun(new Vec2(24f, -18f), new Vec2(24f, 17f));

            // y = 6: frontier row / work row. Doors: Deep Seam-Ore Quarry (x -16), Garden-Hub (x 0), Overgrowth-Herb Grove (x 16)
            b.WallRun(new Vec2(-24f, 6f), new Vec2(-19f, 6f));
            b.WallRun(new Vec2(-13f, 6f), new Vec2(-3f, 6f));
            b.WallRun(new Vec2(3f, 6f), new Vec2(13f, 6f));
            b.WallRun(new Vec2(19f, 6f), new Vec2(24f, 6f));

            // x = ±8 column walls in the upper block. Doors only Ore Quarry-Hub and Hub-Herb Grove (y 0..6, beside
            // the stall): the frontier sectors are reached through the near work sectors, never from the Garden
            b.WallRun(new Vec2(-8f, -6f), new Vec2(-8f, 0f));
            b.WallRun(new Vec2(-8f, 6f), new Vec2(-8f, 17f));
            b.WallRun(new Vec2(8f, -6f), new Vec2(8f, 0f));
            b.WallRun(new Vec2(8f, 6f), new Vec2(8f, 17f));

            // y = -6: bottom of the upper block. Doors: Ore Quarry-Sump (x -19..-13), Hub-Bunkhouse (x 1..7, east of the
            // hearth), Herb Grove-Glow Caves (x 13..19)
            b.WallRun(new Vec2(-24f, -6f), new Vec2(-19f, -6f));
            b.WallRun(new Vec2(-13f, -6f), new Vec2(1f, -6f));
            b.WallRun(new Vec2(7f, -6f), new Vec2(13f, -6f));
            b.WallRun(new Vec2(19f, -6f), new Vec2(24f, -6f));

            // Lower wing (Bunkhouse over Road Gate). Door: Bunkhouse-Road Gate (x 1..7, in line with the hub door). Its
            // side walls also close the Sump and the Glow Caves, which were solid rock before Epic 10 G2
            b.WallRun(new Vec2(-8f, -18f), new Vec2(-8f, -6f));
            b.WallRun(new Vec2(8f, -18f), new Vec2(8f, -6f));
            // y = -18: the lower wing's floor, with the Sump's door down to the Drowned Gallery (x -19..-13, Epic 10 G5)
            b.WallRun(new Vec2(-24f, -18f), new Vec2(-19f, -18f));
            b.WallRun(new Vec2(-13f, -18f), new Vec2(24f, -18f));
            // The Drowned Gallery's east and south walls, and solid rock beside it under the Road Gate and the Glow Caves. Only
            // 6 deep: Floor 1 still fits a 1280x720 window whole at one screen pixel per art pixel, top-down
            b.WallRun(new Vec2(-8f, -24f), new Vec2(-8f, -18f));
            b.WallRun(new Vec2(-24f, -24f), new Vec2(-8f, -24f));
            b.Wall(new RectF(-7.45f, -24.55f, 32f, 6f));
            b.WallRun(new Vec2(-8f, -12f), new Vec2(1f, -12f));
            b.WallRun(new Vec2(7f, -12f), new Vec2(8f, -12f));

            // --- Sectors (bounds meet on wall centre lines; no overlaps) ---
            b.Sector(HubSectorId, "Outpost Hub", "Trade stall, bounty board and the common hearth where crawlers gather.",
                new RectF(-8f, -6f, 16f, 12f));
            b.Sector(BunkhouseSectorId, "Bunkhouse", "Quiet bunks where tired crawlers sleep off a long run.",
                new RectF(-8f, -12f, 16f, 6f));
            b.Sector(GardenSectorId, "Garden", "Ration plots that regrow between harvests: the outpost's baseline food.",
                new RectF(-8f, 6f, 16f, 12f));
            b.Sector(OreQuarrySectorId, "Ore Quarry", "Worked iron veins a short walk west of the hub.",
                new RectF(-24f, -6f, 16f, 12f));
            b.Sector(HerbGroveSectorId, "Herb Grove", "Medicinal herb patches a short walk east of the hub.",
                new RectF(8f, -6f, 16f, 12f));
            b.Sector(DeepSeamSectorId, "Deep Seam", "Uncharted rich iron seams beyond the quarry.",
                new RectF(-24f, 6f, 16f, 12f), new Vec2(-16f, 12f), 0f, startsExplored: false);
            b.Sector(OvergrowthSectorId, "Overgrowth", "Uncharted overgrown ruins beyond the grove, thick with herbs.",
                new RectF(8f, 6f, 16f, 12f), new Vec2(16f, 12f), 0f, startsExplored: false);
            b.Sector(RoadGateSectorId, "Road Gate", "Where the road meets the outpost; crawlers arrive here.",
                new RectF(-8f, -18f, 16f, 6f));
            b.Sector(SumpSectorId, "The Sump", "A flooded pit under the quarry. Its foul air sickens whoever lingers; glowstone crusts its walls.",
                new RectF(-24f, -18f, 16f, 12f), hazard: SumpHazard);
            b.Sector(GlowCavesSectorId, "Glow Caves", "Unstable caves under the grove, lit by glowstone. Loose rock falls on whoever stays.",
                new RectF(8f, -18f, 16f, 12f), hazard: GlowCavesHazard);
            b.Sector(DrownedGallerySectorId, "The Drowned Gallery", "A flooded gallery below the Sump, its air thick with water. Too dangerous to go alone; a rich glowstone seam rewards a party.",
                new RectF(-24f, -24f, 16f, 6f), hazard: DrownedGalleryHazard);

            // --- Hub ---
            b.Poi(Stall, PoiKind.Stall, new Vec2(0f, 2.5f), 3.0f);
            b.Poi(Hearth, PoiKind.Hearth, new Vec2(-4.5f, -3.5f), 3.0f);

            // --- Bunkhouse: two rows of four in the west half, clear of the gate-to-hub lane (x 2..6) ---
            for (int i = 0; i < BunkCount; i++)
            {
                b.Poi($"bunk.{i + 1}", PoiKind.Bunk, new Vec2(-6f + (i % 4) * 2f, i < 4 ? -8f : -10f), 1.0f);
            }

            // --- Road Gate spawn points: two rows of six ---
            for (int i = 0; i < SpawnPointCount; i++)
            {
                b.Poi($"gate.spawn.{i + 1}", PoiKind.SpawnPoint, new Vec2(-5f + (i % 6) * 2f, i < 6 ? -14f : -16f));
            }

            // --- Resources: near sectors (explored) ---
            b.Node("garden.1", new Vec2(-5f, 12f), ItemType.Ration, FoodPlotYield, FoodRegrowTicks);
            b.Node("garden.2", new Vec2(-2f, 10f), ItemType.Ration, FoodPlotYield, FoodRegrowTicks);
            b.Node("garden.3", new Vec2(2f, 10f), ItemType.Ration, FoodPlotYield, FoodRegrowTicks);
            b.Node("garden.4", new Vec2(5f, 12f), ItemType.Ration, FoodPlotYield, FoodRegrowTicks);
            b.Node("quarry.1", new Vec2(-20f, 2f), ItemType.IronOre, 3, OreRegrowTicks);
            b.Node("quarry.2", new Vec2(-16f, -2f), ItemType.IronOre, 3, OreRegrowTicks);
            b.Node("quarry.3", new Vec2(-12f, 2f), ItemType.IronOre, 3, OreRegrowTicks);
            b.Node("grove.1", new Vec2(12f, 2f), ItemType.MedicinalHerb, 3, HerbRegrowTicks);
            b.Node("grove.2", new Vec2(16f, -2f), ItemType.MedicinalHerb, 3, HerbRegrowTicks);
            b.Node("grove.3", new Vec2(20f, 2f), ItemType.MedicinalHerb, 3, HerbRegrowTicks);

            // --- Resources: frontier sectors (shrouded, richer) ---
            b.Node("seam.1", new Vec2(-20f, 14f), ItemType.IronOre, 6, OreRegrowTicks);
            b.Node("seam.2", new Vec2(-16f, 10f), ItemType.IronOre, 6, OreRegrowTicks);
            b.Node("seam.3", new Vec2(-12f, 14f), ItemType.IronOre, 6, OreRegrowTicks);
            b.Node("overgrowth.1", new Vec2(12f, 14f), ItemType.MedicinalHerb, 6, HerbRegrowTicks);
            b.Node("overgrowth.2", new Vec2(16f, 10f), ItemType.MedicinalHerb, 6, HerbRegrowTicks);
            b.Node("overgrowth.3", new Vec2(20f, 14f), ItemType.MedicinalHerb, 6, HerbRegrowTicks);

            // --- Resources: hazard sectors (Epic 10 G2): the rare salvage, for whoever dares ---
            b.Node("sump.1", new Vec2(-20f, -10f), ItemType.Glowstone, 1, GlowstoneRegrowTicks);
            b.Node("sump.2", new Vec2(-13f, -14f), ItemType.Glowstone, 1, GlowstoneRegrowTicks);
            b.Node("sump.3", new Vec2(-20f, -15f), ItemType.Glowstone, 1, GlowstoneRegrowTicks);
            b.Node("caves.1", new Vec2(20f, -10f), ItemType.Glowstone, 1, GlowstoneRegrowTicks);
            b.Node("caves.2", new Vec2(13f, -14f), ItemType.Glowstone, 1, GlowstoneRegrowTicks);
            b.Node("caves.3", new Vec2(20f, -15f), ItemType.Glowstone, 1, GlowstoneRegrowTicks);
            b.Node(GallerySeam, new Vec2(-21f, -21f), ItemType.Glowstone, GallerySeamYield, GlowstoneRegrowTicks); // only an expedition works it (D11)

            // --- Labels (sector names near each sector's top edge) ---
            b.Label("Outpost Hub", new Vec2(0f, 4.6f), new Rgba(0.95f, 0.85f, 0.4f));
            b.Label("Bunkhouse", new Vec2(0f, -7.4f), new Rgba(0.7f, 0.75f, 0.95f));
            b.Label("Garden", new Vec2(0f, 16.6f), new Rgba(0.45f, 0.9f, 0.45f));
            b.Label("Ore Quarry", new Vec2(-16f, 4.6f), new Rgba(0.95f, 0.55f, 0.2f));
            b.Label("Herb Grove", new Vec2(16f, 4.6f), new Rgba(0.45f, 0.9f, 0.8f));
            b.Label("Deep Seam", new Vec2(-16f, 16.6f), new Rgba(0.8f, 0.5f, 0.3f));
            b.Label("Overgrowth", new Vec2(16f, 16.6f), new Rgba(0.35f, 0.75f, 0.6f));
            b.Label("Road Gate", new Vec2(0f, -13.4f), new Rgba(0.8f, 0.8f, 0.8f));
            b.Label("The Sump", new Vec2(-16f, -7.4f), new Rgba(0.65f, 0.7f, 0.35f));
            b.Label("Glow Caves", new Vec2(16f, -7.4f), new Rgba(0.55f, 0.85f, 1f));
            b.Label("The Drowned Gallery", new Vec2(-16f, -19.4f), new Rgba(0.35f, 0.6f, 0.8f));

            return b.Build();
        }
    }
}
