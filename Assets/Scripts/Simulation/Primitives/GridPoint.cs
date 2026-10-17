using System;

namespace Simpiens
{
    /// <summary>
    /// An integer grid cell (Epic CORE C4), replacing Unity's <c>Vector2Int</c> with the same formulas, hash code and layout
    /// (two ints). Rounding to a cell is half to even, as Unity's <c>RoundToInt</c>.
    /// </summary>
    [Serializable]
    public struct GridPoint : IEquatable<GridPoint>
    {
        public int X;
        public int Y;

        public GridPoint(int x, int y)
        {
            X = x;
            Y = y;
        }

        public static readonly GridPoint Zero = new GridPoint(0, 0);
        public static readonly GridPoint One = new GridPoint(1, 1);
        public static readonly GridPoint Up = new GridPoint(0, 1);
        public static readonly GridPoint Down = new GridPoint(0, -1);
        public static readonly GridPoint Left = new GridPoint(-1, 0);
        public static readonly GridPoint Right = new GridPoint(1, 0);

        public readonly int SqrMagnitude => X * X + Y * Y;
        public readonly float Magnitude => (float)Math.Sqrt(X * X + Y * Y);

        public static GridPoint RoundToInt(Vec2 v) => new GridPoint(SimMath.RoundToInt(v.X), SimMath.RoundToInt(v.Y));
        public static GridPoint FloorToInt(Vec2 v) => new GridPoint(SimMath.FloorToInt(v.X), SimMath.FloorToInt(v.Y));
        public static GridPoint CeilToInt(Vec2 v) => new GridPoint(SimMath.CeilToInt(v.X), SimMath.CeilToInt(v.Y));

        public static float Distance(GridPoint a, GridPoint b)
        {
            float dx = a.X - b.X;
            float dy = a.Y - b.Y;
            return (float)Math.Sqrt(dx * dx + dy * dy);
        }

        public static GridPoint Min(GridPoint a, GridPoint b) => new GridPoint(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y));
        public static GridPoint Max(GridPoint a, GridPoint b) => new GridPoint(Math.Max(a.X, b.X), Math.Max(a.Y, b.Y));

        /// <summary>The cell's position as a point, as Unity's implicit Vector2Int to Vector2.</summary>
        public static implicit operator Vec2(GridPoint v) => new Vec2(v.X, v.Y);

        public static GridPoint operator -(GridPoint v) => new GridPoint(-v.X, -v.Y);
        public static GridPoint operator +(GridPoint a, GridPoint b) => new GridPoint(a.X + b.X, a.Y + b.Y);
        public static GridPoint operator -(GridPoint a, GridPoint b) => new GridPoint(a.X - b.X, a.Y - b.Y);
        public static GridPoint operator *(GridPoint a, GridPoint b) => new GridPoint(a.X * b.X, a.Y * b.Y);
        public static GridPoint operator *(int a, GridPoint b) => new GridPoint(a * b.X, a * b.Y);
        public static GridPoint operator *(GridPoint a, int b) => new GridPoint(a.X * b, a.Y * b);
        public static GridPoint operator /(GridPoint a, int b) => new GridPoint(a.X / b, a.Y / b);

        public static bool operator ==(GridPoint lhs, GridPoint rhs) => lhs.X == rhs.X && lhs.Y == rhs.Y;
        public static bool operator !=(GridPoint lhs, GridPoint rhs) => !(lhs == rhs);

        public readonly bool Equals(GridPoint other) => X == other.X && Y == other.Y;
        public override readonly bool Equals(object obj) => obj is GridPoint other && Equals(other);

        /// <summary>Unity's Vector2Int hash, kept exactly.</summary>
        public override readonly int GetHashCode() => (X * 73856093) ^ (Y * 83492791);

        public override readonly string ToString() => "(" + X + ", " + Y + ")";
    }
}
