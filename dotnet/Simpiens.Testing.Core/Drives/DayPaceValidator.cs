using System;
using Simpiens.Cognition;
using Simpiens.Simulation;
using Simpiens.Simulation.Economy;

namespace Simpiens.Testing.Drives
{
    /// <summary>
    /// The 24-hour day's pace (Epic 10 Part 2, milestone T; <c>docs/time-model.md</c>): a real crawler's state, ticked at
    /// <see cref="DayTuning.Default"/>, meets the day's targets.
    /// </summary>
    public class DayPaceValidator
    {
        private const float Tolerance = 0.5f;

        public void Start()
        {
            TestLog.Log("[DayPaceValidator] Beginning validation of the 24-hour day's pace...");

            ValidateHungerFillsOverADay();
            ValidateSocialEmptiesOverSixteenHours();
            ValidateEnergyLastsAWakingDay();
            ValidateSleepersWake();
            ValidateTrustFadesOverDays();

            TestLog.Log("<color=green>[DayPaceValidator] ALL 5 TESTS PASSED SUCCESSFULLY!</color>");
        }

        // D19: fed to starving over 24 hours, so a crawler eating from 50 eats two or three times a day
        private void ValidateHungerFillsOverADay()
        {
            var day = DayTuning.Default;
            var state = Crawler(hunger: 0f);
            Run(state, day, DayClock.TicksPerDay / 2);
            Assert(Math.Abs(state.Hunger - 50f) < Tolerance, $"Half a day takes hunger from 0 to 50 (actual: {state.Hunger:F2})");
            Run(state, day, DayClock.TicksPerDay / 2);
            Assert(Math.Abs(state.Hunger - 100f) < Tolerance, $"A day takes hunger from 0 to 100 (actual: {state.Hunger:F2})");
            TestLog.Log("[PASS] Test 1: hunger fills over a day.");
        }

        // T2.1: company once or twice a day
        private void ValidateSocialEmptiesOverSixteenHours()
        {
            var state = Crawler(hunger: 0f);
            state.Social = 100f;
            Run(state, DayTuning.Default, 16 * DayClock.TicksPerHour);
            Assert(state.Social < Tolerance, $"Sixteen hours empty social (actual: {state.Social:F2})");
            TestLog.Log("[PASS] Test 2: social empties over sixteen hours.");
        }

        // D20, T2.2: standing, a rested crawler reaches the tired line (40) after a waking day of about 16 hours (walking tires
        // three times as fast); a collapse (energy 0), recovering where it fell at half a bunk's rate, is up at 30 after 7.5 hours
        private void ValidateEnergyLastsAWakingDay()
        {
            var day = DayTuning.Default;
            var state = Crawler(hunger: 0f);
            Run(state, day, 16 * DayClock.TicksPerHour);
            Assert(Math.Abs(state.Energy - 56f) < Tolerance, $"Sixteen hours standing take energy from 100 to 56 (actual: {state.Energy:F2})");
            Assert(SimMath.Approximately(day.EnergyWalkingPerSecond, 3f * day.EnergyStandingPerSecond), "Walking tires three times as fast as standing");
            Assert(SimMath.Approximately(day.BunkRecoveryPerSecond * 8f * DayClock.SecondsPerHour, 64f), "Eight hours in a bunk restore 64");

            var collapsed = Crawler(hunger: 0f);
            collapsed.CollapseFromExhaustion();
            int ticks = 0;
            while (collapsed.IsExhaustionCollapsed && ticks < DayClock.TicksPerDay) { collapsed.Tick(SimulationClock.FixedTickDeltaTime, day); ticks++; }
            float hours = ticks / (float)DayClock.TicksPerHour;
            Assert(!collapsed.IsExhaustionCollapsed && Math.Abs(hours - 7.5f) < 0.1f, $"A collapsed crawler is up after 7.5 hours (actual: {hours:F2} h)");
            TestLog.Log("[PASS] Test 3: energy lasts a waking day.");
        }

        // T2.2: a sleeper is up when rested, or when hunger reaches the starvation line
        private void ValidateSleepersWake()
        {
            var day = DayTuning.Default;
            Assert(!day.Wakes(energy: 99f, hunger: 60f) && day.Wakes(energy: 100f, hunger: 0f) && day.Wakes(energy: 50f, hunger: 85f), "Up when rested or starving");
            TestLog.Log("[PASS] Test 4: sleepers wake rested or starving.");
        }

        // T4.3, D27: a crawler's memory fades affinities at the session's pace, 5 every 12 game hours; a memory no session
        // has set fades every 1,000 ticks
        private void ValidateTrustFadesOverDays()
        {
            var peer = EntityId.Generate();
            var memory = new Simpiens.Cognition.Memory.AgentMemory();
            memory.RecordInteraction(peer, deltaWarmth: 0, deltaTrust: 40, deltaFear: 0, deltaRespect: 0, currentTick: 0);
            uint halfDay = DayClock.Hours(12);
            Assert(memory.GetAffinity(peer, halfDay).Trust == 40 - 5 * (int)(halfDay / 1000), "A memory no session has set fades 5 every 1,000 ticks");
            memory.TrustFadeTicks = DayTuning.Default.TrustFadeTicks;
            Assert(memory.GetAffinity(peer, halfDay - 1).Trust == 40 && memory.GetAffinity(peer, halfDay).Trust == 35, "On the day, 5 every 12 game hours");
            var snapshot = memory.GetSnapshot(halfDay);
            Assert(snapshot.GetAffinity(peer, 2 * halfDay).Trust == 30, "Cognition's snapshot fades at the same pace");
            TestLog.Log("[PASS] Test 5: trust fades over days.");
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
