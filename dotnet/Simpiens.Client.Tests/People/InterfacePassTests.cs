using Simpiens.Presentation;
using Xunit;

namespace Simpiens.Client.Tests.People
{
    /// <summary>Epic UI-Beta B5, the interface pass: the stall opens the merchant terminal.</summary>
    public class InterfacePassTests
    {
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
