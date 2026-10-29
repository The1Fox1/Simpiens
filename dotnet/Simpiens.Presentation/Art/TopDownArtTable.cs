using Simpiens.Entities;

namespace Simpiens.Presentation.Art
{
    /// <summary>
    /// Top-down's art as data (graphics V1; Epic CLIENT M0–M1): the generated 16x16 sprites at Unity's sizes, centred
    /// on their points. Sizes are in world units, at <see cref="TopDownProjection.BasePixelsPerUnit"/> art pixels each:
    /// crawlers 0.7 of a cell, resource nodes 0.8, the hearth, bunks and the ring 0.9. No shadow under crawlers.
    /// </summary>
    public static class TopDownArtTable
    {
        public const float CrawlerSize = 0.7f;
        public const float ResourceSize = 0.8f;
        public const float HearthSize = 0.9f;
        public const float BunkSize = 0.9f;
        public const float SelectionRingSize = 0.9f;

        /// <summary>The hearth: a two-frame flame at 6 Hz and a subtle pulse (Unity's CampfireView).</summary>
        public const float HearthFramesPerSecond = 6f, HearthPulseRate = 4f, HearthPulseAmplitude = 0.03f;

        private const float Art = TopDownProjection.BasePixelsPerUnit;
        private const DrawSheet Sheet = DrawSheet.World;

        private static ArtCatalog Build()
        {
            var art = new ArtCatalog.Builder("top-down") { NameOffset = new Vec2(0f, 0.65f), NameHeight = 0f }; // above the crawler (Unity's CameraController)
            art.Add(ArtKeys.TileFloor, Sheet, new[] { "Map_Floor" }, 1f, 1f, Art)
               .Add(ArtKeys.TileRock, Sheet, new[] { "Map_Rock" }, 1f, 1f, Art)
               .Add(ArtKeys.TileWall, Sheet, new[] { "Obstacle_StoneWall" }, 1f, 1f, Art)
               .Add(ArtKeys.TileWallLow, Sheet, new[] { "Obstacle_StoneWall" }, 1f, 1f, Art) // top-down's walls are flat
               .Add(ArtKeys.PropHearth, Sheet, new[] { "Anchor_Campfire_0", "Anchor_Campfire_1" }, HearthSize, HearthSize, Art,
                   framesPerSecond: HearthFramesPerSecond, pulseRate: HearthPulseRate, pulseAmplitude: HearthPulseAmplitude)
               .Add(ArtKeys.PropBunk, Sheet, new[] { "Anchor_Bunk" }, BunkSize, BunkSize, Art)
               .Add(ArtKeys.SelectionRing, Sheet, new[] { "Selection_Ring" }, SelectionRingSize, SelectionRingSize, Art)
               .Add(ArtKeys.Missing, Sheet, new[] { MissingArt.Name }, 1f, 1f, Art);
            foreach (var (key, sprite) in Nodes) art.Add(key, Sheet, new[] { sprite }, ResourceSize, ResourceSize, Art);
            foreach (var (state, sprites) in Crawlers) art.Add(ArtKeys.Crawler(state), Sheet, sprites, CrawlerSize, CrawlerSize, Art);
            return art.Build();
        }

        /// <summary>
        /// The resource nodes, by their items' catalog sprite keys (rations' is the berry bush); an item whose key has no
        /// row draws the missing art. Shared with isometric, which stands the same sprites on their feet.
        /// </summary>
        internal static readonly (string Key, string Sprite)[] Nodes =
        {
            (ArtKeys.Node("Resource_BerryBush"), "Resource_BerryBush"),
            (ArtKeys.Node("Resource_OreVein"), "Resource_OreVein"),
            (ArtKeys.Node("Resource_HerbPatch"), "Resource_HerbPatch"),
            (ArtKeys.Node("Resource_Glowstone"), "Resource_Glowstone"),
        };

        /// <summary>Each crawler state's two frames. Shared with isometric.</summary>
        internal static readonly (AgentVisualState State, string[] Sprites)[] Crawlers =
        {
            (AgentVisualState.Idle, Frames("Idle")),
            (AgentVisualState.Walking, Frames("Walk")),
            (AgentVisualState.Harvesting, Frames("Harvest")),
            (AgentVisualState.Gossiping, Frames("Gossip")),
            (AgentVisualState.Panicking, Frames("Panic")),
            (AgentVisualState.Thinking, Frames("Think")),
            (AgentVisualState.Resting, Frames("Rest")),
            (AgentVisualState.Yielding, Frames("Yield")),
            (AgentVisualState.Sharing, Frames("Share")),
            (AgentVisualState.Rallying, Frames("Rally")),
            (AgentVisualState.Defiant, Frames("Defiant")),
            (AgentVisualState.Sleeping, Frames("Sleep")),
        };

        private static string[] Frames(string stem) => new[] { "Agent_" + stem + "_0", "Agent_" + stem + "_1" };

        // Last: static fields initialize in the order they are written, and the build reads the rows above
        public static ArtCatalog Catalog { get; } = Build();
    }
}
