using Simpiens.Client.Ui;
using Xunit;

namespace Simpiens.Client.Tests
{
    /// <summary>
    /// The interface's motion (the UI polish plan, U7), where the host asks for it: a window arrives over 160 ms (its veil
    /// fades in, its frame rises 8 pixels to rest), and the header's gold rolls to a new figure. Without the option
    /// nothing moves, which is how every render is recorded.
    /// </summary>
    public class MotionTests
    {
        private static UiDriver Start(bool motion) => UiDriver.Start(new ClientOptions { UiMotion = motion }, 1280, 720);

        [Fact]
        public void AWindowArrivesAndComesToRest()
        {
            var ui = Start(motion: true);
            var dashboard = ui.Game.Play.Dashboard;
            dashboard.Model.OpenBounties();
            dashboard.Refresh();
            var window = dashboard.Bounties.Window;
            Assert.True(window.Arriving);
            Assert.Equal(Motion.WindowRise, window.Rise);

            window.Animate(Motion.WindowIn * 0.5f);
            Assert.True(window.Rise > 0f && window.Rise < Motion.WindowRise, $"Halfway, the frame is {window.Rise} px low.");
            Assert.Equal(System.MathF.Round(window.Rise), window.Rise); // whole pixels: icons stay on texel centres

            window.Animate(Motion.WindowIn);
            Assert.False(window.Arriving);
            Assert.Equal(0f, window.Rise);
        }

        [Fact]
        public void TheGoldRollsToItsNewFigure()
        {
            var ui = Start(motion: true);
            var dashboard = ui.Game.Play.Dashboard;
            var sim = (Simpiens.Simulation.SimulationManager)ui.Game.Session.Simulation.Simulation;
            var stall = sim.ActiveStalls[0];
            long before = stall.TreasuryGold;
            sim.UpdateStall(stall.WithTreasury((uint)(before + 400)));
            dashboard.Refresh();
            Assert.True(dashboard.Header.Rolling);
            Assert.Equal($"{before}", dashboard.Header.GoldText);

            dashboard.Header.Animate(Motion.NumberRoll * 0.5f);
            long halfway = long.Parse(dashboard.Header.GoldText, System.Globalization.CultureInfo.InvariantCulture);
            Assert.InRange(halfway, before + 1, before + 399);

            dashboard.Header.Animate(Motion.NumberRoll);
            Assert.False(dashboard.Header.Rolling);
            Assert.Equal($"{before + 400}", dashboard.Header.GoldText);
        }

        [Fact]
        public void WithoutMotionNothingWaits()
        {
            var ui = Start(motion: false);
            var dashboard = ui.Game.Play.Dashboard;
            dashboard.Model.OpenBounties();
            dashboard.Refresh();
            Assert.False(dashboard.Bounties.Window.Arriving);
            Assert.Equal(0f, dashboard.Bounties.Window.Rise);

            var sim = (Simpiens.Simulation.SimulationManager)ui.Game.Session.Simulation.Simulation;
            var stall = sim.ActiveStalls[0];
            sim.UpdateStall(stall.WithTreasury(stall.TreasuryGold + 400));
            dashboard.Refresh();
            Assert.False(dashboard.Header.Rolling);
            Assert.Equal($"{stall.TreasuryGold + 400}", dashboard.Header.GoldText);
        }
    }
}
