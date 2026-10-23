using Gum.Forms.Controls;
using Simpiens.Presentation;
using Xunit;

namespace Simpiens.Client.Tests
{
    /// <summary>
    /// The header's pause and speed controls, and their keys (review D3): clicks and keys queue host commands, and the
    /// world stops or changes speed on the next update, before its ticks.
    /// </summary>
    public class SpeedControlTests
    {
        private static Vec2 Centre(FrameworkElement control)
            => new Vec2(control.AbsoluteLeft + control.ActualWidth * 0.5f, control.AbsoluteTop + control.ActualHeight * 0.5f);

        [Fact]
        public void TheHeaderPausesResumesAndPicksTheSpeedAndSoDoTheKeys()
        {
            var ui = UiDriver.Start(width: 1280, height: 720); // the driver holds the clock at zero: a paused world
            var game = ui.Game;
            var clock = game.Session.Simulation.Clock;
            var header = game.Play.Dashboard.Header;
            Assert.True(header.PauseButton.IsVisible && header.SpeedButton.IsVisible);
            Assert.Equal("PLAY", header.PauseButton.Text);
            Assert.Equal("1x", header.SpeedButton.Text);

            ui.Click(Centre(header.PauseButton));
            ui.Frame(); // the host applies it before its next ticks
            Assert.Equal(1f, clock.TimeMultiplier);
            Assert.False(game.Session.Paused);
            Assert.Equal("PAUSE", header.PauseButton.Text);

            ui.Click(Centre(header.SpeedButton));
            ui.Frame();
            Assert.Equal(2f, clock.TimeMultiplier);
            Assert.Equal("2x", header.SpeedButton.Text);

            ui.Script.Keys = new KeyInput { SpeedKey = 4 };
            ui.Frame();
            ui.Script.Keys = default;
            ui.Frame();
            Assert.Equal(10f, clock.TimeMultiplier);
            Assert.Equal("10x", header.SpeedButton.Text);

            ui.Script.Keys = new KeyInput { PausePressed = true };
            ui.Frame();
            ui.Script.Keys = default;
            ui.Frame();
            Assert.True(game.Session.Paused);
            Assert.Equal(0f, clock.TimeMultiplier);
            Assert.Equal("PLAY", header.PauseButton.Text);
            Assert.Equal("10x", header.SpeedButton.Text); // the speed waits for the resume
            long held = clock.CurrentTick;
            ui.Frame(10);
            Assert.Equal(held, clock.CurrentTick);

            ui.Click(Centre(header.SpeedButton)); // 10x round to 1x, still paused
            ui.Frame();
            Assert.Equal("1x", header.SpeedButton.Text);
            Assert.Equal(0f, clock.TimeMultiplier);
            ui.Click(Centre(header.PauseButton));
            ui.Frame();
            Assert.Equal(1f, clock.TimeMultiplier);
        }
    }
}
