using System.Collections.Generic;
using System.Text;
using Simpiens.Simulation.Spatial;
using Simpiens.Simulation.World;

namespace Simpiens.Testing.World
{
    /// <summary>
    /// A map's walkable ground, measured (Epic PLACES plan 1, P.1): its walkable cells, each zone's, the doors between zones
    /// and their widths, and the walk from one zone to every other along the static grid, as A* walks it (eight ways, never
    /// cutting a corner, a diagonal √2). A baseline: a step that changes walls or furniture is re-measured against it, so what
    /// moved is seen. Reads the map; test-only.
    /// </summary>
    public static class MapMeasure
    {
        /// <summary>One zone's ground and the walk to it.</summary>
        public readonly struct Zone
        {
            public readonly byte Id;
            public readonly string Name;
            public readonly int WalkableCells;

            /// <summary>The walk from the measured zone's target point to this one's, in units; negative when there is no way.</summary>
            public readonly float Walk;

            public Zone(byte id, string name, int walkableCells, float walk)
            {
                Id = id;
                Name = name;
                WalkableCells = walkableCells;
                Walk = walk;
            }
        }

        /// <summary>A door: two zones sharing an edge, and the walkable cell pairs across it.</summary>
        public readonly struct Door
        {
            public readonly string A, B;
            public readonly int Width;

            public Door(string a, string b, int width)
            {
                A = a;
                B = b;
                Width = width;
            }
        }

        public sealed class Report
        {
            public int WalkableCells;
            public string From;
            public readonly List<Zone> Zones = new List<Zone>();
            public readonly List<Door> Doors = new List<Door>();

            public override string ToString()
            {
                var text = new StringBuilder();
                text.AppendLine($"walkable cells {WalkableCells}; walks from {From}");
                for (int i = 0; i < Zones.Count; i++)
                {
                    var z = Zones[i];
                    text.AppendLine($"  {z.Id,2} {z.Name,-16} cells {z.WalkableCells,4}  walk {(z.Walk < 0f ? "none" : z.Walk.ToString("0.0"))}");
                }
                for (int i = 0; i < Doors.Count; i++) text.AppendLine($"  door {Doors[i].A} - {Doors[i].B}: {Doors[i].Width}");
                return text.ToString();
            }
        }

        private static readonly int[] Dx = { 1, -1, 0, 0, 1, 1, -1, -1 };
        private static readonly int[] Dy = { 0, 0, 1, -1, 1, -1, 1, -1 };
        private const float Sqrt2 = 1.41421356f;

        /// <summary>Measures <paramref name="map"/>, its walks from zone <paramref name="from"/>'s target point.</summary>
        public static Report Measure(MapDefinition map, byte from)
        {
            var grid = Rasterized(map);
            var report = new Report();
            map.TryGetSector(from, out var origin);
            report.From = origin.Name;

            for (int y = 0; y < grid.Height; y++)
                for (int x = 0; x < grid.Width; x++)
                    if (grid.IsCellWalkable(x, y)) report.WalkableCells++;

            var cost = Walks(grid, origin.TargetPoint);
            for (int i = 0; i < map.Sectors.Count; i++)
            {
                var s = map.Sectors[i];
                int cells = 0;
                for (int y = 0; y < grid.Height; y++)
                    for (int x = 0; x < grid.Width; x++)
                        if (grid.IsCellWalkable(x, y) && s.Bounds.Contains(grid.CellToWorld(x, y))) cells++;
                report.Zones.Add(new Zone(s.Id, s.Name, cells, WalkTo(grid, cost, s.TargetPoint)));
            }

            for (int i = 0; i < map.Sectors.Count; i++)
                for (int j = i + 1; j < map.Sectors.Count; j++)
                {
                    int width = DoorWidth(grid, map.Sectors[i].Bounds, map.Sectors[j].Bounds);
                    if (width > 0) report.Doors.Add(new Door(map.Sectors[i].Name, map.Sectors[j].Name, width));
                }
            return report;
        }

        /// <summary>The map's static grid, as a load writes it.</summary>
        public static StaticNavGrid Rasterized(MapDefinition map)
        {
            var cells = map.PartitionCells(1f);
            var grid = new StaticNavGrid(cells.X, cells.Y, 1f);
            WallRasterizer.Rasterize(map, grid);
            grid.EnsureRegions();
            return grid;
        }

