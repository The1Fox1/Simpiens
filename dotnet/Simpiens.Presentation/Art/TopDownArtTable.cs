using System.Collections.Generic;
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
        /// <summary>An item lying in the world (on the stall's counter), and the marker over its carrier: 8 texels at a whole 8 pixels.</summary>
        public const float ItemMapSize = 0.75f, CarriedSize = 0.5f;

        /// <summary>The hearth: a two-frame flame at 6 Hz and a subtle pulse (Unity's CampfireView).</summary>
        public const float HearthFramesPerSecond = 6f, HearthPulseRate = 4f, HearthPulseAmplitude = 0.03f;

        private const float Art = TopDownProjection.BasePixelsPerUnit;
        private const DrawSheet Sheet = DrawSheet.World;

        private static ArtCatalog Build()
        {
            var art = new ArtCatalog.Builder("top-down")
            {
                NameOffset = new Vec2(0f, 0.65f), NameHeight = 0f, // above the crawler (Unity's CameraController)
                CarryOffset = new Vec2(0.25f, 0.3f), CarryHeight = 0f, // over its pack, at its upper right
                // On the stall's counter, its lower half: four goods as their 8x8 markers (a 12-px map sprite would cover it)
                DisplaySlots = new[] { (new Vec2(-0.69f, -0.22f), 0f), (new Vec2(-0.23f, -0.22f), 0f), (new Vec2(0.23f, -0.22f), 0f), (new Vec2(0.69f, -0.22f), 0f) },
                DisplayPicture = ItemPicture.Carried,
            };
            art.Add(ArtKeys.TileFloor, Sheet, new[] { "Map_Floor" }, 1f, 1f, Art)
               .Add(ArtKeys.TileRock, Sheet, new[] { "Map_Rock" }, 1f, 1f, Art)
               .Add(ArtKeys.TileWall, Sheet, new[] { "Obstacle_StoneWall" }, 1f, 1f, Art)
               .Add(ArtKeys.TileWallLow, Sheet, new[] { "Obstacle_StoneWall" }, 1f, 1f, Art) // top-down's walls are flat
               .Add(ArtKeys.PropHearth, Sheet, new[] { "Anchor_Campfire_0", "Anchor_Campfire_1" }, HearthSize, HearthSize, Art,
                   framesPerSecond: HearthFramesPerSecond, pulseRate: HearthPulseRate, pulseAmplitude: HearthPulseAmplitude)
               .Add(ArtKeys.PropBunk, Sheet, new[] { "Anchor_Bunk" }, BunkSize, BunkSize, Art)
               .Add(ArtKeys.PropStall, Sheet, new[] { "Prop_Stall" }, 2f, 1f, Art) // two cells by one
               .Add(ArtKeys.SelectionRing, Sheet, new[] { "Selection_Ring" }, SelectionRingSize, SelectionRingSize, Art)
               .Add(ArtKeys.Missing, Sheet, new[] { MissingArt.Name }, 1f, 1f, Art);
            foreach (var (key, sprite) in Nodes) art.Add(key, Sheet, new[] { sprite }, ResourceSize, ResourceSize, Art);
            foreach (var (state, sprites) in Crawlers) art.Add(ArtKeys.Crawler(state), Sheet, sprites, CrawlerSize, CrawlerSize, Art);
            foreach (var key in WornSprites.Keys)
                foreach (var (state, sprites) in Crawlers)
                    art.Add(ArtKeys.CrawlerWearing(state, key), Sheet, new[] { WornSprites.Name(sprites[0], key), WornSprites.Name(sprites[1], key) }, CrawlerSize, CrawlerSize, Art);
            foreach (var key in ItemSprites.Keys)
            {
                art.Add(ArtKeys.Item(key, ItemPicture.Map), Sheet, new[] { ItemSprites.MapName(key) }, ItemMapSize, ItemMapSize, Art);
                art.Add(ArtKeys.Item(key, ItemPicture.Carried), Sheet, new[] { ItemSprites.CarriedName(key) }, CarriedSize, CarriedSize, Art);
            }
            return art.Build();
        }

        /// <summary>
        /// The resource nodes, by their items' art keys (rations grow on the berry bush); an item whose key has no row
        /// draws the missing art. Shared with isometric, which stands the same sprites on their feet.
        /// </summary>
        public static IReadOnlyList<(string Key, string Sprite)> Nodes { get; } = new[]
        {
            (ArtKeys.Node("ration"), "Resource_BerryBush"),
            (ArtKeys.Node("iron-ore"), "Resource_OreVein"),
            (ArtKeys.Node("medicinal-herb"), "Resource_HerbPatch"),
            (ArtKeys.Node("glowstone"), "Resource_Glowstone"),
        };

        /// <summary>Each crawler state's two frames. Shared with isometric.</summary>
        public static IReadOnlyList<(AgentVisualState State, string[] Sprites)> Crawlers { get; } = new[]
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
