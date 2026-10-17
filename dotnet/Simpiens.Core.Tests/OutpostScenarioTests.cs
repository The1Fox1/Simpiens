using System.Collections.Generic;
using System.IO;
using System.Linq;
using Simpiens.Core.Session;
using Simpiens.Hosting;
using Simpiens.Simulation;
using Simpiens.Simulation.Economy;
using Simpiens.Simulation.Persistence;
using Simpiens.Testing.Environment;
using Xunit;

namespace Simpiens.Core.Tests
{
    /// <summary>
    /// The Definition-of-Done scenarios (ROADMAP §1B) run headless on the real outpost session, with nothing injected:
    /// the same assertions as Unity's OutpostLifecycleScenario, OutpostSupplyLoopScenario and SaveLoadScenario, except
    /// the visual anomaly detector, which needs the views. Ticks are stepped as fast as the machine allows.
    /// </summary>
    public class OutpostScenarioTests
    {
        private static HeadlessSimulation Outpost()
        {
            var sim = HeadlessSimulation.Create(SimulationSettings.DefaultSeed);
            sim.Respawn(OutpostSession.SessionId);
            return sim;
        }

        /// <summary>The outpost's whole day (Epic MAP's DoD): 9,000 ticks.</summary>
        [Fact]
        public void TheOutpostsDayEmerges()
        {
            using var sim = Outpost();
            var watch = new OutpostWatch(sim);
            watch.Run(9000);
            watch.Finish();
            TestContext.Current.TestOutputHelper?.WriteLine(watch.Report());

            var failures = watch.DayFailures();
            Assert.True(failures.Count == 0, "Outpost lifecycle failures:\n  " + string.Join("\n  ", failures));
        }

        /// <summary>The Milestone 1 supply loop: sale, scout, gather, restock and regrowth by their deadlines.</summary>
        [Fact]
        public void TheSupplyLoopEmergesWithoutInjection()
        {
            using var sim = Outpost();
            var watch = new OutpostWatch(sim);
            watch.Run(6000);
            TestContext.Current.TestOutputHelper?.WriteLine(watch.Report());

            Assert.True(watch.GatherTick >= 0 && watch.GatherTick <= 900, $"Gather bounty not fulfilled by t900 (t{watch.GatherTick}).");
            Assert.True(watch.GatherClaimantTrust > 0, "The Gather bounty claimant's trust toward the player did not rise.");
            Assert.True(watch.SaleTick >= 0 && watch.SaleTick <= 400, $"No salvage sold at the stall by t400 (t{watch.SaleTick}).");
            Assert.True(watch.ScoutTick >= 0 && watch.ScoutTick <= 550, $"Scout bounty not fulfilled by t550 (t{watch.ScoutTick}).");
            Assert.True(watch.ScoutCharted, "Scout bounty completed but the Deep Seam was not charted.");
            Assert.Equal(0, watch.FogLeaks);
            Assert.True(watch.GatherTick > watch.ScoutTick, "The Deep Seam Gather bounty was fulfilled before the sector was charted.");
            Assert.True(watch.RestockTick >= 0 && watch.RestockTick <= 6000, $"No caravan restock by t6000 (t{watch.RestockTick}).");
            Assert.True(watch.RegrowthTick >= 0 && watch.RegrowthTick <= 6000, $"No node regrowth by t6000 (t{watch.RegrowthTick}).");
            Assert.Equal(0, watch.ConservationViolations);
        }

        /// <summary>
        /// Milestone 1's save/load DoD: save while a crawler holds the Gather bounty, load into a fresh session (byte for
        /// byte, same world hash), then play on: the ledger balances, nobody starves, every crawler eats, a harvest-to-sell
        /// cycle completes and the claimed bounty is delivered and paid from escrow.
        /// </summary>
        [Fact]
        public void TheLoadedOutpostPlaysOn()
        {
            string directory = Path.Combine(Path.GetTempPath(), "Simpiens", "SaveLoadTests");
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
            try
            {
                using var sim = Outpost();
                var slot = new SaveGameService(sim.Host, directory);

                while (sim.Clock.CurrentTick < 2000 && !(sim.Simulation.TryGetBounty(OutpostSession.SeedGatherBountyId, out var g) && g.Status == BountyStatus.Claimed)) sim.Step(1);
                long saveTick = sim.Clock.CurrentTick;
                Assert.True(sim.Simulation.TryGetBounty(OutpostSession.SeedGatherBountyId, out var gatherAtSave) && gatherAtSave.Status == BountyStatus.Claimed,
                    "No crawler claimed the Gather bounty by t2000: nothing was in progress at the save.");
                var savedHash = WorldStateHash.Capture(saveTick, sim.Host, sim.Simulation, sim.Sectors, includeWork: false);
                var saved = slot.QuickSave();
                Assert.True(saved.Succeeded, saved.Message);

                sim.Respawn(OutpostSession.SessionId);
                sim.Step(100);
                var loaded = slot.QuickLoad();
                Assert.True(loaded.Succeeded, loaded.Message);
                Assert.Equal(saveTick, sim.Clock.CurrentTick);
                Assert.Equal(File.ReadAllBytes(slot.QuickSavePath), SaveFile.Encode(sim.Host.Capture()));
                var loadedHash = WorldStateHash.Capture(sim.Clock.CurrentTick, sim.Host, sim.Simulation, sim.Sectors, includeWork: false);
                Assert.True(savedHash.Total == loadedHash.Total, $"The world hash differs after the load: {savedHash.DescribeDifference(loadedHash)}");

                long loadTick = sim.Clock.CurrentTick;
                var watch = new OutpostWatch(sim);
                watch.Run(3000);
                TestContext.Current.TestOutputHelper?.WriteLine($"saved and loaded at t{saveTick}; " + watch.Report());

                Assert.Equal(0, watch.ConservationViolations);
                Assert.Empty(watch.Crawlers.Where(c => c.Starved).Select(c => c.Name));
                Assert.Empty(watch.Crawlers.Where(c => c.State.Activity.Meals <= c.MealsAtStart).Select(c => c.Name));
                Assert.True(watch.Crawlers.Sum(c => c.State.Activity.Cycles - c.CyclesAtStart) > 0, "No harvest-to-sell cycle completed after the load.");
                Assert.True(watch.GatherTick > 0, "The Gather bounty claimed before the save was not delivered after the load.");
                Assert.True(watch.GatherEscrowReleased, "The delivered bounty's reward was not paid from escrow.");
            }
            finally
            {
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
            }
        }
    }
}
