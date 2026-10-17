using System;
using Simpiens.Core.Session.Layouts;
using Simpiens.Testing.Sessions;
using Simpiens.Simulation.Economy;
using Simpiens.Simulation.Spatial;
using Simpiens.Simulation.World;

namespace Simpiens.Testing.World
{
    /// <summary>
    /// Epic MAP Phase 1: loading a map into the simulation. Partition reconfiguration with retained snapshots,
    /// WorldMap.Load (bounds, walls, sectors, version), the sector registry, and null-dependency isolation.
    /// </summary>
    public class WorldMapValidator
    {
        // Dev Map pen sector 6 starts shrouded
        private const byte DirectiveSectorId = 6;

        public void RunAllValidationTests()
        {
            TestLog.Log("[WorldMapValidator] Beginning world map validation...");

            ValidatePartitionReconfigure();
            ValidateWorldMapLoad();
            ValidateSectorRegistry();
            ValidateNullDependencies();

            TestLog.Log("<color=green>[WorldMapValidator] ALL 4 TESTS PASSED SUCCESSFULLY!</color>");
        }

        private void ValidatePartitionReconfigure()
        {
            var partition = new SpatialHashGrid();
            Assert(partition.Configure(20f, 20f, 1f), "First Configure must build the partition.");
            Assert(!partition.Configure(20f, 20f, 1f), "Configure with unchanged dimensions must be a no-op.");
            var oldGrid = partition.StaticGrid;

            // A background reader retains this tick's snapshot across the reconfiguration
            partition.UpdateFromRegistry(null);
            var retained = partition.GetActiveSnapshot();
            retained.Retain();

            Assert(partition.Configure(30f, 24f, 1f), "Configure with new dimensions must rebuild.");
            var newGrid = partition.StaticGrid;
            Assert(!ReferenceEquals(oldGrid, newGrid) && newGrid.Width == 30 && newGrid.Height == 24, "Rebuild must create a 30x24 static grid.");
            Assert(ReferenceEquals(retained.StaticGrid, oldGrid) && retained.Width == 20, "A retained snapshot keeps its original, consistent grid.");

            // Old snapshots released after the rebuild must never come back out of the pool
            retained.Release();
            for (int i = 0; i < 8; i++)
            {
                partition.UpdateFromRegistry(null);
                var s = partition.GetActiveSnapshot();
                Assert(ReferenceEquals(s.StaticGrid, newGrid) && s.Width == 30, $"Update {i} handed out a snapshot from the previous world.");
            }

            TestLog.Log("[PASS] Test 1: Partition reconfiguration is idempotent, isolates retained readers and drops stale snapshots.");
        }

        private void ValidateWorldMapLoad()
        {
            var partition = new SpatialHashGrid();
            var sectors = new SectorRegistry();
            var worldMap = new WorldMap(partition, sectors);

            var dev = DevMapLayout.Definition;
            var loaded = worldMap.Load(dev);
            var cells = dev.PartitionCells(1f);
            var grid = partition.StaticGrid;
            Assert(grid.Width == cells.X && grid.Height == cells.Y, $"Partition must be sized from the map ({cells.X}x{cells.Y}), got {grid.Width}x{grid.Height}.");
            Assert(grid.PlayableBounds == dev.PlayableBounds, "Static grid carries the map's playable bounds.");
            Assert(!IsWalkable(grid, new Vec2(0f, 3.5f)), "Pen wall at (0, 3.5) must be blocked after load.");
            Assert(ReferenceEquals(worldMap.Current, dev) && worldMap.Version == 1, "Current map and version after the first load.");
            Assert(ReferenceEquals(sectors.Map, dev) && sectors.IsExplored(DirectiveSectorId), "Sectors loaded with default exploration (the Dev Map pens start charted).");

            loaded.RegisterEntity("probe", new EntityId(1u, 2u, 3u, 4u));
            Assert(loaded.TryGetEntityId("probe", out _) && !loaded.TryGetEntityId("missing", out _), "LoadedWorld key lookup.");

            // A different map replaces walls, bounds and sectors
            var other = new MapDefinitionBuilder("Other", new RectF(-12f, -12f, 24f, 24f))
                .WallRun(new Vec2(3f, -2f), new Vec2(3f, 2f))
                .Sector(1, "Only", "", new RectF(-12f, -12f, 24f, 24f), startsExplored: false)
                .Build();
            worldMap.Load(other);
            grid = partition.StaticGrid;
            var otherCells = other.PartitionCells(1f);
            Assert(grid.Width == otherCells.X && grid.Height == otherCells.Y, "Partition resized for the second map.");
            Assert(IsWalkable(grid, new Vec2(0f, 3.5f)) && !IsWalkable(grid, new Vec2(3f, 0f)), "Second map's walls replace the first map's.");
            Assert(worldMap.Version == 2 && sectors.GetSectorAt(Vec2.Zero).Id == 1, "Second load bumps the version and swaps the sector table.");
            Assert(!sectors.IsExplored(1), "Loading a map resets exploration to the new map's defaults.");
            sectors.SetExplored(DirectiveSectorId, false);
            worldMap.Load(dev);
            Assert(sectors.IsExplored(DirectiveSectorId), "Reloading a map restores its default exploration.");

            TestLog.Log("[PASS] Test 2: WorldMap.Load sizes the partition, writes walls, loads sectors and replaces the previous map.");
        }

