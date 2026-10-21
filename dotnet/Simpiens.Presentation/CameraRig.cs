using System;

namespace Simpiens.Presentation
{
    /// <summary>
    /// Camera maths for the map view (Epic CLIENT M1.4), a port of Unity's <c>CameraRig</c>:
    /// <list type="bullet">
    /// <item><description>an orthographic view (centre, half-height) clamped to the map's camera frame plus a margin;</description></item>
    /// <item><description>zoom that keeps the world point under the cursor fixed;</description></item>
    /// <item><description>smooth follow and reframe.</description></item>
    /// </list>
    /// One addition for pixel art: given the viewport's height in pixels, reframing shows the whole map at a whole
    /// number of screen pixels per art pixel (crisp), and that view is the zoom-out limit. Without a pixel height it is
    /// Unity's rig exactly.
    /// The rig works on the projection's view plane (review A1): <see cref="Position"/> is a floor point, and the view's
    /// extent, the clamp and the pans are measured on screen axes. In top-down the view plane is the world, so the
    /// numbers are Unity's; an isometric projection needs no other code.
    /// </summary>
    public sealed class CameraRig
    {
        /// <summary>World units the view may show beyond the map's camera frame.</summary>
        public const float Margin = 2f;
        /// <summary>Closest zoom: about one room's height.</summary>
        public const float MinSize = 3f;
        /// <summary>Follow closes this fraction of the gap per second, exponentially (frame-rate independent).</summary>
        public const float FollowSharpness = 6f;

        private IViewProjection _projection = TopDownProjection.Instance;

        /// <summary>How the map is projected; setting it keeps the view inside the limits.</summary>
        public IViewProjection Projection
        {
            get => _projection;
            set
            {
                _projection = value ?? TopDownProjection.Instance;
                Size = SimMath.Clamp(Size, MinSize, MaxSize);
                Clamp();
            }
        }

        public RectF Frame { get; private set; }
        public float Aspect { get; private set; } = 16f / 9f;
        /// <summary>The viewport's height in pixels, or 0 for Unity's continuous framing.</summary>
        public int PixelHeight { get; private set; }
        /// <summary>View centre: the floor point at the middle of the screen.</summary>
        public Vec2 Position { get; private set; }
        /// <summary>Orthographic size: half the view's height in view units (world units in top-down).</summary>
        public float Size { get; private set; } = MinSize;

        /// <summary>The size that just fits the whole frame on screen (Unity's <c>MaxSize</c>).</summary>
        public float FitSize
        {
            get
            {
                var frame = _projection.ViewBounds(Frame);
                return SimMath.Max(MinSize, SimMath.Max(frame.Height * 0.5f, frame.Width * 0.5f / Aspect));
            }
        }

        /// <summary>The furthest zoom: the fit, or with a pixel height, the next size out at whole pixels per art pixel.</summary>
        public float MaxSize => PixelHeight > 0 ? SimMath.Max(FitSize, WholePixelSize(FitSize)) : FitSize;

        /// <summary>Sets the map frame and the screen shape, and shows the whole frame.</summary>
        public void Configure(RectF frame, float aspect, int pixelHeight = 0)
        {
            Frame = frame;
            Aspect = aspect > 0f ? aspect : 16f / 9f;
            PixelHeight = Math.Max(0, pixelHeight);
            Reframe();
        }

        public void Reframe()
        {
            Size = MaxSize;
            Position = Frame.Center;
            Clamp();
        }

        /// <summary>The screen changed shape: keep the view inside the new limits.</summary>
        public void SetAspect(float aspect)
        {
            if (aspect <= 0f || SimMath.Approximately(aspect, Aspect)) return;
            Aspect = aspect;
            Size = SimMath.Clamp(Size, MinSize, MaxSize);
            Clamp();
        }

        /// <summary>The window's height changed (pixels): the whole-pixel limit follows it.</summary>
        public void SetPixelHeight(int pixelHeight)
        {
            if (pixelHeight <= 0 || pixelHeight == PixelHeight) return;
            PixelHeight = pixelHeight;
            Size = SimMath.Clamp(Size, MinSize, MaxSize);
            Clamp();
        }

