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

        public readonly float SqrMagnitude => X * X + Y * Y;
        public readonly float Magnitude => (float)Math.Sqrt(X * X + Y * Y);
        public readonly Vec2 Normalized => Normalize(this);

        public static Vec2 Normalize(Vec2 value)
        {
            float magnitude = value.Magnitude;
            if (!(magnitude > 1E-05f)) return Zero;
            return new Vec2(value.X / magnitude, value.Y / magnitude);
        }

        public static float Dot(Vec2 lhs, Vec2 rhs) => lhs.X * rhs.X + lhs.Y * rhs.Y;

        public static float Distance(Vec2 a, Vec2 b)
        {
            float dx = a.X - b.X;
            float dy = a.Y - b.Y;
            return (float)Math.Sqrt(dx * dx + dy * dy);
        }

        public static Vec2 Lerp(Vec2 a, Vec2 b, float t)
        {
            t = SimMath.Clamp01(t);
            return new Vec2(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t);
        }

        public static Vec2 LerpUnclamped(Vec2 a, Vec2 b, float t) => new Vec2(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t);

        public static Vec2 MoveTowards(Vec2 current, Vec2 target, float maxDistanceDelta)
        {
            float dx = target.X - current.X;
            float dy = target.Y - current.Y;
            float sqr = dx * dx + dy * dy;
            if (sqr == 0f || (maxDistanceDelta >= 0f && sqr <= maxDistanceDelta * maxDistanceDelta)) return target;
            float distance = (float)Math.Sqrt(sqr);
            return new Vec2(current.X + dx / distance * maxDistanceDelta, current.Y + dy / distance * maxDistanceDelta);
        }

        public static Vec2 ClampMagnitude(Vec2 vector, float maxLength)
        {
            float sqr = vector.SqrMagnitude;
            if (sqr > maxLength * maxLength)
            {
                float length = (float)Math.Sqrt(sqr);
                return new Vec2(vector.X / length * maxLength, vector.Y / length * maxLength);
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
            float dx = lhs.X - rhs.X;
            float dy = lhs.Y - rhs.Y;
            return dx * dx + dy * dy < 9.9999994E-11f;
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
