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
        private static LocalSimulation Outpost()
        {
            var sim = LocalSimulation.Create(SimulationSettings.DefaultSeed);
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

        public static TheoryData<ulong> GateSeeds => new TheoryData<ulong>(OutpostWatch.GateSeeds);

        /// <summary>
        /// The day holds on every gate seed (Epic 10 G0, finding F2), not only the default one: nobody starves, every
        /// crawler closes a harvest-to-sell cycle, both bounties are delivered, the ledger balances, nobody stacks.
        /// </summary>
        [Theory]
        [MemberData(nameof(GateSeeds))]
        public void TheOutpostsDayHoldsOnEverySeed(ulong seed)
        {
            using var sim = LocalSimulation.Create(seed);
            sim.Respawn(OutpostSession.SessionId);
            var watch = new OutpostWatch(sim);
            watch.Run(9000);
            watch.Finish();

            var failures = watch.DayFailures();
            Assert.True(failures.Count == 0, $"Seed {seed}'s day failed:\n  " + string.Join("\n  ", failures) + "\n" + watch.Report());
        }

        /// <summary>
        /// Drifters (Epic 10 G1's Definition of Done): on the real outpost, with nothing injected, strangers come in by the
        /// Road Gate within the day, buy at the stall and leave. Their purses enter the ledger as DrifterPurses, what they
        /// take away leaves it as DrifterDepartures, the gold balances every tick and the residents' day still holds.
        /// </summary>
        [Fact]
        public void DriftersComeBuyAndGo()
        {
            using var sim = Outpost();
            var watch = new OutpostWatch(sim);
            watch.Run(9000);
            watch.Finish();
            TestContext.Current.TestOutputHelper?.WriteLine(watch.Report());

            Assert.True(watch.DriftersArrived >= 2, $"Only {watch.DriftersArrived} drifters came in during the day.");
            Assert.True(watch.DriftersLeft >= 2, $"Only {watch.DriftersLeft} drifters left by the Road Gate.");
            Assert.True(watch.DrifterPurchases >= 2, $"The drifters who left bought {watch.DrifterPurchases} rations.");
            var ledger = sim.Simulation.EconomyLedger;
            Assert.True(ledger.GetFaucetTotal("DrifterPurses") > 0, "No DrifterPurses faucet in the ledger.");
            Assert.True(ledger.GetSinkTotal("DrifterDepartures") > 0, "No DrifterDepartures sink in the ledger.");
            Assert.Equal(0, watch.ConservationViolations);
            var failures = watch.DayFailures();
            Assert.True(failures.Count == 0, "The residents' day failed:\n  " + string.Join("\n  ", failures));
        }

        /// <summary>
        /// A save made while drifters are in the outpost loads byte for byte: each drifter comes back with its departure
        /// tick, the next arrival is still due when it was, and the session plays on with the gold balanced (Epic 10 G1).
        /// </summary>
        [Fact]
        public void ASaveWithDriftersLoadsAndTheirScheduleResumes()
        {
            using var sim = Outpost();
            while (sim.Clock.CurrentTick < 4000 && !sim.Host.CrawlerStates.Any(s => s.IsDrifter)) sim.Step(1);
            var drifters = sim.Host.CrawlerStates.Where(s => s.IsDrifter).Select(s => (s.AgentId, s.DepartsAtTick)).ToList();
            Assert.NotEmpty(drifters);
            long next = sim.Host.NextDrifterTick;
            int arrived = sim.Host.DriftersArrived;
            byte[] saved = SaveFile.Encode(sim.Host.Capture());

            sim.Respawn(OutpostSession.SessionId);
            sim.Step(100);
            Assert.True(SaveFile.TryDecode(saved, out var save, out var refusal), refusal);
            Assert.True(sim.Host.TryRestore(save, out refusal), refusal);

            Assert.Equal(saved, SaveFile.Encode(sim.Host.Capture()));
            Assert.Equal(drifters, sim.Host.CrawlerStates.Where(s => s.IsDrifter).Select(s => (s.AgentId, s.DepartsAtTick)).ToList());
            Assert.Equal(next, sim.Host.NextDrifterTick);
            Assert.Equal(arrived, sim.Host.DriftersArrived);

            var watch = new OutpostWatch(sim);
            watch.Run(3500);
            Assert.Equal(0, watch.ConservationViolations);
            Assert.True(watch.DriftersLeft >= 1, "No drifter left after the load.");
            Assert.True(watch.IntentFaults == 0, $"Intent handlers threw: {sim.Simulation.FirstIntentFault}");
        }

        /// <summary>
        /// Danger (Epic 10 G2's Definition of Done): on the real outpost, with nothing injected, a bold crawler goes into a
        /// hazard sector on its own for its glowstone, is hurt, comes out and recovers (in the bunks, sooner with a herb:
        /// herbs are eaten); no cautious crawler sets foot in one; the caravan buys the glowstone at its premium.
        /// </summary>
        [Fact]
        public void TheBoldBraveTheHazardsAndHealAndTheCautiousStayOut()
        {
            using var sim = Outpost();
            var watch = new OutpostWatch(sim);
            watch.Run(9000);
            watch.Finish();
            TestContext.Current.TestOutputHelper?.WriteLine(watch.Report());

            var bold = watch.Crawlers.Where(c => c.State.Personality.RiskTolerance >= Simpiens.Cognition.Vitality.VentureRiskTolerance).ToList();
            var cautious = watch.Crawlers.Where(c => c.State.Personality.RiskTolerance < Simpiens.Cognition.Vitality.VentureRiskTolerance).ToList();
            Assert.NotEmpty(bold);
            Assert.Contains(bold, c => c.State.Activity.HazardTicks > 0 && c.LowestHealth < OutpostWatch.HurtBelow && c.HurtThenRecovered);
            Assert.True(watch.Crawlers.Sum(c => c.State.Activity.HerbsEaten) > 0, "Nobody ate a herb to heal.");
            Assert.Empty(cautious.Where(c => c.State.Activity.HazardTicks > 0).Select(c => c.Name));
            Assert.True(sim.Simulation.EconomyLedger.GetFaucetTotal("CaravanSalvageSales") > 0);
            Assert.Contains(sim.Host.CrawlerStates, s => s.Activity.SalvageSales > 0);
            var failures = watch.DayFailures();
            Assert.True(failures.Count == 0, "The day failed:\n  " + string.Join("\n  ", failures));
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
            Assert.True(watch.IntentFaults == 0, $"Intent handlers threw: {sim.Simulation.FirstIntentFault}");
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
                Assert.True(watch.IntentFaults == 0, $"Intent handlers threw: {sim.Simulation.FirstIntentFault}");
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
