namespace Simpiens.Simulation
{
    /// <summary>
    /// The outpost's day (Epic 10 Part 2, milestone T, D17; <c>docs/time-model.md</c>): 10,080 ticks, RimWorld's 1,000
    /// seconds at 1x rounded so a game minute is a whole 7 ticks and an hour 420. Tick 0 is 06:00 on day 1. Everything a
    /// crawler does or undergoes is written in the day's time and turned into ticks here (D18); motion stays in ticks
    /// and seconds of the <see cref="SimulationClock"/>.
    /// </summary>
    public static class DayClock
    {
        public const int TicksPerMinute = 7;
        public const int MinutesPerHour = 60;
        public const int HoursPerDay = 24;
        public const int TicksPerHour = TicksPerMinute * MinutesPerHour;
        public const int TicksPerDay = TicksPerHour * HoursPerDay;
        public const int MinutesPerDay = MinutesPerHour * HoursPerDay;

        /// <summary>Real seconds in a game hour at 1x (42): a rate per game hour divided by this is a rate per second.</summary>
        public const float SecondsPerHour = TicksPerHour * SimulationClock.FixedTickDeltaTime;

        /// <summary>The time a session starts: 06:00, in minutes after midnight.</summary>
        public const int StartMinute = 6 * MinutesPerHour;

        /// <summary>Night, from 18:00 to 06:00.</summary>
        public const int NightStartsMinute = 18 * MinutesPerHour;
        public const int NightEndsMinute = 6 * MinutesPerHour;

        /// <summary>Ticks in <paramref name="minutes"/> game minutes.</summary>
        public static uint Minutes(int minutes) => (uint)(minutes * TicksPerMinute);

        /// <summary>Ticks in <paramref name="hours"/> game hours.</summary>
        public static uint Hours(int hours) => (uint)(hours * TicksPerHour);

        /// <summary>A rate per game hour as a rate per second of simulation time, for needs ticked with a delta in seconds.</summary>
        public static float PerSecond(float perHour) => perHour / SecondsPerHour;

        /// <summary>The day, from 1, at <paramref name="tick"/>.</summary>
        public static long Day(long tick) => (Clamp(tick) + (long)StartMinute * TicksPerMinute) / TicksPerDay + 1;

        /// <summary>Minutes after midnight at <paramref name="tick"/>.</summary>
        public static int MinuteOfDay(long tick) => (int)((StartMinute + Clamp(tick) / TicksPerMinute) % MinutesPerDay);

        /// <summary>The hour, 0 to 23, at <paramref name="tick"/>.</summary>
        public static int HourOfDay(long tick) => MinuteOfDay(tick) / MinutesPerHour;

        public static bool IsNight(long tick)
        {
            int minute = MinuteOfDay(tick);
            return minute < NightEndsMinute || minute >= NightStartsMinute;
        }

        private static long Clamp(long tick) => tick < 0 ? 0 : tick;
    }
}
