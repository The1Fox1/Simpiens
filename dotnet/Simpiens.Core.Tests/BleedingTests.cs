using System.Linq;
using Simpiens.Cognition;
using Simpiens.Core.Session;
using Simpiens.Core.Session.Layouts;
using Simpiens.Hosting;
using Simpiens.Simulation;
using Simpiens.Simulation.Economy;
using Xunit;

namespace Simpiens.Core.Tests
{
    /// <summary>
    /// Collapse and bleeding out (Epic 10 Part 3, K5.1, Q3), on the real outpost: mechanism tests, the crawler brought low by
    /// hand. Downed in a hazard, it collapses where it lies and doesn't move; unrescued, it dies 2 game hours on, of the
    /// hazard, its body where it fell. A healing draught it carries stands it up (Q3a). Downed in safety, it limps as before.
    /// </summary>
    public class BleedingTests
    {
        private static readonly Vec2 InTheSump = new Vec2(-16f, -12f);

        [Fact]
        public void DownedInAHazardItCollapsesAndBleedsOut()
        {
            using var sim = Outpost();
            var crawler = Resident(sim);
            Empty(crawler);
            DownedAt(sim, crawler, InTheSump);
            sim.Step(1);
            Assert.True(crawler.IsCollapsed, "Downed in the Sump, it collapses.");
            uint bleedsOut = crawler.BleedsOutAt;
            Assert.Equal((uint)sim.Clock.CurrentTick - 1 + DayClock.Hours(SimulationManager.BleedOutHours), bleedsOut);
            var at = crawler.Position;
            while (sim.Clock.CurrentTick < bleedsOut - 1)
            {
                sim.Step(1);
                Assert.True(Vec2.Distance(at, crawler.Position) < 0.01f, $"Collapsed, it doesn't move (t{sim.Clock.CurrentTick}: {crawler.Position}).");
                Assert.Contains(crawler, sim.Host.CrawlerStates);
            }
            sim.Step(3);
            Assert.DoesNotContain(crawler, sim.Host.CrawlerStates);
            var departed = sim.Host.Departed.Single(d => d.Id == crawler.AgentId);
            Assert.Equal(DeathCause.Hazard, departed.Cause);
            Assert.Equal((byte)Floor1Layout.Definition.Sectors.First(s => s.Id == Floor1Layout.SumpSectorId).Hazard.Kind, departed.CauseDetail);
            Assert.Contains(sim.Simulation.Piles.Piles, p => p.BodyOf == crawler.AgentId && Vec2.Distance(p.Position, at) < 0.01f);
        }

        [Fact]
        public void ADraughtItCarriesStandsItUp()
        {
            using var sim = Outpost();
            var crawler = Resident(sim);
            Empty(crawler);
            Assert.True(crawler.Inventory.TryAddItem(new ItemInstance(ItemType.HealingDraught, 1, 0), out _));
            DownedAt(sim, crawler, InTheSump);
            sim.Step(1);
            Assert.True(crawler.IsCollapsed);
            uint bleedsOut = crawler.BleedsOutAt;
            bool stood = false;
            while (sim.Clock.CurrentTick < bleedsOut + 10 && !stood)
            {
                sim.Step(1);
                stood = !crawler.IsDowned;
            }
            Assert.True(stood, "It took the draught where it lay and stood.");
            Assert.Equal(0u, crawler.BleedsOutAt);
            Assert.Equal(0, crawler.Inventory.GetItemCount(ItemType.HealingDraught));
            sim.Step(DayClock.Hours(1));
            Assert.Contains(crawler, sim.Host.CrawlerStates);
        }

        [Fact]
        public void ASaveWhileItBleedsLoadsItsClock()
        {
            // K5.7: the clock and what downed it are saved; after a load it bleeds out at the same tick
            using var sim = Outpost();
            var crawler = Resident(sim);
            Empty(crawler);
            DownedAt(sim, crawler, InTheSump);
            sim.Step(5);
            Assert.True(crawler.IsCollapsed);
            uint bleedsOut = crawler.BleedsOutAt;
            byte[] saved = Simpiens.Simulation.Persistence.SaveSerializer.Serialize(sim.Host.Capture());
            sim.Step(30);
            Assert.True(sim.Host.TryRestore(Simpiens.Simulation.Persistence.SaveSerializer.Deserialize<Simpiens.Simulation.Persistence.SessionSave>(saved), out string refusal), refusal);
            Assert.Equal(saved, Simpiens.Simulation.Persistence.SaveSerializer.Serialize(sim.Host.Capture()));
            var loaded = sim.Host.CrawlerStates.Single(c => c.AgentId == crawler.AgentId);
            Assert.True(loaded.IsCollapsed);
            Assert.Equal(bleedsOut, loaded.BleedsOutAt);
            Assert.Equal(DeathCause.Hazard, loaded.DownedBy);
            sim.RunTo(bleedsOut + 3);
            Assert.True(sim.Host.Departed.Any(d => d.Id == crawler.AgentId && d.Died) || !loaded.IsDowned, "It bled out at its saved time, unless rescued or stood up.");
        }

        [Fact]
        public void DownedInSafetyItLimpsAsBefore()
        {
            using var sim = Outpost();
            var crawler = Resident(sim);
            crawler.Health = 0f;
            crawler.IsDowned = true; // by hand, in the hub
            var at = crawler.Position;
            sim.Step(60);
            Assert.False(sim.Simulation.InDanger(crawler.Position));
            Assert.Equal(0u, crawler.BleedsOutAt);
            Assert.False(crawler.IsCollapsed);
            Assert.Contains(crawler, sim.Host.CrawlerStates);
        }

        // By hand: held at a place, near death, until the place downs it; then left to itself
        private static void DownedAt(LocalSimulation sim, AgentState crawler, Vec2 at)
        {
            crawler.Health = 1f;
            for (int t = 0; t < 600 && !crawler.IsDowned; t++)
            {
                crawler.Position = at;
                sim.Step(1);
            }
            Assert.True(crawler.IsDowned, "The hazard downed it.");
            Assert.True(sim.Simulation.InDanger(crawler.Position));
        }

        private static AgentState Resident(LocalSimulation sim) => sim.Host.CrawlerStates.First(s => !s.IsDrifter);

        private static void Empty(AgentState crawler)
        {
            for (int i = 0; i < crawler.Inventory.Capacity; i++)
            {
                var slot = crawler.Inventory.GetSlot(i);
                if (!slot.IsEmpty) crawler.Inventory.TryRemoveItem(slot.Type, slot.Quantity);
            }
        }

        private static LocalSimulation Outpost()
        {
            var sim = LocalSimulation.Create(SimulationSettings.DefaultSeed);
            sim.Respawn(OutpostSession.SessionId);
            sim.Step(20);
            return sim;
        }
    }
}
