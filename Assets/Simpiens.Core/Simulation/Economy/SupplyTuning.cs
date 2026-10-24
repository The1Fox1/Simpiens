namespace Simpiens.Simulation.Economy
{
    /// <summary>
    /// The outpost's periodic economy (part of EconomyTuning): the supply caravan and the upkeep paid on its schedule.
    /// All durations are in logical simulation ticks (10 Hz). On each visit, in this order: the caravan buys salvage from
    /// the stall up to a quota per type (the "CaravanSalvageSales" faucet, review §3.3 D1), the stall pays its upkeep
    /// (the "OutpostUpkeep" sink, D2), and the caravan tops the stall's rations up to the cap (the "SupplyCaravan" sink).
    /// </summary>
    public readonly struct SupplyTuning
    {
        /// <summary>Ticks between supply caravan visits (600 = 60 s).</summary>
        public readonly uint RestockIntervalTicks;

        /// <summary>Caravan tops each stall's ration stock up to this cap.</summary>
        public readonly ushort RationStockCap;

        /// <summary>Gold per ration the stall pays the caravan; leaves the economy as the "SupplyCaravan" sink.</summary>
        public readonly ushort WholesaleRationCost;

        /// <summary>Most Iron Ore units the caravan buys from a stall per visit.</summary>
        public readonly ushort OreQuota;

        /// <summary>Most Medicinal Herb units the caravan buys from a stall per visit.</summary>
        public readonly ushort HerbQuota;

        /// <summary>Gold per Iron Ore unit the caravan pays; enters the economy as the "CaravanSalvageSales" faucet.</summary>
        public readonly ushort OreMarketPrice;

        /// <summary>Gold per Medicinal Herb unit the caravan pays.</summary>
        public readonly ushort HerbMarketPrice;

        /// <summary>Gold the stall pays per visit to keep the outpost running; leaves the economy as "OutpostUpkeep".</summary>
        public readonly ushort UpkeepPerVisit;

        /// <summary>Most Glowstone units the caravan buys per visit, and what it pays: the rare salvage's premium (Epic 10 G2).</summary>
        public readonly ushort GlowstoneQuota;
        public readonly ushort GlowstoneMarketPrice;

        public SupplyTuning(uint restockIntervalTicks, ushort rationStockCap, ushort wholesaleRationCost)
            : this(restockIntervalTicks, rationStockCap, wholesaleRationCost, 0, 0, 0, 0, 0)
        {
        }

        public SupplyTuning(uint restockIntervalTicks, ushort rationStockCap, ushort wholesaleRationCost,
            ushort oreQuota, ushort herbQuota, ushort oreMarketPrice, ushort herbMarketPrice, ushort upkeepPerVisit = 0,
            ushort glowstoneQuota = 0, ushort glowstoneMarketPrice = 0)
        {
            GlowstoneQuota = glowstoneQuota;
            GlowstoneMarketPrice = glowstoneMarketPrice;
            RestockIntervalTicks = restockIntervalTicks;
            RationStockCap = rationStockCap;
            WholesaleRationCost = wholesaleRationCost;
            OreQuota = oreQuota;
            HerbQuota = herbQuota;
            OreMarketPrice = oreMarketPrice;
            HerbMarketPrice = herbMarketPrice;
            UpkeepPerVisit = upkeepPerVisit;
        }

        /// <summary>Market prices sit about 20% above the stall's default buy prices (ore 20g, herb 25g).</summary>
        public static SupplyTuning Default => new SupplyTuning(restockIntervalTicks: 600, rationStockCap: 50, wholesaleRationCost: 2,
            oreQuota: 2, herbQuota: 2, oreMarketPrice: 24, herbMarketPrice: 30, upkeepPerVisit: 80,
            glowstoneQuota: 2, glowstoneMarketPrice: 70);

        /// <summary>The most gold the caravan can pay a stall for salvage in one visit.</summary>
        public uint MaxSalvagePaymentPerVisit => (uint)OreQuota * OreMarketPrice + (uint)HerbQuota * HerbMarketPrice + (uint)GlowstoneQuota * GlowstoneMarketPrice;
    }
}
