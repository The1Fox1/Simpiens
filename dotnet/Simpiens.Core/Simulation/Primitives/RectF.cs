using System;

namespace Simpiens
{
    /// <summary>
    /// An axis-aligned rectangle in world units (Epic CORE C4), replacing Unity's <c>Rect</c> with the same storage
    /// (x, y, width, height) and formulas. <see cref="Contains"/> includes the minimum edges and excludes the maximum ones.
    /// </summary>
    [Serializable]
    public struct RectF : IEquatable<RectF>
    {
        public float X;
        public float Y;
        public float Width;
        public float Height;

        public RectF(float x, float y, float width, float height)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }

        public RectF(Vec2 position, Vec2 size) : this(position.X, position.Y, size.X, size.Y)
        {
        }

        public static RectF MinMaxRect(float xMin, float yMin, float xMax, float yMax) => new RectF(xMin, yMin, xMax - xMin, yMax - yMin);

        public readonly float XMin => X;
        public readonly float YMin => Y;
        public readonly float XMax => Width + X;
        public readonly float YMax => Height + Y;
        public readonly Vec2 Min => new Vec2(XMin, YMin);
        public readonly Vec2 Max => new Vec2(XMax, YMax);
        public readonly Vec2 Size => new Vec2(Width, Height);
        public readonly Vec2 Center => new Vec2(X + Width * 0.5f, Y + Height * 0.5f);

        public readonly bool Contains(Vec2 point) => point.X >= X && point.X < XMax && point.Y >= Y && point.Y < YMax;

        public readonly bool Overlaps(RectF other) => other.XMax > X && other.X < XMax && other.YMax > Y && other.Y < YMax;

        public static bool operator ==(RectF a, RectF b) => a.X == b.X && a.Y == b.Y && a.Width == b.Width && a.Height == b.Height;
        public static bool operator !=(RectF a, RectF b) => !(a == b);

        public readonly bool Equals(RectF other) => X.Equals(other.X) && Y.Equals(other.Y) && Width.Equals(other.Width) && Height.Equals(other.Height);
        public override readonly bool Equals(object obj) => obj is RectF other && Equals(other);

        public override readonly int GetHashCode() => X.GetHashCode() ^ (Width.GetHashCode() << 2) ^ (Y.GetHashCode() >> 2) ^ (Height.GetHashCode() >> 1);

        public override readonly string ToString() => $"(x:{X:F2}, y:{Y:F2}, width:{Width:F2}, height:{Height:F2})";
    }

    /// <summary>A colour as four floats in 0–1 (Epic CORE C4): map data for the views, which convert it to the engine's colour.</summary>
    [Serializable]
    public readonly struct Rgba : IEquatable<Rgba>
    {
        public readonly float R;
        public readonly float G;
        public readonly float B;
        public readonly float A;

        public Rgba(float r, float g, float b, float a = 1f)
        {
            R = r;
            G = g;
            B = b;
            A = a;
        }

        public static readonly Rgba White = new Rgba(1f, 1f, 1f);

        public bool Equals(Rgba other) => R == other.R && G == other.G && B == other.B && A == other.A;
        public override bool Equals(object obj) => obj is Rgba other && Equals(other);
        public override int GetHashCode() => R.GetHashCode() ^ (G.GetHashCode() << 2) ^ (B.GetHashCode() >> 2) ^ (A.GetHashCode() >> 1);
        public override string ToString() => $"RGBA({R:F3}, {G:F3}, {B:F3}, {A:F3})";
    }
}
