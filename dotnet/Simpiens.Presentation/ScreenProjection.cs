namespace Simpiens.Presentation
{
    /// <summary>
    /// Screen space for hard cutscenes (M3 design D9): a virtual screen of <see cref="VirtualWidth"/> by
    /// <see cref="VirtualHeight"/> units, x right and y down from its top-left, one art pixel a unit. Through a
    /// <see cref="View"/> (<see cref="Fit"/>) it fills the window at a whole number of pixels a unit when it can,
    /// letterboxed in the middle; the renderer, its draw list and sort key work unchanged. There is no depth: within a
    /// layer, things are drawn in the order they were added.
    /// </summary>
    public sealed class ScreenProjection : IViewProjection
    {
        public const int VirtualWidth = 640;
        public const int VirtualHeight = 360;

        public static ScreenProjection Instance { get; } = new ScreenProjection();

        private ScreenProjection() { }

        public float ArtPixelsPerUnit => 1f;

        /// <summary>The view plane's y is up; the virtual screen's is down. Height lifts a point up the screen.</summary>
        public Vec2 ToView(Vec2 point, float height = 0f) => new Vec2(point.X, -point.Y + height);

        public Vec2 ToFloor(Vec2 view) => new Vec2(view.X, -view.Y);

        public RectF ViewBounds(RectF rect) => new RectF(rect.XMin, -rect.YMax, rect.Width, rect.Height);

        public float Depth(Vec2 point, float height = 0f) => 0f;

        /// <summary>The backdrop lies under everything; the rest stands in one layer, in the order added.</summary>
        public int LayerOf(DrawRole role) => role == DrawRole.Floor ? RenderLayer.Floor : RenderLayer.World;

        /// <summary>
        /// The virtual screen in a window of <paramref name="width"/> by <paramref name="height"/> pixels: the largest whole
        /// scale that fits, centred (letterboxed); in a window smaller than the virtual screen, the scale that just fits.
        /// </summary>
        public static View Fit(int width, int height)
        {
            float fit = SimMath.Min((float)width / VirtualWidth, (float)height / VirtualHeight);
            float scale = fit >= 1f ? SimMath.Floor(fit) : fit;
            return new View(Instance, new ViewCamera(new Vec2(VirtualWidth * 0.5f, VirtualHeight * 0.5f), scale, width, height));
        }

        /// <summary>Where the virtual screen lands in the window, in pixels: the letterbox's inside.</summary>
        public static RectF Letterbox(in View view)
        {
            var topLeft = view.WorldToScreen(Vec2.Zero);
            return new RectF(topLeft.X, topLeft.Y, VirtualWidth * view.PixelsPerUnit, VirtualHeight * view.PixelsPerUnit);
        }
    }
}
