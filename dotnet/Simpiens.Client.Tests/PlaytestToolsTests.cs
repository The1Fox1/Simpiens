using System.Linq;
using Gum.Wireframe;
using Simpiens.Simulation;
using Xunit;

namespace Simpiens.Client.Tests
{
    /// <summary>
    /// The dossier's playtest tools (2026-11-20): with <see cref="ClientOptions.PlaytestTools"/> (the desktop game's development
    /// builds), Max trust and Down sit under the dossier's mood line and leave as commands; Down greys once the crawler is down.
    /// Without the option there are none.
    /// </summary>
    public class PlaytestToolsTests
    {
        [Fact]
        public void MaxTrustAndDownFromTheDossier()
        {
            var ui = UiDriver.Start(new ClientOptions { PlaytestTools = true }, 1280, 720);
            var game = ui.Game;
            var crawler = game.Session.Simulation.Host.CrawlerStates.First(c => !c.IsDrifter);
            game.Session.DashboardModel.SelectCrawler(crawler.AgentId);
            ui.Frame(30);
            var panels = game.Play.Dashboard.Panels;
            Assert.NotNull(panels.MaxTrustButton);
            Assert.True(panels.MaxTrustButton.IsEnabled && panels.DownButton.IsEnabled);
            var button = panels.MaxTrustButton.Visual;
            Assert.True(button.AbsoluteLeft + button.AbsoluteWidth <= panels.Root.AbsoluteLeft + panels.Root.AbsoluteWidth && button.AbsoluteTop + button.AbsoluteHeight <= 720, "On the screen.");

            ui.Click(Centre(panels.MaxTrustButton.Visual));
            ui.Ticks(1);
            Assert.Equal(100, (int)crawler.Memory.GetAffinity(WorldRegistry.PlayerEntityId, (uint)game.Session.Simulation.Clock.CurrentTick).Trust);

            ui.Click(Centre(panels.DownButton.Visual));
            ui.Ticks(1);
            ui.Frame(30);
            Assert.True(crawler.IsDowned);
            Assert.False(panels.DownButton.IsEnabled, "Down greys once it is down.");
        }

        [Fact]
        public void WithoutTheOptionThereAreNone()
        {
            var ui = UiDriver.Start(new ClientOptions(), 1280, 720);
            Assert.Null(ui.Game.Play.Dashboard.Panels.MaxTrustButton);
            Assert.Null(ui.Game.Play.Dashboard.Panels.DownButton);
        }

        private static Vec2 Centre(GraphicalUiElement control)
            => new Vec2(control.AbsoluteLeft + control.AbsoluteWidth * 0.5f, control.AbsoluteTop + control.AbsoluteHeight * 0.5f);
    }
}
