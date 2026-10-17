using System;

namespace Simpiens
{
    /// <summary>
    /// The simulation's scalar maths (Epic CORE C4): Unity's <c>Mathf</c> formulas, copied exactly, so replacing <c>Mathf</c>
    /// changes no result and the golden world hashes hold. Engine-free; root namespace like <see cref="EntityId"/>.
    /// </summary>
    public static class SimMath
    {
        public const float PI = 3.14159274f; // (float)Math.PI, as Mathf.PI

        /// <summary>Unity's Mathf.Epsilon on a desktop runtime (flush-to-zero off): the smallest denormal float.</summary>
        public const float Epsilon = 1.401298E-45f;

        private const float ApproxEpsilon = Epsilon * 8f;

        public static float Sin(float f) => (float)Math.Sin(f);
        public static float Cos(float f) => (float)Math.Cos(f);
        public static float Atan2(float y, float x) => (float)Math.Atan2(y, x);
        public static float Sqrt(float f) => (float)Math.Sqrt(f);
        public static float Abs(float f) => Math.Abs(f);
        public static int Abs(int value) => Math.Abs(value);

        public static float Min(float a, float b) => a < b ? a : b;
        public static float Max(float a, float b) => a > b ? a : b;
        public static int Min(int a, int b) => a < b ? a : b;
        public static int Max(int a, int b) => a > b ? a : b;

        public static float Floor(float f) => (float)Math.Floor(f);
        public static float Ceil(float f) => (float)Math.Ceiling(f);

        /// <summary>Rounds half to even, as Mathf.Round (Math.Round on a double).</summary>
        public static float Round(float f) => (float)Math.Round(f);

        public static int FloorToInt(float f) => (int)Math.Floor(f);
        public static int CeilToInt(float f) => (int)Math.Ceiling(f);

        /// <summary>Rounds half to even, as Mathf.RoundToInt.</summary>
        public static int RoundToInt(float f) => (int)Math.Round(f);

        public static float Clamp(float value, float min, float max)
        {
            if (value < min) value = min;
            else if (value > max) value = max;
            return value;
        }

        public static int Clamp(int value, int min, int max)
        {
            if (value < min) value = min;
            else if (value > max) value = max;
            return value;
        }

        public static float Clamp01(float value)
        {
            if (value < 0f) return 0f;
            if (value > 1f) return 1f;
            return value;
        }

        public static float Lerp(float a, float b, float t) => a + (b - a) * Clamp01(t);

        /// <summary>Unity's relative comparison: within a millionth of the larger magnitude, or a few denormals.</summary>
        public static bool Approximately(float a, float b)
        {
            return Abs(b - a) < Max(1E-06f * Max(Abs(a), Abs(b)), ApproxEpsilon);
        }
    }
}
