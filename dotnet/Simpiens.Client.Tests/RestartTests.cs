using System;
using System.IO;
using System.Linq;
using Gum.Wireframe;
using Simpiens.Presentation;
using Xunit;

namespace Simpiens.Client.Tests
{
    /// <summary>A game restarted in its window is a new game (epic-client F15: the tests reuse a few windows).</summary>
    public class RestartTests
    {
        [Fact]
        public void ARestartedGameIsANewGameInTheSameWindow()
        {
            var game = Offscreen.Start(new ClientOptions { TicksPerUpdate = 100, DemoScenes = true }, frames: 3, width: 1280, height: 720);
            var session = game.Session;
            var window = game.Window;
            var device = game.GraphicsDevice;
            game.PlayCutscene(DemoScenes.Arrival); // whatever plays ends
            game.RunOneFrame();

            game.Restart(new ClientOptions
            {
                View = ViewMode.TopDown, Seed = 20261009UL, TicksPerUpdate = 10, Width = 800, Height = 600, VSync = false,
                ViewClockFromTicks = true, DashboardRefreshSeconds = 0f,
                SaveDirectory = Path.Combine(Path.GetTempPath(), "simpiens-client-tests", Guid.NewGuid().ToString("N")),
            });
            Assert.Same(window, game.Window);
            Assert.Same(device, game.GraphicsDevice);
            Assert.NotSame(session, game.Session);
            Assert.Equal(20261009UL, game.Session.Options.Seed);
            Assert.Equal(0L, game.Session.Simulation.Clock.CurrentTick);
            Assert.Equal(1, game.Screens.Count);
            Assert.Same(game.Play, game.Screens.Top);
            Assert.Same(ViewMode.TopDown, game.World.Mode);
            Assert.Empty(game.Session.Scenes.Triggers); // its options have no demonstrations

            game.RunOneFrame();
            Assert.Equal(10L, game.Session.Simulation.Clock.CurrentTick);
            Assert.Equal((800, 600), (game.GraphicsDevice.Viewport.Width, game.GraphicsDevice.Viewport.Height));
            Assert.True(game.Play.Dashboard.Shown);
            Assert.Single(game.Ui.Root.Children.OfType<GraphicalUiElement>(), e => e.Name == "header-bar"); // the UI started afresh, once
        }
    }
}
