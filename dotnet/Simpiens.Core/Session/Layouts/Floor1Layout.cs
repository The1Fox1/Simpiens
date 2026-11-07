using System.Collections.Generic;
using Simpiens.Simulation.Economy;
using Simpiens.Simulation.Items;
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
    ///   y-18   +----D-----+-------D--+----------+
    ///          | Drowned  | Workshop D Guildhall|   the Drowned Gallery (Epic 10 G5, D11): low and long, below the
    ///   y-24   +-Gallery--+---Row----+----------+   Sump; too dangerous to go alone, its glowstone seam at the far end.
    ///                                               The workshop row and the guildhall (Epic 10 Part 2, D4), where the
    ///                                               rock was: the lane runs on from the gate; the guildhall lies past it
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
        public const byte WorkshopRowSectorId = 12;
        public const byte GuildhallSectorId = 13;

        /// <summary>
        /// The hazard sectors' danger (Epic 10 G2): health lost per second by a crawler standing in them. On the 24-hour day
        /// (Epic 10 Part 2, T3.2a) a quarter of Part 1's figures, since ventures last game hours: the Sump about 32 health a
        /// game hour, the Glow Caves 42.
        /// </summary>
        public static readonly SectorHazard SumpHazard = new SectorHazard(HazardKind.FoulAir, 0.75f);
        public static readonly SectorHazard GlowCavesHazard = new SectorHazard(HazardKind.FallingRock, 1f);

        /// <summary>1.5 health a second, 0.75 with gear: past the 1 a second a crawler braves alone (Vitality.LoneHazardLimit), so only an expedition goes (Epic 10 G5, D11).</summary>
        public static readonly SectorHazard DrownedGalleryHazard = new SectorHazard(HazardKind.Flooding, 1.5f);

        /// <summary>The Gallery's one rich glowstone seam (D11), at its far west end: a party crosses the flood to reach it.</summary>
        public const string GallerySeam = "gallery.seam";
        public const int GallerySeamYield = 4;
        public const uint GlowstoneRegrowTicks = 6000; // ten minutes: rare (Epic 10 G2)

        /// <summary>
        /// The Garden (review §3.3 D6): 4 plots of 4 rations, regrowing in 300 ticks. On the 24-hour day crawlers buy nine
        /// meals in ten at the stall, and regrowing in 1 or 12 game hours made no difference (Epic 10 Part 2, T4.2, D26),
        /// so the Garden stays the quick fallback it was.
        /// </summary>
        public const uint FoodRegrowTicks = 300;
        public const int FoodPlotYield = 4;
        public const uint HerbRegrowTicks = 600;
        public const uint OreRegrowTicks = 900;

        public const string Stall = "hub.stall";
        public const string Hearth = "hub.hearth";

        /// <summary>Where the forge and the still may be built (Epic 10 Part 2, Q2, Q10): the workshop row's west half, clear of the lane.</summary>
        public const string Forge = "workshop.forge";
        public const string Still = "workshop.still";
        public const float StationRadius = 1.5f;

        public const int SpawnPointCount = 12;

        /// <summary>A bunk per resident (Epic 10 Part 2, T2.3, D21): on the 24-hour day a sleeper holds its bunk about 8 hours.</summary>
        public const int BunkCount = 12;

        /// <summary>The guild's barracks (Epic 10 Part 2, W5.2, D62): a bunk kept for each of its eight members, by D21's reckoning.</summary>
        public const int GuildBunkCount = 8;

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
            new Vec2(4f, -18f),   // Road Gate - Workshop Row (Epic 10 Part 2): the lane runs on from the gate
            new Vec2(8f, -20.5f), // Workshop Row - Guildhall: the guildhall's only door
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
            b.WallRun(new Vec2(24f, -24f), new Vec2(24f, 17f));

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
            // y = -18: the lower wing's floor, with the Sump's door down to the Drowned Gallery (x -19..-13, Epic 10 G5) and
            // the Road Gate's down to the workshop row (x 1..7, in line with the lane; Epic 10 Part 2). The Glow Caves keep
            // a solid floor: the guildhall below them is reached only through the workshop row, never through a hazard
            b.WallRun(new Vec2(-24f, -18f), new Vec2(-19f, -18f));
            b.WallRun(new Vec2(-13f, -18f), new Vec2(1f, -18f));
            b.WallRun(new Vec2(7f, -18f), new Vec2(24f, -18f));
            // The bottom row, 6 deep (Floor 1 still fits a 1280x720 window whole at one screen pixel per art pixel,
            // top-down): the Drowned Gallery, walled off from the workshop row; the workshop row and the guildhall, where
            // the solid rock was (D4), parted by a stub that leaves a door three cells wide (y -22..-19)
            b.WallRun(new Vec2(-8f, -24f), new Vec2(-8f, -18f));
            b.WallRun(new Vec2(-24f, -24f), new Vec2(24f, -24f));
            b.WallRun(new Vec2(8f, -24f), new Vec2(8f, -23f));
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
            b.Sector(WorkshopRowSectorId, "Workshop Row", "Plots for a forge and a still, where trusted hands turn the stall's salvage into tools, gear and draughts.",
                new RectF(-8f, -24f, 16f, 6f), facility: true);
            b.Sector(GuildhallSectorId, "Guildhall", "The guild's own hall past the workshop row: barracks where its members sleep, and a yard by the door.",
                new RectF(8f, -24f, 16f, 6f), facility: true);

            // --- Hub ---
            b.Poi(Stall, PoiKind.Stall, new Vec2(0f, 2.5f), 3.0f);
            b.Poi(Hearth, PoiKind.Hearth, new Vec2(-4.5f, -3.5f), 3.0f);

            // --- Bunkhouse: two rows of six in the west half, 1.5 apart, clear of the gate-to-hub lane (x 2..6), with the
            // walkable strip along the north wall (y -7.5) kept free: it is the way from the bunks to the hub door, and a
            // sleeper lies in its bunk for hours (Epic 10 Part 2, T4.2: a top row there walled a crawler in until it starved) ---
            for (int i = 0; i < BunkCount; i++)
            {
                b.Poi($"bunk.{i + 1}", PoiKind.Bunk, new Vec2(-6.5f + (i % 6) * 1.5f, i < 6 ? -8.5f : -10.5f), 1.0f);
            }

            // --- Guildhall: the barracks, two rows of four in its east half, laid out as the bunkhouse is (1.5 apart, the
            // walkable strip along the north wall kept free), a bunk kept for each member (Epic 10 Part 2, W5.2, D62). The
            // west half, inside the door, is the yard (D64: the muster stays at the stall) ---
            for (int i = 0; i < GuildBunkCount; i++)
            {
                b.Poi($"barracks.{i + 1}", PoiKind.GuildBunk, new Vec2(16.5f + (i % 4) * 1.5f, i < 4 ? -20.5f : -22.5f), 1.0f);
            }

            // --- Workshop row: where the forge and the still may be built (Epic 10 Part 2); nothing stands there yet ---
            b.Station(Forge, StationKind.Forge, new Vec2(-5f, -21f), StationRadius);
            b.Station(Still, StationKind.Still, new Vec2(-1f, -21f), StationRadius);

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
            b.Label("Workshop Row", new Vec2(-3f, -19.4f), new Rgba(0.95f, 0.7f, 0.45f));
            b.Label("Guildhall", new Vec2(16f, -19.4f), new Rgba(0.75f, 0.65f, 0.95f));

            return b.Build();
        }
    }
}
