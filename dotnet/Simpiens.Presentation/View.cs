namespace Simpiens.Presentation
{
    /// <summary>
    /// How a view maps the world onto the screen (Epic CLIENT, review A1): one small stateless object per kind of view,
    /// top-down today and isometric at M3. A projection is linear: it maps the floor onto a view plane in world-sized
    /// units (x right, y up), and <see cref="View"/> does the rest (the camera, pixels, snapping) the same way for every
    /// projection. Top-down's view plane is the world itself.
    /// </summary>
    public interface IViewProjection
    {
        /// <summary>Art pixels per view unit at zoom 1 (top-down: 16, a sprite's pixels per cell). A power of two, so sizes in art pixels convert exactly.</summary>
        float ArtPixelsPerUnit { get; }

        /// <summary>A point <paramref name="height"/> world units above the floor point, on the view plane.</summary>
        Vec2 ToView(Vec2 floor, float height = 0f);

        /// <summary>The floor point (height 0) at a point of the view plane: the inverse of <see cref="ToView"/>.</summary>
        Vec2 ToFloor(Vec2 view);

        /// <summary>The smallest view-plane rectangle holding a floor rectangle.</summary>
        RectF ViewBounds(RectF floor);

        /// <summary>Order within a draw layer: higher depth is drawn later, in front.</summary>
        float Depth(Vec2 floor, float height = 0f);

        /// <summary>The draw layer for items of a role (review A2): which roles share a layer, and so interleave by depth.</summary>
        int LayerOf(DrawRole role);
    }

    /// <summary>Where a view looks and how close: the floor point at the viewport's centre, the scale and the viewport.</summary>
    public readonly struct ViewCamera
    {
        public ViewCamera(Vec2 centre, float pixelsPerUnit, int viewportWidth, int viewportHeight)
        {
            Centre = centre;
            PixelsPerUnit = pixelsPerUnit;
            ViewportWidth = viewportWidth;
            ViewportHeight = viewportHeight;
        }

        /// <summary>The floor point at the viewport's centre.</summary>
        public Vec2 Centre { get; }
        /// <summary>Screen pixels per view unit.</summary>
        public float PixelsPerUnit { get; }
        public int ViewportWidth { get; }
        public int ViewportHeight { get; }
    }

    /// <summary>
    /// A projection seen through a camera: world to screen pixels and back, for the renderer, the picker, the map's
    /// text and the camera controls. Screen y points down. Built once per frame; holds no references but the projection.
    /// </summary>
    public readonly struct View
    {
        private readonly Vec2 _centre; // the camera's centre on the view plane
        private readonly float _zoom;

        public View(IViewProjection projection, ViewCamera camera)
        {
            Projection = projection;
            Camera = camera;
            _centre = projection.ToView(camera.Centre);
            _zoom = camera.PixelsPerUnit / projection.ArtPixelsPerUnit;
        }

        public IViewProjection Projection { get; }
        public ViewCamera Camera { get; }

        public float PixelsPerUnit => Camera.PixelsPerUnit;
        /// <summary>Screen pixels per art pixel.</summary>
        public float Zoom => _zoom;
        public int ViewportWidth => Camera.ViewportWidth;
        public int ViewportHeight => Camera.ViewportHeight;

        public Vec2 WorldToScreen(Vec2 floor, float height = 0f)
        {
            var v = Projection.ToView(floor, height);
            return new Vec2((v.X - _centre.X) * Camera.PixelsPerUnit + Camera.ViewportWidth * 0.5f, Camera.ViewportHeight * 0.5f - (v.Y - _centre.Y) * Camera.PixelsPerUnit);
        }

        /// <summary>The floor point under a screen pixel.</summary>
        public Vec2 ScreenToFloor(Vec2 screen)
            => Projection.ToFloor(new Vec2((screen.X - Camera.ViewportWidth * 0.5f) / Camera.PixelsPerUnit + _centre.X, (Camera.ViewportHeight * 0.5f - screen.Y) / Camera.PixelsPerUnit + _centre.Y));

        /// <summary>
        /// The same view moved by under a pixel so that the view plane's whole units land on whole screen pixels: map
        /// tiles then start on pixel boundaries and point-sampled art keeps every pixel.
        /// </summary>
        public View SnappedToPixels()
        {
            float ppu = Camera.PixelsPerUnit;
            float offsetX = Camera.ViewportWidth * 0.5f - _centre.X * ppu;
            float offsetY = Camera.ViewportHeight * 0.5f + _centre.Y * ppu;
            float x = (Camera.ViewportWidth * 0.5f - SimMath.Round(offsetX)) / ppu;
            float y = (SimMath.Round(offsetY) - Camera.ViewportHeight * 0.5f) / ppu;
            return new View(Projection, new ViewCamera(Projection.ToFloor(new Vec2(x, y)), ppu, Camera.ViewportWidth, Camera.ViewportHeight));
        }

        /// <summary>
        /// The view that shows all of the floor rectangle <paramref name="frame"/> in the viewport. With
        /// <paramref name="wholePixels"/>, the scale is a whole number of screen pixels per art pixel (at least one), so
        /// pixel art stays crisp.
        /// </summary>
        public static View Framing(IViewProjection projection, RectF frame, int viewportWidth, int viewportHeight, bool wholePixels = true)
        {
            var bounds = projection.ViewBounds(frame);
            float fit = SimMath.Min(viewportWidth / bounds.Width, viewportHeight / bounds.Height);
            if (!wholePixels) return new View(projection, new ViewCamera(frame.Center, fit, viewportWidth, viewportHeight));

            float art = projection.ArtPixelsPerUnit;
            fit = SimMath.Max(1f, SimMath.Floor(fit / art)) * art;
            return new View(projection, new ViewCamera(frame.Center, fit, viewportWidth, viewportHeight)).SnappedToPixels();
        }
    }
}
