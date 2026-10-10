using UnityEngine;

namespace Simpiens.UI
{
    /// <summary>
    /// Pure C# utility translating simulation clock ticks into human-readable relative time badges.
    /// Eliminates raw engine tick math for players (e.g. "[Tick 1420]" -> "[1m ago]").
    /// </summary>
    public static class RelativeTimeFormatter
    {
        public const float DefaultTicksPerSecond = 10f;
        private const string JustNow = "[Just now]";

        public static string Format(uint eventTick, uint currentTick, float ticksPerSecond = DefaultTicksPerSecond)
        {
            if (ticksPerSecond <= 0f) ticksPerSecond = DefaultTicksPerSecond;
            if (currentTick <= eventTick) return JustNow;

            uint deltaTicks = currentTick - eventTick;
            float seconds = deltaTicks / ticksPerSecond;

            if (seconds < 10f)
            {
                return JustNow;
            }

            if (seconds < 60f)
            {
                int sec = (int)seconds;
                return $"[{sec}s ago]";
            }

            int minutes = (int)(seconds / 60f);
            if (minutes < 60)
            {
                return $"[{minutes}m ago]";
            }

            int hours = minutes / 60;
            return $"[{hours}h ago]";
        }
    }
}
