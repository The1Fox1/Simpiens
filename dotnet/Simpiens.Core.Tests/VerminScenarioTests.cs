using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using Simpiens.Core.Session;
using Simpiens.Core.Session.Layouts;
using Simpiens.Hosting;
using Xunit;

namespace Simpiens.Core.Tests
{
    /// <summary>
    /// K2's DoD, <c>VerminDriveTheLoneAway</c> (Epic 10 Part 3), live and headless on the 23 gate seeds' day with nothing
    /// injected and no player:
    /// <list type="bullet">
    /// <item>the beetle hollow in the Deep Seam is charted once the seeded scout opens the sector: a crawler sights a beetle,
    /// or is struck by one; on at least 21 of the seeds (every seed until Epic 11's techniques: since S4.7 seed 3's crawlers
    /// keep away from the hollow all day, though its scout opens the Deep Seam at t659 as before; finding F10);</item>
    /// <item>a crawler who strays into its reach (on its way to seam.1 when the hollow was charted) is hurt and flees, and
    /// gets away on its feet: on at least 5 of the seeds (K2.3d measured 8, and 53 of seeds 1-120);</item>
    /// <item>every crawler struck flees;</item>
    /// <item>nobody goes alone into a known lair, and the day's checks hold (OutpostWatch.DayFailures, which judge the
    /// entries too).</item>
    /// </list>
    /// </summary>
    public class VerminScenarioTests
    {
        private const int StraysAtLeast = 5;

        /// <summary>The hollow is charted within the day on this many of the 23 gate seeds at least (22 at S4.7, F10).</summary>
        private const int ChartedAtLeast = 21;

        [Fact]
        public void VerminDriveTheLoneAway()
        {
            var watches = new ConcurrentDictionary<ulong, OutpostWatch>();
            var sims = new ConcurrentBag<LocalSimulation>();
            Parallel.ForEach(OutpostWatch.GateSeeds, seed =>
            {
                var sim = LocalSimulation.Create(seed);
                sims.Add(sim);
                sim.Respawn(OutpostSession.SessionId);
                var watch = new OutpostWatch(sim);
                watch.Run(OutpostWatch.DayTicks);
                watch.Finish();
                watches[seed] = watch;
            });
            try
            {
                int hollow = Floor1Layout.Definition.Lairs.ToList().FindIndex(l => l.Key == Floor1Layout.BeetleHollow);
                int strays = 0, charted = 0;
                foreach (ulong seed in OutpostWatch.GateSeeds)
                {
                    var watch = watches[seed];
                    TestContext.Current.TestOutputHelper?.WriteLine($"seed {seed}: {watch.CreatureSummary()}");
                    var failures = watch.DayFailures();
                    Assert.True(failures.Count == 0, $"Seed {seed}'s day failed:\n  " + string.Join("\n  ", failures));
                    if (watch.LairChartedAt[hollow] >= 0) charted++;
                    Assert.Equal(0, watch.EntriesIntoKnownLairs);
                    Assert.True(watch.Flights >= watch.Struck.Count, $"Seed {seed}: {watch.Struck.Count} crawlers struck, {watch.Flights} flights.");
                    Assert.Equal(0, watch.DownedByCreatures);
                    if (watch.Hits > 0 && watch.Flights > 0) strays++;
                }
                Assert.True(charted >= ChartedAtLeast, $"The beetle hollow was charted on {charted} of {OutpostWatch.GateSeeds.Length} gate seeds; at least {ChartedAtLeast} expected.");
                Assert.True(strays >= StraysAtLeast, $"A stray was hurt and fled on {strays} of {OutpostWatch.GateSeeds.Length} gate seeds; at least {StraysAtLeast} expected.");
            }
            finally
            {
                foreach (var sim in sims) sim.Dispose();
            }
        }

        /// <summary>
        /// The measurement behind the scenario (Epic 11, S4.7): each seed's day as the scenario plays it, with when the hollow is
        /// charted, the blows and flights, and whether the day holds. Seeds from <c>SIMPIENS_SEEDS</c> (comma-separated), or
        /// the gate seeds. Explicit, a minute or two.
        /// </summary>
        [Fact(Explicit = true)]
        public void MeasureVerminOnTheGateSeeds()
        {
            var seeds = System.Environment.GetEnvironmentVariable("SIMPIENS_SEEDS") is string list && list.Length > 0
                ? list.Split(',').Select(ulong.Parse).ToArray() : OutpostWatch.GateSeeds;
            int hollow = Floor1Layout.Definition.Lairs.ToList().FindIndex(l => l.Key == Floor1Layout.BeetleHollow);
            var lines = new string[seeds.Length];
            int charted = 0, strays = 0, held = 0;
            Parallel.For(0, seeds.Length, new ParallelOptions { MaxDegreeOfParallelism = 8 }, i =>
            {
                using var sim = LocalSimulation.Create(seeds[i]);
                sim.Respawn(OutpostSession.SessionId);
                var watch = new OutpostWatch(sim);
                watch.Run(OutpostWatch.DayTicks);
                watch.Finish();
                var failures = watch.DayFailures();
                bool ok = failures.Count == 0 && watch.LairChartedAt[hollow] >= 0 && watch.EntriesIntoKnownLairs == 0 && watch.Flights >= watch.Struck.Count && watch.DownedByCreatures == 0;
                if (watch.LairChartedAt[hollow] >= 0) System.Threading.Interlocked.Increment(ref charted);
                if (watch.Hits > 0 && watch.Flights > 0) System.Threading.Interlocked.Increment(ref strays);
                if (ok) System.Threading.Interlocked.Increment(ref held);
                lines[i] = $"seed {seeds[i]}: hollow charted at t{watch.LairChartedAt[hollow]}; {watch.CreatureSummary()}; {(ok ? "holds" : "FAILS: " + string.Join("; ", failures))}";
            });
            foreach (var line in lines) TestContext.Current.TestOutputHelper?.WriteLine(line);
            TestContext.Current.TestOutputHelper?.WriteLine($"of {seeds.Length} seeds: the hollow charted on {charted}, a stray hurt and fled on {strays}, every check held on {held}");
        }
    }
}
