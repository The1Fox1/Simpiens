using System.Collections.Generic;
using System.Linq;
using Simpiens.Cognition;
using Simpiens.Core.Session;
using Simpiens.Hosting;
using Simpiens.Presentation;
using Simpiens.Presentation.Art;
using Simpiens.Simulation;
using Simpiens.Simulation.Spatial;
using Simpiens.Simulation.World;
using Xunit;

namespace Simpiens.Client.Tests
{
    /// <summary>
    /// The draw order the isometric view needs (review A2, plan step 2.4), on a hand-built room with no art: walls, props
    /// and crawlers interleave by depth in one layer, so a crawler can walk behind a wall; the fog and the selection ring
    /// lie on the floor under them.
    /// </summary>
    public class IsometricDrawOrderTests
    {
        private static readonly IsometricProjection Iso = new IsometricProjection();

        private static readonly EntityId Behind = new EntityId(1u, 0u, 0u, 1u);
        private static readonly EntityId Front = new EntityId(1u, 0u, 0u, 2u);
        private static readonly EntityId Bunk = new EntityId(1u, 0u, 0u, 3u);
        private static readonly EntityId Sleeper = new EntityId(1u, 0u, 0u, 4u);

        /// <summary>A wall row along y = 0 (cells x = -3 to 3) between two charted halves, and an uncharted store room in the north-east.</summary>
        private static MapDefinition Room() => new MapDefinitionBuilder("Depth order", new RectF(-6f, -6f, 12f, 12f))
            .WallRun(new Vec2(-3f, 0f), new Vec2(3f, 0f))
            .Sector(3, "Store", string.Empty, new RectF(4f, 2f, 2f, 4f), startsExplored: false)
            .Sector(1, "South", string.Empty, new RectF(-6f, -6f, 12f, 6f))
            .Sector(2, "North", string.Empty, new RectF(-6f, 0f, 12f, 6f))
            .Build();

        private static WorldEntity Crawler(EntityId id, Vec2 at) => new WorldEntity(id, EntityType.Pawn, at, 0.35f, agent: new AgentState(id));

        /// <summary>
        /// Depth is x + y: a crawler at (0, -1) is on the wall's far side (higher on screen), one at (0, 1) on its near
        /// side. A sleeper lies on its bunk at (2, -2). The far crawler is selected.
        /// </summary>
        private static WorldScene Build(IViewProjection projection)
        {
            var registry = new WorldRegistry();
            registry.Register(Crawler(Behind, new Vec2(0f, -1f)));
            registry.Register(Crawler(Front, new Vec2(0f, 1f)));
            registry.Register(Crawler(Sleeper, new Vec2(2f, -2f))); // before its bunk: the role, not the insertion order, puts it on top
            registry.Register(new WorldEntity(Bunk, EntityType.Bunk, new Vec2(2f, -2f), 0.5f));
            var frames = TestFrames.Of(registry.ActiveEntities, null, 0); // no sector registry: each sector's default charting
            var selection = new WorldSelection(frames);
            selection.SelectCrawler(Behind, follow: false);

            var scene = new WorldScene { Projection = projection };
            scene.Build(Room(), frames, 0f, selection);
            return scene;
        }

        private static List<DrawItem> Items(WorldScene scene) => Enumerable.Range(0, scene.Items.Count).Select(i => scene.Items[i]).ToList();

        private static int IndexOfPick(WorldScene scene, EntityId id)
            => Enumerable.Range(0, scene.Items.Count).Single(i => scene.Items[i].Pick >= 0 && scene.Items.PickedEntity(scene.Items[i].Pick) == id);

        private static int IndexOfSprite(WorldScene scene, PixelSprite sprite, Vec2 at)
            => Enumerable.Range(0, scene.Items.Count).Single(i => scene.Items[i].Sheet == DrawSheet.World && scene.Items[i].SpriteIndex == SpriteSheets.World.IndexOf(sprite)
                && scene.Items[i].X == at.X && scene.Items[i].Y == at.Y);

