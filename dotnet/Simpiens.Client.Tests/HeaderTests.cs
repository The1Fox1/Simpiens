using Gum.Forms.Controls;
using Gum.Wireframe;
using Simpiens.Client.Ui;
using Simpiens.Presentation;
using Simpiens.Presentation.Art;
using Xunit;

namespace Simpiens.Client.Tests
{
    /// <summary>
    /// The header in Lantern &amp; Stone's clusters (the UI polish plan, U4): a window's button stays lit while it is open,
    /// the menu closes before a window does, the figures carry tips, and the place line names the session.
    /// </summary>
    public class HeaderTests
    {
        private static Vec2 Centre(GraphicalUiElement element)
            => new Vec2(element.AbsoluteLeft + element.AbsoluteWidth * 0.5f, element.AbsoluteTop + element.AbsoluteHeight * 0.5f);

        private static Vec2 Centre(FrameworkElement control) => Centre(control.Visual);

        [Fact]
        public void AWindowsButtonStaysLitWhileItsWindowIsOpen()
        {
            var ui = UiDriver.Start(width: 1280, height: 720);
            var header = ui.Game.Play.Dashboard.Header;
            ui.Click(Centre(header.BountiesButton));
            ui.Frame();
            Assert.Equal(UiKit.ButtonActive, StoneSkin.PieceOf(header.BountiesButton));
            Assert.Equal(UiKit.Button, StoneSkin.PieceOf(header.GuildButton));

            ui.Click(Centre(header.GuildButton)); // one window at a time: the lamp moves with it
            ui.Frame();
            Assert.Equal(UiKit.ButtonActive, StoneSkin.PieceOf(header.GuildButton));
            Assert.NotEqual(UiKit.ButtonActive, StoneSkin.PieceOf(header.BountiesButton));
        }

        [Fact]
        public void EscClosesTheMenuBeforeTheWindowUnderIt()
        {
            var ui = UiDriver.Start(width: 1280, height: 720);
            var game = ui.Game;
            var header = game.Play.Dashboard.Header;
            game.Session.DashboardModel.OpenBounties();
            ui.Click(Centre(header.MenuButton));
            Assert.True(header.MenuOpen);

            Assert.True(game.Play.Dashboard.CloseWindow());
            Assert.False(header.MenuOpen);
            Assert.True(game.Session.DashboardModel.Bounties.IsOpen, "Esc closed the window with the menu.");
            Assert.True(game.Play.Dashboard.CloseWindow());
            Assert.False(game.Session.DashboardModel.Bounties.IsOpen);
        }

        [Fact]
        public void TheFiguresNameThemselvesInTheirTips()
        {
            var ui = UiDriver.Start(width: 1280, height: 720);
            var game = ui.Game;
            Offscreen.MakeActive(game);
            var tips = game.Play.Tooltips;
            var header = game.Play.Dashboard.Header;
            foreach (var (counter, tip) in new[] { (header.GoldCounter, "Gold in the stall's treasury"), (header.BountiesCounter, "Bounties on the board"), (header.StockCounters[0], "Rations in the stall") })
            {
                ui.MoveTo(Centre(counter));
                tips.Update(Tooltips.Delay * 1.1, 1280, 720);
                Assert.True(tips.Shown, $"No tip over {tip}.");
                Assert.Equal(tip, tips.Text);
                ui.MoveTo(new Vec2(640f, 400f));
                ui.Frame();
            }
        }

        [Fact]
        public void ThePlaceLineNamesTheOutpostAndTheGoldIsAFigure()
        {
            var ui = UiDriver.Start(width: 1280, height: 720);
            var header = ui.Game.Play.Dashboard.Header;
            Assert.Equal("Floor 1 · The Outpost", header.PlaceText);
            Assert.True(ui.Game.Session.DashboardModel.Context.TryGetStall(out var stall));
            Assert.Equal(stall.TreasuryGold.ToString(System.Globalization.CultureInfo.InvariantCulture), header.GoldText);
        }
    }
}
