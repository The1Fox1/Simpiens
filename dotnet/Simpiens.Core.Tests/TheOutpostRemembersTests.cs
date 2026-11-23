using System.Collections.Generic;
using System.Linq;
using Simpiens.Core.Session;
using Simpiens.Hosting;
using Simpiens.Narrative;
using Simpiens.Simulation;
using Simpiens.Simulation.Economy;
using Simpiens.Simulation.Items;
using Simpiens.Simulation.Skills;
using Xunit;

namespace Simpiens.Core.Tests
{
    /// <summary>
    /// S6's Definition of Done (Epic 11, S6.8): the outpost remembers, on the live outpost with nothing injected. The forge's
    /// player makes blades; the guild's player recruits four and orders clears, armed from the stall, under the Steady rule.
    /// <list type="bullet">
    /// <item><b>An epithet:</b> a crawler earns one on its own from its deeds, as the chronicle tells it.</item>
    /// <item><b>A scar:</b> a member downed in a lair is carried out and stands again, bearing what downed it, as the chronicle
    /// tells it.</item>
    /// </list>
    /// The soak's hunting players never put anyone down (F13: their parties carry draughts); the guild's clears without them do.
    /// </summary>
    public class TheOutpostRemembersTests
    {
        private sealed class Run
        {
            public long EpithetAt = -1, ScarAt = -1;
            public string Epithet = "-", Scar = "-";
            public EntityId Named, Scarred;
            public int Epithets, Scars, Downed;
            public OutpostWatch Watch;

            public bool All => EpithetAt >= 0 && ScarAt >= 0;

            public override string ToString() =>
                $"an epithet: {(EpithetAt >= 0 ? $"t{EpithetAt}, {Epithet}" : "no")} ({Epithets} in all); a scar: {(ScarAt >= 0 ? $"t{ScarAt}, {Scar}" : "no")} ({Scars} in all, {Downed} downings)";
        }

        private static Run Play(ulong seed, long horizon, bool untilAll)
        {
            using var sim = LocalSimulation.Create(seed);
            sim.Respawn(OutpostSession.SessionId);
            new WorkshopPlayer(sim, StationKind.Forge, ItemType.IronBlade, batch: 4);
            var guild = new GuildPlayer(sim, targetSize: 4);
            guild.SendClear();
            var watch = new OutpostWatch(sim);
            var run = new Run { Watch = watch };
            var formatter = (INarrativeFormatter)sim.Services.GetService(typeof(INarrativeFormatter));
            var told = new List<ChronicleEvent>(64);
            long seen = -1;
            while (sim.Clock.CurrentTick < horizon && !(untilAll && run.All))
            {
                watch.Run(1);
                long now = sim.Clock.CurrentTick;
                told.Clear();
                sim.Chronicle.GetAllRecentEvents(told, 64);
                foreach (var e in told)
                {
                    if (e.SimulationTick <= seen) continue;
                    if (e.EventType == ChronicleEventType.CrawlerDowned) run.Downed++;
                    if (e.EventType == ChronicleEventType.EpithetEarned)
                    {
                        run.Epithets++;
                        if (run.EpithetAt < 0)
                        {
                            run.EpithetAt = e.SimulationTick;
                            run.Named = e.ActorAId;
                            run.Epithet = formatter.FormatEvent(e);
                        }
                    }
                    if (e.EventType == ChronicleEventType.ScarTaken)
                    {
                        run.Scars++;
                        if (run.ScarAt < 0)
                        {
                            run.ScarAt = e.SimulationTick;
                            run.Scarred = e.ActorAId;
                            run.Scar = formatter.FormatEvent(e);
                        }
                    }
                }
                seen = now - 1;
            }
            watch.Finish();
            return run;
        }

        /// <summary>The horizon: ten game days.</summary>
        private const long Horizon = 10 * DayClock.TicksPerDay;

        /// <summary>
        /// Gate seed 3 (S6.8, <see cref="MeasureTheOutpostRemembersOnTheGateSeeds"/> on S6's build): over ten days an epithet is
        /// earned on 19 of the 23 gate seeds and a scar borne on 12, both with the days holding on 12. On this one Kwame, downed
        /// by a beetle, is carried out and stands at t19108 bearing its claw, and Ellis, who carried the fallen out twice, is the
        /// Steadfast at t19116.
        /// </summary>
        private const ulong Seed = 3;

        [Fact]
        public void TheOutpostRemembers()
        {
            var run = Play(Seed, Horizon, untilAll: true);
            var watch = run.Watch;
            TestContext.Current.TestOutputHelper?.WriteLine($"{run}\n{watch.CreatureSummary()}\n{watch.Report()}");
            Assert.True(run.EpithetAt >= 0, "A crawler earned an epithet on its own, as the chronicle told it.");
            Assert.True(run.ScarAt >= 0, "A downed crawler stood again bearing a scar, as the chronicle told it.");
            Assert.Contains(", for ", run.Epithet); // the epithet and the deeds that earned it, voiced or plain
            Assert.Contains(" lived, and bears ", run.Scar);
            Assert.Equal(0, watch.ConservationViolations);
            Assert.Equal(0, watch.IntentFaults);
            var failures = watch.DayFailures();
            Assert.True(failures.Count == 0, "The days failed:\n  " + string.Join("\n  ", failures));
        }

        /// <summary>The measurement behind the seed: every gate seed's ten days, eight at a time (explicit, a few minutes).</summary>
        [Fact(Explicit = true)]
        public void MeasureTheOutpostRemembersOnTheGateSeeds()
        {
            var seeds = System.Environment.GetEnvironmentVariable("SIMPIENS_SEEDS") is string list && list.Length > 0
                ? list.Split(',').Select(ulong.Parse).ToArray() : OutpostWatch.GateSeeds;
            var lines = new string[seeds.Length];
            int epithet = 0, scar = 0, all = 0;
            System.Threading.Tasks.Parallel.For(0, seeds.Length, new System.Threading.Tasks.ParallelOptions { MaxDegreeOfParallelism = 8 }, i =>
            {
                var run = Play(seeds[i], Horizon, untilAll: false);
                var failures = run.Watch.DayFailures();
                lines[i] = $"seed {seeds[i]}: {run}; days {(failures.Count == 0 ? "hold" : "FAIL: " + string.Join("; ", failures))}";
                if (run.EpithetAt >= 0) System.Threading.Interlocked.Increment(ref epithet);
                if (run.ScarAt >= 0) System.Threading.Interlocked.Increment(ref scar);
                if (run.All && failures.Count == 0) System.Threading.Interlocked.Increment(ref all);
            });
            foreach (var line in lines) TestContext.Current.TestOutputHelper?.WriteLine(line);
            TestContext.Current.TestOutputHelper?.WriteLine($"of {seeds.Length} seeds in {Horizon / DayClock.TicksPerDay} days: an epithet on {epithet}, a scar on {scar}, both with the days holding on {all}");
        }
    }
}
