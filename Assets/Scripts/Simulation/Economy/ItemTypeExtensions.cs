namespace Simpiens.Simulation.Economy
{
    /// <summary>
    /// Classification helpers for resource node yields.
    /// <see cref="ItemType.None"/> is treated as food so legacy, untyped resource records keep their meaning.
    /// </summary>
    public static class ItemTypeExtensions
    {
        public static bool IsFoodYield(this ItemType type) => type == ItemType.None || type == ItemType.Ration;

        public static bool IsSalvage(this ItemType type) => type == ItemType.IronOre || type == ItemType.MedicinalHerb;

        /// <summary>
        /// Nominal per-unit value stamped onto harvested item instances.
        /// </summary>
        public static ushort BaseValue(this ItemType type)
        {
            switch (type)
            {
                case ItemType.Ration: return 5;
                case ItemType.IronOre: return 8;
                case ItemType.MedicinalHerb: return 12;
                case ItemType.ToolPickaxe: return 30;
                case ItemType.BasicWeapon: return 40;
                default: return 0;
            }
        }
    }
}
