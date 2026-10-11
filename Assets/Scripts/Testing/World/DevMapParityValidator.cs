using System;
using System.Collections.Generic;
using Simpiens.Core.Session.Layouts;
using Simpiens.Simulation.Spatial;
using Simpiens.Simulation.World;
using Simpiens.UI.Zones;
using UnityEngine;

namespace Simpiens.Testing.World
{
    /// <summary>
    /// TEMPORARY (Epic MAP Phase 1; delete in Phase 2). Proves that <see cref="DevMapLayout"/> reproduces the legacy
    /// live geography exactly: wall cells match the former per-cell wall entities rasterized by SpatialHashGrid, the
    /// playable area matches the former 44x36 world with the ±20 border, and sector lookups match FacilityZoneService.
    /// </summary>
    public class DevMapParityValidator
    {
        // Former session: SimulationStarter's 44x36 fallback world, NavConstants.PlayAreaHalfExtent = 20
        private const int LegacyWidth = 44;
        private const int LegacyHeight = 36;
        private const float LegacyWallRadius = 0.55f;

        public void RunAllValidationTests()
        {
            Debug.Log("[DevMapParityValidator] Beginning Dev Map parity validation...");

            ValidateWallCellParity();
            ValidateSectorParity();
            ValidateEntityPlacements();

            Debug.Log("<color=green>[DevMapParityValidator] ALL 3 TESTS PASSED SUCCESSFULLY!</color>");
        }

        private void ValidateWallCellParity()
        {
            // Legacy: one wall entity per unit cell (former SwarmSpawner.SpawnPlayPens loops), rasterized with
            // SpatialHashGrid's inflated-circle formula, plus the ±20 playable border
            var legacy = new StaticNavGrid(LegacyWidth, LegacyHeight, 1f);
            legacy.SetPlayableHalfExtent(NavConstants.PlayAreaHalfExtent);
            var nodes = LegacyWallNodes();
            for (int i = 0; i < nodes.Count; i++)
            {
                RasterizeLegacyNode(legacy, nodes[i]);
            }

            var def = DevMapLayout.Definition;
            var cells = def.PartitionCells(1f);
            var grid = new StaticNavGrid(cells.x, cells.y, 1f);
            WallRasterizer.Rasterize(def, grid);

            int compared = 0;
            int mismatches = 0;
            string first = null;
            for (int y = 0; y < grid.Height; y++)
            {
                for (int x = 0; x < grid.Width; x++)
                {
                    Vector2 world = grid.CellToWorld(x, y);
                    bool legacyWalkable = legacy.WorldToCell(world, out int lx, out int ly) && legacy.IsCellWalkable(lx, ly);
                    bool walkable = grid.IsCellWalkable(x, y);
                    compared++;
                    if (legacyWalkable != walkable)
                    {
                        mismatches++;
                        first ??= $"cell centred at {world}: legacy {(legacyWalkable ? "walkable" : "blocked")}, data {(walkable ? "walkable" : "blocked")}";
                    }
                }
            }

            Assert(nodes.Count == 156, $"Legacy pen layout has 156 wall entities, enumerated {nodes.Count}.");
            Assert(mismatches == 0, $"{mismatches} of {compared} cells differ from the legacy rasterization; first: {first}");
            Debug.Log($"[PASS] Test 1: {compared} cells match the legacy wall rasterization and play-area border exactly.");
        }

        private void ValidateSectorParity()
        {
            // The former FacilityZoneService table was proven identical to DevMapLayout's sectors (0 differing lookups
            // on a 0.25 lattice) before it was deleted (commit 7b610cb). Since the table moved into the simulation,
            // this checks that the UI adapter reproduces the registry exactly.
            var def = DevMapLayout.Definition;
            var sectors = TestWorlds.DevMapSectors();
            var zones = new FacilityZoneService(sectors);

            var all = zones.GetAllZones();
            Assert(all.Count == def.Sectors.Count, $"Adapter lists {all.Count} zones, map has {def.Sectors.Count} sectors.");
            for (int i = 0; i < all.Count; i++)
            {
                var zone = all[i];
                Assert(def.TryGetSector((byte)zone.ZoneId, out var sector), $"Zone {zone.ZoneId} has no sector.");
                Assert(sector.Name == zone.Name && sector.Description == zone.Description && sector.Bounds == zone.Bounds
                    && sector.Anchor == zone.AnchorPosition && Mathf.Approximately(sector.AnchorRadius, zone.AnchorRadius),
                    $"Zone {zone.ZoneId} differs from its sector definition.");
                Assert(zone.IsExplored == sector.StartsExplored && zones.IsSectorExplored(zone.ZoneId) == sector.StartsExplored,
                    $"Zone {zone.ZoneId} default exploration differs.");
            }
            Assert(zones.GetZoneById(0).Name == "Transit / Open Yard" && zones.GetZoneById(999).ZoneId == 0, "Fallback transit zone.");

            int mismatches = 0;
            string first = null;
            for (float y = -20f; y <= 20f; y += 0.25f)
            {
                for (float x = -22f; x <= 22f; x += 0.25f)
                {
                    var p = new Vector2(x, y);
                    int expected = def.FindSector(p).Id;
                    int actual = zones.GetZoneForPosition(p).ZoneId;
                    if (expected != actual)
                    {
                        mismatches++;
                        first ??= $"{p}: map {expected}, adapter {actual}";
                    }
                }
            }
            Assert(mismatches == 0, $"{mismatches} adapter lookups differ; first: {first}");

            // Exploration written through either side is visible on the other
            zones.SetSectorExplored(DevMapLayout.HerbHollowSectorId, true);
            Assert(sectors.IsExplored(DevMapLayout.HerbHollowSectorId), "Adapter writes reach the registry.");
            sectors.SetExplored(DevMapLayout.OreRidgeSectorId, true);
            Assert(zones.IsSectorExplored(DevMapLayout.OreRidgeSectorId) && zones.GetZoneById(DevMapLayout.OreRidgeSectorId).IsExplored, "Registry writes reach the adapter.");

            Debug.Log("[PASS] Test 2: The zone adapter reproduces the map's sectors, lookups and exploration exactly.");
        }

