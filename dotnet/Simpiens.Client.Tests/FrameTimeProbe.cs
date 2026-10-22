using System.Diagnostics;
using Simpiens.Presentation;
using Xunit;

namespace Simpiens.Client.Tests
{
    /// <summary>
    /// How long the client takes to build and draw a frame of the outpost (Epic CLIENT M1.7), against the 30 fps
    /// budget of 33 ms. A measurement, not a gate: it depends on the machine and the renderer (the tests pin Mesa's
    /// software renderer; set LIBGL_ALWAYS_SOFTWARE=0 to measure the GPU). Run it on its own:
    /// <c>dotnet dotnet/Simpiens.Client.Tests/bin/Release/net10.0/Simpiens.Client.Tests.dll -method "*FrameTime*" -explicit only</c>.
    /// </summary>
    public class FrameTimeProbe
    {
        /// <summary>The map with the header over it, in either view (M3 step 2.4: isometric beside top-down).</summary>
        [Theory(Explicit = true)]
        [InlineData("top-down")]
        [InlineData("isometric")]
        public void MeasureFrameTime(string view)
        {
            var game = Offscreen.Start(new ClientOptions { View = view == "isometric" ? ViewMode.Isometric : ViewMode.TopDown, TicksPerUpdate = 1 }, frames: 0);
            game.RunOneFrame();
            game.Play.Dashboard.TogglePanels(); // the map, with the header over it (the game starts on the panels, as Unity did)
            for (int i = 0; i < 300; i++) game.RunOneFrame(); // warm up, a crowd on the move
            const int frames = 300;
            var watch = Stopwatch.StartNew();
            for (int i = 0; i < frames; i++)
            {
                game.RunOneFrame();
                game.GraphicsDevice.Flush();
            }
            watch.Stop();
            double ms = watch.Elapsed.TotalMilliseconds / frames;
            TestContext.Current.TestOutputHelper.WriteLine($"{view}: {ms:F2} ms a frame ({1000.0 / ms:F0} fps) at 800x600 on {Microsoft.Xna.Framework.Graphics.GraphicsAdapter.DefaultAdapter.Description}, {game.World.Scene.Items.Count} draw items, tick {game.Session.Simulation.Clock.CurrentTick}");
            Assert.True(ms > 0);
        }

        /// <summary>The same with the dashboard up (M2.7): its panels over the map at the game's size, or the bounty board over the map.</summary>
        [Theory(Explicit = true)]
        [InlineData(true)]
        [InlineData(false)]
        public void MeasureFrameTimeWithTheDashboard(bool panels)
        {
            var game = Offscreen.Start(new ClientOptions { TicksPerUpdate = 1 }, frames: 0, width: 1280, height: 720);
            game.Options.DashboardRefreshSeconds = 0f;
            for (int i = 0; i < 300; i++) game.RunOneFrame();
            var crawler = System.Linq.Enumerable.First(game.Session.Simulation.Registry.ActiveEntities, e => e.Type == Simpiens.Simulation.Spatial.EntityType.Pawn);
            if (panels) game.Session.DashboardModel.SelectCrawler(crawler.Id);
            else
            {
                game.Play.Dashboard.TogglePanels();
                game.Session.DashboardModel.OpenBounties();
            }
            const int frames = 300;
            var watch = Stopwatch.StartNew();
            for (int i = 0; i < frames; i++)
            {
                game.RunOneFrame();
                game.GraphicsDevice.Flush();
            }
            watch.Stop();
            double ms = watch.Elapsed.TotalMilliseconds / frames;
            TestContext.Current.TestOutputHelper.WriteLine($"{(panels ? "panels with a dossier" : "bounty board over the map")}: {ms:F2} ms a frame ({1000.0 / ms:F0} fps) at 1280x720 on {Microsoft.Xna.Framework.Graphics.GraphicsAdapter.DefaultAdapter.Description}, the dashboard refreshed every frame, tick {game.Session.Simulation.Clock.CurrentTick}");
            Assert.True(ms > 0);
        }
    }
}
