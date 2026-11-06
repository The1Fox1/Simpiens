using System.Linq;
using Simpiens.Core.Session;
using Simpiens.Hosting;
using Simpiens.Simulation;
using Simpiens.Simulation.Economy;
using Simpiens.Simulation.Items;
using Simpiens.Simulation.Workshops;
using Xunit;

namespace Simpiens.Core.Tests
{
    /// <summary>
    /// Grades (Epic 10 Part 2, W4.1, D55–D57): an artisan's grade from its practice; what a grade does to a worn piece
    /// (a quarter more life, a tenth more effect a step); the stall's shelf by grade, the best handed out first, at its life.
    /// </summary>
    public class GradeTests
    {
        [Fact]
        public void PracticeGivesTheGradeAndTheGradeLengthensLifeAndStrengthensEffect()
        {
            var tuning = WorkshopTuning.Default;
            byte Grade(ushort practice) => WorkshopService.GradeOf(new ArtisanContract(default, 0, 5, 0, practice: practice), tuning);
            Assert.Equal(new byte[] { 0, 0, 1, 1, 2, 2 }, new[] { Grade(0), Grade(9), Grade(10), Grade(19), Grade(20), Grade(45) });

            var pickaxe = ItemCatalog.Get(ItemType.ToolPickaxe).Equipment;
            Assert.Equal(new ushort[] { 40, 50, 60 }, new[] { pickaxe.DurabilityAt(Grades.Standard), pickaxe.DurabilityAt(Grades.Fine), pickaxe.DurabilityAt(Grades.Masterwork) });
            Assert.Equal(1.55f, pickaxe.ValueAt(pickaxe.GetModifier(0), Grades.Fine), 4); // a Multiply's bonus a tenth more
            var respirator = ItemCatalog.Get(ItemType.Respirator).Equipment;
            Assert.Equal(0.825f, respirator.ValueAt(respirator.GetModifier(0), Grades.Fine), 4); // an Add a tenth more

            var inventory = new Simpiens.Cognition.Inventory.AgentInventory();
            inventory.TryEquip(new ItemInstance(1, ItemType.ToolPickaxe, 1, 30, quality: Grades.Fine, durability: 50));
            Assert.Equal(1.55f, CrawlerStats.MiningSpeed(inventory, ItemType.IronOre), 4);
            var masked = new Simpiens.Cognition.Inventory.AgentInventory();
            masked.TryEquip(new ItemInstance(1, ItemType.Respirator, 1, 60, quality: Grades.Masterwork, durability: 2250));
            Assert.Equal(0.9f, CrawlerStats.HazardResistance(masked, Simpiens.Simulation.World.HazardKind.Flooding), 4); // capped
        }

        [Fact]
        public void TheShelfKeepsItsGradesAndHandsOutTheBestFirst()
        {
            var stall = new TradeStallData(EntityId.Generate(), Vec2.Zero, 100, 2f, false).WithGood(new StallGood(ItemType.ToolPickaxe, StallRole.Sells, 30, 0));
            stall = stall.WithStocked(ItemType.ToolPickaxe, 2, Grades.Standard).WithStocked(ItemType.ToolPickaxe, 1, Grades.Fine).WithStocked(ItemType.ToolPickaxe, 1, Grades.Masterwork);
            Assert.Equal(4, stall.StockOf(ItemType.ToolPickaxe));
            Assert.Equal(Grades.Masterwork, stall.BestGradeOf(ItemType.ToolPickaxe));
            stall = stall.WithPrice(ItemType.ToolPickaxe, 25);
            Assert.Equal(Grades.Masterwork, stall.BestGradeOf(ItemType.ToolPickaxe)); // a new price keeps the grades
            var taken = new byte[4];
            for (int i = 0; i < 4; i++) stall = stall.TakeBest(ItemType.ToolPickaxe, out taken[i]);
            Assert.Equal(new byte[] { Grades.Masterwork, Grades.Fine, Grades.Standard, Grades.Standard }, taken);
            Assert.Equal(0, stall.StockOf(ItemType.ToolPickaxe));

            stall = stall.WithStocked(ItemType.ToolPickaxe, 2, Grades.Fine).WithStock(ItemType.ToolPickaxe, 1);
            stall.TryGetGood(ItemType.ToolPickaxe, out var good);
            Assert.True(good.Stock == 1 && good.Fine == 1, "A smaller stock keeps its graded pieces within it.");
        }

        [Fact]
        public void ABuyerGetsTheBestGradeAtItsLife()
        {
            using var sim = LocalSimulation.Create(SimulationSettings.DefaultSeed);
            sim.Respawn(OutpostSession.SessionId);
            sim.Step(DayClock.TicksPerHour); // they know the quarry
            var sm = (SimulationManager)sim.Simulation;
            sm.UpdateStall(sm.ActiveStalls[0].WithStocked(ItemType.ToolPickaxe, 1, Grades.Masterwork)); // by hand: the forge's are the scenario's
            foreach (var crawler in sim.Host.CrawlerStates) crawler.Inventory.Gold = 100;
            for (long t = 0; t < 6 * DayClock.TicksPerHour && sim.Simulation.ActiveStalls[0].StockOf(ItemType.ToolPickaxe) > 0; t += 10) sim.Step(10);
            var buyer = sim.Host.CrawlerStates.Single(s => s.Activity.ToolPurchases > 0);
            var worn = buyer.Inventory.GetEquipped(EquipmentSlot.Tool);
            Assert.Equal(Grades.Masterwork, worn.Quality);
            Assert.True(worn.Durability > 40, $"A Masterwork pickaxe lasts longer than a Standard one's 40 (it has {worn.Durability}, some of 60 used).");
        }
    }
}
