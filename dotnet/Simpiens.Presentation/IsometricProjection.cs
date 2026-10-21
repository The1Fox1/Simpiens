using System;

namespace Simpiens.Presentation
{
    /// <summary>
    /// The isometric projection (Epic CLIENT, for M3, not yet the game's view): the usual 2:1 pixel-art projection of the
    /// same square grid, with height visual only. In art pixels, with screen y down, a tile <c>w</c> by <c>h</c> and
    /// <c>h_z</c> pixels per unit of height:
    /// <c>s_x = (x - y) w/2</c>, <c>s_y = (x + y) h/2 - z h_z</c>, and depth <c>x + y + ε z</c>: lower on screen is in
    /// front. The map can be turned by quarter turns first, to choose which corner faces the viewer. A view unit is
    /// <see cref="ArtPixelsPerUnit"/> (the tile's height) art pixels.
    /// </summary>
    public sealed class IsometricProjection : IViewProjection
    {
        /// <summary>Depth per unit of height: enough to order stacked items, too little to pass a neighbouring cell.</summary>
        public const float HeightDepth = 1e-3f;

        private readonly float _halfWidth;  // view units per unit of (x - y)
        private readonly float _halfHeight; // view units per unit of (x + y)
        private readonly float _rise;       // view units per unit of height
        private readonly int _turns;

        /// <param name="tileWidth">A floor tile's width in art pixels (the proposal's 64, to be confirmed in M3).</param>
        /// <param name="tileHeight">Its height in art pixels: half the width for 2:1. A power of two, so sizes in art pixels convert exactly.</param>
        /// <param name="heightPixelsPerUnit">Art pixels per world unit of height.</param>
        /// <param name="quarterTurns">Counter-clockwise quarter turns of the map before projecting (0 to 3).</param>
        public IsometricProjection(int tileWidth = 64, int tileHeight = 32, float heightPixelsPerUnit = 32f, int quarterTurns = 0)
        {
            if (tileWidth <= 0) throw new ArgumentOutOfRangeException(nameof(tileWidth), tileWidth, "A tile has a width.");
            if (tileHeight <= 0) throw new ArgumentOutOfRangeException(nameof(tileHeight), tileHeight, "A tile has a height.");
            TileWidth = tileWidth;
            TileHeight = tileHeight;
            HeightPixelsPerUnit = heightPixelsPerUnit;
            _turns = ((quarterTurns % 4) + 4) % 4;
            _halfWidth = tileWidth * 0.5f / tileHeight;
            _halfHeight = 0.5f;
            _rise = heightPixelsPerUnit / tileHeight;
        }

        public int TileWidth { get; }
        public int TileHeight { get; }
        public float HeightPixelsPerUnit { get; }
        public int QuarterTurns => _turns;

        public float ArtPixelsPerUnit => TileHeight;

        public Vec2 ToView(Vec2 floor, float height = 0f)
        {
            var p = Turn(floor, _turns);
            return new Vec2((p.X - p.Y) * _halfWidth, -(p.X + p.Y) * _halfHeight + height * _rise);
        }

        public Vec2 ToFloor(Vec2 view)
        {
            float difference = view.X / _halfWidth; // x - y
            float sum = -view.Y / _halfHeight;      // x + y
            return Turn(new Vec2((sum + difference) * 0.5f, (sum - difference) * 0.5f), 4 - _turns);
        }

        public RectF ViewBounds(RectF floor)
        {
            var a = ToView(new Vec2(floor.XMin, floor.YMin));
            var b = ToView(new Vec2(floor.XMax, floor.YMin));
            var c = ToView(new Vec2(floor.XMin, floor.YMax));
            var d = ToView(new Vec2(floor.XMax, floor.YMax));
            float xMin = SimMath.Min(SimMath.Min(a.X, b.X), SimMath.Min(c.X, d.X));
            float xMax = SimMath.Max(SimMath.Max(a.X, b.X), SimMath.Max(c.X, d.X));
            float yMin = SimMath.Min(SimMath.Min(a.Y, b.Y), SimMath.Min(c.Y, d.Y));
            float yMax = SimMath.Max(SimMath.Max(a.Y, b.Y), SimMath.Max(c.Y, d.Y));
            return new RectF(xMin, yMin, xMax - xMin, yMax - yMin);
        }

        public float Depth(Vec2 floor, float height = 0f)
        {
            var p = Turn(floor, _turns);
            return p.X + p.Y + height * HeightDepth;
        }

        /// <summary><paramref name="p"/> turned counter-clockwise by <paramref name="turns"/> quarter turns about the origin.</summary>
        private static Vec2 Turn(Vec2 p, int turns)
        {
            switch (turns & 3)
            {
                case 1: return new Vec2(-p.Y, p.X);
                case 2: return new Vec2(-p.X, -p.Y);
                case 3: return new Vec2(p.Y, -p.X);
                default: return p;
            }
        }
    }
}
