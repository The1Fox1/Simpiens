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
    /// t14382.
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
            Assert.Equal(0, watch.ConservationViolations);
            Assert.Equal(0, watch.IntentFaults);
            var failures = watch.DayFailures();
            Assert.True(failures.Count == 0, "The days failed:\n  " + string.Join("\n  ", failures));
        }
    }
}