        [Fact]
        public void WallsPropsAndCrawlersShareOneLayerAndInterleaveByDepth()
        {
            var scene = Build(Iso);
            var items = Items(scene);
            foreach (var item in items)
            {
                var role = DrawSortKey.Role(item.SortKey);
                if (role == DrawRole.Wall || role == DrawRole.Prop || role == DrawRole.Crawler) Assert.Equal(RenderLayer.World, DrawSortKey.Layer(item.SortKey));
            }

            int wall = IndexOfSprite(scene, ProceduralSprites.WallSprite, Vec2.Zero);
            Assert.True(IndexOfPick(scene, Behind) < wall, "a crawler on the far side of a wall is drawn before it (hidden behind it)");
            Assert.True(IndexOfPick(scene, Front) > wall, "a crawler on the near side is drawn after it");
            Assert.True(IndexOfSprite(scene, ProceduralSprites.BunkSprite, new Vec2(2f, -2f)) < IndexOfPick(scene, Sleeper), "a sleeper lies on its bunk");
        }

        [Fact]
        public void TheFogAndTheSelectionRingLieOnTheFloorUnderAWallInFront()
        {
            var scene = Build(Iso);
            var items = Items(scene);
            int wall = IndexOfSprite(scene, ProceduralSprites.WallSprite, Vec2.Zero);
            int ring = items.FindIndex(i => i.SpriteIndex == SpriteSheets.World.IndexOf(ProceduralSprites.SelectionRing) && i.Sheet == DrawSheet.World);
            int cloud = items.FindIndex(i => i.Sheet == DrawSheet.Fog);
            int lastFloor = items.FindLastIndex(i => DrawSortKey.Role(i.SortKey) == DrawRole.Floor || DrawSortKey.Role(i.SortKey) == DrawRole.Rock);

            Assert.True(cloud >= 0 && ring >= 0, "the uncharted store has a cloud, and the far crawler is selected");
            Assert.Equal(RenderLayer.FloorOverlay, DrawSortKey.Layer(items[cloud].SortKey));
            Assert.Equal(RenderLayer.FloorMarks, DrawSortKey.Layer(items[ring].SortKey));
            Assert.True(lastFloor < cloud && cloud < ring && ring < wall, "floor, then the cloud, then the ring, then everything standing");
        }

        [Fact]
        public void TheOrderIsTheSameEveryBuild()
        {
            var a = Build(Iso);
            var keys = Items(a).Select(i => i.SortKey).ToList();
            a.Build(Room(), null, 0f); // reuse the scene's buffers with other content first
            var b = Build(Iso);
            Assert.Equal(keys, Items(b).Select(i => i.SortKey));
        }

        [Fact]
        public void EveryStandingItemFitsInOneCellInBothProjections()
        {
            // The standard isometric rule: anything wider than a cell is anchored at its front cell or split per cell, or
            // its neighbours sort wrongly. Nothing on the hand-built room or the outpost is wider today.
            AssertOneCellWide(Build(Iso), Iso);
            AssertOneCellWide(Build(TopDownProjection.Instance), TopDownProjection.Instance);

            using var sim = LocalSimulation.Create(SimulationSettings.DefaultSeed);
            sim.Respawn(OutpostSession.SessionId);
            foreach (var projection in new IViewProjection[] { Iso, TopDownProjection.Instance })
            {
                var scene = new WorldScene { Projection = projection };
                scene.Build(sim.Host.Current.Map, TestFrames.Of(sim), 0f);
                AssertOneCellWide(scene, projection);
            }
        }

        private static void AssertOneCellWide(WorldScene scene, IViewProjection projection)
        {
            float cell = projection.ViewBounds(new RectF(0f, 0f, 1f, 1f)).Width * projection.ArtPixelsPerUnit;
            foreach (var item in Items(scene))
            {
                var role = DrawSortKey.Role(item.SortKey);
                if (role != DrawRole.Wall && role != DrawRole.Prop && role != DrawRole.Crawler) continue;
                // The stall is furniture wider than a cell, on purpose (graphics V5): it sorts by its centre, so a crawler
                // beside one end can draw on the wrong side of it (an open item of the epic)
                if (SpriteSheets.World[item.SpriteIndex].Name is "Prop_Stall" or "Iso_Stall") continue;
                Assert.True(item.Width <= cell + 1e-3f, $"a {role} {item.Width} art pixels wide is wider than a cell ({cell})");
            }
        }
    }
}
