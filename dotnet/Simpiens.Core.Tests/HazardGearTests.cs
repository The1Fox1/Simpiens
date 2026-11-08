using System.Collections.Generic;
using Simpiens.Core.Session;
using Simpiens.Core.Session.Layouts;
using Simpiens.Hosting;
using Simpiens.Narrative;
using Simpiens.Simulation;
using Simpiens.Simulation.Economy;
using Simpiens.Simulation.Guild;
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
            var protection = Simpiens.Simulation.Items.EquipmentSlot.Protection;
            float resistance = Simpiens.Simulation.Items.ItemCatalog.Get(ItemType.HazardGear).Equipment.GetModifier(0).Value; // 0.5
            var states = sim.Host.CrawlerStates;
            var bare = states[0];
            var geared = states[1];
            var breaking = states[2];
            var sump = new Vec2(-16f, -12f);
            bare.Position = sump + new Vec2(-2f, 0f);
            geared.Position = sump;
            breaking.Position = sump + new Vec2(2f, 0f);
            Assert.True(geared.Inventory.TryEquip(new ItemInstance(1, ItemType.HazardGear, 1, 40, durability: 1000)), "Gear goes on.");
            Assert.True(breaking.Inventory.TryEquip(new ItemInstance(2, ItemType.HazardGear, 1, 40, durability: 5)), "Worn-down gear goes on.");

            sim.Step(20);

            float bareRate = bare.Activity.HazardDamage / bare.Activity.HazardTicks;
            float gearedRate = geared.Activity.GearedHazardDamage / geared.Activity.GearedHazardTicks;
            float sumpPerSecond = Floor1Layout.SumpHazard.DamagePerSecond;
            float tick = sumpPerSecond * 0.1f;
            TestContext.Current.TestOutputHelper?.WriteLine($"bare {bareRate:F4}/tick over {bare.Activity.HazardTicks} ticks; geared {gearedRate:F4}/tick over {geared.Activity.GearedHazardTicks} ticks");
            Assert.True(bare.Activity.HazardTicks > 0 && bare.Activity.GearedHazardTicks == 0, "The bare crawler was in the Sump, bare.");
            Assert.True(geared.Activity.GearedHazardTicks > 0 && geared.Activity.GearedHazardTicks == geared.Activity.HazardTicks, "The geared crawler was in the Sump, geared.");
            Assert.True(System.Math.Abs(bareRate - tick) < 1e-4f, $"Bare, the Sump takes {tick} a tick (got {bareRate}).");
            Assert.True(System.Math.Abs(gearedRate - (float)(sumpPerSecond * (float)(1f - resistance)) * 0.1f) < 1e-4f && gearedRate < bareRate * 0.51f,
                $"Geared, it takes half (got {gearedRate} against {bareRate}).");
            Assert.Equal(1000 - geared.Activity.GearedHazardTicks, geared.Inventory.GetEquipped(protection).Durability);

            Assert.True(breaking.Inventory.GetEquipped(protection).IsEmpty, "Gear with 5 ticks of life is gone after 20 ticks in the Sump.");
            Assert.Equal(1, breaking.Activity.GearWornOut);
            Assert.Equal(5, breaking.Activity.GearedHazardTicks);
            var told = new List<ChronicleEvent>();
            sim.Chronicle.GetActorEvents(breaking.AgentId, told);
            Assert.Contains(told, e => e.EventType == ChronicleEventType.GearWornOut);
        }
        /// <summary>
        /// What the view's red coat shows (Epic 10 Part 2, S3, D71): a crawler is hurt in a tick exactly when it stands in a
        /// hazard sector, by what its gear lets through; anyone elsewhere is not. Two crawlers are put in the Sump, one bare
        /// and one in a respirator; every crawler is judged every tick for 20 ticks.
        /// </summary>
        [Fact]
        public void ACrawlerIsHurtInATickExactlyWhenItStandsInAHazard()
        {
            using var sim = LocalSimulation.Create(SimulationSettings.DefaultSeed);
            sim.Respawn(OutpostSession.SessionId);
            var map = sim.Host.Current.Map;
            var states = sim.Host.CrawlerStates;
            var bare = states[0];
            var masked = states[1];
            var sump = new Vec2(-16f, -12f);
            bare.Position = sump + new Vec2(-1f, 0f);
            masked.Position = sump + new Vec2(1f, 0f);
            Assert.True(masked.Inventory.TryEquip(new ItemInstance(1, ItemType.Respirator, 1, 60, durability: 1500)), "The respirator goes on.");
            float perTick = (float)(Floor1Layout.SumpHazard.DamagePerSecond * 0.1f);

            int bareHurt = 0, maskedHurt = 0, elsewhere = 0;
            for (int t = 0; t < 20; t++)
            {
                sim.Step(1);
                foreach (var crawler in states)
                {
                    bool inHazard = map.FindSector(crawler.Position).Hazard.IsHazardous;
                    Assert.True(inHazard == crawler.HurtThisTick > 0f, $"t{sim.Clock.CurrentTick}: {crawler.AgentId} in a hazard {inHazard}, hurt {crawler.HurtThisTick}.");
                    if (!inHazard) { elsewhere++; continue; }
                    if (crawler == bare && bare.Health > perTick) { Assert.Equal(perTick, bare.HurtThisTick, 4); bareHurt++; }
                    if (crawler == masked) { Assert.Equal(perTick * 0.25f, masked.HurtThisTick, 4); maskedHurt++; }
                }
            }
            TestContext.Current.TestOutputHelper?.WriteLine($"bare hurt {bareHurt} ticks, masked {maskedHurt}; crawler-ticks elsewhere, unhurt: {elsewhere}");
            Assert.True(bareHurt > 0 && maskedHurt > 0 && elsewhere > 0, "Both in the Sump were hurt, and the rest were not.");
        }

        /// <summary>
        /// Protection by kind (Epic 10 Part 2, W3.1, D47): in the Sump's foul air a respirator lets through a quarter of the
        /// harm, hazard gear half, a hard hat all of it; in the Glow Caves' falling rock the hard hat a quarter and the
        /// respirator all. The resistance is the wearer's: general plus the kind's, at most 0.9.
        /// </summary>
        [Fact]
        public void ProtectionCutsItsOwnKindOfHazard()
        {
            var air = Simpiens.Simulation.World.HazardKind.FoulAir;
            var flood = Simpiens.Simulation.World.HazardKind.Flooding;
            var rock = Simpiens.Simulation.World.HazardKind.FallingRock;
            float Resistance(ItemType worn, Simpiens.Simulation.World.HazardKind kind)
            {
                var inventory = new Simpiens.Cognition.Inventory.AgentInventory();
                if (worn != ItemType.None) inventory.TryEquip(new ItemInstance(1, worn, 1, 60, durability: 1500));
                return Simpiens.Simulation.Items.CrawlerStats.HazardResistance(inventory, kind);
            }
            Assert.Equal(0.75f, Resistance(ItemType.Respirator, air), 3);
            Assert.Equal(0.75f, Resistance(ItemType.Respirator, flood), 3); // the Gallery's flooded air
            Assert.Equal(0f, Resistance(ItemType.Respirator, rock), 3);
            Assert.Equal(0.75f, Resistance(ItemType.HardHat, rock), 3);
            Assert.Equal(0f, Resistance(ItemType.HardHat, air), 3);
            Assert.Equal(0.5f, Resistance(ItemType.HazardGear, rock), 3);
            Assert.Equal(0.5f, Resistance(ItemType.HazardGear, flood), 3);
            Assert.Equal(0f, Resistance(ItemType.None, air), 3);

            using var sim = LocalSimulation.Create(SimulationSettings.DefaultSeed);
            sim.Respawn(OutpostSession.SessionId);
            var host = (IGuildHost)sim.Simulation;
            host.TryGetSector(Floor1Layout.SumpSectorId, out var sump);
            host.TryGetSector(Floor1Layout.GlowCavesSectorId, out var caves);
            var states = sim.Host.CrawlerStates;
            var worn = new[] { ItemType.None, ItemType.Respirator, ItemType.HazardGear, ItemType.HardHat };
            for (int i = 0; i < 4; i++)
            {
                states[i].Position = sump.TargetPoint + new Vec2(i * 0.6f - 1f, 0f);
                states[4 + i].Position = caves.TargetPoint + new Vec2(i * 0.6f - 1f, 0f);
                if (worn[i] == ItemType.None) continue;
                Assert.True(states[i].Inventory.TryEquip(new ItemInstance(1, worn[i], 1, 60, durability: 1500)));
                Assert.True(states[4 + i].Inventory.TryEquip(new ItemInstance(1, worn[i], 1, 60, durability: 1500)));
            }
            sim.Step(20);
            float Rate(Simpiens.Cognition.AgentState crawler) => crawler.Activity.HazardDamage / crawler.Activity.HazardTicks;
            TestContext.Current.TestOutputHelper?.WriteLine($"Sump: bare {Rate(states[0]):F4}, respirator {Rate(states[1]):F4}, gear {Rate(states[2]):F4}, hard hat {Rate(states[3]):F4}; caves: bare {Rate(states[4]):F4}, respirator {Rate(states[5]):F4}, gear {Rate(states[6]):F4}, hard hat {Rate(states[7]):F4}");
            for (int i = 0; i < 8; i++) Assert.True(states[i].Activity.HazardTicks > 0, $"Crawler {i} stood in the hazard.");
            Assert.Equal(0.25f, Rate(states[1]) / Rate(states[0]), 3);
            Assert.Equal(0.5f, Rate(states[2]) / Rate(states[0]), 3);
            Assert.Equal(1f, Rate(states[3]) / Rate(states[0]), 3);
            Assert.Equal(1f, Rate(states[5]) / Rate(states[4]), 3);
            Assert.Equal(0.5f, Rate(states[6]) / Rate(states[4]), 3);
            Assert.Equal(0.25f, Rate(states[7]) / Rate(states[4]), 3);
        }
    }
}
