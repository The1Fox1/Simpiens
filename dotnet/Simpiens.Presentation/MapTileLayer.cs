using System.Collections.Generic;
using Simpiens.Simulation.World;

namespace Simpiens.Presentation
{
    public enum TileKind : byte
    {
        Floor = 0,
        Rock = 1,
        Wall = 2,
    }

    /// <summary>One cell of the map's static picture: what it is and how its floor is tinted.</summary>
    public readonly struct MapTile
    {
        public readonly int X;
        public readonly int Y;
        public readonly TileKind Kind;
        /// <summary>The floor's sector tint; white for rock and walls.</summary>
        public readonly Rgba Tint;

        public MapTile(int x, int y, TileKind kind, Rgba tint)
        {
            X = x;
            Y = y;
            Kind = kind;
            Tint = tint;
        }
    }

    /// <summary>
    /// A map definition as unit cells (Epic CLIENT M0): rock, walls, and floor tinted by its sector. A port of the cell
    /// logic in Unity's <c>MapView</c>, so both clients tile a map identically. Cells are aligned with the map's wall
    /// cells (<see cref="Origin"/>): Floor 1's walls are centred on whole numbers, the Dev Map's on half numbers in y.
    /// A wall or rock tile marks a cell whose centre lies inside a wall rect; every other cell in the playable bounds is
    /// floor. Built once per map; it never changes while the map is loaded.
    /// </summary>
    public sealed class MapTileLayer
    {
        /// <summary>A sector's floor with no label in it.</summary>
        public static readonly Rgba NeutralFloor = new Rgba(0.2f, 0.21f, 0.24f);

        private readonly List<MapTile> _tiles;
        private readonly HashSet<long> _floor;

        private MapTileLayer(MapDefinition map, Vec2 origin, List<MapTile> tiles, HashSet<long> floor)
        {
            Map = map;
            Origin = origin;
            _tiles = tiles;
            _floor = floor;
        }

        /// <summary>True when cell (<paramref name="x"/>, <paramref name="y"/>) is floor: neither wall nor rock, inside the playable bounds.</summary>
        public bool IsFloor(int x, int y) => _floor.Contains(Key(x, y));

        public MapDefinition Map { get; }

        /// <summary>World position of cell (0, 0)'s centre.</summary>
        public Vec2 Origin { get; }

        /// <summary>Every tile: rock and walls in the order of the map's wall rects, then floor row by row.</summary>
        public IReadOnlyList<MapTile> Tiles => _tiles;

        public Vec2 CellCentre(int x, int y) => Origin + new Vec2(x, y);

        public static MapTileLayer Build(MapDefinition map)
        {
            var origin = CellOrigin(map);
            var tiles = new List<MapTile>(1024);
            var rock = new HashSet<long>();
            var walls = new HashSet<long>();

            for (int i = 0; i < map.Walls.Count; i++)
            {
                var solid = map.Walls[i].Solid;
                bool isRock = IsRock(solid);
                CellRange(solid, origin, out int minX, out int maxX, out int minY, out int maxY);
                for (int y = minY; y <= maxY; y++)
                {
                    for (int x = minX; x <= maxX; x++)
                    {
                        // As Unity's tilemaps: one rock and one wall tile at most per cell (a cell can hold both)
                        if (isRock ? rock.Add(Key(x, y)) : walls.Add(Key(x, y)))
                        {
                            tiles.Add(new MapTile(x, y, isRock ? TileKind.Rock : TileKind.Wall, Rgba.White));
                        }
                    }
                }
            }

            var floor = new HashSet<long>();
            CellRange(map.PlayableBounds, origin, out int bMinX, out int bMaxX, out int bMinY, out int bMaxY);
            for (int y = bMinY; y <= bMaxY; y++)
            {
                for (int x = bMinX; x <= bMaxX; x++)
                {
                    long key = Key(x, y);
                    if (walls.Contains(key) || rock.Contains(key)) continue;
                    var tint = TryGetSectorIndex(map, origin + new Vec2(x, y), out int sectorIndex) ? FloorTint(map, map.Sectors[sectorIndex]) : NeutralFloor;
                    tiles.Add(new MapTile(x, y, TileKind.Floor, tint));
                    floor.Add(key);
                }
            }

            return new MapTileLayer(map, origin, tiles, floor);
        }

        /// <summary>A wall rect wider than one cell both ways is solid rock outside the floor, not a wall run.</summary>
        public static bool IsRock(RectF solid)
        {
            float unit = 2f * MapDefinitionBuilder.WallHalfExtent + 0.01f;
            return solid.Width > unit && solid.Height > unit;
        }

        /// <summary>
        /// World position of tile (0, 0)'s centre: the fractional part of the first wall cell's centre on each axis, so
        /// tiles line up with the map's wall cells. A map's walls share one alignment.
        /// </summary>
        public static Vec2 CellOrigin(MapDefinition map)
        {
            if (map.Walls.Count == 0) return Vec2.Zero;
            var solid = map.Walls[0].Solid;
            float inset = IsRock(solid) ? 0.5f : MapDefinitionBuilder.WallHalfExtent;
            return new Vec2(Fraction(solid.XMin + inset), Fraction(solid.YMin + inset));
        }

        private static float Fraction(float v)
        {
            float f = v - SimMath.Floor(v);
            return SimMath.Abs(f - 0.5f) < 0.01f ? 0.5f : 0f;
        }

        /// <summary>The cells whose centres lie inside <paramref name="rect"/>.</summary>
        public static void CellRange(RectF rect, Vec2 origin, out int minX, out int maxX, out int minY, out int maxY)
        {
            minX = SimMath.CeilToInt(rect.XMin - origin.X);
            maxX = SimMath.FloorToInt(rect.XMax - origin.X);
            minY = SimMath.CeilToInt(rect.YMin - origin.Y);
            maxY = SimMath.FloorToInt(rect.YMax - origin.Y);
        }

        /// <summary>The first sector containing the point, in definition order (as <see cref="ISectorRegistry.GetSectorAt"/>).</summary>
        public static bool TryGetSectorIndex(MapDefinition map, Vec2 point, out int index)
        {
            for (int i = 0; i < map.Sectors.Count; i++)
            {
                if (map.Sectors[i].Contains(point)) { index = i; return true; }
            }
            index = -1;
            return false;
        }

        /// <summary>
        /// A sector's floor: a muted version of the colour of the map label inside it, so each room reads as its own
        /// place with no extra data. Neutral when the sector has no label.
        /// </summary>
        public static Rgba FloorTint(MapDefinition map, in SectorDefinition sector)
        {
            for (int i = 0; i < map.Labels.Count; i++)
            {
                if (sector.Contains(map.Labels[i].Position)) return Lerp(NeutralFloor, map.Labels[i].Color, 0.3f);
            }
            return NeutralFloor;
        }

        /// <summary>Unity's <c>Color.Lerp</c>: t clamped to 0–1, every channel including alpha.</summary>
        private static Rgba Lerp(Rgba a, Rgba b, float t)
        {
            t = SimMath.Clamp01(t);
            return new Rgba(a.R + (b.R - a.R) * t, a.G + (b.G - a.G) * t, a.B + (b.B - a.B) * t, a.A + (b.A - a.A) * t);
        }

        private static long Key(int x, int y) => ((long)x << 32) | (uint)y;
    }
}
