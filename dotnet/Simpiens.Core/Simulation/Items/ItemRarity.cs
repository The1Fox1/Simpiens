namespace Simpiens.Simulation.Items
{
    /// <summary>
    /// How rare an item is. Each tier has a score, the item's default weight in a loot table: lower is rarer
    /// (the item system refactor, 2026-10-26). Nothing in play reads it yet.
    /// </summary>
    public enum ItemRarity : byte
    {
        Common = 0,
        Uncommon = 1,
        Rare = 2,
        Epic = 3,
        Legendary = 4
    }

    public static class ItemRarityExtensions
    {
        /// <summary>The tier's default loot weight: 1000, 250, 50, 10, 2.</summary>
        public static uint Score(this ItemRarity rarity)
        {
            switch (rarity)
            {
                case ItemRarity.Common: return 1000;
                case ItemRarity.Uncommon: return 250;
                case ItemRarity.Rare: return 50;
                case ItemRarity.Epic: return 10;
                case ItemRarity.Legendary: return 2;
                default: return 0;
            }
        }
    }
}
