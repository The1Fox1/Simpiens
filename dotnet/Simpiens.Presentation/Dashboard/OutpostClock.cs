using Simpiens.Simulation;
using static System.FormattableString;

namespace Simpiens.Presentation.Dashboard
{
    /// <summary>
    /// The outpost's time of day, in words (Epic UI-Beta B5). The day is the core's <see cref="DayClock"/> (Epic 10
    /// Part 2, milestone T): 10,080 ticks from 06:00 on day 1.
    /// </summary>
    public static class OutpostClock
    {
        /// <summary>The day, from 1, at <paramref name="tick"/>.</summary>
        public static long Day(long tick) => DayClock.Day(tick);

        /// <summary>Minutes after midnight at <paramref name="tick"/>.</summary>
        public static int MinuteOfDay(long tick) => DayClock.MinuteOfDay(tick);

        /// <summary>"Day 1 · 06:30".</summary>
        public static string Text(long tick)
        {
            int minute = MinuteOfDay(tick);
            return Invariant($"Day {Day(tick)} · {minute / 60:D2}:{minute % 60:D2}");
        }

        /// <summary>
        /// A span of game time in words, by the whole game minute (Epic 10 Part 2, T6): "45 min", "2 h", "2 h 15 min"; a
        /// part of a minute rounds up, so what is still to come never reads as 0.
        /// </summary>
        public static string Span(long ticks)
        {
            long minutes = ticks <= 0 ? 0 : (ticks + DayClock.TicksPerMinute - 1) / DayClock.TicksPerMinute;
            long hours = minutes / 60, rest = minutes % 60;
            if (hours == 0) return Invariant($"{rest} min");
            return rest == 0 ? Invariant($"{hours} h") : Invariant($"{hours} h {rest} min");
        }

        /// <summary>Night, from 18:00 to 06:00: the header shows a moon instead of the sun (the UI polish plan, U4).</summary>
        public static bool IsNight(long tick) => DayClock.IsNight(tick);
    }
}
