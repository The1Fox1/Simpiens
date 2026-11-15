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
    /// creatures in the world in their sectors, and a respawn fills them the same; a load puts them back as they were saved
    /// (K2.4). What creatures do is K2.2's.
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
            Assert.Equal(4, creatures.Lairs.Count); // the warren, the hollow, and the Old Stair's web and gate (Epic 10 Part 3, K10.1b)
            var inWorld = sim.Registry.ActiveEntities.Where(e => e.Type == EntityType.Creature).ToList();
            Assert.Equal(creatures.Creatures.Count, inWorld.Count);
            foreach (var entity in inWorld)
            {
                var c = entity.Creature;
                Assert.NotNull(c);
                byte expected = c.Kind == CreatureKind.BurrowRat ? Floor1Layout.OvergrowthSectorId
                    : c.Kind == CreatureKind.StoneBeetle ? Floor1Layout.DeepSeamSectorId : Floor1Layout.OldStairSectorId;
                Assert.Equal(expected, map.FindSector(entity.Position).Id);
            }
            int rats = creatures.Creatures.Count(c => c.Kind == CreatureKind.BurrowRat), beetles = creatures.Creatures.Count(c => c.Kind == CreatureKind.StoneBeetle);
            int spinners = creatures.Creatures.Count(c => c.Kind == CreatureKind.CaveSpinner), keepers = creatures.Creatures.Count(c => c.Kind == CreatureKind.Gatekeeper);
            TestContext.Current.TestOutputHelper?.WriteLine($"{rats} rats in the warren, {beetles} beetles in the hollow, {spinners} spinners in the web, {keepers} at the gate");
            Assert.InRange(rats, 3, 4);
            Assert.InRange(beetles, 1, 2);
            Assert.InRange(spinners, 2, 3);
            Assert.Equal(1, keepers);

            var ids = creatures.Creatures.Select(c => c.Id).ToList();
            sim.Step(100);
            sim.Respawn(OutpostSession.SessionId);
            Assert.Equal(ids, sim.Simulation.Creatures.Creatures.Select(c => c.Id).ToList());

            sim.Step(50);
            Assert.Equal(ids, sim.Simulation.Creatures.Creatures.Select(c => c.Id).ToList());
        }

        /// <summary>
        /// K2.4: a load puts back the lairs and their creatures as they were saved, not afresh: a slain rat's place waiting
        /// for its return, a hurt rat, where each stood and what it did, the id sequence and the charted lairs; byte for byte
        /// and with the same world hash, its creatures in the world. The slain rat comes back on time after the load.
        /// </summary>
        [Fact]
        public void ALoadPutsTheCreaturesBackAsSaved()
        {
            using var sim = LocalSimulation.Create(SimulationSettings.DefaultSeed);
            sim.Respawn(OutpostSession.SessionId);
            var creatures = sim.Simulation.Creatures;
            var world = (ICreatureWorld)sim.Simulation;
            sim.Step(400);
            var slain = creatures.Creatures.First(c => c.Kind == CreatureKind.BurrowRat);
            int lair = slain.LairIndex, slot = slain.Slot;
            Assert.True(creatures.Slay(world, slain.Id, (uint)sim.Clock.CurrentTick));
            uint returnsAt = creatures.Lairs[lair].ReturnAt(slot);
            var hurt = creatures.Creatures.First(c => c.Kind == CreatureKind.BurrowRat);
            hurt.Health = 9f;
            sim.Step(30);

            long tick = sim.Clock.CurrentTick;
            var expected = creatures.Creatures.Select(c => (c.Id, c.Position, c.Health, c.Mode, c.Goal, c.RestUntil)).ToList();
            byte[] saved = SaveSerializer.Serialize(sim.Host.Capture());
            var savedHash = Simpiens.Testing.Environment.WorldStateHash.Capture(tick, sim.Host, sim.Simulation, sim.Sectors, includeWork: false);
            sim.Step(200); // the world moves on; the load puts it back
            Assert.True(sim.Host.TryRestore(SaveSerializer.Deserialize<SessionSave>(saved), out string refusal), refusal);

            creatures = sim.Simulation.Creatures;
            Assert.Equal(expected, creatures.Creatures.Select(c => (c.Id, c.Position, c.Health, c.Mode, c.Goal, c.RestUntil)).ToList());
            Assert.Equal(default, creatures.Lairs[lair].MemberIn(slot));
            Assert.Equal(returnsAt, creatures.Lairs[lair].ReturnAt(slot));
            Assert.Equal(expected.Count, sim.Registry.ActiveEntities.Count(e => e.Type == EntityType.Creature));
            Assert.All(creatures.Creatures, c => Assert.Equal(c.Position, sim.Registry.GetEntity(c.Id).Position));
            Assert.Equal(saved, SaveSerializer.Serialize(sim.Host.Capture()));
            var loadedHash = Simpiens.Testing.Environment.WorldStateHash.Capture(tick, sim.Host, sim.Simulation, sim.Sectors, includeWork: false);
            Assert.True(savedHash.Total == loadedHash.Total, savedHash.DescribeDifference(loadedHash));

            sim.RunTo(returnsAt);
            Assert.NotEqual(default, sim.Simulation.Creatures.Lairs[lair].MemberIn(slot)); // back in its place, on time
        }
    }
}
