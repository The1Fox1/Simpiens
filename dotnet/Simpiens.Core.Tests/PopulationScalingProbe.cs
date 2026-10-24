using System.Diagnostics;
using Simpiens.Core.Session;
using Simpiens.Hosting;
using Simpiens.Simulation;
using Xunit;

namespace Simpiens.Core.Tests
{
    /// <summary>
    /// Measurement only (Epic MAP Phase 3; Unity's PopulationScalingProbe, Epic CLIENT M2.8): the outpost with 12, 16 and
    /// 24 crawlers, stepped as fast as the machine allows. Prints ticks a second and checks integrity (the ledger
    /// balances). Unity also sampled cognition latency in flight; headless every decision lands on the next tick
    /// (CognitionScheduler), so there is no latency to sample. Run it with
    /// <c>dotnet dotnet/Simpiens.Core.Tests/bin/Release/net10.0/Simpiens.Core.Tests.dll -method "*PopulationScaling*" -explicit only -showLiveOutput</c>.
    /// </summary>
    public class PopulationScalingProbe
    {
        [Theory(Explicit = true)]
        [InlineData(12)]
        [InlineData(16)]
        [InlineData(24)]
        public void MeasurePopulationScaling(int crawlers)
        {
            const long WarmupTicks = 200, MeasuredTicks = 20000;
            using var sim = LocalSimulation.Create(SimulationSettings.DefaultSeed);
            sim.Host.Respawn(new OutpostSession(crawlers));
            sim.Step(WarmupTicks);

            var watch = Stopwatch.StartNew();
            sim.Step(MeasuredTicks);
            watch.Stop();

            uint stallGold = 0, crawlerGold = 0;
            foreach (var stall in sim.Simulation.ActiveStalls) stallGold += stall.TreasuryGold;
            foreach (var state in sim.Host.CrawlerStates) crawlerGold += (uint)state.Inventory.Gold;
            bool balanced = sim.Simulation.EconomyLedger.ValidateConservation(stallGold, crawlerGold, out int discrepancy);
            TestContext.Current.TestOutputHelper?.WriteLine($"{crawlers} crawlers: {MeasuredTicks / watch.Elapsed.TotalSeconds:F0} ticks/s ({MeasuredTicks} ticks in {watch.Elapsed.TotalSeconds:F1} s); ledger balanced {balanced}");
            Assert.True(balanced, $"The ledger is off by {discrepancy}g.");
        }
    }
}
