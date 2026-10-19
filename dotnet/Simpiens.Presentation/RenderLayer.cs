namespace Simpiens.Presentation
{
    /// <summary>
    /// Draw layers, bottom to top: the same order as Unity's <c>RenderOrder</c> (Epic MAP Phase 4), so the client draws
    /// the world as Unity does. The fog sits over the props but under the crawlers: the player can't see what an
    /// uncharted room holds, but can watch a scout walk into it (producer decision D1 = A). A draw item's layer is the
    /// first part of its sort key.
    /// </summary>
    public static class RenderLayer
    {
        public const int Floor = -30;
        public const int Rock = -20;
        public const int Walls = -10;
        public const int Props = 0;      // bunks, hearth, resource nodes
        public const int Fog = 10;
        public const int Crawlers = 20;
        public const int Selection = 25;
        /// <summary>The map's text: drawn after the world, in screen space (<see cref="MapLabelLayout"/>), so always on top.</summary>
        public const int Labels = 30;
    }
}