        /// <summary>Every cell's walk from <paramref name="start"/>, as A* steps (Dijkstra's, eight ways, no corner cut).</summary>
        public static float[] Walks(StaticNavGrid grid, Vec2 start)
        {
            var cost = new float[grid.Width * grid.Height];
            for (int i = 0; i < cost.Length; i++) cost[i] = float.MaxValue;
            if (!Nearest(grid, start, out int sx, out int sy)) return cost;
            var open = new PriorityQueue<int, float>();
            cost[sy * grid.Width + sx] = 0f;
            open.Enqueue(sy * grid.Width + sx, 0f);
            while (open.TryDequeue(out int cell, out float g))
            {
                if (g > cost[cell]) continue;
                int cx = cell % grid.Width, cy = cell / grid.Width;
                for (int d = 0; d < 8; d++)
                {
                    int nx = cx + Dx[d], ny = cy + Dy[d];
                    if (!grid.InBounds(nx, ny) || !grid.IsCellWalkable(nx, ny)) continue;
                    if (d >= 4 && (!grid.IsCellWalkable(cx, ny) || !grid.IsCellWalkable(nx, cy))) continue;
                    float next = g + (d >= 4 ? Sqrt2 : 1f);
                    int n = ny * grid.Width + nx;
                    if (next >= cost[n]) continue;
                    cost[n] = next;
                    open.Enqueue(n, next);
                }
            }
            return cost;
        }

        /// <summary>The walk to <paramref name="target"/>'s nearest walkable cell, or -1 when there is no way.</summary>
        public static float WalkTo(StaticNavGrid grid, float[] cost, Vec2 target)
        {
            if (!Nearest(grid, target, out int x, out int y)) return -1f;
            float c = cost[y * grid.Width + x];
            return c == float.MaxValue ? -1f : c;
        }

        /// <summary>The walkable cell at <paramref name="point"/>, or the nearest within <see cref="StaticNavGrid.RegionSnapRadius"/>.</summary>
        private static bool Nearest(StaticNavGrid grid, Vec2 point, out int cellX, out int cellY)
        {
            cellX = cellY = 0;
            if (!grid.WorldToCell(point, out int px, out int py)) return false;
            for (int r = 0; r <= StaticNavGrid.RegionSnapRadius; r++)
                for (int y = py - r; y <= py + r; y++)
                    for (int x = px - r; x <= px + r; x++)
                        if (grid.InBounds(x, y) && grid.IsCellWalkable(x, y))
                        {
                            cellX = x;
                            cellY = y;
                            return true;
                        }
            return false;
        }

        /// <summary>Walkable cell pairs straddling the shared edge of two zones (0 when they don't touch or there is no door).</summary>
        public static int DoorWidth(StaticNavGrid grid, RectF a, RectF b)
        {
            int width = 0;
            if (SimMath.Approximately(a.XMax, b.XMin) || SimMath.Approximately(b.XMax, a.XMin))
            {
                float edge = SimMath.Approximately(a.XMax, b.XMin) ? a.XMax : a.XMin;
                float yMin = SimMath.Max(a.YMin, b.YMin), yMax = SimMath.Min(a.YMax, b.YMax);
                for (float y = yMin + 0.5f; y < yMax; y += 1f)
                    if (Walkable(grid, new Vec2(edge - 0.5f, y)) && Walkable(grid, new Vec2(edge + 0.5f, y))) width++;
            }
            else if (SimMath.Approximately(a.YMax, b.YMin) || SimMath.Approximately(b.YMax, a.YMin))
            {
                float edge = SimMath.Approximately(a.YMax, b.YMin) ? a.YMax : a.YMin;
                float xMin = SimMath.Max(a.XMin, b.XMin), xMax = SimMath.Min(a.XMax, b.XMax);
                for (float x = xMin + 0.5f; x < xMax; x += 1f)
                    if (Walkable(grid, new Vec2(x, edge - 0.5f)) && Walkable(grid, new Vec2(x, edge + 0.5f))) width++;
            }
            return width;
        }

        private static bool Walkable(StaticNavGrid grid, Vec2 point) => grid.WorldToCell(point, out int x, out int y) && grid.IsCellWalkable(x, y);
    }
}