        /// <summary>Moves the view by <paramref name="worldDelta"/> on the floor (<see cref="PanOnScreen"/> for screen directions).</summary>
        public void Pan(Vec2 worldDelta)
        {
            Position += worldDelta;
            Clamp();
        }

        /// <summary>Moves the view by <paramref name="viewDelta"/> along the screen's axes (x right, y up), in view units.</summary>
        public void PanOnScreen(Vec2 viewDelta) => Pan(_projection.ToFloor(viewDelta));

        /// <summary>
        /// Zooms by <paramref name="factor"/> (below 1 zooms in) about <paramref name="worldPoint"/>, which stays at
        /// the same place on screen unless the clamp at a map edge has to move the view.
        /// </summary>
        public void ZoomAt(Vec2 worldPoint, float factor)
        {
            if (factor <= 0f) return;
            float newSize = SimMath.Clamp(Size * factor, MinSize, MaxSize);
            Position = worldPoint + (Position - worldPoint) * (newSize / Size);
            Size = newSize;
            Clamp();
        }

        private Vec2 _lastTarget;
        private bool _hasLastTarget;

        /// <summary>Starts following afresh (a new target): the next <see cref="Follow"/> doesn't carry the old one's motion.</summary>
        public void ResetFollow() => _hasLastTarget = false;

        /// <summary>
        /// Moves the view with <paramref name="target"/> (by its motion since the last call) and closes the remaining
        /// gap exponentially over <paramref name="deltaSeconds"/>, so a crawler walking at 10x never pulls ahead.
        /// </summary>
        public void Follow(Vec2 target, float deltaSeconds)
        {
            if (_hasLastTarget) Position += target - _lastTarget;
            _lastTarget = target;
            _hasLastTarget = true;
            float k = 1f - MathF.Exp(-FollowSharpness * SimMath.Max(0f, deltaSeconds));
            Position = Vec2.Lerp(Position, target, k);
            Clamp();
        }

        /// <summary>The floor point at a viewport position (0..1 on both axes, origin bottom-left).</summary>
        public Vec2 ViewportToWorld(Vec2 viewport)
            => _projection.ToFloor(_projection.ToView(Position) + new Vec2((viewport.X - 0.5f) * 2f * Size * Aspect, (viewport.Y - 0.5f) * 2f * Size));

        /// <summary>The view that draws this into a viewport of the given size (pixels).</summary>
        public View ToView(int viewportWidth, int viewportHeight)
            => new View(_projection, new ViewCamera(Position, viewportHeight / (2f * Size), viewportWidth, viewportHeight)).SnappedToPixels();

        private float WholePixelSize(float size)
        {
            float art = _projection.ArtPixelsPerUnit;
            float pixelsPerUnit = PixelHeight / (2f * size);
            float whole = SimMath.Max(1f, SimMath.Floor(pixelsPerUnit / art)) * art;
            return PixelHeight / (2f * whole);
        }

        /// <summary>Keeps the view (on the view plane) inside the frame plus the margin (a floor rectangle, projected).</summary>
        private void Clamp()
        {
            var allowed = _projection.ViewBounds(new RectF(Frame.XMin - Margin, Frame.YMin - Margin, Frame.Width + 2f * Margin, Frame.Height + 2f * Margin));
            var centre = _projection.ToView(Position);
            float halfW = Size * Aspect;
            float halfH = Size;
            float x = allowed.Width <= 2f * halfW ? allowed.Center.X : SimMath.Clamp(centre.X, allowed.XMin + halfW, allowed.XMax - halfW);
            float y = allowed.Height <= 2f * halfH ? allowed.Center.Y : SimMath.Clamp(centre.Y, allowed.YMin + halfH, allowed.YMax - halfH);
            Position = _projection.ToFloor(new Vec2(x, y));
        }
    }
}
