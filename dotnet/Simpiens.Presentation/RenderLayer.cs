namespace Simpiens.Presentation
{
    /// <summary>
    /// What a draw item is, for its place in the draw order (review A2). The projection maps each role to a layer
    /// (<see cref="IViewProjection.LayerOf"/>); within a layer, at equal depth, the earlier role draws first, so a bunk
    /// is under its sleeper and a prop under a crawler at the same point.
    /// </summary>
    public enum DrawRole : byte
    {
        Floor = 0,
        Rock = 1,
        /// <summary>The fog's cloud over an uncharted sector.</summary>
        Fog = 2,
        /// <summary>The ring under the selected crawler.</summary>
        Selection = 3,
        Wall = 4,
        /// <summary>Bunks, the hearth, resource nodes.</summary>
        Prop = 5,
        Crawler = 6,
    }

    /// <summary>
    /// Draw layers, bottom to top: the first part of a draw item's sort key. Top-down (<see cref="TopDownProjection"/>)
    /// keeps Unity's <c>RenderOrder</c> (Epic MAP Phase 4): a layer per role. What an uncharted sector holds, crawlers
    /// included, isn't drawn at all (producer decision D1 = C, <see cref="FogOfWar.Visibility"/>), so the fog's place in
    /// the order only matters while a cloud fades. Isometric (<see cref="IsometricProjection"/>) puts walls, props and crawlers in one
    /// <see cref="World"/> layer sorted by depth, so a crawler can walk behind a wall; the fog and the selection ring lie
    /// on the floor under it.
    /// </summary>
    public static class RenderLayer
    {
        public const int Floor = -30;
        public const int Rock = -20;
        /// <summary>Isometric: the fog's cloud, on the floor under everything standing.</summary>
        public const int FloorOverlay = -15;
        /// <summary>Isometric: the selection ring, on the floor, so a wall in front hides it.</summary>
        public const int FloorMarks = -14;
        public const int Walls = -10;
        public const int Props = 0;      // bunks, hearth, resource nodes
        /// <summary>Isometric: walls, props and crawlers, interleaved by depth.</summary>
        public const int World = 0;
        public const int Fog = 10;
        public const int Crawlers = 20;
        public const int Selection = 25;
        /// <summary>The map's text: drawn after the world, in screen space (<see cref="MapLabelLayout"/>), so always on top.</summary>
        public const int Labels = 30;
    }
}
