using System.Linq;
using Simpiens.Cognition;
using Simpiens.Entities;
using Simpiens.Presentation;
using Simpiens.Simulation.Spatial;
using Xunit;

namespace Simpiens.Client.Tests
{
    /// <summary>A crawler's look and the props, ported from Unity's AgentView and CampfireView (Epic CLIENT M1.1).</summary>
    public class AppearanceTests
    {
        private static readonly EntityId Id = new EntityId(1, 2, 3, 4);

        // The view sees a crawler as a frame captured it (review A3)
        private static AgentVisualState Desired(AgentState agent) => CrawlerAppearance.Desired(agent.VisualState, WorldViewFrame.FlagsOf(agent));

        private static CrawlerPose Pose(CrawlerAppearance appearance, AgentState agent, long tick, float viewSeconds)
        {
            var flags = WorldViewFrame.FlagsOf(agent);
            appearance.Advance(Id, agent.VisualState, flags, tick);
            return appearance.Pose(Id, agent.VisualState, flags, viewSeconds);
        }

        [Fact]
        public void CrisesAndSocialMomentsShowOverTheSimulationsState()
        {
            var agent = new AgentState(Id) { VisualState = AgentVisualState.Walking };
            Assert.Equal(AgentVisualState.Walking, Desired(agent));

            agent.GossipTimer = 1f;
            Assert.Equal(AgentVisualState.Gossiping, Desired(agent));
            agent.SharingTimer = 1f;
            Assert.Equal(AgentVisualState.Sharing, Desired(agent));
            agent.YieldingTimer = 1f;
            Assert.Equal(AgentVisualState.Yielding, Desired(agent));
            agent.TantrumTimer = 1f;
            Assert.Equal(AgentVisualState.Panicking, Desired(agent));

            // Asleep: a social moment doesn't sit the sleeper up
            var sleeper = new AgentState(Id) { VisualState = AgentVisualState.Sleeping, GossipTimer = 1f };
            Assert.Equal(AgentVisualState.Sleeping, Desired(sleeper));
        }

        [Fact]
        public void AShownStateIsHeldForThreeTicksBeforeItMayChange()
        {
            var appearance = new CrawlerAppearance();
            var agent = new AgentState(Id) { VisualState = AgentVisualState.Thinking };
            Assert.Equal(AgentVisualState.Thinking, Pose(appearance, agent, 10, 0f).State);

            agent.VisualState = AgentVisualState.Idle; // a flash the dwell hides
            Assert.Equal(AgentVisualState.Thinking, Pose(appearance, agent, 11, 0f).State);
            agent.VisualState = AgentVisualState.Walking;
            Assert.Equal(AgentVisualState.Thinking, Pose(appearance, agent, 12, 0f).State);
            Assert.Equal(AgentVisualState.Walking, Pose(appearance, agent, 13, 0f).State);

            // A new world (the scene resets on a new epoch: WorldEpochTests) forgets what was shown
            appearance.Reset();
            agent.VisualState = AgentVisualState.Harvesting;
            Assert.Equal(AgentVisualState.Harvesting, Pose(appearance, agent, 0, 0f).State);
        }

        [Fact]
        public void ACrawlerThatLeavesTheWorldIsForgotten()
        {
            // Review S1: the shown states were kept for every crawler ever seen until the session ended
            var appearance = new CrawlerAppearance();
            var ids = new[] { new EntityId(1u, 0u, 0u, 1u), new EntityId(1u, 0u, 0u, 2u), new EntityId(1u, 0u, 0u, 3u) };
            foreach (var id in ids) appearance.Advance(id, AgentVisualState.Walking, CrawlerFlags.None, 10);
            appearance.ForgetAbsent(10, ids.Length);
            Assert.Equal(3, appearance.Remembered);

            appearance.Advance(ids[0], AgentVisualState.Walking, CrawlerFlags.None, 11); // the third left at tick 11
            appearance.Advance(ids[1], AgentVisualState.Walking, CrawlerFlags.None, 11);
            appearance.ForgetAbsent(11, 2);
            Assert.Equal(2, appearance.Remembered);

            // Forgetting allocates nothing once warm
            appearance.Advance(ids[2], AgentVisualState.Walking, CrawlerFlags.None, 12);
            appearance.ForgetAbsent(13, 0);
            long before = System.GC.GetAllocatedBytesForCurrentThread();
            for (int t = 14; t < 114; t++)
            {
                appearance.Advance(ids[0], AgentVisualState.Walking, CrawlerFlags.None, t);
                bool back = (t & 1) == 1; // every other tick the second is gone, then back
                if (back) appearance.Advance(ids[1], AgentVisualState.Idle, CrawlerFlags.None, t);
                appearance.ForgetAbsent(t, back ? 2 : 1);
            }
            Assert.Equal(before, System.GC.GetAllocatedBytesForCurrentThread());
            Assert.Equal(2, appearance.Remembered); // the last tick, 113, had both
            appearance.ForgetAbsent(114, 0);
            Assert.Equal(0, appearance.Remembered);
        }

