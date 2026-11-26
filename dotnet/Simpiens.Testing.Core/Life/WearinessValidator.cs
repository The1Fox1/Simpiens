using System;
using Simpiens.Cognition;
using Simpiens.Entities;
using Simpiens.Simulation;
using Simpiens.Simulation.Economy;
using Simpiens.Simulation.Life;
using Simpiens.Testing.Environment;

namespace Simpiens.Testing.Life
{
    /// <summary>
    /// Weariness and the windfall (Epic LIFE-1 plan 3, R.5, D33): the life record's fields at 268 and 272; weariness rising 1 a
    /// game hour at work and not at rest, held within 0 and 100; a payment of half the comfortable purse or more recorded as a
    /// windfall with its thought, a smaller one not. Runs as a test (ValidatorTests).
    /// </summary>
    public class WearinessValidator
    {
        public void RunAllValidationTests()
        {
            TestLog.Log("[WearinessValidator] Beginning weariness validation...");
            ValidateTheRecord();
            ValidateWorkWearies();
            ValidateTheWindfall();
            TestLog.Log("<color=green>[WearinessValidator] ALL 3 TESTS PASSED SUCCESSFULLY!</color>");
        }

        private void ValidateTheRecord()
        {
            var life = default(CrawlerLife);
            Assert(life.Weariness == 0f && life.LastWindfall == 0u, "A record written before plan 3 reads unwearied, with no windfall.");
            life.Weary(42.5f);
            life.RecordWindfall(1234u);
            var bytes = CrawlerLife.BytesOf(life);
            Assert(BitConverter.ToSingle(bytes.Slice(268, 4)) == 42.5f && BitConverter.ToUInt32(bytes.Slice(272, 4)) == 1234u, "Weariness at 268, the windfall's tick at 272.");
            life.Weary(500f);
            Assert(life.Weariness == CrawlerLife.MaxWeariness, "Held at 100.");
            life.Weary(-500f);
            Assert(life.Weariness == 0f, "And at 0.");
            life.RecordWindfall(0u);
            Assert(life.LastWindfall != 0u, "A windfall at tick 0 still reads as one.");
            Assert(CrawlerLife.SpareBytes == 216, $"The spare bytes are untouched ({CrawlerLife.SpareBytes}).");
        }

        private void ValidateWorkWearies()
        {
            var day = DayTuning.Default;
            var worker = new AgentState(EntityId.Generate()) { Hunger = 0f };
            for (int t = 0; t < 4 * DayClock.TicksPerHour; t++)
            {
                worker.VisualState = AgentVisualState.Harvesting;
                worker.Tick(SimulationClock.FixedTickDeltaTime, day);
            }
            Assert(Math.Abs(worker.Life.Weariness - 4f) < 0.05f, $"Four game hours at a harvest: weariness 4 ({worker.Life.Weariness:F2}).");

            var idler = new AgentState(EntityId.Generate()) { Hunger = 0f };
            for (int t = 0; t < 4 * DayClock.TicksPerHour; t++) idler.Tick(SimulationClock.FixedTickDeltaTime, day);
            Assert(idler.Life.Weariness == 0f, "Four game hours standing: none.");
        }

        private void ValidateTheWindfall()
        {
            int purse = EconomyTuning.Default.ComfortablePurse;
            var crawler = new AgentState(EntityId.Generate());
            Assert(!crawler.NoteWindfall((uint)(purse / 2 - 1), 500u, purse) && crawler.Life.LastWindfall == 0u, $"A payment under half the comfortable purse ({purse / 2 - 1}g) is no windfall.");
            Assert(crawler.NoteWindfall((uint)(purse / 2), 900u, purse) && crawler.Life.LastWindfall == 900u, $"Half of it ({purse / 2}g) is.");
            bool thought = false;
            for (int i = 0; i < CrawlerLife.ThoughtCapacity; i++) if (crawler.Life.ThoughtAt(i).Thought == (ushort)Thought.EarnedARest) thought = true;
            Assert(thought, "It earned a rest: the thought is held.");
            var row = ThoughtTable.Get(Thought.EarnedARest);
            Assert(row.Value == 4 && row.Hours == 24, "Earned a rest: +4 for 24 hours.");
            Assert(!crawler.NoteWindfall(1000u, 1000u, 0), "No comfortable purse, no windfall.");
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("[WearinessValidator] FAILED: " + message);
        }
    }
}
