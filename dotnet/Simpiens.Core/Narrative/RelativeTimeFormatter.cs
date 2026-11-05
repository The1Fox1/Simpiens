using Simpiens.Simulation;

namespace Simpiens.UI
{
    /// <summary>
    /// Pure C# utility translating simulation clock ticks into human-readable relative time badges, in game time (Epic 10
    /// Part 2, T6: on the 24-hour day a game minute is 7 ticks): "[Just now]" under a game minute, then "[25 min ago]",
    /// "[3 h ago]", "[2 d ago]". Eliminates raw engine tick math for players.
    /// </summary>
    public static class RelativeTimeFormatter
    {
        private const string JustNow = "[Just now]";

        public static string Format(uint eventTick, uint currentTick)
        {
            if (currentTick <= eventTick) return JustNow;
            uint minutes = (currentTick - eventTick) / (uint)DayClock.TicksPerMinute;
            if (minutes < 1) return JustNow;
            if (minutes < DayClock.MinutesPerHour) return $"[{minutes} min ago]";
            uint hours = minutes / DayClock.MinutesPerHour;
            if (hours < DayClock.HoursPerDay) return $"[{hours} h ago]";
            return $"[{hours / DayClock.HoursPerDay} d ago]";
        }
    }
}
