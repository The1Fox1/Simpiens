using Simpiens.Simulation.Items;

namespace Simpiens.Simulation.Economy
{
    /// <summary>
    /// Group checks for item types, read from <see cref="ItemCatalog"/> (no item lists here).
    /// <see cref="ItemType.None"/> is treated as food so legacy, untyped resource records keep their meaning.
    /// </summary>
    public static class ItemTypeExtensions
    {
        public static bool IsFoodYield(this ItemType type) => type == ItemType.None || ItemCatalog.Is(type, ItemCategory.Food);

        /// <summary>
        /// What crawlers gather to sell: a resource. Whether the stall buys it is checked where it trades (its goods table);
        /// "a resource the stall buys" as the definition is deferred (.agents/core_refactor_plan.md, R7).
        /// </summary>
        public static bool IsSalvage(this ItemType type) => ItemCatalog.Is(type, ItemCategory.Resource);

        // Item base values live in EconomyTuning.BaseValue (one economy tuning per session)
    }
}
