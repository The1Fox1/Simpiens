using System.Linq;
using Simpiens.Client.Ui;
using Simpiens.Presentation;
using Simpiens.Simulation.Spatial;
using Xunit;

namespace Simpiens.Client.Tests
{
    /// <summary>
    /// The header over the map (Epic CLIENT M2.3), with Unity's MapHudScenario ported: the header keeps updating over
    /// the map, its buttons open their windows on the first press, and the map takes clicks everywhere but under UI.
    /// </summary>
    public class HudTests
    {
        private const int Width = 1280, Height = 720;

        private static UiDriver MapView()
        {
            var ui = UiDriver.Start(width: Width, height: Height);
            ui.Game.Play.Dashboard.TogglePanels(); // Unity started on the dashboard: Tab to the map
            ui.Frame();
            Assert.False(ui.Game.Session.DashboardModel.PanelsVisible);
            return ui;
        }

        private static Vec2 Centre(Gum.Forms.Controls.FrameworkElement control)
            => new Vec2(control.AbsoluteLeft + control.ActualWidth * 0.5f, control.AbsoluteTop + control.ActualHeight * 0.5f);

        [Fact]
        public void TheHeaderKeepsUpdatingOverTheMap()
        {
            var ui = MapView();
            Assert.Equal("Day 1 · 06:00", ui.Game.Play.Dashboard.Header.TickText); // the time of day (Epic UI-Beta B5)
            ui.Ticks(Simpiens.Simulation.DayClock.TicksPerHour);
            Assert.Equal("Day 1 · 07:00", ui.Game.Play.Dashboard.Header.TickText);
            Assert.True(ui.Game.Session.DashboardModel.Context.TryGetStall(out var stall));
            Assert.Equal($"{stall.TreasuryGold}", ui.Game.Play.Dashboard.Header.GoldText);
        }

        [Fact]
        public void TheButtonsOpenTheirWindowsOnTheFirstPressOneAtATime()
        {
            var ui = MapView();
            var model = ui.Game.Session.DashboardModel;
            ui.Click(Centre(ui.Game.Play.Dashboard.Header.BountiesButton));
            Assert.True(model.Bounties.IsOpen, "Bounties opens on the first press.");
            ui.Click(Centre(ui.Game.Play.Dashboard.Header.MerchantButton));
            Assert.True(model.Merchant.IsOpen, "Merchant opens on the first press.");
            Assert.False(model.Bounties.IsOpen, "Opening the merchant closes the bounty board.");
            ui.Click(Centre(ui.Game.Play.Dashboard.Header.MerchantButton));
            Assert.False(model.Merchant.IsOpen, "A second press closes it.");
        }

        [Fact]
        public void TheMapTakesClicksEverywhereButUnderTheHeader()
        {
            var ui = MapView();
            var game = ui.Game;

            // Under the header, where no control is (its middle holds buttons since the guild's, Epic 10 G4): the UI has the
            // pointer; the map ignores the click
            ui.Click(new Vec2(8f, HeaderBarHeight * 0.5f));
            Assert.True(game.Ui.PointerOverUi);
            Assert.False(game.World.LastInput.Clicked);

            // On the map: a crawler is picked
            var crawler = game.Session.Simulation.Registry.ActiveEntities.First(e => e.Type == EntityType.Pawn);
            ui.Click(game.World.View.WorldToScreen(crawler.Position));
            Assert.False(game.Ui.PointerOverUi);
            Assert.Equal(crawler.Id, game.Session.Selection.SelectedCrawler);
        }

        private const float HeaderBarHeight = Simpiens.Client.Ui.HeaderBar.Height;

        [Fact]
        public void TabSwitchesViewsAndEscClosesAWindowBeforeItStopsTheCamera()
        {
            var ui = MapView();
            var game = ui.Game;
            ui.Script.Keys = new KeyInput { TabPressed = true };
            ui.Frame();
            ui.Script.Keys = default;
            Assert.True(game.Session.DashboardModel.PanelsVisible);
            Assert.Equal("Ledger", game.Play.Dashboard.Header.CurrentView);
            ui.Script.Keys = new KeyInput { TabPressed = true };
            ui.Frame();
            ui.Script.Keys = default;
            Assert.False(game.Session.DashboardModel.PanelsVisible);
            Assert.Equal("Map", game.Play.Dashboard.Header.CurrentView);

            var crawler = game.Session.Simulation.Registry.ActiveEntities.First(e => e.Type == EntityType.Pawn);
            ui.Click(game.World.View.WorldToScreen(crawler.Position));
            Assert.True(game.Session.Selection.Following);
            game.Session.DashboardModel.OpenBounties();

            // A scripted key is a press on every frame it is set (the devices' reader turns holds into one press)
            ui.Script.Keys = new KeyInput { EscapePressed = true };
            ui.Frame();
            ui.Script.Keys = default;
            Assert.False(game.Session.DashboardModel.Bounties.IsOpen, "Esc closes the window...");
            Assert.True(game.Session.Selection.Following, "...and nothing else.");
            ui.Frame();
            ui.Script.Keys = new KeyInput { EscapePressed = true };
            ui.Frame();
            ui.Script.Keys = default;
            Assert.False(game.Session.Selection.Following, "With no window open, Esc stops the camera following.");
        }

        /// <summary>
        /// At every window size the game offers, the header's clusters from the left and from the right keep clear of each
        /// other (Epic 10 Part 2, W1.6a: the Workshop button ran under the clock at 1280), with room for the clock's day to
        /// reach two digits; at 1280 the identity drops its words, from 1440 it has them.
        /// </summary>
        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2)]
        public void TheHeadersClustersKeepClearAtEveryWindowSize(int size)
        {
            var at = WindowSizes.All[size];
            var ui = UiDriver.Start(width: at.X, height: at.Y);
            ui.Frame();
            var header = ui.Game.Play.Dashboard.Header;
            float leftEnd = header.Left.AbsoluteLeft + header.Left.AbsoluteWidth;
            float rightStart = header.Right.AbsoluteLeft;
            const float Clearance = 12f + 12f; // the clusters' own gap, and a second digit in the day with room to spare
            Assert.True(rightStart - leftEnd >= Clearance, $"At {at.X}x{at.Y} the clusters are {rightStart - leftEnd:F0} px apart (from {leftEnd:F0} to {rightStart:F0}).");
            Assert.Equal(at.X >= HeaderBar.WordsFromWidth, header.Left.Children.Count > 0 && ShowsWords(header));
        }

        private static bool ShowsWords(HeaderBar header)
        {
            var identity = (Gum.Wireframe.GraphicalUiElement)header.Left.Children[0];
            return ((Gum.Wireframe.GraphicalUiElement)identity.Children[1]).Visible;
        }

        [Fact]
        public void TheHeaderOverTheMapLooksAsRecorded()
        {
            var game = Offscreen.Start(new ClientOptions { View = ViewMode.TopDown, TicksPerUpdate = 100 }, frames: 3, Width, Height);
            game.Play.Dashboard.TogglePanels();
            game.Session.Simulation.Clock.SetTimeMultiplier(1f);
            game.Play.Dashboard.Refresh();
            RenderTests.CompareWithReference("render-outpost-header-t300", FrameCapture.Capture(game, Width, Height), Width, Height);
        }
    }
}
