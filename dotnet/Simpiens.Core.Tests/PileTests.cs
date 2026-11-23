using System.Linq;
using Simpiens.Core.Session;
using Simpiens.Hosting;
using Simpiens.Simulation;
using Simpiens.Simulation.Economy;
using Simpiens.Simulation.Loot;
using Simpiens.Simulation.Persistence;
using Simpiens.Simulation.Spatial;
using Xunit;

namespace Simpiens.Core.Tests
{
    /// <summary>
    /// Items on the ground (Epic 10 Part 3, K4.1; A5), on the real outpost: a pile dropped by hand lies in the world as an entity;
    /// its victor alone takes from it for a game hour, then anyone; a taker takes what fits its pack, the gold into its purse,
    /// and an emptied pile is gone; a pile fades a game day on; a load puts piles back byte for byte, with the same world hash.
    /// </summary>
    public class PileTests
    {
        [Fact]
        public void APileIsClaimedByItsVictorThenAnyone()
        {
            using var sim = Outpost();
            var piles = sim.Simulation.Piles;
            var world = (IPileWorld)sim.Simulation;
            var victor = sim.Host.CrawlerStates[0];
            var other = sim.Host.CrawlerStates[1];
            uint tick = (uint)sim.Clock.CurrentTick;
            var pile = piles.Drop(world, new Vec2(1f, 1f), new[] { new ItemInstance(ItemType.IronOre, 3, 0), new ItemInstance(ItemType.MedicinalHerb, 1, 0) }, 5, victor.AgentId, tick);
            Assert.NotNull(pile);
            Assert.Contains(sim.Registry.ActiveEntities, e => e.Type == EntityType.Pile && e.Id == pile.Id && e.Pile == pile);

            Assert.Equal(0, piles.Take(world, pile.Id, other.Inventory, other.AgentId, tick + 10, out _, out _)); // claimed
            int gold = victor.Inventory.Gold;
            int units = piles.Take(world, pile.Id, victor.Inventory, victor.AgentId, tick + 10, out int taken, out _);
            Assert.True(units > 0);
            Assert.Equal(5, taken);
            Assert.Equal(gold + 5, victor.Inventory.Gold);
            if (pile.IsEmpty) Assert.False(piles.TryGet(pile.Id, out _), "An emptied pile is gone.");
            else
            {
                Assert.True(piles.TryGet(pile.Id, out _)); // what didn't fit stays
                Assert.True(piles.Take(world, pile.Id, other.Inventory, other.AgentId, tick + DayClock.Hours(PileService.ClaimHours), out _, out _) >= 0, "Past the claim, anyone may take.");
            }
        }

        [Fact]
        public void APileFadesAGameDayOn()
        {
            using var sim = Outpost();
            var pile = sim.Simulation.Piles.Drop((IPileWorld)sim.Simulation, new Vec2(1f, 1f), new[] { new ItemInstance(ItemType.IronOre, 1, 0) }, 0, default, (uint)sim.Clock.CurrentTick);
            uint fades = pile.FadesAt;
            Assert.Equal((uint)sim.Clock.CurrentTick + DayClock.Hours(PileService.FadeHours), fades);
            // The piles' own tick, by hand: crawlers would pick it over first (K4.3)
            var world = (IPileWorld)sim.Simulation;
            sim.Simulation.Piles.Tick(world, fades - 1);
            Assert.True(sim.Simulation.Piles.TryGet(pile.Id, out _));
            sim.Simulation.Piles.Tick(world, fades);
            Assert.False(sim.Simulation.Piles.TryGet(pile.Id, out _));
            Assert.DoesNotContain(sim.Registry.ActiveEntities, e => e.Id == pile.Id);
        }

        [Fact]
        public void ALoadPutsThePilesBack()
        {
            using var sim = Outpost();
            var world = (IPileWorld)sim.Simulation;
            uint tick = (uint)sim.Clock.CurrentTick;
            sim.Simulation.Piles.Drop(world, new Vec2(1f, 1f), new[] { new ItemInstance(ItemType.IronOre, 2, 0) }, 0, sim.Host.CrawlerStates[0].AgentId, tick);
            sim.Simulation.Piles.Drop(world, new Vec2(-3f, 2f), new[] { new ItemInstance(ItemType.Cudgel, 1, 0, durability: 40) }, 9, default, tick);
            sim.Step(5);
            long at = sim.Clock.CurrentTick;
            byte[] saved = SaveSerializer.Serialize(sim.Host.Capture());
            var savedHash = Simpiens.Testing.Environment.WorldStateHash.Capture(at, sim.Host, sim.Simulation, sim.Sectors, includeWork: false);
            sim.Simulation.Piles.Drop(world, new Vec2(5f, 5f), new[] { new ItemInstance(ItemType.Ration, 1, 0) }, 0, default, (uint)sim.Clock.CurrentTick);
            Assert.True(sim.Host.TryRestore(SaveSerializer.Deserialize<SessionSave>(saved), out string refusal), refusal);
            Assert.Equal(2, sim.Simulation.Piles.Piles.Count);
            Assert.Equal(2, sim.Registry.ActiveEntities.Count(e => e.Type == EntityType.Pile));
            Assert.Equal(saved, SaveSerializer.Serialize(sim.Host.Capture()));
            var loadedHash = Simpiens.Testing.Environment.WorldStateHash.Capture(at, sim.Host, sim.Simulation, sim.Sectors, includeWork: false);
            Assert.True(savedHash.Total == loadedHash.Total, savedHash.DescribeDifference(loadedHash));
        }

        private static LocalSimulation Outpost()
        {
            var sim = LocalSimulation.Create(SimulationSettings.DefaultSeed);
            sim.Respawn(OutpostSession.SessionId);
            sim.Step(10);
            return sim;
        }
    }
}
