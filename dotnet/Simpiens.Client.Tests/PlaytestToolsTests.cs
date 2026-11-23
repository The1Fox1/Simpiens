using System.Linq;
using Gum.Wireframe;
using Simpiens.Client.Ui;
using Simpiens.Simulation;
using Xunit;

namespace Simpiens.Client.Tests
{
    /// <summary>
    /// The dossier's playtest tools (2026-11-20): with <see cref="ClientOptions.PlaytestTools"/> (the desktop game's development
    /// builds, or --playtest), Calm, Max Trust and Down sit in a row marked PLAYTEST under the sway's Recruit and leave as
    /// commands; Down greys once the crawler is down. Without the option there are none, and the sway holds Recruit alone
    /// (Calm Frustration is a debug action: the producer, 2026-11-21).
    /// </summary>
    public class PlaytestToolsTests
    {
        [Fact]
        public void MaxTrustAndDownFromTheDossier()
        {
            // Where the dossier ends without them at 720 rows: the sway's button's bottom
            float without;
            {
                var plain = UiDriver.Start(new ClientOptions(), 1280, 720);
                plain.Game.Session.DashboardModel.SelectCrawler(plain.Game.Session.Simulation.Host.CrawlerStates.First(c => !c.IsDrifter).AgentId);
                plain.Frame(30);
                without = Bottom(plain.Game.Play.Dashboard.Panels.RecruitButton.Visual);
            }

            var ui = UiDriver.Start(new ClientOptions { PlaytestTools = true }, 1280, 720);
            var game = ui.Game;
            var crawler = game.Session.Simulation.Host.CrawlerStates.First(c => !c.IsDrifter);
            game.Session.DashboardModel.SelectCrawler(crawler.AgentId);
            ui.Frame(30);
            var panels = game.Play.Dashboard.Panels;
            Assert.NotNull(panels.MaxTrustButton);
            Assert.True(panels.CalmButton.IsEnabled && panels.MaxTrustButton.IsEnabled && panels.DownButton.IsEnabled);
            // A row under Recruit, its right edge with Recruit's; at 720 rows the dossier no taller than without them (the row
            // takes its height from the feelings' list)
            var recruit = panels.RecruitButton.Visual;
            var calm = panels.CalmButton.Visual;
            var trust = panels.MaxTrustButton.Visual;
            var down = panels.DownButton.Visual;
            Assert.True(calm.AbsoluteTop >= Bottom(recruit) && trust.AbsoluteTop == calm.AbsoluteTop && down.AbsoluteTop == calm.AbsoluteTop, "Under Recruit, in one row.");
            Assert.True(calm.AbsoluteLeft < trust.AbsoluteLeft && trust.AbsoluteLeft < down.AbsoluteLeft, "Calm, Max Trust, Down.");
            Assert.True(System.Math.Abs(down.AbsoluteLeft + down.AbsoluteWidth - (recruit.AbsoluteLeft + recruit.AbsoluteWidth)) <= 2f, "Its right edge with Recruit's.");
            Assert.True(Bottom(down) <= without, $"No taller at 720 rows: the tools end at {Bottom(down)}, the dossier without them at {without}.");

            ui.Click(Centre(panels.MaxTrustButton.Visual));
            ui.Ticks(1);
            Assert.Equal(100, (int)crawler.Memory.GetAffinity(WorldRegistry.PlayerEntityId, (uint)game.Session.Simulation.Clock.CurrentTick).Trust);

            ui.Click(Centre(panels.DownButton.Visual));
            ui.Ticks(1);
            ui.Frame(30);
            Assert.True(crawler.IsDowned);
            Assert.False(panels.DownButton.IsEnabled, "Down greys once it is down.");
        }

        /// <summary>At 1080 rows (the desktop game's default window) and larger the row is inside the dossier's frame, with the feelings' list whole.</summary>
        [Theory]
        [InlineData(1920, 1080)]
        [InlineData(2240, 1260)]
        public void TheRowIsInsideTheDossier(int width, int height)
        {
            var ui = UiDriver.Start(new ClientOptions { PlaytestTools = true }, width, height);
            ui.Game.Session.DashboardModel.SelectCrawler(ui.Game.Session.Simulation.Host.CrawlerStates.First(c => !c.IsDrifter).AgentId);
            ui.Frame(30);
            var panels = ui.Game.Play.Dashboard.Panels;
            float frame = panels.Root.AbsoluteTop + panels.Root.AbsoluteHeight - DashboardPanels.Outer - DashboardPanels.Margin;
            Assert.True(Bottom(panels.DownButton.Visual) <= frame, $"Inside the frame: the tools end at {Bottom(panels.DownButton.Visual)}, the frame at {frame}.");
        }

        [Fact]
        public void WithoutTheOptionThereAreNone()
        {
            var ui = UiDriver.Start(new ClientOptions(), 1280, 720);
            Assert.Null(ui.Game.Play.Dashboard.Panels.CalmButton); // a debug action, not the player's sway
            Assert.Null(ui.Game.Play.Dashboard.Panels.MaxTrustButton);
            Assert.Null(ui.Game.Play.Dashboard.Panels.DownButton);
        }

        private static float Bottom(GraphicalUiElement control) => control.AbsoluteTop + control.AbsoluteHeight;

        private static Vec2 Centre(GraphicalUiElement control)
            => new Vec2(control.AbsoluteLeft + control.AbsoluteWidth * 0.5f, control.AbsoluteTop + control.AbsoluteHeight * 0.5f);
    }
}
