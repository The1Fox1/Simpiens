using UnityEngine;

namespace Simpiens.Entities
{
    /// <summary>
    /// Camera maths for the 2D view (Epic MAP Phase 4), plain C# so it can be tested headless: an orthographic view
    /// (centre + half-height) clamped to the map's camera frame plus a margin, zoom that keeps the world point under
    /// the cursor fixed, smooth follow and reframe. <see cref="CameraController"/> feeds it input and applies it.
    /// </summary>
    public sealed class CameraRig
    {
        /// <summary>World units the view may show beyond the map's camera frame.</summary>
        public const float Margin = 2f;

        /// <summary>Closest zoom: about one room's height.</summary>
        public const float MinSize = 3f;

        /// <summary>Follow closes this fraction of the gap per second, exponentially (frame-rate independent).</summary>
        public const float FollowSharpness = 6f;

        public Rect Frame { get; private set; }
        public float Aspect { get; private set; } = 16f / 9f;

        /// <summary>View centre in world units.</summary>
        public Vector2 Position { get; private set; }

        /// <summary>Orthographic size: half the view's height in world units.</summary>
        public float Size { get; private set; } = MinSize;

        /// <summary>The size that fits the whole frame on screen.</summary>
        public float MaxSize => Mathf.Max(MinSize, Mathf.Max(Frame.height * 0.5f, Frame.width * 0.5f / Aspect));

        /// <summary>Sets the map frame and the screen aspect, and shows the whole frame.</summary>
        public void Configure(Rect frame, float aspect)
        {
            Frame = frame;
            Aspect = aspect > 0f ? aspect : 16f / 9f;
            Reframe();
        }

        public void Reframe()
        {
            Size = MaxSize;
            Position = Frame.center;
            Clamp();
        }

        /// <summary>The screen changed shape: keep the view inside the new limits.</summary>
        public void SetAspect(float aspect)
        {
            if (aspect <= 0f || Mathf.Approximately(aspect, Aspect)) return;
            Aspect = aspect;
            Size = Mathf.Clamp(Size, MinSize, MaxSize);
            Clamp();
        }

        public void Pan(Vector2 worldDelta)
        {
            Position += worldDelta;
            Clamp();
        }

        /// <summary>
        /// Zooms by <paramref name="factor"/> (below 1 zooms in) about <paramref name="worldPoint"/>, which stays at
        /// the same place on screen unless the clamp at a map edge has to move the view.
        /// </summary>
        public void ZoomAt(Vector2 worldPoint, float factor)
        {
            if (factor <= 0f) return;
            float newSize = Mathf.Clamp(Size * factor, MinSize, MaxSize);
            Position = worldPoint + (Position - worldPoint) * (newSize / Size);
            Size = newSize;
            Clamp();
        }

        /// <summary>Moves the view toward <paramref name="target"/> over <paramref name="deltaSeconds"/>.</summary>
        public void Follow(Vector2 target, float deltaSeconds)
        {
            float k = 1f - Mathf.Exp(-FollowSharpness * Mathf.Max(0f, deltaSeconds));
            Position = Vector2.Lerp(Position, target, k);
            Clamp();
        }

        /// <summary>The world point at a viewport position (0..1 on both axes, origin bottom-left).</summary>
        public Vector2 ViewportToWorld(Vector2 viewport)
        {
            return Position + new Vector2((viewport.x - 0.5f) * 2f * Size * Aspect, (viewport.y - 0.5f) * 2f * Size);
        }

        private void Clamp()
        {
            var allowed = new Rect(Frame.xMin - Margin, Frame.yMin - Margin, Frame.width + 2f * Margin, Frame.height + 2f * Margin);
            float halfW = Size * Aspect;
            float halfH = Size;
            float x = allowed.width <= 2f * halfW ? allowed.center.x : Mathf.Clamp(Position.x, allowed.xMin + halfW, allowed.xMax - halfW);
            float y = allowed.height <= 2f * halfH ? allowed.center.y : Mathf.Clamp(Position.y, allowed.yMin + halfH, allowed.yMax - halfH);
            Position = new Vector2(x, y);
        }
    }
}
