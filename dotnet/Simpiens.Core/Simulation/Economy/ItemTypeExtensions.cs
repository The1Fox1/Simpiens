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
        /// What crawlers gather to sell: a resource. (The item plan's I5 makes it "a resource the stall buys", from the
        /// session's economy.)
        /// </summary>
        public static bool IsSalvage(this ItemType type) => ItemCatalog.Is(type, ItemCategory.Resource);

        // Item base values live in EconomyTuning.BaseValue (one economy tuning per session)
    }
}
