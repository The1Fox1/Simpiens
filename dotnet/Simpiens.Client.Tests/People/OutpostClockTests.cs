using Simpiens.Presentation.Dashboard;
using Xunit;

namespace Simpiens.Client.Tests.People
{
    /// <summary>The time of day (Epic UI-Beta B5), in words: the core's 10,080-tick day from 06:00 (Epic 10 Part 2, T1).</summary>
    public class OutpostClockTests
    {
        [Theory]
        [InlineData(0L, "Day 1 · 06:00")]
        [InlineData(6L, "Day 1 · 06:00")] // a minute is 7 ticks
        [InlineData(7L, "Day 1 · 06:01")]
        [InlineData(420L, "Day 1 · 07:00")] // an hour is 420 ticks
        [InlineData(7560L, "Day 2 · 00:00")] // midnight starts the next day
        [InlineData(7559L, "Day 1 · 23:59")]
        [InlineData(10080L, "Day 2 · 06:00")]
        [InlineData(15232L, "Day 2 · 18:16")]
        public void TicksReadAsTheTimeOfDay(long tick, string expected)
        {
            Assert.Equal(expected, OutpostClock.Text(tick));
        }
    }
}
