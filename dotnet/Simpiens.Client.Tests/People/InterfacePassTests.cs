using Simpiens.Presentation;
using Xunit;

namespace Simpiens.Client.Tests.People
{
    /// <summary>Epic UI-Beta B5, the interface pass: the stall opens the merchant terminal; the chronicle is grouped and its rows find their crawler.</summary>
    public class InterfacePassTests
    {
        [Fact]
        public void TheChronicleIsGroupedAndAnEventFindsItsCrawler()
        {
            var ui = UiDriver.Start(width: 1280, height: 720);
            var game = ui.Game;
            var model = game.Session.DashboardModel;
            ui.Ticks(400);
            model.SelectSector(Simpiens.Core.Session.Layouts.Floor1Layout.HubSectorId);
            game.Play.Dashboard.Refresh();
            ui.Frame();
            var events = model.Sectors.Events;
            Assert.NotEmpty(events);
            Assert.Equal(events.Count, model.Sectors.EventActors.Count);
            string Words(string line) // the narrative, without its time or its count
            {
                int start = line.IndexOf("] ", System.StringComparison.Ordinal) + 2;
                int times = line.LastIndexOf(" ×", System.StringComparison.Ordinal);
                return times > start ? line.Substring(start, times - start) : line.Substring(start);
            }
            for (int i = 1; i < events.Count; i++) Assert.NotEqual(Words(events[i - 1]), Words(events[i]));

            int row = -1;
            for (int i = 0; i < events.Count && row < 0; i++)
                if (model.Sectors.EventActors[i] != default && game.Session.Simulation.Registry.GetEntity(model.Sectors.EventActors[i])?.Agent != null) row = i;
            Assert.True(row >= 0, "No event names a crawler in the world.");
            var actor = model.Sectors.EventActors[row];
            var box = game.Play.Dashboard.Panels.EventRowBox(row);
            ui.Click(new Vec2(box.AbsoluteLeft + box.AbsoluteWidth * 0.5f, box.AbsoluteTop + box.AbsoluteHeight * 0.5f));
            ui.Frame();
            Assert.False(model.PanelsVisible);
            Assert.Equal(actor, game.Session.Selection.SelectedCrawler);
            Assert.True(game.Session.Selection.Following);
        }

        [Theory]
        [InlineData("top-down")]
        [InlineData("isometric")]
        public void AClickOnTheStallOpensTheMerchantTerminal(string mode)
        {
            var game = Offscreen.Start(new ClientOptions { View = mode == "top-down" ? ViewMode.TopDown : ViewMode.Isometric }, frames: 2, width: 1280, height: 720);
            game.Play.Dashboard.TogglePanels(); // the map
            game.RunOneFrame();
            var frame = game.Session.Frames.Current;
            Assert.True(frame.StallCount > 0);
            var stall = frame.Stall(0);
            Assert.False(game.Session.DashboardModel.Merchant.IsOpen);
            var on = game.World.View.WorldToScreen(stall.Position, 0f);
            game.World.ClickMap(on);
            Assert.True(game.Session.DashboardModel.Merchant.IsOpen, "A click on the stall didn't open the merchant terminal.");
            Assert.Equal(default, game.Session.Selection.SelectedCrawler);
        }
    }
}
