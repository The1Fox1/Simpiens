namespace Simpiens.Entities
{
    /// <summary>
    /// Sorting orders for everything drawn in the 2D view, bottom to top (Epic MAP Phase 4). The fog sits over the
    /// props but under the crawlers: the player can't see what an uncharted room holds, but can watch a scout walk
    /// into it (producer decision D1 = A).
    /// </summary>
    public static class RenderOrder
    {
        public const int Floor = -30;
        public const int Rock = -20;
        public const int Walls = -10;
        public const int Props = 0;      // bunks, hearth, resource nodes
        public const int Fog = 10;
        public const int Crawlers = 20;
        public const int Selection = 25;
        public const int Labels = 30;
    }
}
