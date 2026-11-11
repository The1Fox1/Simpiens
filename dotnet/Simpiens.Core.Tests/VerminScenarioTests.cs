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
    /// or is struck by one;</item>
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
                int strays = 0;
                foreach (ulong seed in OutpostWatch.GateSeeds)
                {
                    var watch = watches[seed];
                    TestContext.Current.TestOutputHelper?.WriteLine($"seed {seed}: {watch.CreatureSummary()}");
                    var failures = watch.DayFailures();
                    Assert.True(failures.Count == 0, $"Seed {seed}'s day failed:\n  " + string.Join("\n  ", failures));
                    Assert.True(watch.LairChartedAt[hollow] >= 0, $"Seed {seed}: nobody charted the beetle hollow.");
                    Assert.Equal(0, watch.EntriesIntoKnownLairs);
                    Assert.True(watch.Flights >= watch.Struck.Count, $"Seed {seed}: {watch.Struck.Count} crawlers struck, {watch.Flights} flights.");
                    Assert.Equal(0, watch.DownedByCreatures);
                    if (watch.Hits > 0 && watch.Flights > 0) strays++;
                }
                Assert.True(strays >= StraysAtLeast, $"A stray was hurt and fled on {strays} of {OutpostWatch.GateSeeds.Length} gate seeds; at least {StraysAtLeast} expected.");
            }
            finally
            {
                foreach (var sim in sims) sim.Dispose();
            }
        }
    }
}
