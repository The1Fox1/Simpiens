using System;
using Simpiens.Simulation;
using Xunit;

namespace Simpiens.Client.Tests
{
    /// <summary>How the driver turns frames into ticks (Epic CLIENT M0; review D2's cap after a stall).</summary>
    public class SimulationDriverTests
    {
        [Fact]
        public void AStalledFrameRunsAtMostFourTicksTimesTheSpeedAndDropsTheRest()
        {
            var clock = new SimulationClock();
            var driver = new SimulationDriver(clock, ticksPerUpdate: 0);

            driver.Advance(TimeSpan.FromSeconds(2.0)); // a debugger, a dragged window
            Assert.Equal(4L, clock.CurrentTick);
            Assert.Equal(TimeSpan.FromSeconds(1.6), driver.Dropped);

            clock.SetTimeMultiplier(10f);
            driver.Advance(TimeSpan.FromSeconds(5.0));
            Assert.Equal(44L, clock.CurrentTick); // 40 more: the cap is in real time, so it scales with the speed

            driver.Advance(TimeSpan.FromSeconds(0.35)); // an ordinary slow frame runs whole
            Assert.Equal(79L, clock.CurrentTick);
            Assert.Equal(TimeSpan.FromSeconds(6.2), driver.Dropped);
        }

        [Fact]
        public void WholeTickUpdatesIgnoreTheFramesTimeAndAHeldDriverRunsNone()
        {
            var clock = new SimulationClock();
            var driver = new SimulationDriver(clock, ticksPerUpdate: 100);
            driver.Advance(TimeSpan.FromSeconds(30));
            Assert.Equal(100L, clock.CurrentTick);
            Assert.Equal(TimeSpan.Zero, driver.Dropped);

            driver.Held = true;
            driver.Advance(TimeSpan.FromSeconds(1));
            Assert.Equal(100L, clock.CurrentTick);
        }
    }
}
