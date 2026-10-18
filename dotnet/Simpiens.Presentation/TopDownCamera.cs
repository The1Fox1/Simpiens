namespace Simpiens.Presentation
{
    /// <summary>
    /// The top-down projection (Epic CLIENT): world units to screen pixels and back. World y points up, screen y down.
    /// Within a layer, what is lower on screen is drawn later (<see cref="Depth"/>), so overlapping crawlers look right.
    /// </summary>
    public readonly struct TopDownCamera
    {
        /// <summary>Pixels per world unit at zoom 1: the sprites are 16x16 pixel art covering one unit.</summary>
        public const float BasePixelsPerUnit = 16f;

        public TopDownCamera(Vec2 centre, float pixelsPerUnit, int viewportWidth, int viewportHeight)
        {
            Centre = centre;
            PixelsPerUnit = pixelsPerUnit;
            ViewportWidth = viewportWidth;
            ViewportHeight = viewportHeight;
        }

        public Vec2 Centre { get; }
        public float PixelsPerUnit { get; }
        public int ViewportWidth { get; }
        public int ViewportHeight { get; }

        /// <summary>
        /// The camera that shows all of <paramref name="frame"/> in the viewport. With <paramref name="wholePixels"/>,
        /// the scale is a whole number of screen pixels per art pixel (at least one), so pixel art stays crisp.
        /// </summary>
        public static TopDownCamera Framing(RectF frame, int viewportWidth, int viewportHeight, bool wholePixels = true)
        {
            float fit = SimMath.Min(viewportWidth / frame.Width, viewportHeight / frame.Height);
            if (wholePixels) fit = SimMath.Max(1f, SimMath.Floor(fit / BasePixelsPerUnit)) * BasePixelsPerUnit;
            return new TopDownCamera(frame.Center, fit, viewportWidth, viewportHeight);
        }

        public Vec2 WorldToScreen(Vec2 world)
            => new Vec2((world.X - Centre.X) * PixelsPerUnit + ViewportWidth * 0.5f, ViewportHeight * 0.5f - (world.Y - Centre.Y) * PixelsPerUnit);

        public Vec2 ScreenToWorld(Vec2 screen)
            => new Vec2((screen.X - ViewportWidth * 0.5f) / PixelsPerUnit + Centre.X, (ViewportHeight * 0.5f - screen.Y) / PixelsPerUnit + Centre.Y);

        /// <summary>Depth within a layer: lower on screen (smaller world y) draws later.</summary>
        public static float Depth(Vec2 world) => -world.Y;
    }
}
