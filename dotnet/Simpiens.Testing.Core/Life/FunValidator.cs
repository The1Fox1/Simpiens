using System;
using Simpiens.Cognition;
using Simpiens.Cognition.Contracts;
using Simpiens.Cognition.Planning;
using Simpiens.Cognition.Planning.Actions;
using Simpiens.Simulation;
using Simpiens.Simulation.Economy;
using Simpiens.Simulation.Life;
using Simpiens.Testing.Environment;

namespace Simpiens.Testing.Life
{
    /// <summary>
    /// Fun (Epic LIFE-1 plan 2, L2.1, D11): held as boredom in the life record, so a record written before fun (a plan 1 save)
    /// reads entertained; boredom within 0 and 100; its pace on the day's (2 a game hour awake, held asleep, eased 6 an hour
    /// at the hearth while relaxing there); its urgency, 100 less fun; and its place in the record, which keeps its size.
    /// Runs as a test (ValidatorTests).
    /// </summary>
    public class FunValidator
    {
        private static readonly EntityId Ama = new EntityId(1u, 0u, 0u, 1u);

        public void RunAllValidationTests()
        {
            TestLog.Log("[FunValidator] Beginning fun validation...");
            ValidateTheRecord();
            ValidateThePace();
            ValidateTheUrgency();
            TestLog.Log("<color=green>[FunValidator] ALL 3 TESTS PASSED SUCCESSFULLY!</color>");
        }

        private void ValidateTheRecord()
        {
            var life = default(CrawlerLife);
            Assert(life.Fun == 100f && life.Boredom == 0f && life.IsEmpty, "A record written before fun reads entertained: fun 100, and still empty.");
            life.Bore(30f);
            Assert(life.Boredom == 30f && life.Fun == 70f && !life.IsEmpty, $"Bored 30: fun 70 ({life.Fun}).");
            life.Bore(500f);
            Assert(life.Boredom == CrawlerLife.MaxBoredom && life.Fun == 0f, "Boredom holds at 100.");
            life.Bore(-500f);
            Assert(life.Boredom == 0f && life.Fun == 100f, "Entertained, it holds at 0.");
            Assert(System.Runtime.CompilerServices.Unsafe.SizeOf<CrawlerLife>() == CrawlerLife.Size, "The record keeps its 512 bytes.");
            Assert(CrawlerLife.SpareBytes == 244, $"244 spare bytes after the wants' room and boredom ({CrawlerLife.SpareBytes}).");
            life.Bore(12.5f);
            var bytes = CrawlerLife.BytesOf(life);
            Assert(BitConverter.ToSingle(bytes.Slice(264, 4)) == 12.5f, "Boredom sits at byte 264 (D6).");
        }

        private void ValidateThePace()
        {
            var day = DayTuning.Default;
            float hour = DayClock.SecondsPerHour;

            var awake = new AgentState(Ama);
            Tick(awake, hour, day);
            Assert(Math.Abs(awake.Life.Boredom - 2f) < 0.05f, $"Awake, a game hour adds 2 boredom ({awake.Life.Boredom:F2}).");

            var asleep = new AgentState(Ama);
            asleep.ActivePlan.AddAction(new SleepAtBunkAction(), 1f);
            Tick(asleep, hour, day);
            Assert(asleep.Life.Boredom == 0f, $"Asleep in a bunk, boredom holds ({asleep.Life.Boredom}).");

            var fireside = new AgentState(Ama);
            var life = fireside.Life;
            life.Bore(20f);
            fireside.Life = life;
            fireside.ActivePlan.AddAction(new RelaxAtAnchorAction(), 1f);
            Tick(fireside, hour, day);
            Assert(Math.Abs(fireside.Life.Boredom - 14f) < 0.05f, $"Relaxing at the hearth, a game hour eases 6 ({fireside.Life.Boredom:F2}).");
        }

        private void ValidateTheUrgency()
        {
            var life = default(CrawlerLife);
            life.Bore(35f);
            var context = new AgentContext(Ama, default, new AgentNeeds(30f, 80f, 60f), 0f, null, null, 0, life: life);
            Assert(Urgency.Of(Motive.Fun, context) == 35f, $"Fun's urgency is 100 less fun: its boredom ({Urgency.Of(Motive.Fun, context)}).");
            var fresh = new AgentContext(Ama, default, new AgentNeeds(30f, 80f, 60f), 0f, null, null, 0);
            Assert(Urgency.Of(Motive.Fun, fresh) == 0f, "Entertained, fun asks for nothing.");
        }

        /// <summary>Ticks <paramref name="state"/>'s drives for <paramref name="seconds"/> of game time, a tick at a time.</summary>
        private static void Tick(AgentState state, float seconds, in DayTuning day)
        {
            const float dt = 0.1f;
            for (float t = 0f; t < seconds - 0.001f; t += dt) state.Tick(dt, day);
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("[FunValidator] FAILED: " + message);
        }
    }
}
