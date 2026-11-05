using Simpiens.Presentation.Dashboard;
using Xunit;

namespace Simpiens.Client.Tests.People
{
    /// <summary>The time of day (Epic UI-Beta B5): a 9,000-tick day from 06:00, in words.</summary>
    public class OutpostClockTests
    {
        [Theory]
        [InlineData(0L, "Day 1 · 06:00")]
        [InlineData(375L, "Day 1 · 07:00")] // an hour is 375 ticks
        [InlineData(6750L, "Day 2 · 00:00")] // midnight starts the next day
        [InlineData(6749L, "Day 1 · 23:59")]
        [InlineData(9000L, "Day 2 · 06:00")]
        [InlineData(13604L, "Day 2 · 18:16")]
        public void TicksReadAsTheTimeOfDay(long tick, string expected)
        {
            Assert.Equal(expected, OutpostClock.Text(tick));
        }
    }
}
