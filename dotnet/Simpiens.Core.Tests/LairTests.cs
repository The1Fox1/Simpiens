using System.Linq;
using Simpiens.Core.Session;
using Simpiens.Core.Session.Layouts;
using Simpiens.Hosting;
using Simpiens.Simulation;
using Simpiens.Simulation.Creatures;
using Simpiens.Simulation.Persistence;
using Simpiens.Simulation.Spatial;
using Xunit;

namespace Simpiens.Core.Tests
{
    /// <summary>
    /// The lairs on the real outpost (Epic 10 Part 3, K2.1): the session opens with the warren and the hollow full, their
    /// creatures in the world in their sectors; a respawn fills them the same, and so does a load (until the creatures are
    /// saved, K2.4). What creatures do is K2.2's.
    /// </summary>
    public class LairTests
    {
        [Fact]
        public void TheOutpostOpensWithItsLairsFull()
        {
            using var sim = LocalSimulation.Create(SimulationSettings.DefaultSeed);
            sim.Respawn(OutpostSession.SessionId);
            var creatures = sim.Simulation.Creatures;
            var map = sim.Host.Current.Map;
            Assert.Equal(2, creatures.Lairs.Count);
            var inWorld = sim.Registry.ActiveEntities.Where(e => e.Type == EntityType.Creature).ToList();
            Assert.Equal(creatures.Creatures.Count, inWorld.Count);
            foreach (var entity in inWorld)
            {
                var c = entity.Creature;
                Assert.NotNull(c);
                byte expected = c.Kind == CreatureKind.BurrowRat ? Floor1Layout.OvergrowthSectorId : Floor1Layout.DeepSeamSectorId;
                Assert.Equal(expected, map.FindSector(entity.Position).Id);
            }
            int rats = creatures.Creatures.Count(c => c.Kind == CreatureKind.BurrowRat), beetles = creatures.Creatures.Count(c => c.Kind == CreatureKind.StoneBeetle);
            TestContext.Current.TestOutputHelper?.WriteLine($"{rats} rats in the warren, {beetles} beetles in the hollow");
            Assert.InRange(rats, 3, 4);
            Assert.InRange(beetles, 1, 2);

            var ids = creatures.Creatures.Select(c => c.Id).ToList();
            sim.Step(100);
            sim.Respawn(OutpostSession.SessionId);
            Assert.Equal(ids, sim.Simulation.Creatures.Creatures.Select(c => c.Id).ToList());

            sim.Step(50);
            byte[] saved = SaveSerializer.Serialize(sim.Host.Capture());
            Assert.True(sim.Host.TryRestore(SaveSerializer.Deserialize<SessionSave>(saved), out string refusal), refusal);
            Assert.Equal(ids, sim.Simulation.Creatures.Creatures.Select(c => c.Id).ToList());
            Assert.Equal(ids.Count, sim.Registry.ActiveEntities.Count(e => e.Type == EntityType.Creature));
        }
    }
}
