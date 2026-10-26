using Simpiens.Simulation.Items;

namespace Simpiens.Simulation.Economy
{
    /// <summary>What the supply caravan does with an item: nothing, buys it from the stall (exports), or sells the stall more (supplies).</summary>
    public enum CaravanRole : byte
    {
        None = 0,
        Exports = 1,
        Supplies = 2
    }

    /// <summary>
    /// The caravan's terms for one item (item system I5). Exports: it buys up to <see cref="Limit"/> units a visit at
    /// <see cref="Price"/>, the "CaravanSalvageSales" faucet. Supplies: it tops the stall's stock up to
    /// <see cref="Limit"/> at <see cref="Price"/> a unit, paid from the treasury under the ledger flow <see cref="FlowName"/>.
    /// </summary>
    public readonly struct CaravanTerms
    {
        public readonly ItemType Item;
        public readonly CaravanRole Role;
        public readonly ushort Limit;
        public readonly ushort Price;
        public readonly string FlowName;

        public CaravanTerms(ItemType item, CaravanRole role, ushort limit, ushort price, string flowName = null)
        {
            Item = item;
            Role = role;
            Limit = limit;
            Price = price;
            FlowName = flowName;
        }

        public static CaravanTerms Exports(ItemType item, ushort quota, ushort marketPrice) => new CaravanTerms(item, CaravanRole.Exports, quota, marketPrice);
        public static CaravanTerms Supplies(ItemType item, ushort stockCap, ushort wholesaleCost, string flowName) => new CaravanTerms(item, CaravanRole.Supplies, stockCap, wholesaleCost, flowName);
    }

    /// <summary>
    /// The outpost's periodic economy (part of EconomyTuning): the supply caravan and the upkeep paid on its schedule.
    /// All durations are in logical simulation ticks (10 Hz). On each visit, in this order: the caravan buys what it
    /// exports from the stall, up to each quota (the "CaravanSalvageSales" faucet, review §3.3 D1), the stall pays its
    /// upkeep (the "OutpostUpkeep" sink, D2), and the caravan tops up what it supplies, food first, then the rest in id
    /// order (rations under "SupplyCaravan", hazard gear under "GearWholesale" since Epic 10 G3). Its terms are a table
    /// by item (item system I5), not a field per item.
    /// </summary>
    public readonly struct SupplyTuning
    {
        /// <summary>Ticks between supply caravan visits (600 = 60 s).</summary>
        public readonly uint RestockIntervalTicks;

        /// <summary>Gold the stall pays per visit to keep the outpost running; leaves the economy as "OutpostUpkeep".</summary>
        public readonly ushort UpkeepPerVisit;

        private readonly CaravanTerms[] _items;

        public SupplyTuning(uint restockIntervalTicks, ushort upkeepPerVisit = 0, params CaravanTerms[] items)
        {
            RestockIntervalTicks = restockIntervalTicks;
            UpkeepPerVisit = upkeepPerVisit;
            _items = new CaravanTerms[ItemCatalog.Capacity];
            if (items != null)
            {
                for (int i = 0; i < items.Length; i++) _items[(byte)items[i].Item] = items[i];
            }
        }

        /// <summary>The caravan's terms for an item (role None when it doesn't trade it).</summary>
        public CaravanTerms Terms(ItemType item) => _items != null ? _items[(byte)item] : default;

        /// <summary>A copy with one item's terms set (role None stops the caravan trading it).</summary>
        public SupplyTuning With(in CaravanTerms terms)
        {
            var copy = new SupplyTuning(RestockIntervalTicks, UpkeepPerVisit);
            if (_items != null) System.Array.Copy(_items, copy._items, _items.Length);
            copy._items[(byte)terms.Item] = terms;
            return copy;
        }

        /// <summary>Market prices sit about 20% above the stall's default buy prices (ore 20g, herb 25g).</summary>
        public static SupplyTuning Default => new SupplyTuning(restockIntervalTicks: 600, upkeepPerVisit: 80,
            CaravanTerms.Supplies(ItemType.Ration, stockCap: 50, wholesaleCost: 2, SupplyService.CaravanSinkName),
            CaravanTerms.Exports(ItemType.IronOre, quota: 2, marketPrice: 24),
            CaravanTerms.Exports(ItemType.MedicinalHerb, quota: 2, marketPrice: 30),
            CaravanTerms.Supplies(ItemType.HazardGear, stockCap: 3, wholesaleCost: 20, SupplyService.GearSinkName),
            CaravanTerms.Exports(ItemType.Glowstone, quota: 2, marketPrice: 70));

        /// <summary>The most gold the caravan can pay a stall for its exports in one visit.</summary>
        public uint MaxSalvagePaymentPerVisit
        {
            get
            {
                uint total = 0;
                var all = ItemCatalog.All;
                for (int i = 0; i < all.Count; i++)
                {
                    var terms = Terms(all[i].Id);
                    if (terms.Role == CaravanRole.Exports) total += (uint)terms.Limit * terms.Price;
                }
                return total;
            }
        }
    }
}
