using System.Linq;
using Simpiens.Cognition;
using Simpiens.Presentation;
using Simpiens.Simulation;
using Simpiens.Simulation.Spatial;
using Xunit;

namespace Simpiens.Client.Tests
{
    /// <summary>Crawlers drawn between ticks (review A3, plan step 3.4): smooth at the display's rate, never across worlds.</summary>
    public class InterpolationTests
    {
        private static readonly EntityId Walker = new EntityId(1u, 0u, 0u, 1u);
        private static readonly EntityId Newcomer = new EntityId(1u, 0u, 0u, 2u);

        private static (WorldRegistry registry, WorldEntity walker) World()
        {
            var registry = new WorldRegistry();
            var walker = new WorldEntity(Walker, EntityType.Pawn, new Vec2(2f, 3f), 0.35f, agent: new AgentState(Walker));
            registry.Register(walker);
            return (registry, walker);
        }

        /// <summary>The walker at (2, 3) on one tick and <paramref name="to"/> on the next.</summary>
        private static WorldViewFrames TwoTicks(Vec2 to, out WorldRegistry registry)
        {
            var (r, walker) = World();
            registry = r;
            var frames = new WorldViewFrames();
            frames.Restart(registry.ActiveEntities, null, 10);
            walker.SetPosition(to);
            frames.Capture(registry.ActiveEntities, null, 11);
            return frames;
        }

        [Fact]
        public void ACrawlerIsDrawnBetweenItsLastTwoPositionsAtTheClocksFraction()
        {
            var frames = TwoTicks(new Vec2(3f, 3f), out _);
            frames.Alpha = 0f;
            Assert.True(frames.TryGetDrawnPosition(Walker, out var at) && at == new Vec2(2f, 3f), "at alpha 0, where it was a tick ago");
            frames.Alpha = 0.5f;
            Assert.True(frames.TryGetDrawnPosition(Walker, out at) && at == new Vec2(2.5f, 3f), $"halfway at alpha 0.5, drawn at {at}");
            frames.Alpha = 1f;
            Assert.True(frames.TryGetDrawnPosition(Walker, out at) && at == new Vec2(3f, 3f), "at alpha 1, where it is");
            Assert.False(frames.TryGetDrawnPosition(Newcomer, out _), "an entity the world doesn't have isn't drawn");
        }

        [Fact]
        public void ANewcomerALongJumpAndARebuiltWorldAreDrawnWhereTheyAre()
        {
            var (registry, walker) = World();
            var frames = new WorldViewFrames();
            frames.Restart(registry.ActiveEntities, null, 10);
            var newcomer = new WorldEntity(Newcomer, EntityType.Pawn, new Vec2(-4f, 0f), 0.35f, agent: new AgentState(Newcomer));
            registry.Register(newcomer);
            walker.SetPosition(new Vec2(2f, 3f + WorldViewFrames.SnapDistance + 0.5f)); // a teleport, not a walk
            frames.Capture(registry.ActiveEntities, null, 11);
            frames.Alpha = 0.5f;
            Assert.True(frames.TryGetDrawnPosition(Newcomer, out var at) && at == newcomer.Position, "a newcomer appears where it is");
            Assert.True(frames.TryGetDrawnPosition(Walker, out at) && at == walker.Position, "a jump longer than the snap distance snaps");

            // A respawn or a load: the same ids in a new world never glide from where the old world had them
            walker.SetPosition(new Vec2(2.5f, 3f));
            frames.Restart(registry.ActiveEntities, null, 0);
            Assert.True(frames.TryGetDrawnPosition(Walker, out at) && at == walker.Position, "a rebuilt world starts where it is");
        }

        [Fact]
        public void TheSceneAndTheFollowedCrawlerUseTheDrawnPosition()
        {
            var frames = TwoTicks(new Vec2(3f, 3f), out _);
            var selection = new WorldSelection(frames);
            selection.SelectCrawler(Walker, follow: true);
            var scene = new WorldScene();

            float previous = float.MinValue;
            foreach (float alpha in new[] { 0.1f, 0.3f, 0.5f, 0.7f, 0.9f })
            {
                frames.Alpha = alpha;
                scene.Build(null, frames, 0f, selection);
                var crawler = Enumerable.Range(0, scene.Items.Count).Select(i => scene.Items[i]).Single(i => DrawSortKey.Role(i.SortKey) == DrawRole.Crawler);
                Assert.True(selection.TryGetCrawlerPosition(out var followed));
                Assert.Equal(crawler.X, followed.X); // the camera follows what is drawn, and a click picks it there
                Assert.True(followed.X > previous, $"at alpha {alpha} the crawler moved on ({followed.X} after {previous})");
                previous = followed.X;
            }
        }

        [Fact]
        public void TheGameDrawsTheLastTickExactlyWhenItsViewRunsOnTicks()
        {
            var game = Offscreen.Start(new ClientOptions { TicksPerUpdate = 1 }, frames: 2); // the tests' view clock runs on ticks
            Assert.Equal(1f, game.Session.ViewAlpha);
            Assert.Equal(1f, game.Session.Frames.Alpha);
            Assert.Equal(game.Session.Simulation.Clock.CurrentTick, game.Session.Frames.Current.Tick);
        }
    }
}
