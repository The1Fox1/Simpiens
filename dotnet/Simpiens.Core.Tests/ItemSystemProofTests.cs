using System.Collections.Generic;
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
    /// The item system's proof (the item system refactor, 2026-10-26): an item is added to the game and the economy, or taken
    /// out of the economy, by data alone, on the real outpost. Adding: a test-only resource joins the catalog (the
    /// catalog's test hook) and the session's economy; a crawler holding some sells them to the stall on its own, and the
    /// caravan exports them, with no other code knowing the item. Removing: a day with glowstone out of the economy still
    /// holds. Making (Epic 10 Part 2, W0.5): a test-only item and its recipe join the catalog and the recipe book, and
    /// the still makes it from the stall's stock into the stall's stock. The holdings are injected; what happens to them
    /// is not. Until W1 gives the work to an artisan, the test does the work, a tick at a time.
    /// </summary>
    public class ItemSystemProofTests
    {
        private static readonly ItemType Shard = (ItemType)200;
        private static readonly ItemType Salve = (ItemType)201;

        [Fact]
        public void AResourceAddedAsDataIsSoldAndExported()
        {
            using var extended = ItemCatalog.ExtendForTest(new ItemDefinition(Shard, "Shard", "Shard",
                ItemCategory.Resource | ItemCategory.Mineral, ItemRarity.Common));
            var economy = EconomyTuning.Default
                .WithItemTerms(Shard, new ItemTerms(baseValue: 10, StallRole.Buys, stallPrice: 26, stallStock: 0))
                .WithSupply(EconomyTuning.Default.Supply.With(CaravanTerms.Exports(Shard, quota: 2, marketPrice: 31)));

            using var sim = LocalSimulation.Create(SimulationSettings.DefaultSeed);
            sim.Host.Respawn(new OutpostSession(OutpostSession.DefaultCrawlerCount, economy));
            Assert.True(sim.Simulation.ActiveStalls[0].BuyPriceOf(Shard) == 26, "The stall opens buying the shard at its terms.");

            var seller = sim.Host.CrawlerStates[0];
            Assert.True(seller.Inventory.TryAddItem(new ItemInstance(1, Shard, 3, 10), out _), "The crawler holds three shards.");
            int mostHeld = 0;
            bool exported = false;
            long soldAt = -1;
            for (long t = 1; t <= 2400 && !(exported && soldAt >= 0 && seller.Inventory.GetItemCount(Shard) == 0); t++)
            {
                sim.Step(1);
                int stock = sim.Simulation.ActiveStalls[0].StockOf(Shard);
                if (stock > 0 && soldAt < 0) soldAt = t;
                if (stock < mostHeld) exported = true;
                if (stock > mostHeld) mostHeld = stock;
            }
            TestContext.Current.TestOutputHelper?.WriteLine($"first sale t{soldAt}; the stall held {mostHeld}; exported {exported}; {seller.Inventory.GetItemCount(Shard)} left with the crawler");
            Assert.True(soldAt >= 0, "The crawler never sold a shard to the stall.");
            Assert.Equal(0, seller.Inventory.GetItemCount(Shard));
            Assert.True(exported, "The caravan never exported a shard.");
            Assert.Equal(0, sim.Simulation.IntentFaults);
        }

        [Fact]
        public void AMadeItemAddedAsDataIsMadeFromTheStallsStock()
        {
            using var item = ItemCatalog.ExtendForTest(new ItemDefinition(Salve, "Salve", "Salve",
                ItemCategory.Consumable | ItemCategory.Medicine, ItemRarity.Common, consume: new[] { new ConsumeEffect(StatKind.Health, 20f) }));
            using var recipe = RecipeBook.ExtendForTest(new Recipe(Salve, StationKind.Still, workTicks: 50, new RecipeInput(ItemType.MedicinalHerb, 2)));
            var economy = EconomyTuning.Default.WithItemTerms(Salve, new ItemTerms(baseValue: 12, StallRole.Sells, stallPrice: 18, stallStock: 0))
                .WithStallTreasury(EconomyTuning.Default.StallTreasury + EconomyTuning.Default.Workshops.ConstructionCost);

            using var sim = LocalSimulation.Create(SimulationSettings.DefaultSeed);
            sim.Host.Respawn(new OutpostSession(OutpostSession.DefaultCrawlerCount, economy));
            var host = (IWorkshopHost)sim.Simulation;
            var workshops = sim.Simulation.Workshops;
            int still = workshops.IndexOf(StationKind.Still);
            Assert.True(still >= 0 && workshops.Build(host, still, 0) == WorkshopRules.Check.Ok, "The still is built, paid for from the treasury.");
            Assert.True(workshops.SetOrder(host, still, Salve, 2), "The still takes an order for two salves: its recipe is data.");

            int herbsTaken = 0, mostStocked = 0;
            for (long t = 1; t <= 400 && workshops[still].HasOrder; t++)
            {
                sim.Step(1);
                int herbs = sim.Simulation.ActiveStalls[0].StockOf(ItemType.MedicinalHerb);
                var step = workshops.Work(host, still);
                herbsTaken += herbs - sim.Simulation.ActiveStalls[0].StockOf(ItemType.MedicinalHerb);
                mostStocked = System.Math.Max(mostStocked, sim.Simulation.ActiveStalls[0].StockOf(Salve));
                Assert.NotEqual(CraftStep.Holding, step);
            }
            TestContext.Current.TestOutputHelper?.WriteLine($"made {workshops[still].Made}; herbs taken {herbsTaken}; the stall held at most {mostStocked} salves");
            Assert.Equal(2u, workshops[still].Made);
            Assert.False(workshops[still].HasOrder, "The order is done.");
            Assert.Equal(4, herbsTaken);
            Assert.True(mostStocked >= 1, "The salves went into the stall's stock.");
            Assert.Equal(0, sim.Simulation.IntentFaults);
        }

        [Fact]
        public void ADayWithGlowstoneOutOfTheEconomyHolds()
        {
            var economy = EconomyTuning.Default
                .WithItemTerms(ItemType.Glowstone, new ItemTerms(EconomyTuning.Default.BaseValue(ItemType.Glowstone)))
                .WithSupply(EconomyTuning.Default.Supply.With(new CaravanTerms(ItemType.Glowstone, CaravanRole.None, 0, 0)));
            using var sim = LocalSimulation.Create(SimulationSettings.DefaultSeed);
            sim.Host.Respawn(new OutpostSession(OutpostSession.DefaultCrawlerCount, economy));
            Assert.False(sim.Simulation.ActiveStalls[0].Trades(ItemType.Glowstone), "The stall doesn't trade glowstone.");

            var watch = new OutpostWatch(sim);
            watch.Run(9000);
            watch.Finish();
            TestContext.Current.TestOutputHelper?.WriteLine(watch.Report());
            Assert.False(sim.Simulation.ActiveStalls[0].Trades(ItemType.Glowstone), "Glowstone stayed out of the stall.");
            var failures = watch.DayFailures();
            Assert.True(failures.Count == 0, "The day failed:\n  " + string.Join("\n  ", failures));
        }
    }
}
