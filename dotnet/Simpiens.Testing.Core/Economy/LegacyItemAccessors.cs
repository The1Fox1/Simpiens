// Test conveniences for today's items (item system I5). Production code trades through tables (a stall's goods, an
// economy's item terms, the caravan's terms) and names no item; validators keep the per-item names they were written
// with. These live in the production namespace so validators see them without a using, and in the test assembly so
// production code can't: Simpiens.Core, Presentation, Client and Hosting don't reference Simpiens.Testing.Core.
using Simpiens.Intervention;

namespace Simpiens.Simulation.Economy
{
    /// <summary>A stall's goods by today's names (tests only).</summary>
    public static class TradeStallTestAccessors
    {
        extension(TradeStallData stall)
        {
            public ushort RationSellPrice => stall.SellPriceOf(ItemType.Ration);
            public ushort RationStock => (ushort)stall.StockOf(ItemType.Ration);
            public ushort OreBuyPrice => stall.BuyPriceOf(ItemType.IronOre);
            public ushort OreStock => (ushort)stall.StockOf(ItemType.IronOre);
            public ushort HerbBuyPrice => stall.BuyPriceOf(ItemType.MedicinalHerb);
            public ushort HerbStock => (ushort)stall.StockOf(ItemType.MedicinalHerb);
            public ushort GlowstoneBuyPrice => stall.BuyPriceOf(ItemType.Glowstone);
            public ushort GlowstoneStock => (ushort)stall.StockOf(ItemType.Glowstone);
            public ushort HazardGearSellPrice => stall.SellPriceOf(ItemType.HazardGear);
            public ushort HazardGearStock => (ushort)stall.StockOf(ItemType.HazardGear);

            public TradeStallData WithRationStock(ushort stock) => stall.WithStock(ItemType.Ration, stock);
            public TradeStallData WithOreStock(ushort stock) => stall.WithStock(ItemType.IronOre, stock);
            public TradeStallData WithHerbStock(ushort stock) => stall.WithStock(ItemType.MedicinalHerb, stock);
            public TradeStallData WithGlowstoneStock(ushort stock) => stall.WithStock(ItemType.Glowstone, stock);
            public TradeStallData WithHazardGearStock(ushort stock) => stall.WithStock(ItemType.HazardGear, stock);
            public TradeStallData WithStock(ushort rationStock, ushort oreStock, ushort herbStock) =>
                stall.WithStock(ItemType.Ration, rationStock).WithStock(ItemType.IronOre, oreStock).WithStock(ItemType.MedicinalHerb, herbStock);
            public TradeStallData WithPrices(ushort rationSellPrice, ushort oreBuyPrice, ushort herbBuyPrice) =>
                stall.WithPrice(ItemType.Ration, rationSellPrice).WithPrice(ItemType.IronOre, oreBuyPrice).WithPrice(ItemType.MedicinalHerb, herbBuyPrice);
            public TradeStallData WithGlowstone(ushort buyPrice, ushort stock) => stall.WithGood(new StallGood(ItemType.Glowstone, StallRole.Buys, buyPrice, stock));
            public TradeStallData WithHazardGear(ushort sellPrice, ushort stock) => stall.WithGood(new StallGood(ItemType.HazardGear, StallRole.Sells, sellPrice, stock));
        }
    }

    /// <summary>A stall built with today's goods by name, as TradeStallData's constructor took them before I5 (tests only).</summary>
    public static class TestStalls
    {
        public static TradeStallData Stall(
            EntityId stallEntityId,
            Vec2 position,
            ushort rationSellPrice = 5,
            ushort rationStock = 50,
            ushort oreBuyPrice = 8,
            ushort oreStock = 0,
            ushort herbBuyPrice = 12,
            ushort herbStock = 0,
            uint treasuryGold = 500,
            float interactionRadius = 2.5f,
            bool inArrears = false,
            ushort glowstoneBuyPrice = 0,
            ushort glowstoneStock = 0,
            ushort hazardGearSellPrice = 0,
            ushort hazardGearStock = 0)
        {
            return new TradeStallData(stallEntityId, position, treasuryGold, interactionRadius, inArrears)
                .WithGood(new StallGood(ItemType.Ration, StallRole.Sells, rationSellPrice, rationStock))
                .WithGood(new StallGood(ItemType.IronOre, StallRole.Buys, oreBuyPrice, oreStock))
                .WithGood(new StallGood(ItemType.MedicinalHerb, StallRole.Buys, herbBuyPrice, herbStock))
                .WithGood(new StallGood(ItemType.HazardGear, StallRole.Sells, hazardGearSellPrice, hazardGearStock))
                .WithGood(new StallGood(ItemType.Glowstone, StallRole.Buys, glowstoneBuyPrice, glowstoneStock));
        }
    }

