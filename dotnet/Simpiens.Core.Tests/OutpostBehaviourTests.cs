using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Simpiens.Cognition;
using Simpiens.Cognition.Contracts;
using Simpiens.Core.Session;
using Simpiens.Entities;
using Simpiens.Hosting;
using Simpiens.Intervention;
using Simpiens.Simulation;
using Xunit;

namespace Simpiens.Core.Tests
{
    /// <summary>
    /// Live scenarios on the real outpost with nothing injected, headless (Epic CLIENT M2.8; Unity's BunkRestScenario
    /// and PriceResponseScenario, with the same bounds in ticks).
    /// </summary>
    public class OutpostBehaviourTests
    {
        private static LocalSimulation Outpost()
        {
            var sim = LocalSimulation.Create(SimulationSettings.DefaultSeed);
            sim.Respawn(OutpostSession.SessionId);
            return sim;
        }

        /// <summary>
        /// The rest anchor (Epic MAP Phase 3): crawlers start rested at the Road Gate and find the Bunkhouse on their own.
        /// Within the bound at least 3 distinct crawlers sleep in a bunk; no bunk ever holds two sleepers; every sleeper
        /// lies on its bunk and is shown asleep; nobody starves; nobody stacks.
        /// </summary>
        [Fact]
        public void TiredCrawlersSleepInTheBunkhouse()
        {
            const long HorizonTicks = 4000;
            const long SleepersDeadlineTicks = 2100; // Unity: the third sleeper at t1375 in 3 of 3 runs, x 1.5
            const int RequiredSleepers = 3;
            const float StarvingHunger = 95f;
            const long StarvingTicks = 300;

            using var sim = Outpost();
            var manager = (SimulationManager)sim.Simulation;
            var detector = AnomalyDetector.For(sim);
            var sleepers = new HashSet<EntityId>();
            var perBunk = new Dictionary<EntityId, int>();
            var hungrySince = new Dictionary<EntityId, long>();
            var starving = new HashSet<EntityId>();
            long thirdSleeperTick = -1;
            int doubleOccupancy = 0, misplaced = 0;
            string firstDouble = null, firstMisplaced = null, firstStarving = null;

            for (long rel = 1; rel <= HorizonTicks; rel++)
            {
                sim.Step(1);
                detector.Observe(sim.Clock.CurrentTick);

                foreach (var pair in manager.BunkSleeps)
                {
                    if (pair.Value > 0 && sleepers.Add(pair.Key) && sleepers.Count == RequiredSleepers) thirdSleeperTick = rel;
                }

                // Single occupancy, checked against the sleepers themselves: every sleep past its first tick holds its bunk
                perBunk.Clear();
                foreach (var pair in ((IIntentExecutionContext)manager).ActiveIntents)
                {
                    if (!(pair.Value.Intent is SleepAtBunkIntent sleep) || pair.Value.ElapsedTime <= 0f) continue;
                    perBunk.TryGetValue(sleep.BunkId, out int n);
                    perBunk[sleep.BunkId] = n + 1;
                    if (n + 1 > 1) { doubleOccupancy++; firstDouble ??= $"t{rel}: bunk {sleep.BunkId}"; }

                    var bunk = sim.Registry.GetEntity(sleep.BunkId);
                    var sleeper = sim.Host.CrawlerStates.FirstOrDefault(s => s.AgentId == pair.Key);
                    if (bunk != null && sleeper != null
                        && (Vec2.Distance(sleeper.Position, bunk.Position) > SimulationManager.BunkSettleDistance || sleeper.VisualState != AgentVisualState.Sleeping))
                    {
                        misplaced++;
                        firstMisplaced ??= $"t{rel}: {sleeper.Position} for bunk {bunk.Position}, shown {sleeper.VisualState}";
                    }
                }

                foreach (var state in sim.Host.CrawlerStates)
                {
                    if (state.Hunger >= StarvingHunger)
                    {
                        if (!hungrySince.TryGetValue(state.AgentId, out long since)) hungrySince[state.AgentId] = since = rel;
                        if (rel - since >= StarvingTicks && starving.Add(state.AgentId)) firstStarving ??= $"t{rel} at {state.Position}";
                    }
                    else hungrySince.Remove(state.AgentId);
                }
            }

            TestContext.Current.TestOutputHelper?.WriteLine($"{sleepers.Count} sleepers, the third at t{thirdSleeperTick}; {detector.Summary()}");
            Assert.True(sleepers.Count >= RequiredSleepers, $"Only {sleepers.Count} crawlers slept in a bunk within {HorizonTicks} ticks.");
            Assert.True(thirdSleeperTick >= 0 && thirdSleeperTick <= SleepersDeadlineTicks, $"The third crawler slept at t{thirdSleeperTick}, after t{SleepersDeadlineTicks}.");
            Assert.True(doubleOccupancy == 0, $"A bunk held two sleepers ({doubleOccupancy}; first {firstDouble}).");
            Assert.True(misplaced == 0, $"A sleeper was off its bunk or not shown asleep ({misplaced}; first {firstMisplaced}).");
            Assert.True(starving.Count == 0, $"{starving.Count} crawlers starved (first {firstStarving}).");
            Assert.True(detector.StackingCount == 0, $"Pawns stacked: {detector.First}");
        }

