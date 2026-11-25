using System;
using Simpiens.Cognition;
using Simpiens.Cognition.Contracts;
using Simpiens.Cognition.Planning;
using Simpiens.Entities;
using Simpiens.Simulation;
using Simpiens.Simulation.Life;
using Simpiens.Testing.Environment;

namespace Simpiens.Testing.Life
{
    /// <summary>
    /// The day's intention (Epic LIFE-1 plan 3, R.7, D34): the odds by input; the draw on its own salt, deterministic and near its
    /// odds; a day off halving weariness and a second clearing it; once a routine day, awake outside the night; a record before
    /// plan 3 (or a stale day) reading a work day; the context's intention and its weights. Runs as a test (ValidatorTests).
    /// </summary>
    public class IntentionValidator
    {
        private static readonly EntityId Ama = new EntityId(5u, 0u, 0u, 5u);

        public void RunAllValidationTests()
        {
            TestLog.Log("[IntentionValidator] Beginning intention validation...");
            ValidateTheOdds();
            ValidateTheDraw();
            ValidateADayOff();
            ValidateOnceADay();
            ValidateTheWeights();
            TestLog.Log("<color=green>[IntentionValidator] ALL 5 TESTS PASSED SUCCESSFULLY!</color>");
        }

        private void ValidateTheOdds()
        {
            var tuning = RhythmTuning.Default;
            var plain = IntentionRules.For(tuning, 0f, false, false, false, false);
            Assert(Near(plain.Rest, 0.05f) && plain.Training == 0f && plain.Want == 0f, $"A work day by default: a rest day 5% ({plain.Rest}).");
            Assert(Near(IntentionRules.For(tuning, 100f, false, false, false, false).Rest, 0.45f) && Near(IntentionRules.For(tuning, 50f, false, false, false, false).Rest, 0.25f),
                "Weariness adds up to 40%.");
            Assert(Near(IntentionRules.For(tuning, 0f, true, false, false, false).Rest, 0.55f), "A windfall in the last day adds 50%.");
            Assert(Near(IntentionRules.For(tuning, 0f, false, true, false, false).Rest, 0.2f), "A purse above its target adds 15%.");
            var all = IntentionRules.For(tuning, 100f, true, true, true, true);
            Assert(all.Rest == 1f && all.Training == 0f && all.Want == 0f, "Held to 1, rest first.");
            var keen = IntentionRules.For(tuning, 0f, false, false, true, true);
            Assert(Near(keen.Training, 0.15f) && Near(keen.Want, 0.25f), "A passion gives a training day 15%, a want a want's day 25%.");
            var none = IntentionRules.For(default, 100f, true, true, true, true);
            Assert(none.Rest == 0f && none.Training == 0f && none.Want == 0f, "A zeroed rhythm draws only work days.");
            Assert(IntentionRules.WindfallWithinADay(1000u, 1000u + DayClock.TicksPerDay) && !IntentionRules.WindfallWithinADay(1000u, 1001u + DayClock.TicksPerDay)
                && !IntentionRules.WindfallWithinADay(0u, 5u), "A windfall counts for a game day after it.");
        }

        private void ValidateTheDraw()
        {
            var odds = new IntentionRules.Odds(0.3f, 0.2f, 0.1f);
            int rest = 0, training = 0, want = 0, n = 4000;
            for (int day = 0; day < n; day++)
            {
                var drawn = IntentionRules.Draw(odds, 424242UL, Ama, day);
                Assert(drawn == IntentionRules.Draw(odds, 424242UL, Ama, day), "The same seed, crawler and day draw the same.");
                if (drawn == DayIntention.Rest) rest++;
                else if (drawn == DayIntention.Training) training++;
                else if (drawn == DayIntention.Want) want++;
            }
            Assert(Math.Abs(rest / (float)n - 0.3f) < 0.03f && Math.Abs(training / (float)n - 0.2f) < 0.03f && Math.Abs(want / (float)n - 0.1f) < 0.03f,
                $"Near its odds ({rest}, {training}, {want} of {n}).");
        }

        private void ValidateADayOff()
        {
            var life = default(CrawlerLife);
            Assert(life.Intention == DayIntention.Work && life.IntentionDay == 0 && life.IntentionFor(3) == DayIntention.Work, "A record before plan 3 reads a work day.");
            life.Weary(60f);
            IntentionRules.Begin(ref life, DayIntention.Work, 3);
            Assert(life.Weariness == 60f && life.IntentionFor(3) == DayIntention.Work && life.IntentionDay == 4, "A work day leaves weariness.");
            IntentionRules.Begin(ref life, DayIntention.Rest, 4);
            Assert(life.Weariness == 30f && life.IntentionFor(4) == DayIntention.Rest, "A day off halves it.");
            Assert(life.IntentionFor(5) == DayIntention.Work && life.IntentionFor(3) == DayIntention.Work, "Another day's intention reads a work day until drawn.");
            IntentionRules.Begin(ref life, DayIntention.Rest, 5);
            Assert(life.Weariness == 0f, "A second in a row clears it.");
            var bytes = CrawlerLife.BytesOf(life);
            Assert(bytes[276] == (byte)DayIntention.Rest && BitConverter.ToUInt16(bytes.Slice(288, 2)) == 6, "The intention at 276, its day plus one at 288.");
        }

