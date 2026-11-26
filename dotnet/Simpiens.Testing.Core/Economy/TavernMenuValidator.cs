using System;
using System.Collections.Generic;
using Simpiens.Simulation;
using Simpiens.Simulation.Economy;
using Simpiens.Simulation.Items;
using Simpiens.Simulation.Life;
using Simpiens.Simulation.World;
using Simpiens.Testing.Environment;

namespace Simpiens.Testing.Economy
{
    /// <summary>
    /// The tavern's menu (Epic LIFE-1 plan 2, T.3, D18, D19): ale and stew are served goods, in no group anyone buys or eats by;
    /// the ale and the supper cost them; the stall sells them at 4g and 10g; the caravan brings a visit's worth (12 ale at 2g,
    /// 8 stew at 4g, into <c>TavernWholesale</c>) only while a tavern stands; the tavern is a station kind with a keeper and
    /// no recipes, built for 200g. Runs as a test (ValidatorTests).
    /// </summary>
    public class TavernMenuValidator
    {
        public void RunAllValidationTests()
        {
            TestLog.Log("[TavernMenuValidator] Beginning tavern menu validation...");
            ValidateTheMenu();
            ValidateTheCaravanWaitsForATavern();
            TestLog.Log("<color=green>[TavernMenuValidator] ALL 2 TESTS PASSED SUCCESSFULLY!</color>");
        }

        private void ValidateTheMenu()
        {
            foreach (var item in new[] { ItemType.Ale, ItemType.Stew })
            {
                Assert(ItemCatalog.Is(item, ItemCategory.Served), $"{item}: served.");
                foreach (var group in new[] { ItemCategory.Food, ItemCategory.Consumable, ItemCategory.Equipment, ItemCategory.Manual, ItemCategory.Medicine, ItemCategory.Resource })
                    Assert(!ItemCatalog.Is(item, group), $"{item}: not {group}, so nobody buys it to carry or eats it from a pack.");
            }
            Assert(ActivityTable.Get(Activity.Ale).Cost == ItemType.Ale && ActivityTable.Get(Activity.Supper).Cost == ItemType.Stew, "An ale costs an ale; a supper a stew.");
            var economy = EconomyTuning.Default;
            Assert(economy.Terms(ItemType.Ale).StallRole == StallRole.Sells && economy.Terms(ItemType.Ale).StallPrice == 4 && economy.Terms(ItemType.Ale).StallStock == 0, "The stall sells ale at 4g, none to start.");
            Assert(economy.Terms(ItemType.Stew).StallRole == StallRole.Sells && economy.Terms(ItemType.Stew).StallPrice == 10 && economy.Terms(ItemType.Stew).StallStock == 0, "And stew at 10g.");
            var ale = economy.Supply.Terms(ItemType.Ale);
            Assert(ale.Role == CaravanRole.Supplies && ale.Limit == 12 && ale.Price == 2 && ale.FlowName == SupplyService.TavernSinkName && ale.ForStation == StationKind.Tavern, "The caravan brings 12 ale at 2g, for a tavern.");
            var row = StationTable.Get(StationKind.Tavern);
            Assert(row.TakesArtisan && !row.HasRecipes && row.Cost == StationCost.Tavern && economy.Workshops.CostOf(StationKind.Tavern) == 200, "The tavern: a keeper, no recipes, 200g.");
        }

        private void ValidateTheCaravanWaitsForATavern()
        {
            var map = new MapDefinitionBuilder("TavernMenu", new RectF(-10f, -10f, 20f, 20f))
                .Sector(1, "Room", "A room.", new RectF(-10f, -10f, 20f, 20f))
                .Station("tavern", StationKind.Tavern, new Vec2(2f, 2f), 1.5f)
                .Build();
            var world = new WorldRegistry();
            var ledger = new EconomyLedger();
            var sectors = new SectorRegistry();
            sectors.Load(map);
            var sim = new SimulationManager(world, null, sectors: sectors, economyLedger: ledger, supplyService: new SupplyService());
            sim.SetEconomy(EconomyTuning.Default);
            var goods = new List<StallGood>(TradeStallData.GoodsCapacity);
            EconomyTuning.Default.StallGoods(goods);
            var stall = new TradeStallData(EntityId.Generate(), Vec2.Zero, 1000, 2f);
            foreach (var good in goods) stall = stall.WithGood(good);
            sim.RegisterTradeStall(stall);
            ledger.RecordFaucet(1000, "InitialSeed");
            uint interval = EconomyTuning.Default.Supply.RestockIntervalTicks;

            sim.Tick(interval);
            Assert(sim.ActiveStalls[0].SellStockOf(ItemType.Ale) == 0 && ledger.GetSinkTotal(SupplyService.TavernSinkName) == 0, "With no tavern, the caravan brings no ale.");

            Assert(sim.Workshops.Build(sim, 0, (uint)sim.CurrentTick) == Simpiens.Simulation.Workshops.WorkshopRules.Check.Ok, "The tavern is built.");
            sim.Tick(interval);
            var after = sim.ActiveStalls[0];
            Assert(after.SellStockOf(ItemType.Ale) == 12 && after.SellStockOf(ItemType.Stew) == 8, $"With a tavern, a visit's 12 ale and 8 stew ({after.SellStockOf(ItemType.Ale)}, {after.SellStockOf(ItemType.Stew)}).");
            Assert(ledger.GetSinkTotal(SupplyService.TavernSinkName) == 12u * 2 + 8u * 4, $"Paid into {SupplyService.TavernSinkName} ({ledger.GetSinkTotal(SupplyService.TavernSinkName)}g).");
            Assert(ledger.ValidateConservation(after.TreasuryGold, 0, out int discrepancy), $"The ledger balances ({discrepancy}g).");
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("[TavernMenuValidator] FAILED: " + message);
        }
    }
}
