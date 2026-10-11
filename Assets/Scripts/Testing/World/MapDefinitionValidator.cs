using System;
using Simpiens.Simulation.Economy;
using Simpiens.Simulation.Spatial;
using Simpiens.Simulation.World;
using UnityEngine;

namespace Simpiens.Testing.World
{
    /// <summary>
    /// Epic MAP Phase 1: plain-data map definitions. Builder invariants, wall rasterization (inflation, half-integer
    /// wall rows, base layer surviving dynamic-wall resets) and playable bounds.
    /// </summary>
    public class MapDefinitionValidator
    {
        public void RunAllValidationTests()
        {
            Debug.Log("[MapDefinitionValidator] Beginning map definition validation...");

            ValidateBuilderInvariants();
            ValidateWallRunInflation();
            ValidateHalfIntegerWallRows();
            ValidateBaseLayerSurvivesClear();
            ValidatePlayableBounds();
            ValidatePartitionSizing();

            Debug.Log("<color=green>[MapDefinitionValidator] ALL 6 TESTS PASSED SUCCESSFULLY!</color>");
        }

        private void ValidateBuilderInvariants()
        {
            var bounds = new Rect(-10f, -10f, 20f, 20f);

            ExpectThrows(() => new MapDefinitionBuilder("Dup", bounds)
                .Sector(1, "A", "", new Rect(0f, 0f, 1f, 1f))
                .Sector(1, "B", "", new Rect(2f, 2f, 1f, 1f))
                .Build(), "duplicate sector ids");

            ExpectThrows(() => new MapDefinitionBuilder("Zero", bounds)
                .Sector(0, "A", "", new Rect(0f, 0f, 1f, 1f))
                .Build(), "sector id 0 (reserved for the fallback)");

            ExpectThrows(() => new MapDefinitionBuilder("Keys", bounds)
                .Node("n", Vector2.zero, ItemType.Ration, 1, 10)
                .Poi("n", PoiKind.Hearth, Vector2.one)
                .Build(), "duplicate keys across nodes and POIs");

            ExpectThrows(() => new MapDefinitionBuilder("Outside", bounds)
                .Node("far", new Vector2(50f, 0f), ItemType.Ration, 1, 10)
                .Build(), "a node outside the playable bounds");

            ExpectThrows(() => new MapDefinitionBuilder("Diagonal", bounds)
                .WallRun(new Vector2(0f, 0f), new Vector2(3f, 3f)), "a diagonal wall run");

            var def = new MapDefinitionBuilder("Ok", bounds)
                .Sector(3, "Three", "", new Rect(0f, 0f, 2f, 2f), new Vector2(1f, 1f), 0.5f, startsExplored: false)
                .Poi("stall", PoiKind.Stall, new Vector2(1f, 1f), 3f)
                .Build();
            Assert(def.TryGetSector(3, out var s) && !s.StartsExplored, "Sector 3 must be found and start shrouded.");
            Assert(def.TryGetSector(0, out var fallback) && fallback.Id == 0, "Id 0 resolves to the fallback sector.");
            Assert(def.TryGetFirstPoi(PoiKind.Stall, out var stall) && stall.Key == "stall", "Stall POI lookup by kind.");

            Debug.Log("[PASS] Test 1: Builder rejects duplicate ids/keys, reserved id 0, out-of-bounds entries and diagonal walls.");
        }

        private void ValidateWallRunInflation()
        {
            // Vertical run at x = 0 from y = -2 to 2 on a 20x20 grid (cell edges on whole units)
            var def = new MapDefinitionBuilder("Run", new Rect(-10f, -10f, 20f, 20f))
                .WallRun(new Vector2(0f, -2f), new Vector2(0f, 2f))
                .Build();
            var grid = new StaticNavGrid(20, 20, 1f);
            WallRasterizer.Rasterize(def, grid);

            // Solid half extent 0.55 + body 0.25 = 0.8: cells [-1, 0) and [0, 1) blocked, [-2,-1) and [1,2) open
            Assert(!IsWalkable(grid, new Vector2(-0.5f, 0f)) && !IsWalkable(grid, new Vector2(0.5f, 0f)), "Cells either side of the wall centre line must be blocked.");
            Assert(IsWalkable(grid, new Vector2(-1.5f, 0f)) && IsWalkable(grid, new Vector2(1.5f, 0f)), "Cells beyond the inflated wall must stay open.");
            Assert(!IsWalkable(grid, new Vector2(0.5f, 2.5f)) && IsWalkable(grid, new Vector2(0.5f, 3.5f)), "The run's end cap is inflated by exactly one cell.");

            // Any pawn centre within solid + body radius of the wall must be blocked
            float reach = MapDefinitionBuilder.WallHalfExtent + NavConstants.PawnBodyRadius - 0.01f;
            for (int i = 0; i < 16; i++)
            {
                float a = i * Mathf.PI * 2f / 16f;
                var p = new Vector2(0f, 0f) + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * reach;
                Assert(!IsWalkable(grid, p), $"Point {p} within body reach of the wall must be blocked.");
            }

            Debug.Log("[PASS] Test 2: Wall runs rasterize inflated by the pawn body radius.");
        }

