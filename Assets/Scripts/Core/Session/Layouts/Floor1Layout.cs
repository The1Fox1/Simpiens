using Simpiens.Simulation.Economy;
using Simpiens.Simulation.World;
using UnityEngine;

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
    ///          |  Ore     D   Hub    D   Herb   |
    ///          |  Quarry  |stall+fire|   Grove  |
    ///   y -6   +----------+----D-----+----------+
    ///          |   rock   | Bunkhouse|   rock   |
    ///   y-12   |          +----D-----+          |
    ///          |          | Road Gate|          |
    ///   y-18   +----------+----------+----------+
    ///
    /// Wall centre lines sit on whole units; every doorway leaves 4 walkable cells after body inflation.
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

        public const uint FoodRegrowTicks = 450;
        public const uint HerbRegrowTicks = 600;
        public const uint OreRegrowTicks = 900;

        public const string Stall = "hub.stall";
        public const string Hearth = "hub.hearth";

        private static MapDefinition _definition;

        /// <summary>Built once; definitions are immutable.</summary>
        public static MapDefinition Definition => _definition ??= Build();

        private static MapDefinition Build()
        {
            var b = new MapDefinitionBuilder("Floor1", new Rect(-24f, -18f, 48f, 36f));

            // --- Outer walls of the 3x2 upper block ---
            b.WallRun(new Vector2(-24f, 18f), new Vector2(24f, 18f));
            b.WallRun(new Vector2(-24f, -6f), new Vector2(-24f, 17f));
            b.WallRun(new Vector2(24f, -6f), new Vector2(24f, 17f));

            // y = 6: frontier row / work row. Doors: Deep Seam-Ore Quarry (x -16), Garden-Hub (x 0), Overgrowth-Herb Grove (x 16)
            b.WallRun(new Vector2(-24f, 6f), new Vector2(-19f, 6f));
            b.WallRun(new Vector2(-13f, 6f), new Vector2(-3f, 6f));
            b.WallRun(new Vector2(3f, 6f), new Vector2(13f, 6f));
            b.WallRun(new Vector2(19f, 6f), new Vector2(24f, 6f));

            // x = ±8 column walls in the upper block. Doors only Ore Quarry-Hub and Hub-Herb Grove (y 0):
            // the frontier sectors are reached through the near work sectors, never directly from the Garden
            b.WallRun(new Vector2(-8f, -6f), new Vector2(-8f, -3f));
            b.WallRun(new Vector2(-8f, 3f), new Vector2(-8f, 17f));
            b.WallRun(new Vector2(8f, -6f), new Vector2(8f, -3f));
            b.WallRun(new Vector2(8f, 3f), new Vector2(8f, 17f));

            // y = -6: bottom of the upper block. Door: Hub-Bunkhouse (x 0)
            b.WallRun(new Vector2(-24f, -6f), new Vector2(-3f, -6f));
            b.WallRun(new Vector2(3f, -6f), new Vector2(24f, -6f));

            // Lower wing (Bunkhouse over Road Gate). Door: Bunkhouse-Road Gate (x 0)
            b.WallRun(new Vector2(-8f, -18f), new Vector2(-8f, -7f));
            b.WallRun(new Vector2(8f, -18f), new Vector2(8f, -7f));
            b.WallRun(new Vector2(-8f, -18f), new Vector2(8f, -18f));
            b.WallRun(new Vector2(-8f, -12f), new Vector2(-3f, -12f));
            b.WallRun(new Vector2(3f, -12f), new Vector2(8f, -12f));

            // Solid rock either side of the lower wing (inside the playable rect, outside the floor)
            b.Wall(Rect.MinMaxRect(-24.5f, -18.5f, -8.5f, -6.5f));
            b.Wall(Rect.MinMaxRect(8.5f, -18.5f, 24.5f, -6.5f));

            // --- Sectors (bounds meet on wall centre lines; no overlaps) ---
            b.Sector(HubSectorId, "Outpost Hub", "Trade stall, bounty board and the common hearth where crawlers gather.",
                new Rect(-8f, -6f, 16f, 12f));
            b.Sector(BunkhouseSectorId, "Bunkhouse", "Quiet bunks where tired crawlers sleep off a long run.",
                new Rect(-8f, -12f, 16f, 6f));
            b.Sector(GardenSectorId, "Garden", "Ration plots that regrow between harvests: the outpost's baseline food.",
                new Rect(-8f, 6f, 16f, 12f));
            b.Sector(OreQuarrySectorId, "Ore Quarry", "Worked iron veins a short walk west of the hub.",
                new Rect(-24f, -6f, 16f, 12f));
            b.Sector(HerbGroveSectorId, "Herb Grove", "Medicinal herb patches a short walk east of the hub.",
                new Rect(8f, -6f, 16f, 12f));
            b.Sector(DeepSeamSectorId, "Deep Seam", "Uncharted rich iron seams beyond the quarry.",
                new Rect(-24f, 6f, 16f, 12f), new Vector2(-16f, 12f), 0f, startsExplored: false);
            b.Sector(OvergrowthSectorId, "Overgrowth", "Uncharted overgrown ruins beyond the grove, thick with herbs.",
                new Rect(8f, 6f, 16f, 12f), new Vector2(16f, 12f), 0f, startsExplored: false);
            b.Sector(RoadGateSectorId, "Road Gate", "Where the road meets the outpost; crawlers arrive here.",
                new Rect(-8f, -18f, 16f, 6f));

            // --- Hub ---
            b.Poi(Stall, PoiKind.Stall, new Vector2(0f, 2.5f), 3.0f);
            b.Poi(Hearth, PoiKind.Hearth, new Vector2(0f, -3f), 3.0f);

            // --- Bunkhouse ---
            b.Poi("bunk.1", PoiKind.Bunk, new Vector2(-5f, -9f), 1.0f);
            b.Poi("bunk.2", PoiKind.Bunk, new Vector2(-2f, -9f), 1.0f);
            b.Poi("bunk.3", PoiKind.Bunk, new Vector2(2f, -9f), 1.0f);
            b.Poi("bunk.4", PoiKind.Bunk, new Vector2(5f, -9f), 1.0f);

            // --- Road Gate spawn points ---
            for (int i = 0; i < 6; i++)
            {
                b.Poi($"gate.spawn.{i + 1}", PoiKind.SpawnPoint, new Vector2(-5f + i * 2f, -15f));
            }

            // --- Resources: near sectors (explored) ---
            b.Node("garden.1", new Vector2(-5f, 12f), ItemType.Ration, 3, FoodRegrowTicks);
            b.Node("garden.2", new Vector2(-2f, 10f), ItemType.Ration, 3, FoodRegrowTicks);
            b.Node("garden.3", new Vector2(2f, 10f), ItemType.Ration, 3, FoodRegrowTicks);
            b.Node("garden.4", new Vector2(5f, 12f), ItemType.Ration, 3, FoodRegrowTicks);
            b.Node("quarry.1", new Vector2(-20f, 2f), ItemType.IronOre, 3, OreRegrowTicks);
            b.Node("quarry.2", new Vector2(-16f, -2f), ItemType.IronOre, 3, OreRegrowTicks);
            b.Node("quarry.3", new Vector2(-12f, 2f), ItemType.IronOre, 3, OreRegrowTicks);
            b.Node("grove.1", new Vector2(12f, 2f), ItemType.MedicinalHerb, 3, HerbRegrowTicks);
            b.Node("grove.2", new Vector2(16f, -2f), ItemType.MedicinalHerb, 3, HerbRegrowTicks);
            b.Node("grove.3", new Vector2(20f, 2f), ItemType.MedicinalHerb, 3, HerbRegrowTicks);

            // --- Resources: frontier sectors (shrouded, richer) ---
            b.Node("seam.1", new Vector2(-20f, 14f), ItemType.IronOre, 6, OreRegrowTicks);
            b.Node("seam.2", new Vector2(-16f, 10f), ItemType.IronOre, 6, OreRegrowTicks);
            b.Node("seam.3", new Vector2(-12f, 14f), ItemType.IronOre, 6, OreRegrowTicks);
            b.Node("overgrowth.1", new Vector2(12f, 14f), ItemType.MedicinalHerb, 6, HerbRegrowTicks);
            b.Node("overgrowth.2", new Vector2(16f, 10f), ItemType.MedicinalHerb, 6, HerbRegrowTicks);
            b.Node("overgrowth.3", new Vector2(20f, 14f), ItemType.MedicinalHerb, 6, HerbRegrowTicks);

            // --- Labels (sector names near each sector's top edge) ---
            b.Label("Outpost Hub", new Vector2(0f, 4.6f), new Color(0.95f, 0.85f, 0.4f));
            b.Label("Bunkhouse", new Vector2(0f, -7.4f), new Color(0.7f, 0.75f, 0.95f));
            b.Label("Garden", new Vector2(0f, 16.6f), new Color(0.45f, 0.9f, 0.45f));
            b.Label("Ore Quarry", new Vector2(-16f, 4.6f), new Color(0.95f, 0.55f, 0.2f));
            b.Label("Herb Grove", new Vector2(16f, 4.6f), new Color(0.45f, 0.9f, 0.8f));
            b.Label("Deep Seam", new Vector2(-16f, 16.6f), new Color(0.8f, 0.5f, 0.3f));
            b.Label("Overgrowth", new Vector2(16f, 16.6f), new Color(0.35f, 0.75f, 0.6f));
            b.Label("Road Gate", new Vector2(0f, -13.4f), new Color(0.8f, 0.8f, 0.8f));

            return b.Build();
        }
    }
}
