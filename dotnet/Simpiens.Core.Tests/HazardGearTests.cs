using System.Collections.Generic;
using Simpiens.Core.Session;
using Simpiens.Core.Session.Layouts;
using Simpiens.Hosting;
using Simpiens.Narrative;
using Simpiens.Simulation;
using Simpiens.Simulation.Economy;
using Xunit;

namespace Simpiens.Core.Tests
{
    /// <summary>
    /// Hazard gear's mechanism on the real floor (Epic 10 G3), with the preconditions injected: three crawlers are put
    /// in the Sump, one bare, one with gear that will outlast the test, one with gear 5 ticks from breaking. Damage per
    /// tick of exposure is the hazard's through the gear (half, by the default tuning); gear wears a tick for every tick
    /// in danger, and breaks at 0, told in the chronicle. The live outcome, gear bought and worn on its own, is
    /// OutpostScenarioTests' G3 scenario.
    /// </summary>
    public class HazardGearTests
    {
        [Fact]
        public void GearHalvesHazardDamageAndWearsOut()
        {
            using var sim = LocalSimulation.Create(SimulationSettings.DefaultSeed);
            sim.Respawn(OutpostSession.SessionId);
            var gear = sim.Simulation.Economy.Gear;
            var states = sim.Host.CrawlerStates;
            var bare = states[0];
            var geared = states[1];
            var breaking = states[2];
            var sump = new Vec2(-16f, -12f);
            bare.Position = sump + new Vec2(-2f, 0f);
            geared.Position = sump;
            breaking.Position = sump + new Vec2(2f, 0f);
            Assert.True(geared.Inventory.TryWear(new ItemInstance(1, ItemType.HazardGear, 1, 40, durability: 1000)), "Gear goes on.");
            Assert.True(breaking.Inventory.TryWear(new ItemInstance(2, ItemType.HazardGear, 1, 40, durability: 5)), "Worn-down gear goes on.");

            sim.Step(20);

            float bareRate = bare.Activity.HazardDamage / bare.Activity.HazardTicks;
            float gearedRate = geared.Activity.GearedHazardDamage / geared.Activity.GearedHazardTicks;
            float sumpPerSecond = Floor1Layout.SumpHazard.DamagePerSecond;
            float tick = sumpPerSecond * 0.1f;
            TestContext.Current.TestOutputHelper?.WriteLine($"bare {bareRate:F4}/tick over {bare.Activity.HazardTicks} ticks; geared {gearedRate:F4}/tick over {geared.Activity.GearedHazardTicks} ticks");
            Assert.True(bare.Activity.HazardTicks > 0 && bare.Activity.GearedHazardTicks == 0, "The bare crawler was in the Sump, bare.");
            Assert.True(geared.Activity.GearedHazardTicks > 0 && geared.Activity.GearedHazardTicks == geared.Activity.HazardTicks, "The geared crawler was in the Sump, geared.");
            Assert.True(System.Math.Abs(bareRate - tick) < 1e-4f, $"Bare, the Sump takes {tick} a tick (got {bareRate}).");
            Assert.True(System.Math.Abs(gearedRate - gear.DamageThrough(sumpPerSecond, true) * 0.1f) < 1e-4f && gearedRate < bareRate * 0.51f,
                $"Geared, it takes half (got {gearedRate} against {bareRate}).");
            Assert.Equal(1000 - geared.Activity.GearedHazardTicks, geared.Inventory.Gear.Durability);

            Assert.True(breaking.Inventory.Gear.IsEmpty, "Gear with 5 ticks of life is gone after 20 ticks in the Sump.");
            Assert.Equal(1, breaking.Activity.GearWornOut);
            Assert.Equal(5, breaking.Activity.GearedHazardTicks);
            var told = new List<ChronicleEvent>();
            sim.Chronicle.GetActorEvents(breaking.AgentId, told);
            Assert.Contains(told, e => e.EventType == ChronicleEventType.GearWornOut);
        }
    }
}
