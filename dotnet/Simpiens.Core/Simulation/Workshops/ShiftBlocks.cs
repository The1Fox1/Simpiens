namespace Simpiens.Simulation.Workshops
{
    /// <summary>
    /// The day's six blocks of work (Epic 10 Part 2, W1.3, D12): four game hours each, block 0 from 06:00 to 10:00, then
    /// 10–14, 14–18, 18–22, 22–02 and 02–06. An artisan's shifts are a mask of them, bit 0 for block 0.
    /// </summary>
    public static class ShiftBlocks
    {
        public const int Count = 6;
        public const int HoursEach = 4;
        public const int TicksEach = HoursEach * DayClock.TicksPerHour;

        /// <summary>Every block.</summary>
        public const byte All = (1 << Count) - 1;

        /// <summary>The day shift the station window offers: 10:00 to 18:00, two blocks.</summary>
        public const byte DayShift = 0b000110;

        /// <summary>The block <paramref name="tick"/> falls in, 0 to 5: tick 0 is 06:00, the start of block 0.</summary>
        public static int At(long tick) => (int)((tick < 0 ? 0 : tick) % DayClock.TicksPerDay / TicksEach);

        /// <summary>Whether <paramref name="shifts"/> includes the block <paramref name="tick"/> falls in.</summary>
        public static bool Covers(byte shifts, long tick) => (shifts & (1 << At(tick))) != 0;

        /// <summary>The hour of day block <paramref name="block"/> starts at: 6, 10, 14, 18, 22 or 2.</summary>
        public static int StartHour(int block) => (6 + block * HoursEach) % DayClock.HoursPerDay;

        /// <summary>Blocks in <paramref name="shifts"/>.</summary>
        public static int CountOf(byte shifts)
        {
            int n = 0;
            for (int b = 0; b < Count; b++) if ((shifts & (1 << b)) != 0) n++;
            return n;
        }

        /// <summary>The longest run of blocks in a row, around midnight and the day's turn included (02–06 runs on into 06–10).</summary>
        public static int LongestRun(byte shifts)
        {
            if ((shifts & All) == All) return Count;
            int best = 0, run = 0;
            for (int i = 0; i < 2 * Count; i++)
            {
                run = (shifts & (1 << (i % Count))) != 0 ? run + 1 : 0;
                if (run > best) best = run;
            }
            return best > Count ? Count : best;
        }
    }
}