    /// <summary>An economy's per-item terms by today's names (tests only).</summary>
    public static class EconomyTuningTestAccessors
    {
        extension(EconomyTuning economy)
        {
            public ushort StallRationPrice => economy.Terms(ItemType.Ration).StallPrice;
            public ushort StallRationStock => economy.Terms(ItemType.Ration).StallStock;
            public ushort StallOreBuyPrice => economy.Terms(ItemType.IronOre).StallPrice;
            public ushort StallOreStock => economy.Terms(ItemType.IronOre).StallStock;
            public ushort StallHerbBuyPrice => economy.Terms(ItemType.MedicinalHerb).StallPrice;
            public ushort StallHerbStock => economy.Terms(ItemType.MedicinalHerb).StallStock;
            public ushort StallGlowstoneBuyPrice => economy.Terms(ItemType.Glowstone).StallPrice;
            public ushort StallHazardGearPrice => economy.Terms(ItemType.HazardGear).StallPrice;
            public ushort StallHazardGearStock => economy.Terms(ItemType.HazardGear).StallStock;

            public EconomyTuning WithStallTerms(ushort rationPrice, ushort oreBuyPrice, ushort herbBuyPrice, uint treasury) => economy
                .WithItemTerms(ItemType.Ration, economy.Terms(ItemType.Ration).WithStallPrice(rationPrice))
                .WithItemTerms(ItemType.IronOre, economy.Terms(ItemType.IronOre).WithStallPrice(oreBuyPrice))
                .WithItemTerms(ItemType.MedicinalHerb, economy.Terms(ItemType.MedicinalHerb).WithStallPrice(herbBuyPrice))
                .WithStallTreasury(treasury);

            public EconomyTuning WithBaseValues(ushort ration, ushort ore, ushort herb) => economy
                .WithItemTerms(ItemType.Ration, economy.Terms(ItemType.Ration).WithBaseValue(ration))
                .WithItemTerms(ItemType.IronOre, economy.Terms(ItemType.IronOre).WithBaseValue(ore))
                .WithItemTerms(ItemType.MedicinalHerb, economy.Terms(ItemType.MedicinalHerb).WithBaseValue(herb));
        }
    }

    /// <summary>The caravan's per-item terms by today's names (tests only).</summary>
    public static class SupplyTuningTestAccessors
    {
        extension(SupplyTuning supply)
        {
            public ushort RationStockCap => supply.Terms(ItemType.Ration).Limit;
            public ushort WholesaleRationCost => supply.Terms(ItemType.Ration).Price;
            public ushort OreQuota => supply.Terms(ItemType.IronOre).Limit;
            public ushort OreMarketPrice => supply.Terms(ItemType.IronOre).Price;
            public ushort HerbQuota => supply.Terms(ItemType.MedicinalHerb).Limit;
            public ushort HerbMarketPrice => supply.Terms(ItemType.MedicinalHerb).Price;
            public ushort GlowstoneQuota => supply.Terms(ItemType.Glowstone).Limit;
            public ushort GlowstoneMarketPrice => supply.Terms(ItemType.Glowstone).Price;
            public ushort GearStockCap => supply.Terms(ItemType.HazardGear).Limit;
            public ushort WholesaleGearCost => supply.Terms(ItemType.HazardGear).Price;
        }
    }

    /// <summary>Caravan terms built by today's names, as SupplyTuning's constructor took them before I5 (tests only).</summary>
    public static class TestSupply
    {
        public static SupplyTuning Tuning(uint restockIntervalTicks, ushort rationStockCap, ushort wholesaleRationCost,
            ushort oreQuota = 0, ushort herbQuota = 0, ushort oreMarketPrice = 0, ushort herbMarketPrice = 0, ushort upkeepPerVisit = 0,
            ushort glowstoneQuota = 0, ushort glowstoneMarketPrice = 0, ushort gearStockCap = 0, ushort wholesaleGearCost = 0)
        {
            return new SupplyTuning(restockIntervalTicks, upkeepPerVisit,
                CaravanTerms.Supplies(ItemType.Ration, rationStockCap, wholesaleRationCost, SupplyService.CaravanSinkName),
                CaravanTerms.Exports(ItemType.IronOre, oreQuota, oreMarketPrice),
                CaravanTerms.Exports(ItemType.MedicinalHerb, herbQuota, herbMarketPrice),
                CaravanTerms.Supplies(ItemType.HazardGear, gearStockCap, wholesaleGearCost, SupplyService.GearSinkName),
                CaravanTerms.Exports(ItemType.Glowstone, glowstoneQuota, glowstoneMarketPrice));
        }
    }

    /// <summary>A price command by today's names, as UpdateStallPricesCommand's constructor took them before I5 (tests only).</summary>
    public static class TestPrices
    {
        public static UpdateStallPricesCommand Command(EntityId stallId, ushort rationSellPrice, ushort oreBuyPrice, ushort herbBuyPrice,
            ushort glowstoneBuyPrice = 0, ushort hazardGearSellPrice = 0)
        {
            return new UpdateStallPricesCommand(stallId).With(ItemType.Ration, rationSellPrice).With(ItemType.IronOre, oreBuyPrice)
                .With(ItemType.MedicinalHerb, herbBuyPrice).With(ItemType.Glowstone, glowstoneBuyPrice).With(ItemType.HazardGear, hazardGearSellPrice);
        }
    }
}