        /// <summary>
        /// Review §3.2: the ration price is not an on/off switch. The player changes only the ration price, through the
        /// price command, every 2.5 sim-minutes; personalities and hunger give each crawler its own reservation price, so
        /// demand falls off in steps: at least three distinct buyer counts, buyers at the lowest price, none above every
        /// reservation price. Buyers are the residents (drifters come and go).
        /// </summary>
        [Fact]
        public void DemandFallsGraduallyWithThePrice()
        {
            ushort[] prices = { 10, 15, 20, 25, 30, 60 }; // 60g is above any reservation price (at most 45g)
            const long StepTicks = 1500;

            using var sim = Outpost();
            var intervention = sim.Services.GetRequiredService<IPlayerInterventionService>();
            var ledger = sim.Simulation.EconomyLedger;
            var residents = sim.Host.CrawlerStates.Where(s => !s.IsDrifter).ToList();
            var buyers = new int[prices.Length];
            var purchases = new uint[prices.Length];

            for (int step = 0; step < prices.Length; step++)
            {
                var stall = sim.Simulation.ActiveStalls[0];
                intervention.QueueInfluence(InfluenceEvent.CreateUpdateStallPrices("Player", new UpdateStallPricesCommand(stall.StallEntityId, prices[step], stall.OreBuyPrice, stall.HerbBuyPrice)));
                uint startPurchases = ledger.Flows.Purchases;
                var lastGold = residents.Select(s => s.Inventory.Gold).ToArray();
                var bought = new bool[residents.Count];
                for (long t = 0; t < StepTicks; t++)
                {
                    sim.Step(1);
                    // Crawlers spend gold only on rations, so a drop in a crawler's gold is a purchase
                    for (int c = 0; c < residents.Count; c++)
                    {
                        int gold = residents[c].Inventory.Gold;
                        if (gold < lastGold[c]) bought[c] = true;
                        lastGold[c] = gold;
                    }
                }
                buyers[step] = bought.Count(b => b);
                purchases[step] = ledger.Flows.Purchases - startPurchases;
            }

            TestContext.Current.TestOutputHelper?.WriteLine($"buyers {string.Join("/", buyers)} at {string.Join("/", prices)}g; purchases {string.Join("/", purchases)}");
            Assert.True(buyers[0] > 0, $"Nobody bought at {prices[0]}g.");
            Assert.True(purchases[prices.Length - 1] == 0, $"Purchases at {prices[prices.Length - 1]}g, above every reservation price.");
            Assert.True(buyers.Distinct().Count() >= 3, $"Demand is binary: buyers {string.Join("/", buyers)} at {string.Join("/", prices)}g.");
        }
    }
}
