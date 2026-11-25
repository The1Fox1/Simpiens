using System;
using Simpiens.Cognition.Contracts;
using Simpiens.Simulation;
using Simpiens.Simulation.Life;
using Simpiens.Simulation.Workshops;
using Simpiens.Testing.Environment;

namespace Simpiens.Testing.Life
{
    /// <summary>
    /// Chronotypes and the routine (Epic LIFE-1 plan 3, R.1, D28, D29): the draw's odds; the life record's byte; each day's
    /// blocks covering it once, at the chronotype's hours and diligence's length; an artisan's working block from its shifts;
    /// the same inputs giving the same routine; the context's block; and no allocation. Runs as a test (ValidatorTests).
    /// </summary>
    public class RoutineValidator
    {
        private static readonly EntityId Ama = new EntityId(1u, 0u, 0u, 1u);

        public void RunAllValidationTests()
        {
            TestLog.Log("[RoutineValidator] Beginning routine validation...");
            ValidateTheDraw();
            ValidateTheRecord();
            ValidateTheBlocks();
            ValidateAnArtisansShifts();
            ValidatePureAndQuiet();
            TestLog.Log("<color=green>[RoutineValidator] ALL 5 TESTS PASSED SUCCESSFULLY!</color>");
        }

        private void ValidateTheDraw()
        {
            int lark = 0, owl = 0, n = 5000;
            for (uint i = 0; i < n; i++)
            {
                var c = Routine.Draw(424242UL, new EntityId(i + 7u, 3u, 0u, i));
                if (c == Chronotype.Lark) lark++;
                else if (c == Chronotype.Owl) owl++;
                Assert(c == Routine.Draw(424242UL, new EntityId(i + 7u, 3u, 0u, i)), "The same seed and crawler draw the same chronotype.");
            }
            Assert(Math.Abs(lark / (float)n - Routine.LarkOdds) < 0.025f, $"Larks about 20% ({100f * lark / n:F1}%).");
            Assert(Math.Abs(owl / (float)n - Routine.OwlOdds) < 0.025f, $"Owls about 20% ({100f * owl / n:F1}%).");
        }

        private void ValidateTheRecord()
        {
            var life = default(CrawlerLife);
            Assert(life.Chronotype == Chronotype.Average, "A record with none drawn reads as average.");
            life.SetChronotype(Chronotype.Owl);
            Assert(life.Chronotype == Chronotype.Owl && CrawlerLife.BytesOf(life)[279] == (byte)Chronotype.Owl, "The chronotype is the record's byte 279.");
            Assert(CrawlerLife.SpareBytes == 224, $"Its spare bytes are untouched ({CrawlerLife.SpareBytes}).");
        }

        private void ValidateTheBlocks()
        {
            foreach (var chronotype in new[] { Chronotype.Lark, Chronotype.Average, Chronotype.Owl })
                foreach (float diligence in new[] { -1f, 0f, 1f })
                {
                    for (long day = 1; day < 6; day++)
                    {
                        // A day's waking falls at its hour, give or take the day's shift
                        long wakes = Routine.WakeTick(chronotype, Ama, day);
                        int minute = DayClock.MinuteOfDay(wakes);
                        Assert(Math.Abs(minute - Routine.WakeMinute(chronotype)) <= Routine.WakeShiftMinutes, $"{chronotype}: wakes at {minute / 60:D2}:{minute % 60:D2} on day {day}.");

                        var routine = Routine.Of(chronotype, diligence, Ama, wakes + 1);
                        Assert(routine.WakesAt == wakes, $"{chronotype}: a tick after waking is that day's routine.");
                        Assert(Routine.Of(chronotype, diligence, Ama, wakes - 1).NextWake == wakes, $"{chronotype}: a tick before waking is the night before's.");
                        long work = routine.WorkUntil - routine.WorkFrom;
                        long expected = (Routine.WorkMinutes + (long)MathF.Round(diligence * Routine.WorkByDiligenceMinutes)) * DayClock.TicksPerMinute;
                        Assert(work == expected && work >= DayClock.Hours(5) && work <= DayClock.Hours(9), $"{chronotype}, diligence {diligence}: works {work / DayClock.TicksPerHour} h.");
                        long night = routine.NextWake - routine.NightFrom;
                        Assert(Math.Abs(night - DayClock.Hours(8)) <= 2L * Routine.WakeShiftMinutes * DayClock.TicksPerMinute, $"{chronotype}: a night of about 8 h ({night / (float)DayClock.TicksPerHour:F1}).");

                        // Every tick of the day in exactly one block, in order: leisure, working, leisure, night
                        int changes = 0;
                        var last = RoutineBlock.None;
                        for (long t = routine.WakesAt; t < routine.NextWake; t += DayClock.TicksPerMinute)
                        {
                            var block = routine.BlockAt(t);
                            Assert(block != RoutineBlock.None, "Every tick is in a block.");
                            Assert(Routine.Of(chronotype, diligence, Ama, t).WakesAt == routine.WakesAt, "Every tick of the day finds the same routine.");
                            if (block != last) changes++;
                            last = block;
                        }
                        Assert(changes == 4 && last == RoutineBlock.Night, $"{chronotype}: leisure, working, leisure, night ({changes} blocks).");
                    }
                }

            // The neutral average: up about 07:00, at work about 08:00–15:00, in bed about 23:00
            var day2 = Routine.Of(Chronotype.Average, 0f, Ama, DayClock.TicksPerDay + DayClock.Hours(6));
            Assert(day2.BlockAt(day2.WakesAt + DayClock.Hours(3)) == RoutineBlock.Working && day2.BlockAt(day2.WakesAt + DayClock.Hours(12)) == RoutineBlock.Leisure
                && day2.BlockAt(day2.WakesAt + DayClock.Hours(17)) == RoutineBlock.Night, "The average's day: working mid-morning, leisure in the evening, asleep before the small hours.");
        }

