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
using Simpiens.Simulation.Economy;
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
        /// On the 24-hour day they tire after about 16 hours, so by the next dawn at least 8 distinct crawlers have slept in a
        /// bunk (the gate seeds: 9 to 12, the eighth by t8926 at the latest; Epic 10 Part 2, T5); no bunk ever holds two
        /// sleepers; every sleeper lies on its bunk and is shown asleep; nobody starves; nobody stacks.
        /// </summary>
        [Fact]
        public void TiredCrawlersSleepInTheBunkhouse()
        {
            const long HorizonTicks = OutpostWatch.DayTicks;
            const long SleepersDeadlineTicks = OutpostWatch.DayTicks;
            const int RequiredSleepers = 8;
            const float StarvingHunger = OutpostWatch.StarvingHunger;
            const long StarvingTicks = OutpostWatch.StarvingTicks;

            using var sim = Outpost();
            var manager = (SimulationManager)sim.Simulation;
            var detector = AnomalyDetector.For(sim);
            var sleepers = new HashSet<EntityId>();
            var perBunk = new Dictionary<EntityId, int>();
            var hungrySince = new Dictionary<EntityId, long>();
            var starving = new HashSet<EntityId>();
            long lastSleeperTick = -1;
            int doubleOccupancy = 0, misplaced = 0;
            string firstDouble = null, firstMisplaced = null, firstStarving = null;

            for (long rel = 1; rel <= HorizonTicks; rel++)
            {
                sim.Step(1);
                detector.Observe(sim.Clock.CurrentTick);

                foreach (var pair in manager.BunkSleeps)
                {
                    if (pair.Value > 0 && sleepers.Add(pair.Key) && sleepers.Count == RequiredSleepers) lastSleeperTick = rel;
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

            TestContext.Current.TestOutputHelper?.WriteLine($"{sleepers.Count} sleepers, the {RequiredSleepers}th at t{lastSleeperTick}; {detector.Summary()}");
            Assert.True(sleepers.Count >= RequiredSleepers, $"Only {sleepers.Count} crawlers slept in a bunk within {HorizonTicks} ticks.");
            Assert.True(lastSleeperTick >= 0 && lastSleeperTick <= SleepersDeadlineTicks, $"The {RequiredSleepers}th crawler slept at t{lastSleeperTick}, after t{SleepersDeadlineTicks}.");
            Assert.True(doubleOccupancy == 0, $"A bunk held two sleepers ({doubleOccupancy}; first {firstDouble}).");
            Assert.True(misplaced == 0, $"A sleeper was off its bunk or not shown asleep ({misplaced}; first {firstMisplaced}).");
            Assert.True(starving.Count == 0, $"{starving.Count} crawlers starved (first {firstStarving}).");
            Assert.True(detector.StackingCount == 0, $"Pawns stacked: {detector.First}");
        }

        /// <summary>
        /// Review §3.2: the ration price is not an on/off switch. The player sets only the ration price, through the price
        /// command; personalities and hunger give each crawler its own reservation price, so demand falls off in steps: at
        /// least three distinct buyer counts, buyers at the lowest price, none above every reservation price. Buyers are the
        /// residents (drifters come and go). Each price gets a fresh outpost and a whole game day from its start: on the
        /// 24-hour day purchases cluster at mealtimes, so a price held for a few hours measured the clock, not the price
        /// (Epic 10 Part 2, W5.2).
        /// </summary>
        [Fact]
        public void DemandFallsGraduallyWithThePrice()
        {
            ushort[] prices = { 10, 15, 20, 25, 30, 60 }; // 60g is above any reservation price (at most 45g)
            var buyers = new int[prices.Length];
            var purchases = new int[prices.Length];

            for (int step = 0; step < prices.Length; step++)
            {
                using var sim = Outpost();
                var intervention = sim.Services.GetRequiredService<IPlayerInterventionService>();
                var residents = sim.Host.CrawlerStates.Where(s => !s.IsDrifter).ToList();
                var stall = sim.Simulation.ActiveStalls[0];
                intervention.QueueInfluence(InfluenceEvent.CreateUpdateStallPrices("Player", TestPrices.Command(stall.StallEntityId, prices[step], stall.OreBuyPrice, stall.HerbBuyPrice)));
                sim.Step(DayClock.TicksPerDay);
                buyers[step] = residents.Count(s => s.Activity.RationPurchases > 0);
                purchases[step] = residents.Sum(s => s.Activity.RationPurchases);
            }

            TestContext.Current.TestOutputHelper?.WriteLine($"buyers {string.Join("/", buyers)} at {string.Join("/", prices)}g; purchases {string.Join("/", purchases)}");
            Assert.True(buyers[0] > 0, $"Nobody bought at {prices[0]}g.");
            Assert.True(purchases[prices.Length - 1] == 0, $"Purchases at {prices[prices.Length - 1]}g, above every reservation price.");
            Assert.True(buyers.Distinct().Count() >= 3, $"Demand is binary: buyers {string.Join("/", buyers)} at {string.Join("/", prices)}g.");
        }
    }
}