        private void ValidateHalfIntegerWallRows()
        {
            // Legacy pen rows sit on half-integer y (3.5): one unit wall row must block three cell rows
            var def = new MapDefinitionBuilder("HalfRow", new Rect(-10f, -10f, 20f, 20f))
                .WallRun(new Vector2(-3f, 3.5f), new Vector2(3f, 3.5f))
                .Build();
            var grid = new StaticNavGrid(20, 20, 1f);
            WallRasterizer.Rasterize(def, grid);

            Assert(!IsWalkable(grid, new Vector2(0f, 2.5f)), "Row y in [2,3) blocked (3.5 - 0.8 = 2.7).");
            Assert(!IsWalkable(grid, new Vector2(0f, 3.5f)), "Row y in [3,4) blocked.");
            Assert(!IsWalkable(grid, new Vector2(0f, 4.5f)), "Row y in [4,5) blocked (3.5 + 0.8 = 4.3).");
            Assert(IsWalkable(grid, new Vector2(0f, 1.5f)) && IsWalkable(grid, new Vector2(0f, 5.5f)), "Rows beyond the inflated wall stay open.");

            Debug.Log("[PASS] Test 3: Half-integer wall rows block the same three cell rows as legacy wall nodes.");
        }

        private void ValidateBaseLayerSurvivesClear()
        {
            var def = new MapDefinitionBuilder("Base", new Rect(-10f, -10f, 20f, 20f))
                .WallRun(new Vector2(2f, 0f), new Vector2(2f, 0f))
                .Build();
            var grid = new StaticNavGrid(20, 20, 1f);
            WallRasterizer.Rasterize(def, grid);

            // A dynamic wall elsewhere, then the dynamic reset that SpatialHashGrid performs when a wall disappears
            grid.WorldToCell(new Vector2(-4.5f, -4.5f), out int dx, out int dy);
            grid.SetWall(dx, dy);
            Assert(!grid.IsCellWalkable(dx, dy), "Dynamic wall must block its cell.");
            grid.Clear();

            Assert(grid.IsCellWalkable(dx, dy), "Clear() must remove dynamic walls.");
            Assert(!IsWalkable(grid, new Vector2(2.5f, 0f)), "Clear() must keep static map walls.");
            grid.WorldToCell(new Vector2(2.5f, 0f), out int bx, out int by);
            Assert(grid.IsBaseWall(bx, by) && !grid.IsBaseWall(dx, dy), "Base-wall query distinguishes map walls from dynamic walls.");

            // Re-rasterizing a different map replaces the base layer
            var empty = new MapDefinitionBuilder("Empty", new Rect(-10f, -10f, 20f, 20f)).Build();
            WallRasterizer.Rasterize(empty, grid);
            Assert(IsWalkable(grid, new Vector2(2.5f, 0f)), "Loading another map must drop the previous map's walls.");

            Debug.Log("[PASS] Test 4: Static map walls survive dynamic resets; reloading replaces them.");
        }

        private void ValidatePlayableBounds()
        {
            var def = new MapDefinitionBuilder("Bounds", new Rect(-6f, -4f, 12f, 8f)).Build();
            var grid = new StaticNavGrid(20, 20, 1f);
            WallRasterizer.Rasterize(def, grid);

            Assert(IsWalkable(grid, new Vector2(5.5f, 3.5f)), "Cell inside the bounds stays open.");
            Assert(!IsWalkable(grid, new Vector2(6.5f, 0f)), "Cell right of the bounds is blocked.");
            Assert(!IsWalkable(grid, new Vector2(0f, -4.5f)), "Cell below the bounds is blocked.");
            Assert(grid.HasPlayableBounds && grid.PlayableBounds == def.PlayableBounds, "Grid reports the map's playable bounds.");

            // Wider bounds on reload must reopen cells the old bounds blocked
            var wide = new MapDefinitionBuilder("Wide", new Rect(-9f, -9f, 18f, 18f)).Build();
            WallRasterizer.Rasterize(wide, grid);
            Assert(IsWalkable(grid, new Vector2(6.5f, 0f)), "Reloading with wider bounds reopens cells.");

            Debug.Log("[PASS] Test 5: Playable bounds block outside cells and are replaced on reload.");
        }

        private void ValidatePartitionSizing()
        {
            var asymmetric = new MapDefinitionBuilder("Asym", new Rect(-20f, -18f, 44f, 36f)).Build();
            var cells = asymmetric.PartitionCells(1f);
            // |x| up to 24 -> 2 * (24 + 2) = 52; |y| up to 18 -> 2 * (18 + 2) = 40
            Assert(cells.x == 52 && cells.y == 40, $"Partition cells expected 52x40, got {cells.x}x{cells.y}.");
            Assert(cells.x % 2 == 0 && cells.y % 2 == 0, "Partition cell counts must be even so cell edges stay on whole units.");

            Debug.Log("[PASS] Test 6: Partition size covers the bounds symmetrically, independent of the camera.");
        }

        private static bool IsWalkable(StaticNavGrid grid, Vector2 world)
        {
            return grid.WorldToCell(world, out int x, out int y) && grid.IsCellWalkable(x, y);
        }

        private static void ExpectThrows(Action action, string what)
        {
            try
            {
                action();
            }
            catch (Exception)
            {
                return;
            }
            throw new Exception($"[MapDefinitionValidator] Expected the builder to reject {what}.");
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new Exception($"[MapDefinitionValidator] Assertion failed: {message}");
        }
    }
}
