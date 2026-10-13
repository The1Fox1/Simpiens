using System;
using Simpiens.Core.Session.Layouts;
using Simpiens.Entities;
using Simpiens.Simulation.Spatial;
using Simpiens.Simulation.World;
using Simpiens.Testing.Sessions;
using UnityEngine;
using UnityEngine.Tilemaps;
using Object = UnityEngine.Object;

namespace Simpiens.Testing.World
{
    /// <summary>
    /// Epic MAP Phase 4: the map view draws what the simulation uses. Wall and rock tiles sit exactly on the
    /// definition's wall cells (whole-number centres on Floor 1, half-number rows on the Dev Map); every cell the pathfinder treats as walkable is drawn as floor; the view is a handful
    /// of GameObjects, not one per wall cell. Needs GameObjects, so it runs inside PlayMode.
    /// </summary>
    public class MapViewValidator
    {
        /// <summary>The whole view (root, grid, three tilemaps, labels) stays under this many GameObjects.</summary>
        public const int MaxViewObjects = 20;

        public void RunAllValidationTests()
        {
            Debug.Log("[MapViewValidator] Beginning map view validation...");

            ValidateMap(Floor1Layout.Definition, 1);
            ValidateMap(DevMapLayout.Definition, 2);

            Debug.Log("<color=green>[MapViewValidator] ALL 2 TESTS PASSED SUCCESSFULLY!</color>");
        }

        private void ValidateMap(MapDefinition map, int testNumber)
        {
            var root = MapView.Build(map, $"[MapViewValidator {map.Name}]");
            try
            {
                var floor = Find(root, "Floor");
                var rock = Find(root, "Rock");
                var walls = Find(root, "Walls");
                var origin = MapView.CellOrigin(map);

                // 1. Wall and rock tiles are exactly the definition's wall cells
                int wallCells = 0;
                for (int i = 0; i < map.Walls.Count; i++)
                {
                    var solid = map.Walls[i].Solid;
                    var expected = MapView.IsRock(solid) ? rock : walls;
                    MapView.CellRange(solid, origin, out int minX, out int maxX, out int minY, out int maxY);
                    for (int y = minY; y <= maxY; y++)
                    {
                        for (int x = minX; x <= maxX; x++)
                        {
                            var cell = new Vector3Int(x, y, 0);
                            Assert(expected.HasTile(cell), $"{map.Name}: wall cell ({x}, {y}) of {solid} has no {expected.name} tile.");
                            Assert(!floor.HasTile(cell), $"{map.Name}: wall cell ({x}, {y}) is also drawn as floor.");
                            wallCells++;
                        }
                    }
                }
                CheckNoStrayTiles(map, walls, origin);
                CheckNoStrayTiles(map, rock, origin);

                // 2. Everywhere a crawler can stand is drawn as floor: the four tiles around each walkable nav cell's centre
                var partition = new SpatialHashGrid();
                new WorldMap(partition, new SectorRegistry()).Load(map);
                var grid = partition.StaticGrid;
                int walkable = 0;
                for (int cy = 0; cy < grid.Height; cy++)
                {
                    for (int cx = 0; cx < grid.Width; cx++)
                    {
                        if (!grid.IsCellWalkable(cx, cy)) continue;
                        walkable++;
                        var centre = grid.CellToWorld(cx, cy);
                        for (int q = 0; q < 4; q++)
                        {
                            var probe = centre + new Vector2((q & 1) == 0 ? -0.25f : 0.25f, (q & 2) == 0 ? -0.25f : 0.25f);
                            var cell = MapView.CellAt(origin, probe);
                            Assert(floor.HasTile(cell), $"{map.Name}: walkable nav cell at {centre} stands on undrawn cell ({cell.x}, {cell.y}).");
                        }
                    }
                }

                // 3. Every sector shows floor, the layers stack in RenderOrder, and the view is a handful of objects
                for (int i = 0; i < map.Sectors.Count; i++)
                {
                    var sector = map.Sectors[i];
                    var c = MapView.CellAt(origin, sector.TargetPoint);
                    Assert(floor.HasTile(c), $"{map.Name}: sector {sector.Name} has no floor at its target point.");
                }
                Assert(Order(floor) == RenderOrder.Floor && Order(rock) == RenderOrder.Rock && Order(walls) == RenderOrder.Walls,
                    $"{map.Name}: tilemap sorting orders follow RenderOrder.");
                int objects = root.GetComponentsInChildren<Transform>(true).Length;
                Assert(objects < MaxViewObjects, $"{map.Name}: the view is {objects} GameObjects (limit {MaxViewObjects}).");

                Debug.Log($"[PASS] Test {testNumber}: {map.Name} view matches the map ({wallCells} wall cells, {walkable} walkable nav cells on floor, {objects} GameObjects).");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>No wall or rock tile outside the definition's wall rects.</summary>
        private static void CheckNoStrayTiles(MapDefinition map, Tilemap tilemap, Vector2 origin)
        {
            var bounds = tilemap.cellBounds;
            foreach (var cell in bounds.allPositionsWithin)
            {
                if (!tilemap.HasTile(cell)) continue;
                bool inWall = false;
                for (int i = 0; i < map.Walls.Count && !inWall; i++)
                {
                    inWall = map.Walls[i].Solid.Contains(MapView.CellCentre(origin, cell.x, cell.y));
                }
                Assert(inWall, $"{map.Name}: {tilemap.name} tile at ({cell.x}, {cell.y}) is not on a wall.");
            }
        }

        private static Tilemap Find(GameObject root, string name)
        {
            var maps = root.GetComponentsInChildren<Tilemap>(true);
            for (int i = 0; i < maps.Length; i++)
            {
                if (maps[i].name == name) return maps[i];
            }
            throw new Exception($"[MapViewValidator] Assertion failed: no '{name}' tilemap.");
        }

        private static int Order(Tilemap tilemap) => tilemap.GetComponent<TilemapRenderer>().sortingOrder;

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new Exception($"[MapViewValidator] Assertion failed: {message}");
        }
    }
}
