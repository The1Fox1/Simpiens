using System;
using Simpiens.Cognition;
using Simpiens.Simulation;
using Simpiens.Simulation.Economy;

namespace Simpiens.Testing.Drives
{
    /// <summary>
    /// The 24-hour day's pace (Epic 10 Part 2, milestone T; <c>docs/time-model.md</c>): a real crawler's state, ticked at
    /// <see cref="DayTuning.FullDay"/>, meets the day's targets. Until the cut-over the default pace is
    /// <see cref="DayTuning.Legacy"/>, so this is where the new day is checked.
    /// </summary>
    public class DayPaceValidator
    {
        private const float Tolerance = 0.5f;

        public void Start()
        {
            TestLog.Log("[DayPaceValidator] Beginning validation of the 24-hour day's pace...");

            ValidateHungerFillsOverADay();
            ValidateSocialEmptiesOverSixteenHours();

            TestLog.Log("<color=green>[DayPaceValidator] ALL 2 TESTS PASSED SUCCESSFULLY!</color>");
        }

        // D19: fed to starving over 24 hours, so a crawler eating from 50 eats two or three times a day
        private void ValidateHungerFillsOverADay()
        {
            var day = DayTuning.FullDay;
            var state = Crawler(hunger: 0f);
            Run(state, day, DayClock.TicksPerDay / 2);
            Assert(Math.Abs(state.Hunger - 50f) < Tolerance, $"Half a day takes hunger from 0 to 50 (actual: {state.Hunger:F2})");
            Run(state, day, DayClock.TicksPerDay / 2);
            Assert(Math.Abs(state.Hunger - 100f) < Tolerance, $"A day takes hunger from 0 to 100 (actual: {state.Hunger:F2})");

            var before = Crawler(hunger: 0f);
            Run(before, DayTuning.Legacy, 670);
            Assert(Math.Abs(before.Hunger - 100f) < Tolerance, $"Before the day, hunger filled in 67 s (actual: {before.Hunger:F2})");
            TestLog.Log("[PASS] Test 1: hunger fills over a day.");
        }

        // T2.1: company once or twice a day
        private void ValidateSocialEmptiesOverSixteenHours()
        {
            var state = Crawler(hunger: 0f);
            state.Social = 100f;
            Run(state, DayTuning.FullDay, 16 * DayClock.TicksPerHour);
            Assert(state.Social < Tolerance, $"Sixteen hours empty social (actual: {state.Social:F2})");
            TestLog.Log("[PASS] Test 2: social empties over sixteen hours.");
        }

        private static AgentState Crawler(float hunger)
        {
            var state = new AgentState(EntityId.Generate());
            state.Hunger = hunger;
            return state;
        }

        private static void Run(AgentState state, in DayTuning day, int ticks)
        {
            for (int i = 0; i < ticks; i++) state.Tick(SimulationClock.FixedTickDeltaTime, day);
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new Exception($"[ASSERTION FAILED] {message}");
        }
    }
}
