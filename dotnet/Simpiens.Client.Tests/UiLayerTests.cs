using System.Linq;
using Gum.Forms.Controls;
using Simpiens.Presentation;
using Simpiens.Simulation.Spatial;
using Xunit;

namespace Simpiens.Client.Tests
{
    /// <summary>Gum in the client (Epic CLIENT M2.0): drawn over the map, clicked through a scripted pointer, and in the map's way.</summary>
    public class UiLayerTests
    {
        private static Button AddButton(SimpiensGame game, string text, float x, float y, float width = 160f, float height = 40f)
        {
            var button = new Button { Text = text, X = x, Y = y, Width = width, Height = height };
            game.Ui.Root.AddChild(button);
            return button;
        }

        [Fact]
        public void AScriptedClickPressesAButton()
        {
            var ui = UiDriver.Start();
            var button = AddButton(ui.Game, "PRESS", 100f, 100f);
            int clicks = 0;
            button.Click += (_, _) => clicks++;
            ui.Frame();

            ui.Click(new Vec2(180f, 120f));
            Assert.Equal(1, clicks);

            ui.Click(new Vec2(500f, 400f)); // elsewhere
            Assert.Equal(1, clicks);
        }

        [Fact]
        public void AClickOnAButtonNeverReachesTheCrawlerUnderIt()
        {
            var ui = UiDriver.Start();
            var crawler = ui.Game.Simulation.Registry.ActiveEntities.First(e => e.Type == EntityType.Pawn);
            var screen = ui.Game.Camera.WorldToScreen(crawler.Position);

            AddButton(ui.Game, "OVER A CRAWLER", screen.X - 80f, screen.Y - 20f);
            ui.Frame();
            ui.Click(screen);
            Assert.Equal(default, ui.Game.Selection.SelectedCrawler);

            ui.Game.Ui.Root.Children.Clear(); // the same click with nothing in the way
            ui.Frame();
            ui.Click(screen);
            Assert.NotEqual(default, ui.Game.Selection.SelectedCrawler);
        }

        [Fact]
        public void TheGameThatStartedLastOwnsTheUiAndTheOtherSkipsIt()
        {
            var first = Offscreen.Start(frames: 1);
            Assert.True(first.Ui.IsOwner);
            var second = Offscreen.Start(frames: 1);
            Assert.True(second.Ui.IsOwner);
            Assert.False(first.Ui.IsOwner);

            first.RunOneFrame(); // draws its map, skips the UI it no longer has
            Assert.False(first.Ui.PointerOverUi);
            Assert.NotEmpty(FrameCapture.Capture(first, 800, 600));
        }
    }
}
