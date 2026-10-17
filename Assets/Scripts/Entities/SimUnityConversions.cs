using UnityEngine;

namespace Simpiens
{
    /// <summary>
    /// Conversions at the view boundary between the simulation's engine-free maths (<see cref="Vec2"/>,
    /// <see cref="GridPoint"/>, <see cref="RectF"/>, <see cref="Rgba"/>) and Unity's (Epic CORE C4). Views, the
    /// dashboard and the test harness convert here; the simulation never sees an engine type.
    /// </summary>
    public static class SimUnityConversions
    {
        public static Vector2 ToUnity(this Vec2 v) => new Vector2(v.X, v.Y);
        public static Vector3 ToUnity3(this Vec2 v, float z = 0f) => new Vector3(v.X, v.Y, z);
        public static Vec2 ToSim(this Vector2 v) => new Vec2(v.x, v.y);
        public static Vec2 ToSim(this Vector3 v) => new Vec2(v.x, v.y);

        public static Vector2Int ToUnity(this GridPoint p) => new Vector2Int(p.X, p.Y);
        public static GridPoint ToSim(this Vector2Int p) => new GridPoint(p.x, p.y);

        public static Rect ToUnity(this RectF r) => new Rect(r.X, r.Y, r.Width, r.Height);
        public static RectF ToSim(this Rect r) => new RectF(r.x, r.y, r.width, r.height);

        public static Color ToUnity(this Rgba c) => new Color(c.R, c.G, c.B, c.A);
        public static Rgba ToSim(this Color c) => new Rgba(c.r, c.g, c.b, c.a);
    }
}