        private void ValidateAnArtisansShifts()
        {
            // The day shift, 10:00 to 18:00: working then, whatever its routine says; its routine's working hours outside are leisure
            long day = 2;
            long midnight = day * DayClock.TicksPerDay - (long)DayClock.StartMinute * DayClock.TicksPerMinute;
            for (int hour = 0; hour < 24; hour++)
            {
                long t = midnight + DayClock.Hours(hour) + DayClock.Minutes(30);
                var plain = Routine.Of(Chronotype.Lark, 0f, Ama, t).BlockAt(t);
                var artisan = Routine.BlockAt(Chronotype.Lark, 0f, Ama, t, ShiftBlocks.DayShift);
                if (ShiftBlocks.Covers(ShiftBlocks.DayShift, t)) Assert(artisan == RoutineBlock.Working, $"On shift at {hour:D2}:30: working.");
                else Assert(artisan == (plain == RoutineBlock.Working ? RoutineBlock.Leisure : plain), $"Off shift at {hour:D2}:30: its routine, working as leisure ({plain}, {artisan}).");
                Assert(Routine.BlockAt(Chronotype.Lark, 0f, Ama, t, 0) == plain, "No shifts: the routine alone.");
            }
            // The night shift keeps an owl at work in its night
            long late = midnight + DayClock.Hours(23);
            Assert(Routine.BlockAt(Chronotype.Lark, 0f, Ama, late, 0) == RoutineBlock.Night, "A lark is in its night at 23:00.");
            Assert(Routine.BlockAt(Chronotype.Lark, 0f, Ama, late, 1 << 4) == RoutineBlock.Working, "On the 22:00 shift it is working.");
        }

        private void ValidatePureAndQuiet()
        {
            // The context computes its block once, as the routine does, an artisan's from its shifts
            var life = default(CrawlerLife);
            life.SetChronotype(Chronotype.Owl);
            long noon = DayClock.TicksPerDay + DayClock.Hours(6);
            var personality = new AgentPersonality(0f, 0f, 0f, 0f, 0.5f);
            var context = new AgentContext(Ama, default, AgentNeeds.Default, 0f, null, null, (uint)noon, personality: personality, life: life);
            Assert(context.Block == Routine.BlockAt(Chronotype.Owl, personality.Diligence, Ama, noon, 0), $"The context's block is the routine's ({context.Block}).");
            var shifted = new AgentContext(Ama, default, AgentNeeds.Default, 0f, null, null, (uint)noon, personality: personality, life: life,
                station: StationOrder.OffShift(ShiftBlocks.DayShift));
            Assert(shifted.Block == RoutineBlock.Working && !shifted.Station.IsActive, "An artisan off its station's work but in its hours is working; its order is not active.");

            Routine.BlockAt(Chronotype.Lark, 0.3f, Ama, noon, 0);
            long before = GC.GetAllocatedBytesForCurrentThread();
            var sum = 0;
            for (int i = 0; i < 1000; i++) sum += (int)Routine.BlockAt(Chronotype.Lark, 0.3f, Ama, noon + i * 97L, ShiftBlocks.DayShift);
            Assert(GC.GetAllocatedBytesForCurrentThread() == before && sum > 0, "The routine allocates nothing.");
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("[RoutineValidator] FAILED: " + message);
        }
    }
}
