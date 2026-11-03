using System.Linq;
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

        /// <summary>The lit pip: the speed the world runs at, or will when it resumes.</summary>
        private static int LitPip(Simpiens.Client.Ui.HeaderBar header)
        {
            int lit = -1;
            for (int i = 0; i < header.SpeedButtons.Count; i++)
            {
                if (Simpiens.Client.Ui.StoneSkin.PieceOf(header.SpeedButtons[i]) != Simpiens.Presentation.Art.UiKit.ButtonActive) continue;
                Assert.Equal(-1, lit); // one pip at a time
                lit = i;
            }
            return lit;
        }

        [Fact]
        public void TheHeaderPausesResumesAndPicksTheSpeedAndSoDoTheKeys()
        {
            var ui = UiDriver.Start(width: 1280, height: 720); // the driver holds the clock at zero: a paused world
            var game = ui.Game;
            var clock = game.Session.Simulation.Clock;
            var header = game.Play.Dashboard.Header;
            Assert.True(header.PauseButton.IsVisible && header.SpeedButtons.All(b => b.IsVisible));
            Assert.Equal("Play", header.PauseOffers);
            Assert.Equal(0, LitPip(header));

            ui.Click(Centre(header.PauseButton));
            ui.Frame(); // the host applies it before its next ticks
            Assert.Equal(1f, clock.TimeMultiplier);
            Assert.False(game.Session.Paused);
            Assert.Equal("Pause", header.PauseOffers);

            ui.Click(Centre(header.SpeedButtons[1]));
            ui.Frame();
            Assert.Equal(2f, clock.TimeMultiplier);
            Assert.Equal(1, LitPip(header));

            ui.Script.Keys = new KeyInput { SpeedKey = 4 };
            ui.Frame();
            ui.Script.Keys = default;
            ui.Frame();
            Assert.Equal(10f, clock.TimeMultiplier);
            Assert.Equal(3, LitPip(header));

            ui.Script.Keys = new KeyInput { PausePressed = true };
            ui.Frame();
            ui.Script.Keys = default;
            ui.Frame();
            Assert.True(game.Session.Paused);
            Assert.Equal(0f, clock.TimeMultiplier);
            Assert.Equal("Play", header.PauseOffers);
            Assert.Equal(3, LitPip(header)); // the speed waits for the resume
            long held = clock.CurrentTick;
            ui.Frame(10);
            Assert.Equal(held, clock.CurrentTick);

            ui.Click(Centre(header.SpeedButtons[0])); // 1x, still paused
            ui.Frame();
            Assert.Equal(0, LitPip(header));
            Assert.Equal(0f, clock.TimeMultiplier);
            ui.Click(Centre(header.PauseButton));
            ui.Frame();
            Assert.Equal(1f, clock.TimeMultiplier);
        }
    }
}
