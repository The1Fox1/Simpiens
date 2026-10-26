using System.Linq;
using Gum.Forms.Controls;
using Simpiens.Presentation;
using Simpiens.Simulation.Spatial;
using Xunit;

namespace Simpiens.Client.Tests
{
    /// <summary>The merchant terminal (Epic CLIENT M2.4): opened from the header, driven with the scripted pointer.</summary>
    public class MerchantWindowTests
    {
        private const int Width = 1280, Height = 720;

        private static Vec2 Centre(FrameworkElement control)
            => new Vec2(control.AbsoluteLeft + control.ActualWidth * 0.5f, control.AbsoluteTop + control.ActualHeight * 0.5f);

        private static UiDriver OpenMerchant()
        {
            var ui = UiDriver.Start(width: Width, height: Height);
            ui.Game.Play.Dashboard.TogglePanels(); // to the map, as a player would to see the outpost
            ui.Frame();
            ui.Click(Centre(ui.Game.Play.Dashboard.Header.MerchantButton));
            Assert.True(ui.Game.Play.Dashboard.Merchant.Window.IsShown);
            return ui;
        }

        [Fact]
        public void TheWindowShowsTheStallAndItsPrices()
        {
            var ui = OpenMerchant();
            var window = ui.Game.Play.Dashboard.Merchant;
            Assert.True(ui.Game.Session.DashboardModel.Context.TryGetStall(out var stall));
            Assert.Equal("Outpost Stall @ Outpost Hub", window.StatusText);
            Assert.Equal($"Treasury: {stall.TreasuryGold} Gold", window.TreasuryText);
            Assert.Equal((double)stall.RationSellPrice, window.RationSlider.Value);
            Assert.Equal((double)stall.OreBuyPrice, window.OreSlider.Value);
            Assert.Equal($"{stall.HerbBuyPrice} Gold", window.HerbValue.Text);
        }

        /// <summary>
        /// The status bar's treasury and stock line never overlap, even with a four-digit treasury and every stock at its
        /// most (Epic 10 G2 and G3 each lengthened the stock line).
        /// </summary>
        [Fact]
        public void TheTreasuryAndTheStockLineDoNotOverlapAtTheirLongest()
        {
            var ui = OpenMerchant();
            var sim = (Simpiens.Simulation.SimulationManager)ui.Game.Session.Simulation.Simulation;
            var stall = sim.ActiveStalls[0];
            sim.UpdateStall(stall.WithTreasury(9999).WithStock(50, 20, 20).WithGlowstoneStock(20).WithHazardGearStock(99));
            ui.Frame();
            var window = ui.Game.Play.Dashboard.Merchant;
            Assert.Equal("Treasury: 9999 Gold", window.TreasuryText);
            Assert.True(window.StockLeft >= window.TreasuryRight + 8f, $"The stock line starts at {window.StockLeft}, the treasury ends at {window.TreasuryRight}.");
        }

        [Fact]
        public void DraggingASliderAndPressingApplyChangesTheStallOnTheNextTick()
        {
            var ui = OpenMerchant();
            var game = ui.Game;
            var window = game.Play.Dashboard.Merchant;
            Assert.True(game.Session.DashboardModel.Context.TryGetStall(out var before));

            // Drag the ration slider's thumb to the right end of its track
            var slider = window.RationSlider;
            var track = slider.Visual;
            var thumb = Centre(slider);
            float startX = track.AbsoluteLeft + track.AbsoluteWidth * (float)((slider.Value - slider.Minimum) / (slider.Maximum - slider.Minimum));
            var grab = new Vec2(startX, thumb.Y);
            ui.MoveTo(grab);
            ui.Script.Left = true;
            ui.Frame();
            ui.MoveTo(new Vec2(track.AbsoluteLeft + track.AbsoluteWidth + 40f, thumb.Y));
            ui.Script.Left = false;
            ui.Frame();
            Assert.Equal(MerchantModelMax, (int)slider.Value);
            Assert.Equal($"{MerchantModelMax} Gold", window.RationValue.Text);
            Assert.Equal(MerchantModelMax, game.Session.DashboardModel.Merchant.RationPrice);

            ui.Click(Centre(window.ApplyButton));
            Assert.StartsWith("Rates updated: Ration 50g", window.FeedbackText);
            Assert.True(game.Session.Simulation.Simulation.ActiveStalls[0].RationSellPrice == before.RationSellPrice, "Nothing changes before the tick.");
            ui.Ticks(1);
            Assert.Equal(MerchantModelMax, (int)game.Session.Simulation.Simulation.ActiveStalls[0].RationSellPrice);
        }

        private const int MerchantModelMax = Simpiens.Presentation.Dashboard.MerchantModel.MaxPrice;

        [Fact]
        public void AnOpenWindowTakesTheClicksAndItsCloseButtonClosesIt()
        {
            var ui = OpenMerchant();
            var game = ui.Game;
            var crawler = game.Session.Simulation.Registry.ActiveEntities.First(e => e.Type == EntityType.Pawn);
            ui.Click(game.World.View.WorldToScreen(crawler.Position));
            Assert.Equal(default, game.Session.Selection.SelectedCrawler); // the veil is over the map

            ui.Click(Centre(game.Play.Dashboard.Merchant.Window.CloseButton));
            Assert.False(game.Play.Dashboard.Merchant.Window.IsShown);
            Assert.False(game.Session.DashboardModel.Merchant.IsOpen);
            ui.Click(game.World.View.WorldToScreen(crawler.Position));
            Assert.Equal(crawler.Id, game.Session.Selection.SelectedCrawler);
        }

        [Fact]
        public void TheMerchantTerminalLooksAsRecorded()
        {
            var game = Offscreen.Start(new ClientOptions { View = ViewMode.TopDown, TicksPerUpdate = 100 }, frames: 3, Width, Height);
            game.Play.Dashboard.TogglePanels();
            game.Session.Simulation.Clock.SetTimeMultiplier(1f);
            game.Session.DashboardModel.OpenMerchant();
            game.Play.Dashboard.Refresh();
            game.RunOneFrame();
            RenderTests.CompareWithReference("render-outpost-merchant-t300", FrameCapture.Capture(game, Width, Height), Width, Height);
        }
    }
}
