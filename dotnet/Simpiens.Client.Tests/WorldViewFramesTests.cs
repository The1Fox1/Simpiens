using System;
using Simpiens.Core.Session;
using Simpiens.Hosting;
using Simpiens.Presentation;
using Simpiens.Simulation;
using Xunit;

namespace Simpiens.Client.Tests
{
    /// <summary>The frames the views draw from (review A3, plan step 3.2): what the world was when each tick completed.</summary>
    public class WorldViewFramesTests
    {
        private static HeadlessSimulation Outpost()
        {
            var sim = HeadlessSimulation.Create(SimulationSettings.DefaultSeed);
            sim.Respawn(OutpostSession.SessionId);
            return sim;
        }

        private static void AssertShowsTheWorld(WorldViewFrame frame, HeadlessSimulation sim)
        {
            Assert.Equal(sim.Clock.CurrentTick, frame.Tick);
            var entities = sim.Registry.ActiveEntities;
            Assert.Equal(entities.Count, frame.Count);
            for (int i = 0; i < entities.Count; i++)
            {
                var entity = entities[i];
                ref readonly var view = ref frame[i];
                Assert.Equal(entity.Id, view.Id);
                Assert.Equal(entity.Type, view.Type);
                Assert.Equal(entity.ResourceKind, view.ResourceKind);
                Assert.True(entity.Position.X == view.Position.X && entity.Position.Y == view.Position.Y, $"{entity} is at {entity.Position}, drawn at {view.Position}");
                Assert.Equal(entity.Agent != null, view.IsCrawler);
                if (entity.Agent == null) continue;
                Assert.Equal(entity.Agent.VisualState, view.VisualState);
                Assert.Equal(entity.Agent.IsInTantrum, (view.Flags & CrawlerFlags.Tantrum) != 0);
                Assert.Equal(entity.Agent.IsStarvationPanicking, (view.Flags & CrawlerFlags.StarvationPanic) != 0);
                Assert.Equal(entity.Agent.IsYielding, (view.Flags & CrawlerFlags.Yielding) != 0);
                Assert.Equal(entity.Agent.IsSharing, (view.Flags & CrawlerFlags.Sharing) != 0);
                Assert.Equal(entity.Agent.IsGossiping, (view.Flags & CrawlerFlags.Gossiping) != 0);
                Assert.True(frame.TryGetIndex(entity.Id, out int index) && index == i);
            }
            Assert.Equal(sim.Sectors.ExploredMask, frame.ChartedMask);
            foreach (var sector in sim.Host.Current.Map.Sectors) Assert.Equal(sim.Sectors.IsExplored(sector.Id), frame.IsCharted(sector));
        }

        [Fact]
        public void AfterEveryTickTheCurrentFrameIsTheWorldAndThePreviousTheTickBefore()
        {
            using var sim = Outpost();
            using var frames = new WorldViewFrames();
            frames.Follow(sim.Host, sim.Registry, sim.Sectors, sim.Clock);
            AssertShowsTheWorld(frames.Current, sim); // at once, at tick 0

            sim.Step(1);
            AssertShowsTheWorld(frames.Current, sim);
            Assert.Equal(0L, frames.Previous.Tick);

            sim.Step(299);
            AssertShowsTheWorld(frames.Current, sim);
            Assert.Equal(299L, frames.Previous.Tick);
            Assert.Equal(frames.Current.Epoch, frames.Previous.Epoch);
        }

        [Fact]
        public void ARespawnOrALoadStartsANewEpochInBothFramesAtOnce()
        {
            using var sim = Outpost();
            using var frames = new WorldViewFrames();
            frames.Follow(sim.Host, sim.Registry, sim.Sectors, sim.Clock);
            sim.Step(50);
            var save = sim.Host.Capture();
            uint epoch = frames.Epoch;

            sim.Respawn(OutpostSession.SessionId);
            Assert.Equal(epoch + 1, frames.Epoch);
            AssertShowsTheWorld(frames.Current, sim);
            AssertShowsTheWorld(frames.Previous, sim); // nothing to draw between: the old world is gone
            Assert.Equal(frames.Epoch, frames.Previous.Epoch);

            Assert.True(sim.Host.TryRestore(save, out string refusal), refusal);
            Assert.Equal(epoch + 2, frames.Epoch);
            Assert.Equal(50L, frames.Current.Tick);
            AssertShowsTheWorld(frames.Current, sim);
        }

        [Fact]
        public void ASteadyCaptureAllocatesNothing()
        {
            using var sim = Outpost();
            sim.Step(300);
            using var frames = new WorldViewFrames();
            for (int i = 0; i < 3; i++) frames.Capture(sim.Registry.ActiveEntities, sim.Sectors, sim.Clock.CurrentTick); // warm up both buffers

            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 100; i++) frames.Capture(sim.Registry.ActiveEntities, sim.Sectors, sim.Clock.CurrentTick);
            Assert.Equal(0L, GC.GetAllocatedBytesForCurrentThread() - before);
        }
    }
}
