using static System.FormattableString;

namespace Simpiens.Presentation.Dashboard
{
    /// <summary>
    /// The outpost's time of day, in words (Epic UI-Beta B5): a day is the 15 minutes the live scenarios play (9,000 ticks
    /// at 10 a second), starting at 06:00 on day 1. Presentation only: nothing in the simulation reads it.
    /// </summary>
    public static class OutpostClock
    {
        /// <summary>Ticks in a day: the outpost's day of the live scenarios and the gate seeds.</summary>
        public const long TicksPerDay = 9000;

        /// <summary>The time a session starts: 06:00, in minutes after midnight.</summary>
        public const int StartMinute = 6 * 60;

        private const int MinutesPerDay = 24 * 60;

        /// <summary>The day, from 1, at <paramref name="tick"/>.</summary>
        public static long Day(long tick) => (tick < 0 ? 0 : tick + (long)StartMinute * TicksPerDay / MinutesPerDay) / TicksPerDay + 1;

        /// <summary>Minutes after midnight at <paramref name="tick"/>.</summary>
        public static int MinuteOfDay(long tick) => (int)((StartMinute + (tick < 0 ? 0 : tick) * MinutesPerDay / TicksPerDay) % MinutesPerDay);

        /// <summary>"Day 1 · 06:30".</summary>
        public static string Text(long tick)
        {
            int minute = MinuteOfDay(tick);
            return Invariant($"Day {Day(tick)} · {minute / 60:D2}:{minute % 60:D2}");
        }

        /// <summary>Night, from 18:00 to 06:00: the header shows a moon instead of the sun (the UI polish plan, U4).</summary>
        public static bool IsNight(long tick)
        {
            int minute = MinuteOfDay(tick);
            return minute < 6 * 60 || minute >= 18 * 60;
        }
    }
}
