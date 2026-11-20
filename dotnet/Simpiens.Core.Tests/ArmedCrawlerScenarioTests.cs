using System.Collections.Generic;
using System.Linq;
using Simpiens.Cognition;
using Simpiens.Cognition.Contracts;
using Simpiens.Core.Session;
using Simpiens.Hosting;
using Simpiens.Narrative;
using Simpiens.Simulation;
using Simpiens.Simulation.Economy;
using Simpiens.Simulation.Items;
using Xunit;

namespace Simpiens.Core.Tests
{
    /// <summary>
    /// K3's DoD, <c>AnArmedCrawlerFightsBack</c> (Epic 10 Part 3), live and headless on a gate seed with nothing injected: the
    /// scripted workshop player builds a forge and orders iron blades, through commands, as a player would. A bold resident
    /// buys a blade (over the caravan's cudgel, traded in) with its own gold, braves a known lair it expects to clear (D21),
    /// meets the creatures there, fights back and slays one, within four game days. The days hold, the ledger balances, no
    /// intent handler throws. Gate seed 18: K3.3 measured kills on 7 of the 23 gate seeds in four days, this one's first at
    /// t14382; with skills on 5, and with a lair judged by the crawler's skill (Epic 10 Part 3, K9.8b) on 6, with no death and
    /// the days held on all 23 (<see cref="MeasureFightsBackOnTheGateSeeds"/>), this one's first at t19597.
    /// </summary>
    public class ArmedCrawlerScenarioTests
    {
        private const ulong Seed = 18;
        private const long Horizon = 4 * DayClock.TicksPerDay;

        [Fact]
        public void AnArmedCrawlerFightsBack()
        {
            using var sim = LocalSimulation.Create(Seed);
            sim.Respawn(OutpostSession.SessionId);
            new WorkshopPlayer(sim, StationKind.Forge, ItemType.IronBlade);
            var watch = new OutpostWatch(sim);
            var fought = new HashSet<EntityId>();
            while (sim.Clock.CurrentTick < Horizon && watch.Kills == 0)
            {
                watch.Run(1);
                foreach (var crawler in sim.Host.CrawlerStates) if (crawler.CurrentIntent is FightIntent) fought.Add(crawler.AgentId);
            }
            watch.Finish();
            var told = new List<ChronicleEvent>();
            sim.Chronicle.GetAllRecentEvents(told, 256);
            var kill = told.FirstOrDefault(e => e.EventType == ChronicleEventType.CreatureSlain);
            var slayer = sim.Host.CrawlerStates.FirstOrDefault(c => c.AgentId == kill.ActorAId);
            int index = slayer != null ? sim.Host.CrawlerStates.ToList().IndexOf(slayer) : -1;
            TestContext.Current.TestOutputHelper?.WriteLine($"slain at t{sim.Clock.CurrentTick} by {(index >= 0 ? sim.Host.CrawlerNames[index] : "nobody")}; {watch.CreatureSummary()}\n{watch.Report()}");

            Assert.True(watch.Kills > 0 && slayer != null, $"No creature was slain within {Horizon} ticks.");
            Assert.False(slayer.IsDrifter, "A resident slew it.");
            Assert.True(slayer.Personality.RiskTolerancePercent >= Vitality.VentureRiskTolerancePercent, "A bold one.");
            Assert.Equal(ItemType.IronBlade, slayer.Inventory.GetEquipped(EquipmentSlot.Weapon).Type);
            Assert.True(slayer.Activity.GearPurchases > 0, "It bought its own arms.");
            Assert.Contains(slayer.AgentId, fought);
            Assert.False(slayer.IsDowned);
            Assert.True(slayer.Traits.Count(Simpiens.Simulation.Skills.Deed.CreatureSlain) >= 1, "The kill is the slayer's deed (Epic 11, S0.5).");
            Assert.Equal(watch.Kills, watch.DeedTotal(Simpiens.Simulation.Skills.Deed.CreatureSlain));
            Assert.Equal(0, watch.ConservationViolations);
            Assert.Equal(0, watch.IntentFaults);
            var failures = watch.DayFailures();
            Assert.True(failures.Count == 0, "The days failed:\n  " + string.Join("\n  ", failures));
        }

        /// <summary>
        /// The measurement behind the seed (explicit, a minute or two): every gate seed's four days with the forge making
        /// blades, eight at a time: the first kill and its slayer, the residents who braved a known lair, the deaths and the
        /// days' checks.
        /// </summary>
        [Fact(Explicit = true)]
        public void MeasureFightsBackOnTheGateSeeds()
        {
            var seeds = OutpostWatch.GateSeeds;
            var lines = new string[seeds.Length];
            int killed = 0, held = 0, deaths = 0;
            var gate = new object();
            System.Threading.Tasks.Parallel.For(0, seeds.Length, new System.Threading.Tasks.ParallelOptions { MaxDegreeOfParallelism = 8 }, i =>
            {
                using var sim = LocalSimulation.Create(seeds[i]);
                sim.Respawn(OutpostSession.SessionId);
                new WorkshopPlayer(sim, StationKind.Forge, ItemType.IronBlade);
                var watch = new OutpostWatch(sim);
                var braved = new HashSet<EntityId>();
                long firstKill = -1;
                while (sim.Clock.CurrentTick < Horizon)
                {
                    watch.Run(1);
                    if (firstKill < 0 && watch.Kills > 0) firstKill = sim.Clock.CurrentTick;
                    var lairs = sim.Simulation.Creatures.Lairs;
                    foreach (var crawler in sim.Host.CrawlerStates)
                    {
                        if (crawler.IsDrifter || braved.Contains(crawler.AgentId)) continue;
                        for (int l = 0; l < lairs.Count; l++)
                        {
                            if (Simpiens.Simulation.Creatures.LairReach.Holds(lairs[l].Definition, crawler.Position)
                                && Vitality.BravesLair(lairs[l].Definition, sim.Simulation.Creatures.IsBossAlive(l), crawler.Skills, crawler.Inventory, crawler.Traits, crawler.Health, crawler.Personality.RiskTolerancePercent, crawler.IsDowned))
                            {
                                braved.Add(crawler.AgentId);
                                break;
                            }
                        }
                    }
                }
                watch.Finish();
                var failures = watch.DayFailures();
                lines[i] = $"seed {seeds[i]}: first kill {(firstKill >= 0 ? $"t{firstKill}" : "never")}, kills {watch.Kills}, braved by {braved.Count}, deaths {watch.Deaths}; days {(failures.Count == 0 ? "hold" : "FAIL: " + string.Join("; ", failures))}";
                lock (gate)
                {
                    if (firstKill >= 0) killed++;
                    if (failures.Count == 0) held++;
                    deaths += watch.Deaths;
                }
            });
            foreach (var line in lines) TestContext.Current.TestOutputHelper?.WriteLine(line);
            TestContext.Current.TestOutputHelper?.WriteLine($"in four days, of {seeds.Length} seeds: a kill on {killed}, the days held on {held}, {deaths} deaths in all");
        }
    }
}