        [Fact]
        public void AnimationFramesAndMicroAnimationsFollowTheViewClock()
        {
            // 4 frames a second: frame 0 for 0.0-0.24 s, 1 for 0.25-0.49 s
            Assert.Equal(0, CrawlerAppearance.AnimationFrame(AgentVisualState.Walking, 0.2f));
            Assert.Equal(1, CrawlerAppearance.AnimationFrame(AgentVisualState.Walking, 0.3f));
            Assert.Equal(0, CrawlerAppearance.AnimationFrame(AgentVisualState.Walking, 0.5f));
            Assert.Equal(1, CrawlerAppearance.AnimationFrame(AgentVisualState.Panicking, 0.2f)); // 8 a second
            Assert.Equal(0, CrawlerAppearance.AnimationFrame(AgentVisualState.Sleeping, 0.6f));  // 1.5 a second

            // Unity's formulas: sharing leans 3 degrees forward, idle breathes with its height opposite its width
            CrawlerAppearance.MicroAnimation(AgentVisualState.Sharing, 0f, out float tilt, out float sx, out float sy);
            Assert.Equal((-3f, 1f, 1f), (tilt, sx, sy));
            CrawlerAppearance.MicroAnimation(AgentVisualState.Idle, 0.5f, out tilt, out sx, out sy);
            Assert.Equal(0f, tilt);
            Assert.Equal(2f, sx + sy, 5);
            CrawlerAppearance.MicroAnimation(AgentVisualState.Walking, 0.157f, out tilt, out _, out _);
            Assert.Equal(3f, tilt, 2); // sin(1.57) * 3
        }

        [Fact]
        public void TheOutpostsPropsAreDrawnOnThePropsLayerAtUnitysSizes()
        {
            using var sim = Simpiens.Hosting.LocalSimulation.Create(Simpiens.Simulation.SimulationSettings.DefaultSeed);
            sim.Respawn(Simpiens.Core.Session.OutpostSession.SessionId);
            var scene = new WorldScene();
            scene.Build(sim.Host.Current.Map, TestFrames.Of(sim, 0), 0f);

            var props = Enumerable.Range(0, scene.Items.Count).Select(i => scene.Items[i]).Where(i => DrawSortKey.Layer(i.SortKey) == RenderLayer.Props).ToList();
            var shown = sim.Registry.ActiveEntities.Where(e => scene.Fog.Visibility(e.Position, 0f) > 0f).ToList(); // the fog hides the rest (D1 = C)
            int bunks = shown.Count(e => e.Type == EntityType.Bunk);
            int hearths = shown.Count(e => e.Type == EntityType.Anchor);
            int resources = shown.Count(e => e.Type == EntityType.Resource);
            Assert.True(bunks > 0 && hearths > 0 && resources > 0);
            Assert.Equal(bunks + hearths + resources, props.Count);
            Assert.Equal(bunks, props.Count(p => p.SpriteIndex == ProceduralSprites.BunkSprite.Index && p.Width == TopDownArt.BunkSize * TopDownProjection.BasePixelsPerUnit));
            Assert.Equal(hearths, props.Count(p => p.SpriteIndex == ProceduralSprites.GetCampfireSprite(0).Index)); // frame 0 at 0 s
        }
    }
}