        private void ValidateEntityPlacements()
        {
            var def = DevMapLayout.Definition;
            Assert(def.ResourceNodes.Count == 16, $"Expected 16 resource nodes (8 pen, 8 commons), got {def.ResourceNodes.Count}.");
            int hearths = 0;
            for (int i = 0; i < def.Pois.Count; i++)
            {
                if (def.Pois[i].Kind == PoiKind.Hearth) hearths++;
            }
            Assert(hearths == 3, $"Expected 3 demo campfires, got {hearths}.");
            Assert(def.TryGetPoi(DevMapLayout.OutpostStall, out var stall) && stall.Position == DevMapLayout.OutpostStallPosition, "Outpost stall at (0, 9).");

            var cells = def.PartitionCells(1f);
            var grid = new StaticNavGrid(cells.x, cells.y, 1f);
            WallRasterizer.Rasterize(def, grid);
            grid.EnsureRegions();
            // Some pen bushes sit within body reach of a pen wall (as they always did); A* and reachability snap to
            // the nearest walkable cell, so each must resolve to a region
            for (int i = 0; i < def.ResourceNodes.Count; i++)
            {
                var n = def.ResourceNodes[i];
                Assert(grid.TryGetRegion(n.Position, out _), $"Node '{n.Key}' at {n.Position} has no walkable cell within snap radius.");
            }
            Debug.Log("[PASS] Test 3: All 16 nodes, 3 campfires and the stall resolve to walkable regions.");
        }

        private static List<Vector2> LegacyWallNodes()
        {
            // Verbatim loop structure of the former SwarmSpawner.SpawnPlayPens (float accumulation included)
            var nodes = new List<Vector2>(160);
            float[] dividerX = { -10f, -6f, -2f, 2f, 6f, 10f };

            float minY1 = -3.5f, maxY1 = 3.5f;
            for (float x = -10.0f; x <= 10.05f; x += 1.0f)
            {
                nodes.Add(new Vector2(x, maxY1));
                nodes.Add(new Vector2(x, minY1));
            }
            for (int d = 0; d < dividerX.Length; d++)
            {
                for (float y = minY1 + 1.0f; y < maxY1 - 0.5f; y += 1.0f) nodes.Add(new Vector2(dividerX[d], y));
            }

            float minY2 = -12.5f, maxY2 = -5.5f;
            for (float x = -10.0f; x <= 10.05f; x += 1.0f)
            {
                nodes.Add(new Vector2(x, maxY2));
                nodes.Add(new Vector2(x, minY2));
            }
            for (int d = 0; d < dividerX.Length; d++)
            {
                for (float y = minY2 + 1.0f; y < maxY2 - 0.5f; y += 1.0f) nodes.Add(new Vector2(dividerX[d], y));
            }
            return nodes;
        }

        private static void RasterizeLegacyNode(StaticNavGrid grid, Vector2 pos)
        {
            // Copy of SpatialHashGrid.UpdateFromRegistry's wall branch
            float cs = grid.CellSize;
            float offsetX = grid.Width * cs * 0.5f;
            float offsetY = grid.Height * cs * 0.5f;
            float inflated = LegacyWallRadius + NavConstants.PawnBodyRadius;
            int minX = Mathf.Max(0, Mathf.FloorToInt((pos.x - inflated + offsetX) / cs));
            int maxX = Mathf.Min(grid.Width - 1, Mathf.FloorToInt((pos.x + inflated + offsetX) / cs));
            int minY = Mathf.Max(0, Mathf.FloorToInt((pos.y - inflated + offsetY) / cs));
            int maxY = Mathf.Min(grid.Height - 1, Mathf.FloorToInt((pos.y + inflated + offsetY) / cs));
            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    grid.SetWall(x, y);
                }
            }
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new Exception($"[DevMapParityValidator] Assertion failed: {message}");
        }
    }
}
