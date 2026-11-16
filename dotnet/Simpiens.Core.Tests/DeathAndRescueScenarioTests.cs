using System.Collections.Generic;
using System.Linq;
using Simpiens.Cognition;
using Simpiens.Core.Session;
using Simpiens.Core.Session.Layouts;
using Simpiens.Hosting;
using Simpiens.Narrative;
using Simpiens.Simulation;
using Simpiens.Simulation.Guild;
using Xunit;

namespace Simpiens.Core.Tests
{
    /// <summary>
    /// K5's live evidence (Epic 10 Part 3), headless with nothing injected.
    /// <para>
    /// <c>TheCarefulLoseFew</c>: on the 23 gate seeds' day, with no player action, deaths stay within the band (D33, a
    /// default for the producer: at most one across the seeds). Measured at K5: none.
    /// </para>
    /// <para>
    /// The measurement behind finding F6 (explicit): the scripted guild player sends four, ungeared and under the Bold rule,
    /// into the Drowned Gallery for eight days, as a careless player would. Nobody is downed there: the expedition's retreat
    /// brings everyone out above 15 health. So <c>AFriendCarriesTheFallenOut</c> and <c>DeathHasItsCost</c>, which need a
    /// downing in danger, wait for K7's Clear order (DeathHasItsCost's own wording sends a party against beetles).
    /// </para>
    /// </summary>
    public class DeathAndRescueScenarioTests
    {
        /// <summary>What a run saw.</summary>
        private sealed class Run
        {
            public int Collapsed, Rescues, Deaths, Newcomers, Dispatches;
            public float LowestHealth = 100f;
            public long FirstRescueAt = -1, FirstDeathAt = -1;
            public EntityId Rescued, Dead;
            public OutpostWatch Watch;
            public override string ToString() => $"dispatches {Dispatches}, lowest health {LowestHealth:F0}, collapsed {Collapsed}, rescues {Rescues}{(FirstRescueAt >= 0 ? $" (first t{FirstRescueAt})" : "")}, deaths {Deaths}{(FirstDeathAt >= 0 ? $" (first t{FirstDeathAt})" : "")}, newcomers {Newcomers}";
        }

        private static Run Play(ulong seed, long horizon, byte sector, System.Func<Run, bool> until = null)
        {
            using var sim = LocalSimulation.Create(seed);
            sim.Respawn(OutpostSession.SessionId);
            var player = new GuildPlayer(sim, targetSize: 4);
            player.SendExpeditions(sector, goal: 12, rule: RetreatRule.Bold, outfit: false);
            var watch = new OutpostWatch(sim);
            var run = new Run { Watch = watch };
            var collapsed = new HashSet<EntityId>();
            var told = new List<ChronicleEvent>(512);
            long seen = 0;
            while (sim.Clock.CurrentTick < horizon && (until == null || !until(run)))
            {
                watch.Run(1);
                foreach (var crawler in sim.Host.CrawlerStates)
                {
                    if (crawler.IsCollapsed && collapsed.Add(crawler.AgentId)) run.Collapsed++;
                    if (crawler.Health < run.LowestHealth) run.LowestHealth = crawler.Health;
                }
                told.Clear();
                sim.Chronicle.GetAllRecentEvents(told, 64);
                foreach (var e in told)
                {
                    if (e.SimulationTick <= seen) continue;
                    if (e.EventType == ChronicleEventType.CrawlerRescued)
                    {
                        run.Rescues++;
                        if (run.FirstRescueAt < 0) { run.FirstRescueAt = e.SimulationTick; run.Rescued = e.ActorBId; }
                    }
                    if (e.EventType == ChronicleEventType.CrawlerDied && run.FirstDeathAt < 0) { run.FirstDeathAt = e.SimulationTick; run.Dead = e.ActorAId; }
                }
                seen = sim.Clock.CurrentTick - 1;
            }
            watch.Finish();
            run.Dispatches = player.Dispatches;
            run.Deaths = watch.Deaths;
            run.Newcomers = sim.Host.NewcomersArrived;
            return run;
        }

        /// <summary>The band (D33, a default for the producer): deaths across the 23 gate seeds' day with no player action.</summary>
        public const int MostDeathsOnTheGateSeeds = 1;

        [Fact]
        public void TheCarefulLoseFew()
        {
            int deaths = 0;
            var who = new List<string>();
            foreach (var seed in OutpostWatch.GateSeeds)
            {
                using var sim = LocalSimulation.Create(seed);
                sim.Respawn(OutpostSession.SessionId);
                var watch = new OutpostWatch(sim);
                watch.Run(OutpostWatch.DayTicks);
                watch.Finish();
                deaths += watch.Deaths;
                if (watch.Deaths > 0) who.Add($"seed {seed}: {watch.FirstDeath}");
                Assert.Equal(0, watch.ConservationViolations);
            }
            TestContext.Current.TestOutputHelper?.WriteLine($"{deaths} deaths on the {OutpostWatch.GateSeeds.Length} gate seeds' day{(who.Count > 0 ? ": " + string.Join("; ", who) : "")}");
            Assert.True(deaths <= MostDeathsOnTheGateSeeds, $"{deaths} deaths: " + string.Join("; ", who));
        }

        /// <summary>The measurement behind F6: every gate seed's eight days with an ungeared bold party in the gallery (explicit, a few minutes).</summary>
        [Fact(Explicit = true)]
        public void MeasureDeathAndRescueOnTheGateSeeds()
        {
            int rescued = 0, died = 0;
            foreach (var seed in OutpostWatch.GateSeeds)
            {
                var run = Play(seed, 8 * DayClock.TicksPerDay, Floor1Layout.DrownedGallerySectorId);
                if (run.Rescues > 0) rescued++;
                if (run.Deaths > 0) died++;
                var failures = run.Watch.DayFailures();
                TestContext.Current.TestOutputHelper?.WriteLine($"seed {seed}: {run}; {run.Watch.CreatureSummary()}; days {(failures.Count == 0 ? "hold" : "FAIL: " + string.Join("; ", failures))}");
            }
            TestContext.Current.TestOutputHelper?.WriteLine($"a rescue on {rescued}, a death on {died} of {OutpostWatch.GateSeeds.Length} seeds");
        }
    }
}
