using System;

namespace Simpiens.Simulation.Life
{
    /// <summary>
    /// When a crawler's day starts (Epic LIFE-1 plan 3, R.1, D28): drawn at spawn and saved in its life record (byte 279), so
    /// cognition reads it from its copy without the seed. Average is 0, so a record written before it keeps today's hours.
    /// Never reused: the value is what the record holds.
    /// </summary>
    public enum Chronotype : byte
    {
        Average = 0,
        Lark = 1,
        Owl = 2,
    }

    /// <summary>The part of its day a crawler's routine is in at a tick (D29): the weights by family read it (D30).</summary>
    public enum RoutineBlock : byte
    {
        None = 0,
        Working = 1,
        Leisure = 2,
        Night = 3,
    }

    /// <summary>
    /// A crawler's routine for one of its days (Epic LIFE-1 plan 3, R.1, D29; the epic's §2.5): computed, never saved, from its
    /// chronotype, its diligence and the day, as a pure function cognition and the main thread agree on.
    /// <list type="bullet">
    /// <item><b>Waking:</b> 05:00 for a lark, 07:00 for the average, 10:00 for an owl, shifted up to
    /// <see cref="WakeShiftMinutes"/> either way by a draw keyed on the crawler and the day (no session seed, so cognition
    /// computes it from what its context holds).</item>
    /// <item><b>Working:</b> from an hour after waking, 5 to 9 hours by diligence (7 for the neutral).</item>
    /// <item><b>Leisure:</b> the waking hour, and from the working block's end until bedtime, <see cref="WakingHours"/> after
    /// waking.</item>
    /// <item><b>Night:</b> from bedtime until the next day's waking.</item>
    /// <item><b>An artisan's working block is its shifts</b> (§2.5): when the player has set its hours, the routine's working
    /// block gives way to them (<see cref="BlockAt(Chronotype, float, EntityId, long, byte)"/>).</item>
    /// </list>
    /// Allocation-free; a day is found from the tick, so a routine needs no state of its own.
    /// </summary>
    public readonly struct Routine
    {
        /// <summary>The waking minute after midnight by chronotype.</summary>
        public const int LarkWakes = 5 * DayClock.MinutesPerHour;
        public const int AverageWakes = 7 * DayClock.MinutesPerHour;
        public const int OwlWakes = 10 * DayClock.MinutesPerHour;

        /// <summary>How far a day's draw shifts the waking hour, either way.</summary>
        public const int WakeShiftMinutes = 40;

        /// <summary>The working block starts this long after waking.</summary>
        public const int WorkStartsAfterMinutes = 60;

        /// <summary>The working block's length for a neutral diligence, and how far diligence moves it either way (5 to 9 hours).</summary>
        public const int WorkMinutes = 7 * DayClock.MinutesPerHour;
        public const int WorkByDiligenceMinutes = 2 * DayClock.MinutesPerHour;

        /// <summary>Bedtime comes this long after waking: the waking day, the rest its night.</summary>
        public const int WakingHours = 16;

        /// <summary>The draw's odds (D28): a lark 20%, an owl 20%, the rest average.</summary>
        public const float LarkOdds = 0.2f;
        public const float OwlOdds = 0.2f;

        /// <summary>The routine day: the clock day of its waking, 0 for day 1 (-1 for the night before the first waking).</summary>
        public readonly long Day;

        /// <summary>This routine day's waking tick (it may be before tick 0 on the first morning).</summary>
        public readonly long WakesAt;

        /// <summary>The working block, from and until.</summary>
        public readonly long WorkFrom, WorkUntil;

        /// <summary>Bedtime: the night from here until <see cref="NextWake"/>.</summary>
        public readonly long NightFrom;

        /// <summary>The next day's waking tick: the routine day ends here.</summary>
        public readonly long NextWake;

        private Routine(long day, long wakesAt, long workFrom, long workUntil, long nightFrom, long nextWake)
        {
            Day = day;
            WakesAt = wakesAt;
            WorkFrom = workFrom;
            WorkUntil = workUntil;
            NightFrom = nightFrom;
            NextWake = nextWake;
        }

        /// <summary>
        /// The routine day <paramref name="tick"/> falls in, for a crawler of <paramref name="chronotype"/> and
        /// <paramref name="diligence"/> (-1 to 1): it begins at that day's waking, so the small hours before waking are the night
        /// before's.
        /// </summary>
        public static Routine Of(Chronotype chronotype, float diligence, EntityId crawler, long tick)
        {
            long day = ClockDay(tick);
            long wakes = WakeTick(chronotype, crawler, day);
            if (tick < wakes)
            {
                day--;
                wakes = WakeTick(chronotype, crawler, day);
            }
            long next = WakeTick(chronotype, crawler, day + 1);
            int workMinutes = WorkMinutes + (int)MathF.Round(Math.Clamp(diligence, -1f, 1f) * WorkByDiligenceMinutes);
            long workFrom = wakes + (long)WorkStartsAfterMinutes * DayClock.TicksPerMinute;
            long workUntil = workFrom + (long)workMinutes * DayClock.TicksPerMinute;
            long night = wakes + (long)WakingHours * DayClock.TicksPerHour;
            return new Routine(day, wakes, workFrom, workUntil, night, next);
        }

        /// <summary>The block <paramref name="tick"/> falls in, within this routine day.</summary>
        public RoutineBlock BlockAt(long tick)
        {
            if (tick >= NightFrom) return RoutineBlock.Night;
            if (tick >= WorkFrom && tick < WorkUntil) return RoutineBlock.Working;
            return RoutineBlock.Leisure;
        }

        /// <summary>
        /// The block <paramref name="tick"/> falls in, for a crawler with <paramref name="shifts"/> (an artisan's
        /// <see cref="Workshops.ShiftBlocks"/> mask, 0 for a crawler who works no station): an artisan works its shifts, so a
        /// tick in one is working and a tick of its routine's working block outside them is leisure; the night stays the
        /// night outside its shifts.
        /// </summary>
        public static RoutineBlock BlockAt(Chronotype chronotype, float diligence, EntityId crawler, long tick, byte shifts) =>
            Of(chronotype, diligence, crawler, tick).BlockAt(tick, shifts);

        /// <summary>The block <paramref name="tick"/> falls in within this routine day, for a crawler with <paramref name="shifts"/> (see the static overload).</summary>
        public RoutineBlock BlockAt(long tick, byte shifts)
        {
            var block = BlockAt(tick);
            if (shifts == 0) return block;
            if (Workshops.ShiftBlocks.Covers(shifts, tick)) return RoutineBlock.Working;
            return block == RoutineBlock.Working ? RoutineBlock.Leisure : block;
        }

        /// <summary>The minute after midnight <paramref name="chronotype"/> wakes at, before the day's shift.</summary>
        public static int WakeMinute(Chronotype chronotype) => chronotype switch
        {
            Chronotype.Lark => LarkWakes,
            Chronotype.Owl => OwlWakes,
            _ => AverageWakes,
        };

        /// <summary>The day's shift of the waking minute, -40 to 40, keyed on the crawler and the clock day.</summary>
        public static int WakeShift(EntityId crawler, long day)
        {
            var rng = SimRandom.For(0UL, crawler, day, SimRandomSalt.Chronotype);
            return (int)MathF.Floor(rng.Range(-WakeShiftMinutes, WakeShiftMinutes + 1));
        }

        /// <summary>The tick <paramref name="crawler"/> wakes at on clock day <paramref name="day"/> (0 for day 1, whose midnight is before tick 0).</summary>
        public static long WakeTick(Chronotype chronotype, EntityId crawler, long day)
        {
            long midnight = day * DayClock.TicksPerDay - (long)DayClock.StartMinute * DayClock.TicksPerMinute;
            return midnight + (long)(WakeMinute(chronotype) + WakeShift(crawler, day)) * DayClock.TicksPerMinute;
        }

        /// <summary>The clock day <paramref name="tick"/> falls in, from midnight: 0 for day 1.</summary>
        public static long ClockDay(long tick) => ((tick < 0 ? 0 : tick) + (long)DayClock.StartMinute * DayClock.TicksPerMinute) / DayClock.TicksPerDay;

        /// <summary>A crawler's chronotype, drawn at spawn on its own salt (D28): no earlier draw moves.</summary>
        public static Chronotype Draw(ulong seed, EntityId crawler)
        {
            var rng = SimRandom.For(seed, crawler, 0, SimRandomSalt.Chronotype);
            float roll = rng.NextFloat();
            if (roll < LarkOdds) return Chronotype.Lark;
            if (roll >= 1f - OwlOdds) return Chronotype.Owl;
            return Chronotype.Average;
        }
    }
}
