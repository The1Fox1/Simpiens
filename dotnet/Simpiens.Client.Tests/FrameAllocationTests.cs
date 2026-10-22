using System;
using System.Linq;
using Simpiens.Simulation.Spatial;
using Xunit;

namespace Simpiens.Client.Tests
{
    /// <summary>
    /// A steady frame of the world allocates nothing (AGENTS.md, zero allocations in hot paths; review R2, M3 plan step
    /// 0.4): the view's update, the scene presented and culled, the renderer and the map's text. GC pauses matter most on
    /// phones (M4).
    /// </summary>
    public class FrameAllocationTests
    {
        [Fact]
        public void TheWorldsFramePathAllocatesNothing()
        {
            var game = Offscreen.Start(new ClientOptions { TicksPerUpdate = 100 }, frames: 3);
            var crawler = game.Session.Simulation.Registry.ActiveEntities.First(e => e.Type == EntityType.Pawn);
            game.Session.Selection.SelectCrawler(crawler.Id, follow: true); // its ring, its name, the camera's follow
            game.World.Rig.ZoomAt(crawler.Position, 0.5f); // part of the map off screen: culling at work

            for (int i = 0; i < 10; i++) Frame(game, i); // buffers grow once
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 100; i++) Frame(game, i);
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.True(allocated == 0, $"100 frames allocated {allocated} bytes.");
        }

        private static void Frame(SimpiensGame game, int i)
        {
            game.Session.Frames.Alpha = i % 10 / 10f; // crawlers drawn between ticks
            game.World.Update(1f / 30f, 800, 600, default, default, hasInput: false);
            game.World.Draw(game.GraphicsDevice, 800, 600);
        }
    }
}