        private void ValidateSectorRegistry()
        {
            var sectors = new SectorRegistry();
            Assert(sectors.GetSectorAt(Vec2.Zero).Id == 0 && sectors.IsExplored(0), "Before a load, everything is the explored fallback.");

            sectors.Load(CommonsYardFixture.Definition);
            Assert(sectors.GetSectorAt(new Vec2(-8.5f, 10f)).Id == CommonsYardFixture.OreRidgeSectorId, "Ore Ridge anchor is in Ore Ridge.");
            Assert(sectors.GetSectorAt(CommonsYardFixture.OutpostStallPosition).Id == CommonsYardFixture.OutpostMarketSectorId, "Stall is in the Outpost Market.");
            Assert(sectors.GetSectorAt(new Vec2(13f, 16f)).Id == 0, "Open yard corner falls back to sector 0.");
            Assert(sectors.TryGetSector(0, out var fallback) && fallback.Name == "Transit / Open Yard", "Id 0 resolves to the fallback.");
            Assert(!sectors.TryGetSector(99, out _), "Unknown id is not found.");

            Assert(sectors.IsExplored(CommonsYardFixture.OutpostMarketSectorId) && !sectors.IsExplored(CommonsYardFixture.OreRidgeSectorId), "Default exploration from the definition.");
            sectors.SetExplored(CommonsYardFixture.OreRidgeSectorId, true);
            Assert(sectors.IsExplored(CommonsYardFixture.OreRidgeSectorId), "SetExplored marks a sector.");
            sectors.SetAllExplored(false);
            Assert(!sectors.IsExplored(0) && !sectors.IsExplored(CommonsYardFixture.OutpostMarketSectorId), "SetAllExplored(false) clears everything.");
            sectors.SetAllExplored(true);
            for (int i = 0; i < sectors.All.Count; i++)
            {
                Assert(sectors.IsExplored(sectors.All[i].Id), $"Sector {sectors.All[i].Id} explored after SetAllExplored(true).");
            }

            TestLog.Log("[PASS] Test 3: Sector registry lookups, fallback and exploration state.");
        }

        private void ValidateNullDependencies()
        {
            var worldMap = new WorldMap(null, null);
            var loaded = worldMap.Load(DevMapLayout.Definition);
            Assert(loaded != null && worldMap.Current == DevMapLayout.Definition, "WorldMap must load without a partition or sector registry.");

            var registry = new SectorRegistry();
            registry.Load(null);
            Assert(registry.GetSectorAt(Vec2.Zero).Id == 0 && registry.All.Count == 0, "Loading a null map leaves only the fallback.");

            TestLog.Log("[PASS] Test 4: Null-dependency isolation.");
        }

        private static bool IsWalkable(StaticNavGrid grid, Vec2 world)
        {
            return grid.WorldToCell(world, out int x, out int y) && grid.IsCellWalkable(x, y);
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new Exception($"[WorldMapValidator] Assertion failed: {message}");
        }
    }
}
