using System.Collections.Generic;
using Simpiens.Simulation.Economy;

namespace Simpiens.Simulation.Items
{
    /// <summary>
    /// Every item the game knows, by id (.agents/item_system_plan.md §3.2). What an item is doesn't vary by session, so
    /// the catalog is static and immutable: any code reads it, on any thread, without being handed it. How a session
    /// trades an item is its economy (EconomyTuning).
    /// <para>
    /// To add an item: an <see cref="ItemType"/> value (never reuse one), a definition below, a sprite, and the map nodes
    /// that yield it. To remove one: delete its definition and mark its id retired in <see cref="ItemType"/>; a save that
    /// still holds it is refused on load.
    /// </para>
    /// </summary>
    public static class ItemCatalog
    {
        public const int Capacity = 256;

        private static readonly ItemDefinition[] _byId = new ItemDefinition[Capacity];
        private static readonly List<ItemDefinition> _all = new List<ItemDefinition>(16);
        private static readonly ItemType[][] _byCategory = new ItemType[32][];

        static ItemCatalog()
        {
            Define(new ItemDefinition(ItemType.Ration, "Ration", "Ration",
                ItemCategory.Consumable | ItemCategory.Food, ItemRarity.Common, spriteKey: "Resource_BerryBush"));
            Define(new ItemDefinition(ItemType.IronOre, "IronOre", "Iron Ore",
                ItemCategory.Resource | ItemCategory.Mineral, ItemRarity.Common, spriteKey: "Resource_OreVein"));
            Define(new ItemDefinition(ItemType.MedicinalHerb, "MedicinalHerb", "Medicinal Herb",
                ItemCategory.Resource | ItemCategory.Botanical | ItemCategory.Consumable | ItemCategory.Medicine, ItemRarity.Common, spriteKey: "Resource_HerbPatch"));
            Define(new ItemDefinition(ItemType.ToolPickaxe, "Pickaxe", "Pickaxe",
                ItemCategory.Equipment | ItemCategory.Tool | ItemCategory.Mineral, ItemRarity.Uncommon)); // no source until Epic 10 Part 2
            Define(new ItemDefinition(ItemType.HazardGear, "HazardGear", "Hazard Gear",
                ItemCategory.Equipment | ItemCategory.Protection, ItemRarity.Uncommon));
            Define(new ItemDefinition(ItemType.Glowstone, "Glowstone", "Glowstone",
                ItemCategory.Resource | ItemCategory.Mineral | ItemCategory.Hazardous, ItemRarity.Rare, spriteKey: "Resource_Glowstone"));

            // Each group's members in id order, built once: queries never allocate
            for (int bit = 0; bit < 32; bit++)
            {
                var members = new List<ItemType>();
                for (int i = 0; i < _all.Count; i++)
                {
                    if (((uint)_all[i].Categories & (1u << bit)) != 0) members.Add(_all[i].Id);
                }
                _byCategory[bit] = members.ToArray();
            }
        }

        private static void Define(ItemDefinition definition)
        {
            _byId[(byte)definition.Id] = definition;
            int index = _all.Count;
            while (index > 0 && _all[index - 1].Id > definition.Id) index--;
            _all.Insert(index, definition);
        }

        /// <summary>Every defined item, in id order.</summary>
        public static IReadOnlyList<ItemDefinition> All => _all;

        public static bool IsDefined(ItemType id) => _byId[(byte)id] != null;

        /// <summary>The item's definition, or null for <see cref="ItemType.None"/> and undefined ids.</summary>
        public static ItemDefinition Get(ItemType id) => _byId[(byte)id];

        /// <summary>True when the item is defined and in every group of <paramref name="categories"/>.</summary>
        public static bool Is(ItemType id, ItemCategory categories)
        {
            var definition = _byId[(byte)id];
            return definition != null && definition.Is(categories);
        }

        /// <summary>The items in one group (a single flag), in id order. Built once; never allocates.</summary>
        public static IReadOnlyList<ItemType> InCategory(ItemCategory category)
        {
            uint flag = (uint)category;
            if (flag == 0 || (flag & (flag - 1)) != 0) throw new System.ArgumentException($"One group at a time: {category}.", nameof(category));
            int bit = 0;
            while ((flag >> bit) != 1u) bit++;
            return _byCategory[bit];
        }

        /// <summary>A code name for logs: the definition's, or the raw id for an undefined one.</summary>
        public static string NameOf(ItemType id) => _byId[(byte)id]?.Name ?? (id == ItemType.None ? "None" : $"Item{(byte)id}");
    }
}
