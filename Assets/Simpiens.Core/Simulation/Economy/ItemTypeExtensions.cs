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

        // Item base values live in EconomyTuning.BaseValue (one economy tuning per session)
    }
}
