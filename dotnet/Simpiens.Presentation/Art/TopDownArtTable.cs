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

        /// <summary>The caravan's stride, both views (Epic 10 Part 2, D70).</summary>
        public const float CaravanFramesPerSecond = 4f;
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
                RideOffset = new Vec2(0f, 0.3f), RideHeight = 0f, // a carried crawler across its back, up the screen (Epic 10 Part 3, K5.8a)
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
               .Add(ArtKeys.PropCairn, Sheet, new[] { "Prop_Cairn" }, 1f, 1f, Art) // a body (Epic 10 Part 3, K5.8b)
               // The stations (Epic 10 Part 2, W1.6a): a cell each at the art's own 16 pixels a unit, as isometric's
               .Add(ArtKeys.PropForge, Sheet, new[] { "Prop_Forge" }, 1f, 1f, Art)
               .Add(ArtKeys.PropStill, Sheet, new[] { "Prop_Still" }, 1f, 1f, Art)
               .Add(ArtKeys.PropStationPlot, Sheet, new[] { "Prop_StationPlot" }, 1f, 1f, Art)
               .Add(ArtKeys.PropDrillYard, Sheet, new[] { "Prop_DrillYard" }, 1f, 1f, Art) // Epic 11, S3.4
               .Add(ArtKeys.SelectionRing, Sheet, new[] { "Selection_Ring" }, SelectionRingSize, SelectionRingSize, Art)
               // The trade caravan (Epic 10 Part 2, the stretch, D70): a porter and its cart, walking
               .Add(ArtKeys.PropCaravan, Sheet, HazardSprites.Names(CaravanSprites.Name, CaravanSprites.Frames), 1f, 1f, Art, framesPerSecond: CaravanFramesPerSecond)
               // The hazards' animations (Epic 10 Part 2, the stretch, D69): their frames run at HazardEffects' rates
               .Add(ArtKeys.FxFoulAir, Sheet, HazardSprites.Names(HazardSprites.PuffName, HazardSprites.PuffFrames), 1.5f, 1.5f, Art, framesPerSecond: HazardEffects.PuffFramesPerSecond)
               .Add(ArtKeys.FxFallingRock, Sheet, HazardSprites.Names(HazardSprites.RockName, HazardSprites.RockFrames), 1f, 1f, Art, framesPerSecond: HazardEffects.RockFramesPerSecond)
               .Add(ArtKeys.FxFlood, Sheet, HazardSprites.Names(HazardSprites.RippleName, HazardSprites.RippleFrames), 1.5f, 1.5f, Art, framesPerSecond: HazardEffects.RippleFramesPerSecond)
               // Shots in flight (Epic 10 Part 3, K8.4a): eight pixels, at the art's sixteen a unit
               .Add(ArtKeys.FxShot, Sheet, new[] { ShotSprites.ShotName }, 0.5f, 0.5f, Art)
               .Add(ArtKeys.FxSpit, Sheet, new[] { ShotSprites.SpitName }, 0.5f, 0.5f, Art)
               // A boss's health bar (Epic 10 Part 3, K10.5b): twelve pixels by one and a half, for a creature a crawler's size
               .Add(ArtKeys.FxBar, Sheet, new[] { BarSprites.Name }, 0.75f, 0.1f, Art)
               // The descent gate (Epic 10 Part 3, K10.5c): two cells, sealed or breached
               .Add(ArtKeys.PropGate, Sheet, new[] { GateSprites.Sealed }, 2f, 2f, Art)
               .Add(ArtKeys.PropGateOpen, Sheet, new[] { GateSprites.Open }, 2f, 2f, Art)
               .Add(ArtKeys.Missing, Sheet, new[] { MissingArt.Name }, 1f, 1f, Art);
            foreach (var (key, sprite) in Nodes) art.Add(key, Sheet, new[] { sprite }, ResourceSize, ResourceSize, Art);
            foreach (var (state, sprites) in Crawlers) art.Add(ArtKeys.Crawler(state), Sheet, sprites, CrawlerSize, CrawlerSize, Art);
            // The creatures (Epic 10 Part 3, K2.5): a crawler's canvas at a crawler's size, so their pixels match
            // A boss on its bigger canvas at the same scale (K10.5a)
            foreach (var (key, framesPerSecond) in Creatures)
            {
                float size = CrawlerSize * CreatureSprites.SizeOf(key) / CreatureSprites.Size;
                art.Add(ArtKeys.Creature(key), Sheet, CreatureSprites.Names(key), size, size, Art, framesPerSecond: framesPerSecond);
            }
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

        /// <summary>
        /// Each creature kind's row, by its art key (Epic 10 Part 3, K2.5): how fast its frames run while it moves (a rat
        /// scuttles, a beetle plods); still, it holds its first. Shared with isometric.
        /// </summary>
        public static IReadOnlyList<(string Key, float FramesPerSecond)> Creatures { get; } = new[]
        {
            ("burrow-rat", 8f),
            ("stone-beetle", 4f),
            ("cave-spinner", 6f),
            ("matriarch", 8f), // the bosses (Epic 10 Part 3, K10)
            ("gatekeeper", 2f),
        };

        /// <summary>Each crawler state's two frames (<see cref="CrawlerFrames"/>). Shared with isometric.</summary>
        public static IReadOnlyList<(AgentVisualState State, string[] Sprites)> Crawlers => CrawlerFrames.All;

        // Last: static fields initialize in the order they are written, and the build reads the rows above
        public static ArtCatalog Catalog { get; } = Build();
    }
}