        private void ValidateOnceADay()
        {
            var state = new AgentState(Ama) { Hunger = 10f };
            var life = state.Life;
            life.SetChronotype(Chronotype.Average);
            state.Life = life;
            var routine = Routine.Of(Chronotype.Average, 0f, Ama, DayClock.TicksPerDay + DayClock.Hours(4));
            uint night = (uint)(routine.WakesAt - 7), morning = (uint)(routine.WakesAt + 7);
            var first = Routine.Of(Chronotype.Average, 0f, Ama, DayClock.Hours(4));
            Assert(first.Day == 0 && !state.IntentionDue((uint)(first.WakesAt + 7), out _), "The outpost's first routine day is a work day: no night's sleep came before it.");
            Assert(!state.IntentionDue(night, out _), "Not in its night.");
            state.VisualState = AgentVisualState.Sleeping;
            Assert(!state.IntentionDue(morning, out _), "Not asleep.");
            state.VisualState = AgentVisualState.Idle;
            Assert(state.IntentionDue(morning, out long day) && day == routine.Day, "Awake at its waking hour: due, for that routine day.");
            state.DrawIntention(day, morning, 424242UL, RhythmTuning.Default, 80);
            Assert(!state.IntentionDue(morning + 70, out _) && state.Life.IntentionDay == day + 1, "Drawn once for the day.");
            Assert(state.IntentionDue((uint)(routine.NextWake + 7), out long next) && next == day + 1, "Due again the next morning.");
            var drifter = new AgentState(EntityId.Generate()) { DepartsAtTick = 99999u };
            Assert(!drifter.IntentionDue(morning, out _), "A drifter draws none.");
        }

        private void ValidateTheWeights()
        {
            var routine = Routine.Of(Chronotype.Average, 0f, Ama, DayClock.TicksPerDay + DayClock.Hours(4));
            uint working = (uint)(routine.WorkFrom + 7);
            AgentContext With(DayIntention intention, long day)
            {
                var life = default(CrawlerLife);
                life.SetIntention(intention, day);
                return new AgentContext(Ama, default, new AgentNeeds(20f, 80f, 80f), 0f, null, null, working, life: life);
            }
            var rest = With(DayIntention.Rest, routine.Day);
            Assert(rest.Intention == DayIntention.Rest && With(DayIntention.Rest, routine.Day - 1).Intention == DayIntention.Work, "The context reads today's intention, a stale one as a work day.");
            var earn = new Simpiens.Cognition.Planning.Goals.FulfillBountyGoal();
            var recreate = new Simpiens.Cognition.Planning.Goals.RecreateGoal();
            var gather = new Simpiens.Cognition.Planning.Goals.GatherAtAnchorGoal();
            var study = new Simpiens.Cognition.Planning.Goals.StudyGoal();
            var fulfil = new Simpiens.Cognition.Planning.Goals.FulfilWantGoal();
            var eat = new Simpiens.Cognition.Planning.Goals.SatiateHungerGoal();
            var work = With(DayIntention.Work, routine.Day);
            Assert(Near(MotiveModel.Weight(earn, rest), MotiveModel.Weight(earn, work) * 0.4f) && Near(MotiveModel.Weight(recreate, rest), MotiveModel.Weight(recreate, work) * 1.3f)
                && Near(MotiveModel.Weight(gather, rest), MotiveModel.Weight(gather, work)) && MotiveModel.Weight(eat, rest) == 1f,
                "A rest day: work 0.4, leisure 1.3, company as any day (D34 amended); hunger unweighted.");
            var training = With(DayIntention.Training, routine.Day);
            Assert(Near(MotiveModel.Weight(study, training), MotiveModel.Weight(study, work) * 1.5f) && Near(MotiveModel.Weight(earn, training), MotiveModel.Weight(earn, work) * 0.8f),
                "A training day: growth 1.5, work 0.8.");
            var wanting = With(DayIntention.Want, routine.Day);
            Assert(Near(MotiveModel.Weight(fulfil, wanting), MotiveModel.Weight(fulfil, work) * 1.5f) && MotiveModel.Weight(recreate, wanting) == MotiveModel.Weight(recreate, work),
                "A want's day: the want 1.5, the rest as a work day.");
            // Duty on a day off (R.9, D36): 0.8, though the routine never weighs it
            var comply = new Simpiens.Cognition.Planning.Goals.ComplyDirectiveGoal();
            Assert(Near(MotiveModel.Weight(comply, rest), 0.8f) && MotiveModel.Weight(comply, work) == 1f && MotiveModel.Weight(comply, training) == 1f,
                "Duty weighs 0.8 on a rest day, 1 on any other.");

            // Called in (D36): a rest day turned to a work day; weariness kept
            var life = default(CrawlerLife);
            life.Weary(40f);
            IntentionRules.Begin(ref life, DayIntention.Rest, 9);
            IntentionRules.CallIn(ref life, 9);
            Assert(life.IntentionFor(9) == DayIntention.Work && life.Weariness == 20f, "Called in: a work day, weariness as the day off left it.");
            IntentionRules.CallIn(ref life, 10);
            Assert(life.IntentionFor(10) == DayIntention.Work && life.IntentionDay == 10, "Calling in on another day changes nothing.");

            // Q11's switch (D37): every seventh day a rest day for everyone, whatever the odds
            var outpost = RhythmTuning.Default with { OutpostRestDay = true };
            Assert(IntentionRules.IsOutpostRestDay(outpost, 6) && IntentionRules.IsOutpostRestDay(outpost, 13) && !IntentionRules.IsOutpostRestDay(outpost, 7)
                && !IntentionRules.IsOutpostRestDay(RhythmTuning.Default, 6), "Days 7, 14, ... under the switch; never without it.");
            var resident = new AgentState(Ama);
            Assert(resident.DrawIntention(6, 1000u, 424242UL, outpost with { RestBase = 0f }, 80) == DayIntention.Rest, "The switch's day is a rest day even at no odds.");
        }

        private static bool Near(float a, float b) => Math.Abs(a - b) < 1e-5f;

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("[IntentionValidator] FAILED: " + message);
        }
    }
}
