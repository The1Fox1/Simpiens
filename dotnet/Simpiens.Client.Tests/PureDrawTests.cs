using System.Linq;
using Simpiens.Simulation.Spatial;
using Xunit;

namespace Simpiens.Client.Tests
{
    /// <summary>
    /// Drawing changes no view state (review A6, M3 plan step 0.2): fades, poses and the selection move on in an update,
    /// so two draws, or a capture at another size between them, give the same picture.
    /// </summary>
    public class PureDrawTests
    {
        [Fact]
        public void TwoDrawsWithNoUpdateBetweenGiveTheSamePicture()
        {
            var game = Offscreen.Start(new ClientOptions { TicksPerUpdate = 100 }, frames: 3);
            var crawler = game.Session.Simulation.Registry.ActiveEntities.First(e => e.Type == EntityType.Pawn);
            game.Session.Selection.SelectCrawler(crawler.Id, follow: false);
            game.RunOneFrame();

            var first = FrameCapture.CaptureMap(game, 800, 600);
            var second = FrameCapture.CaptureMap(game, 800, 600);
            FrameCapture.CaptureMap(game, 640, 480); // another size between: a second view, a thumbnail
            var third = FrameCapture.CaptureMap(game, 800, 600);
            Assert.Equal(first, second);
            Assert.Equal(first, third);
        }

        [Fact]
        public void ADrawStartsNoFadeAndTouchesNoSelection()
        {
            var game = Offscreen.Start(new ClientOptions { TicksPerUpdate = 100 }, frames: 1);
            var crawler = game.Session.Simulation.Registry.ActiveEntities.First(e => e.Type == EntityType.Pawn);
            game.Session.Selection.SelectCrawler(crawler.Id, follow: true);
            game.RunOneFrame();
            int changes = 0;
            game.Session.Selection.Changed += () => changes++;

            // Between two updates the frames show a scout's report, and then a rebuilt world
            int sector = game.World.Scene.Fog.Overlays.First(o => game.World.Scene.Fog.IsCovered(o.SectorId)).SectorId;
            game.Session.Simulation.Sectors.SetExplored(sector, true);
            game.Session.Frames.Capture(game.Session.Simulation.Registry.ActiveEntities, game.Session.Simulation.Sectors, game.Session.Simulation.Clock.CurrentTick);
            FrameCapture.CaptureMap(game, 800, 600);
            FrameCapture.Capture(game, 800, 600);
            Assert.True(game.World.Scene.Fog.IsCovered(sector), "a draw starts no fade");

            game.Session.Frames.Restart(game.Session.Simulation.Registry.ActiveEntities, game.Session.Simulation.Sectors, game.Session.Simulation.Clock.CurrentTick);
            FrameCapture.CaptureMap(game, 800, 600);
            Assert.Equal(0, changes);
            Assert.Equal(crawler.Id, game.Session.Selection.SelectedCrawler); // still there until the next update drops it

            game.RunOneFrame();
            Assert.False(game.World.Scene.Fog.HasOverlay(sector), "the update loads the rebuilt world, charted");
            Assert.Equal(default, game.Session.Selection.SelectedCrawler);
            Assert.Equal(1, changes);
        }
    }
}
