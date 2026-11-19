using System.Collections.Generic;
using System.Linq;
using Simpiens.Cognition;
using Simpiens.Core.Session;
using Simpiens.Hosting;
using Simpiens.Narrative;
using Simpiens.Simulation;
using Simpiens.Simulation.Creatures;
using Simpiens.Simulation.Economy;
using Simpiens.Simulation.Items;
using Xunit;

namespace Simpiens.Core.Tests
{
    /// <summary>
    /// K6's DoD, <c>AHuntBountyClearsALair</c> (Epic 10 Part 3), live and headless with nothing injected: the scripted workshop
    /// player makes blades, and the scripted hunt player posts a hunt on the first charted lair once a bold resident is armed,
    /// through commands, as a player would. Bold, armed residents join it, empty the lair and are paid
    /// by the damage each dealt. The days hold, the ledger balances, no intent handler throws.
    /// </summary>
    public class HuntScenarioTests
    {
        private sealed class Run
        {
            public long PostedAt = -1, JoinedAt = -1, PaidAt = -1;
            public int Hunters, Paid;
            public OutpostWatch Watch;
            public override string ToString() => $"posted {(PostedAt >= 0 ? "t" + PostedAt : "never")}, joined {(JoinedAt >= 0 ? "t" + JoinedAt : "never")}, most hunters {Hunters}, paid {(PaidAt >= 0 ? $"t{PaidAt} to {Paid}" : "never")}";
        }

        private static Run Play(ulong seed, long horizon, bool untilPaid)
        {
            using var sim = LocalSimulation.Create(seed);
            sim.Respawn(OutpostSession.SessionId);
            new WorkshopPlayer(sim, StationKind.Forge, ItemType.IronBlade);
            var hunter = new HuntPlayer(sim, CreatureKind.None); // the first charted lair
            var watch = new OutpostWatch(sim);
            var run = new Run { Watch = watch };
            var told = new List<ChronicleEvent>(256);
            ushort huntId = 0;
            while (sim.Clock.CurrentTick < horizon && (!untilPaid || run.PaidAt < 0))
            {
                watch.Run(1);
                if (run.PostedAt < 0 && hunter.FirstPostTick >= 0) run.PostedAt = hunter.FirstPostTick;
                var hunts = sim.Simulation.Hunts;
                if (hunts.Count > 0)
                {
                    huntId = hunts[0].BountyId;
                    if (hunts[0].Count > 0 && run.JoinedAt < 0) run.JoinedAt = sim.Clock.CurrentTick;
                    if (hunts[0].Count > run.Hunters) run.Hunters = hunts[0].Count;
                }
                if (run.PaidAt < 0 && huntId != 0 && sim.Simulation.Hunts.Count == 0)
                {
                    told.Clear();
                    sim.Chronicle.GetAllRecentEvents(told, 256);
                    int paid = told.Count(e => e.EventType == ChronicleEventType.BountyFulfilled && e.SimulationTick == sim.Clock.CurrentTick);
                    if (paid > 0)
                    {
                        run.PaidAt = sim.Clock.CurrentTick;
                        run.Paid = paid;
                    }
                }
            }
            watch.Finish();
            return run;
        }

        /// <summary>
        /// Gate seed 3: K6.4 measured a hunt paid on 21 of the 23 gate seeds in eight days; with skills (K9) on 20, and on 18,
        /// K6's seed, by one hunter alone. Since a lair is judged by the crawler's skill (Epic 10 Part 3, K9.8b) it is still paid
        /// on 20, to two or more on 7 (8 before): on seed 1, K9's seed, a skilled hunter now clears it alone at t10450. On this
        /// one the hunt is posted at t13016 and paid at t17755 to three hunters.
        /// Gate seed 8 since passions and practice by challenge (Epic 11, S1.5): a hunt paid on 19 of 23, to two or more on 5
        /// (5, 8, 13, 19, 20); seed 3 draws a single hunter now. On this one the hunt is posted at t12938 and paid at t17557 to two.
        /// </summary>
        private const ulong Seed = 8;

        [Fact]
        public void AHuntBountyClearsALair()
        {
            var run = Play(Seed, 4 * DayClock.TicksPerDay, untilPaid: true);
            var watch = run.Watch;
            TestContext.Current.TestOutputHelper?.WriteLine($"{run}\n{watch.CreatureSummary()}\n{watch.Report()}");
            Assert.True(run.PostedAt >= 0, "The player posted a hunt.");
            Assert.True(run.JoinedAt > run.PostedAt, "Residents joined it.");
            Assert.True(run.Hunters >= 2, $"More than one hunted ({run.Hunters}).");
            Assert.True(run.PaidAt > run.JoinedAt, "The lair was emptied and the hunt paid.");
            Assert.True(watch.DeedTotal(Simpiens.Simulation.Skills.Deed.HuntPaid) >= 2, "Each hunter paid has the deed (Epic 11, S0.5).");
            Assert.Equal(0, watch.ConservationViolations);
            Assert.Equal(0, watch.IntentFaults);
            var failures = watch.DayFailures();
            Assert.True(failures.Count == 0, "The days failed:\n  " + string.Join("\n  ", failures));
        }

        /// <summary>The measurement behind the seed: every gate seed's eight days (explicit, a few minutes).</summary>
        [Fact(Explicit = true)]
        public void MeasureHuntsOnTheGateSeeds()
        {
            int paid = 0;
            foreach (var seed in OutpostWatch.GateSeeds)
            {
                var run = Play(seed, 8 * DayClock.TicksPerDay, untilPaid: false);
                if (run.PaidAt >= 0) paid++;
                var failures = run.Watch.DayFailures();
                TestContext.Current.TestOutputHelper?.WriteLine($"seed {seed}: {run}; deaths {run.Watch.Deaths}; days {(failures.Count == 0 ? "hold" : "FAIL: " + string.Join("; ", failures))}");
            }
            TestContext.Current.TestOutputHelper?.WriteLine($"a hunt paid on {paid} of {OutpostWatch.GateSeeds.Length} seeds");
        }
    }
}
