using Simpiens.Client.Ui;
using Xunit;

namespace Simpiens.Client.Tests.People
{
    /// <summary>Tooltips (Epic UI-Beta B5): half a second over a header button shows its tip beside the cursor; moving off hides it.</summary>
    public class TooltipTests
    {
        [Fact]
        public void AButtonShowsItsTipAfterAHover()
        {
            var ui = UiDriver.Start(width: 1280, height: 720);
            var game = ui.Game;
            Offscreen.MakeActive(game);
            var tips = game.Play.Tooltips;
            var pause = game.Play.Dashboard.Header.PauseButton;
            var over = new Vec2(pause.AbsoluteLeft + pause.ActualWidth * 0.5f, pause.AbsoluteTop + pause.ActualHeight * 0.5f);
            ui.MoveTo(over); // the cursor is over the button (an offscreen frame passes almost no real time: the test gives the tip its time)
            Assert.False(tips.Shown, "The tip showed before its delay.");
            tips.Update(Tooltips.Delay * 0.5, 1280, 720);
            Assert.False(tips.Shown, "The tip showed before its delay.");
            tips.Update(Tooltips.Delay * 0.6, 1280, 720);
            Assert.True(tips.Shown);
            Assert.Equal("Pause or resume the world (P)", tips.Text);
            Assert.True(tips.Panel.X >= over.X, "The tip isn't beside the cursor.");

            ui.MoveTo(new Vec2(640f, 400f)); // the map
            ui.Frame();
            Assert.False(tips.Shown);
        }
    }
}
