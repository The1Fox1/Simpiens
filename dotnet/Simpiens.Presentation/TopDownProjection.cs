namespace Simpiens.Presentation
{
    /// <summary>
    /// The top-down projection (Epic CLIENT): the view plane is the world, y up. Within a layer, what is lower on screen
    /// is drawn later (<see cref="Depth"/>), so overlapping crawlers look right. The main view until isometric (M3); then
    /// the developers' map designer and debugging.
    /// </summary>
    public sealed class TopDownProjection : IViewProjection
    {
        /// <summary>Art pixels per world unit: the sprites are 16x16 pixel art covering one cell.</summary>
        public const float BasePixelsPerUnit = 16f;

        public static TopDownProjection Instance { get; } = new TopDownProjection();

        private TopDownProjection() { }

        public float ArtPixelsPerUnit => BasePixelsPerUnit;

        public Vec2 ToView(Vec2 floor, float height = 0f) => floor;

        public Vec2 ToFloor(Vec2 view) => view;

        public RectF ViewBounds(RectF floor) => floor;

        /// <summary>Lower on screen (smaller world y) draws later.</summary>
        public float Depth(Vec2 floor, float height = 0f) => -floor.Y;
    }
}
