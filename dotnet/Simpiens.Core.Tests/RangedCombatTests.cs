using System.Linq;
using Simpiens.Cognition;
using Simpiens.Core.Session;
using Simpiens.Core.Session.Layouts;
using Simpiens.Hosting;
using Simpiens.Simulation;
using Simpiens.Simulation.Creatures;
using Simpiens.Simulation.Economy;
using Simpiens.Simulation.Items;
using Xunit;

namespace Simpiens.Core.Tests
{
    /// <summary>
    /// Ranged combat's mechanism (Epic 10 Part 3, K8.1–K8.3, D44–D48), on the real floor with the preconditions set by hand: a
    /// crossbow's shot needs a clear line over the walls and carries across the room to its range; and a creature with a range,
    /// the cave spinner (on a variant of the floor with a lair of them, since no lair holds spinners until K10's approach),
    /// spits from beyond its reach and keeps its distance from a crawler.
    /// </summary>
    public class RangedCombatTests
    {
        [Fact]
        public void AShotNeedsAClearLineAndCarriesToItsRange()
        {
            using var sim = LocalSimulation.Create(SimulationSettings.DefaultSeed);
            sim.Respawn(OutpostSession.SessionId);
            sim.Step(20);
            var archer = sim.Host.CrawlerStates.First(s => !s.IsDrifter);
            Assert.True(archer.Inventory.TryEquip(Outfitting.NewPiece(ItemType.Crossbow, Grades.Standard)));
            var node = sim.Registry.GetEntity(archer.AgentId);
            var creature = sim.Simulation.Creatures.Creatures.First(c => c.IsAlive);
            int life = archer.Inventory.GetEquipped(EquipmentSlot.Weapon).Durability;

            // By hand: the creature in the Overgrowth by its west wall (x = 8), the archer 3 units off across the wall
            int shots = Shoot(sim, node, creature, new Vec2(9.5f, 12f), new Vec2(6.5f, 12f));
            Assert.Equal(0, shots); // walled off
            Assert.Equal(life, archer.Inventory.GetEquipped(EquipmentSlot.Weapon).Durability);

            // The same 3 units, the same side of the wall: a shot, and the crossbow wears for it, hit or miss
            shots = Shoot(sim, node, creature, new Vec2(9.5f, 12f), new Vec2(12.5f, 12f));
            Assert.Equal(1, shots);
            Assert.Equal(life - 1, archer.Inventory.GetEquipped(EquipmentSlot.Weapon).Durability);

            // Past its range of 7 across the gap: none
            archer.NextSwingAt = 0;
            shots = Shoot(sim, node, creature, new Vec2(9.5f, 12f), new Vec2(17.5f, 12f));
            Assert.Equal(0, shots);
        }

        // Puts the two where given, asks for the archer's shot and runs the tick; the shots the archer loosed in it
        private static int Shoot(LocalSimulation sim, WorldEntity archer, CreatureState creature, Vec2 creatureAt, Vec2 archerAt)
        {
            creature.Position = creatureAt;
            archer.Agent.Position = archerAt;
            archer.Agent.NextSwingAt = 0;
            sim.Simulation.Creatures.AskSwing(archer, creature.Id);
            sim.Step(1);
            return sim.Simulation.Creatures.ShotsThisTick.Count(s => s.Shooter == archer.Id);
        }

        [Fact]
        public void SpinnersSpitFromRangeAndKeepTheirDistance()
        {
            var heart = new Vec2(-13f, 1f); // in the Ore Quarry, charted from the start
            var map = Floor1Layout.BuildWith(b => b.Lair("test.spinners", CreatureKind.CaveSpinner, heart));
            using var sim = LocalSimulation.Create(SimulationSettings.DefaultSeed);
            sim.Host.Respawn(new OutpostSession(OutpostSession.DefaultCrawlerCount, EconomyTuning.Default, map));
            sim.Step(5);
            var service = sim.Simulation.Creatures;
            var spinners = service.Creatures.Where(c => c.Kind == CreatureKind.CaveSpinner).Select(c => c.Id).ToHashSet();
            Assert.NotEmpty(spinners);
            var kind = CreatureCatalog.Get(CreatureKind.CaveSpinner);

            var stray = sim.Host.CrawlerStates.First(s => !s.IsDrifter);
            var strayNode = sim.Registry.GetEntity(stray.AgentId);
            var at = heart + new Vec2(3f, 0f);
            int spits = 0, fromRange = 0, closeAfterNotice = 0;
            long firstSpit = -1;
            for (int t = 0; t < 400 && !stray.IsDowned; t++)
            {
                stray.Position = at; // by hand: held where it stands
                sim.Step(1);
                foreach (var shot in service.ShotsThisTick)
                {
                    if (!shot.ByCreature || !spinners.Contains(shot.Shooter)) continue;
                    spits++;
                    if (firstSpit < 0) firstSpit = sim.Clock.CurrentTick;
                    float gap = Vec2.Distance(shot.From, shot.To) - kind.BodyRadius - strayNode.Radius;
                    if (gap > kind.Reach) fromRange++;
                }
                if (firstSpit < 0 || sim.Clock.CurrentTick < firstSpit + 30) continue;
                foreach (var c in service.Creatures)
                {
                    if (!spinners.Contains(c.Id) || !c.IsAlive) continue;
                    float gap = Vec2.Distance(c.Position, stray.Position) - kind.BodyRadius - strayNode.Radius;
                    if (gap <= kind.Reach) closeAfterNotice++;
                }
            }
            TestContext.Current.TestOutputHelper?.WriteLine($"{spits} spits, {fromRange} from beyond reach, first t{firstSpit}; health {stray.Health:F0}; within reach after the first spit {closeAfterNotice} creature-ticks");
            Assert.True(spits > 0, "The spinners spat at the stray.");
            Assert.True(fromRange > 0 && fromRange * 2 >= spits, "Mostly from beyond their reach.");
            Assert.True(stray.Health < 100f, "Their spit hurt it.");
            Assert.Equal(0, closeAfterNotice); // they keep their distance
        }
    }
}
