using System;
using System.Globalization;

namespace Simpiens
{
    /// <summary>
    /// A 2D point or direction in world units (Epic CORE C4). Engine-free; its arithmetic copies Unity's <c>Vector2</c>
    /// formula for formula, so replacing <c>Vector2</c> changes no result and the golden world hashes hold. Same layout
    /// (two floats), so saved records keep their format. Two of Unity's quirks are kept on purpose:
    /// <list type="bullet">
    /// <item><c>==</c> is approximate (squared distance under 1e-10), while <see cref="Equals(Vec2)"/> is exact;</item>
    /// <item><see cref="Normalized"/> is zero for vectors shorter than 1e-5.</item>
    /// </list>
    /// Every scalar step is cast to float (Epic 10 G1, Epic CORE F1): Unity's Mono keeps a chain's intermediates at
    /// double precision and CoreCLR doesn't, so <c>x * x + y * y</c> could differ in its last bit, and crowding tests on
    /// near-equal distances went different ways (the golden seeds parted). An explicit cast makes Mono round as CoreCLR
    /// does; on CoreCLR it changes nothing.
    /// </summary>
    [Serializable]
    public struct Vec2 : IEquatable<Vec2>
    {
        public float X;
        public float Y;

        public Vec2(float x, float y)
        {
            X = x;
            Y = y;
        }

        public static readonly Vec2 Zero = new Vec2(0f, 0f);
        public static readonly Vec2 One = new Vec2(1f, 1f);
        public static readonly Vec2 Up = new Vec2(0f, 1f);
        public static readonly Vec2 Down = new Vec2(0f, -1f);
        public static readonly Vec2 Left = new Vec2(-1f, 0f);
        public static readonly Vec2 Right = new Vec2(1f, 0f);

        public readonly float SqrMagnitude => (float)((float)(X * X) + (float)(Y * Y));
        public readonly float Magnitude => (float)Math.Sqrt(SqrMagnitude);
        public readonly Vec2 Normalized => Normalize(this);

        public static Vec2 Normalize(Vec2 value)
        {
            float magnitude = value.Magnitude;
            if (!(magnitude > 1E-05f)) return Zero;
            return new Vec2(value.X / magnitude, value.Y / magnitude);
        }

        public static float Dot(Vec2 lhs, Vec2 rhs) => (float)((float)(lhs.X * rhs.X) + (float)(lhs.Y * rhs.Y));

        public static float Distance(Vec2 a, Vec2 b)
        {
            float dx = (float)(a.X - b.X);
            float dy = (float)(a.Y - b.Y);
            return (float)Math.Sqrt((float)((float)(dx * dx) + (float)(dy * dy)));
        }

        public static Vec2 Lerp(Vec2 a, Vec2 b, float t)
        {
            t = SimMath.Clamp01(t);
            return LerpUnclamped(a, b, t);
        }

        public static Vec2 LerpUnclamped(Vec2 a, Vec2 b, float t) =>
            new Vec2((float)(a.X + (float)((float)(b.X - a.X) * t)), (float)(a.Y + (float)((float)(b.Y - a.Y) * t)));

        public static Vec2 MoveTowards(Vec2 current, Vec2 target, float maxDistanceDelta)
        {
            float dx = (float)(target.X - current.X);
            float dy = (float)(target.Y - current.Y);
            float sqr = (float)((float)(dx * dx) + (float)(dy * dy));
            if (sqr == 0f || (maxDistanceDelta >= 0f && sqr <= (float)(maxDistanceDelta * maxDistanceDelta))) return target;
            float distance = (float)Math.Sqrt(sqr);
            return new Vec2((float)(current.X + (float)((float)(dx / distance) * maxDistanceDelta)),
                            (float)(current.Y + (float)((float)(dy / distance) * maxDistanceDelta)));
        }

        public static Vec2 ClampMagnitude(Vec2 vector, float maxLength)
        {
            float sqr = vector.SqrMagnitude;
            if (sqr > (float)(maxLength * maxLength))
            {
                float length = (float)Math.Sqrt(sqr);
                return new Vec2((float)((float)(vector.X / length) * maxLength), (float)((float)(vector.Y / length) * maxLength));
            }
            return vector;
        }

        public static Vec2 Scale(Vec2 a, Vec2 b) => new Vec2(a.X * b.X, a.Y * b.Y);

        public static Vec2 Min(Vec2 a, Vec2 b) => new Vec2(SimMath.Min(a.X, b.X), SimMath.Min(a.Y, b.Y));
        public static Vec2 Max(Vec2 a, Vec2 b) => new Vec2(SimMath.Max(a.X, b.X), SimMath.Max(a.Y, b.Y));

        public static Vec2 operator +(Vec2 a, Vec2 b) => new Vec2(a.X + b.X, a.Y + b.Y);
        public static Vec2 operator -(Vec2 a, Vec2 b) => new Vec2(a.X - b.X, a.Y - b.Y);
        public static Vec2 operator *(Vec2 a, Vec2 b) => new Vec2(a.X * b.X, a.Y * b.Y);
        public static Vec2 operator /(Vec2 a, Vec2 b) => new Vec2(a.X / b.X, a.Y / b.Y);
        public static Vec2 operator -(Vec2 a) => new Vec2(0f - a.X, 0f - a.Y);
        public static Vec2 operator *(Vec2 a, float d) => new Vec2(a.X * d, a.Y * d);
        public static Vec2 operator *(float d, Vec2 a) => new Vec2(a.X * d, a.Y * d);
        public static Vec2 operator /(Vec2 a, float d) => new Vec2(a.X / d, a.Y / d);

        /// <summary>Approximate, as Unity's Vector2: true when the squared distance is under 1e-10.</summary>
        public static bool operator ==(Vec2 lhs, Vec2 rhs)
        {
            float dx = (float)(lhs.X - rhs.X);
            float dy = (float)(lhs.Y - rhs.Y);
            return (float)((float)(dx * dx) + (float)(dy * dy)) < 9.9999994E-11f;
        }

        public static bool operator !=(Vec2 lhs, Vec2 rhs) => !(lhs == rhs);

        /// <summary>Exact, as Unity's Vector2.Equals.</summary>
        public readonly bool Equals(Vec2 other) => X == other.X && Y == other.Y;

        public override readonly bool Equals(object obj) => obj is Vec2 other && Equals(other);

        public override readonly int GetHashCode() => X.GetHashCode() ^ (Y.GetHashCode() << 2);

        /// <summary>"(x, y)" with two decimals, as Unity's Vector2.ToString.</summary>
        public override readonly string ToString()
        {
            return "(" + X.ToString("F2", CultureInfo.InvariantCulture) + ", " + Y.ToString("F2", CultureInfo.InvariantCulture) + ")";
        }
    }
}
