using System.Linq;
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
            Assert.Equal("TICK: 00000", ui.Game.Play.Dashboard.Header.TickText);
            ui.Ticks(50);
            Assert.Equal("TICK: 00050", ui.Game.Play.Dashboard.Header.TickText);
            Assert.True(ui.Game.Session.DashboardModel.Context.TryGetStall(out var stall));
            Assert.Equal($"{stall.TreasuryGold} GOLD", ui.Game.Play.Dashboard.Header.TreasuryText);
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

            // Under the header: the UI has the pointer; the map ignores the click
            ui.Click(new Vec2(Width * 0.5f, HeaderBarHeight * 0.5f));
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
            Assert.Equal("[TAB] MAP VIEW", game.Play.Dashboard.Header.TabButton.Text);
            ui.Script.Keys = new KeyInput { TabPressed = true };
            ui.Frame();
            ui.Script.Keys = default;
            Assert.False(game.Session.DashboardModel.PanelsVisible);
            Assert.Equal("[TAB] DASHBOARD", game.Play.Dashboard.Header.TabButton.Text);

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

        [Fact]
        public void TheHeaderOverTheMapLooksAsRecorded()
        {
            var game = Offscreen.Start(new ClientOptions { TicksPerUpdate = 100 }, frames: 3, Width, Height);
            game.Play.Dashboard.TogglePanels();
            game.Session.Simulation.Clock.SetTimeMultiplier(1f);
            game.Play.Dashboard.Refresh();
            RenderTests.CompareWithReference("render-outpost-header-t300", FrameCapture.Capture(game, Width, Height), Width, Height);
        }
    }
}
