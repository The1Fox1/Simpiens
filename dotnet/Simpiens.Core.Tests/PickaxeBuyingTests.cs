using System.Linq;
using Simpiens.Core.Session;
using Simpiens.Hosting;
using Simpiens.Simulation;
using Simpiens.Simulation.Economy;
using Simpiens.Simulation.Items;
using Xunit;

namespace Simpiens.Core.Tests
{
    /// <summary>
    /// The pickaxe's buyers (Epic 10 Part 2, W1.4b, D38), on the real outpost: with pickaxes in the stall's stock, residents
    /// who mine and carry the price buy them and wear them; drifters don't; nobody buys what isn't in stock or what its
    /// purse can't spare. A greedy resident values a pickaxe under its 30g and waits for the player to price it lower. Mechanism tests: the stock (and, where said, the purses) are set by hand. The live scenario,
    /// with the forge's pickaxes and nothing injected, is W1.5b's.
    /// </summary>
    public class PickaxeBuyingTests
    {
        private static LocalSimulation Outpost()
        {
            var sim = LocalSimulation.Create(SimulationSettings.DefaultSeed);
            sim.Respawn(OutpostSession.SessionId);
            return sim;
        }

        private static void Stock(LocalSimulation sim, ushort pickaxes)
        {
            var stall = sim.Simulation.ActiveStalls[0];
            ((SimulationManager)sim.Simulation).UpdateStall(stall.WithStock(ItemType.ToolPickaxe, pickaxes)); // by hand: a fixture
        }

        private static int Wearing(LocalSimulation sim)
            => sim.Host.CrawlerStates.Count(s => s.Inventory.GetEquipped(EquipmentSlot.Tool).Type == ItemType.ToolPickaxe);

        [Fact]
        public void ResidentsWhoMineBuyTheStallsPickaxesAndWearThem()
        {
            using var sim = Outpost();
            sim.Step(DayClock.TicksPerHour); // an hour in: the residents know the quarry
            Stock(sim, 3);
            foreach (var crawler in sim.Host.CrawlerStates) crawler.Inventory.Gold = 100; // purses that can spare the price (W1.4c measures the live ones)
            for (long t = 0; t < 6 * DayClock.TicksPerHour && sim.Simulation.ActiveStalls[0].StockOf(ItemType.ToolPickaxe) > 0; t += 10) sim.Step(10);

            var buyers = sim.Host.CrawlerStates.Where(s => s.Activity.ToolPurchases > 0).ToList();
            TestContext.Current.TestOutputHelper?.WriteLine($"{buyers.Count} bought by t{sim.Clock.CurrentTick}: {string.Join(", ", buyers.Select(b => $"greed {b.Personality.Greed} gold {b.Inventory.Gold}"))}");
            Assert.Equal(3, buyers.Count);
            Assert.Equal(0, sim.Simulation.ActiveStalls[0].StockOf(ItemType.ToolPickaxe));
            Assert.Equal(3, Wearing(sim));
            Assert.All(buyers, b => Assert.False(b.IsDrifter, "Drifters pass through; they don't buy tools."));
            Assert.All(buyers, b => Assert.Equal(1, b.Activity.ToolPurchases));
            Assert.All(buyers, b => Assert.True(b.Personality.Greed <= 0f, "A greedy resident values a pickaxe under its price."));
            Assert.Equal(0, sim.Simulation.IntentFaults);
        }

        [Fact]
        public void NobodyBuysWhatIsntInStockOrWhatItsPurseCantSpare()
        {
            using var sim = Outpost();
            sim.Step(12 * DayClock.TicksPerHour);
            Assert.Equal(0, Wearing(sim)); // the stall opens with none: only the forge makes them (D40)

            using var poor = Outpost();
            poor.Step(DayClock.TicksPerHour);
            Stock(poor, 3);
            foreach (var crawler in poor.Host.CrawlerStates) crawler.Inventory.Gold = 20; // under 30g and a ration
            poor.Step(DayClock.TicksPerHour / 2);
            Assert.Equal(0, Wearing(poor));
        }
    }
}
