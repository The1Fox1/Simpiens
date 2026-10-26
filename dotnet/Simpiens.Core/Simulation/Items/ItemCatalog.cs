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

        /// <summary>The catalog's tables, built once and swapped whole (only by a test's <see cref="ExtendForTest"/>), so readers never see a half-built one.</summary>
        private sealed class Tables
        {
            public readonly ItemDefinition[] ById = new ItemDefinition[Capacity];
            public readonly List<ItemDefinition> All = new List<ItemDefinition>(16);
            public readonly ItemType[][] ByCategory = new ItemType[32][];

            public Tables(IEnumerable<ItemDefinition> definitions)
            {
                foreach (var definition in definitions)
                {
                    if (ById[(byte)definition.Id] != null) throw new System.ArgumentException($"Two items have id {(byte)definition.Id}.");
                    ById[(byte)definition.Id] = definition;
                    int index = All.Count;
                    while (index > 0 && All[index - 1].Id > definition.Id) index--;
                    All.Insert(index, definition);
                }
                // Each group's members in id order, built once: queries never allocate
                for (int bit = 0; bit < 32; bit++)
                {
                    var members = new List<ItemType>();
                    for (int i = 0; i < All.Count; i++)
                    {
                        if (((uint)All[i].Categories & (1u << bit)) != 0) members.Add(All[i].Id);
                    }
                    ByCategory[bit] = members.ToArray();
                }
            }
        }

        private static volatile Tables _tables = new Tables(Definitions());

        /// <summary>The game's items. Adding one: an <see cref="ItemType"/> value, a definition here, a sprite, and its nodes.</summary>
        private static ItemDefinition[] Definitions() => new[]
        {
            new ItemDefinition(ItemType.Ration, "Ration", "Ration",
                ItemCategory.Consumable | ItemCategory.Food, ItemRarity.Common, spriteKey: "Resource_BerryBush",
                consume: new[] { new ConsumeEffect(StatKind.Hunger, -40f) }),
            new ItemDefinition(ItemType.IronOre, "IronOre", "Iron Ore",
                ItemCategory.Resource | ItemCategory.Mineral, ItemRarity.Common, spriteKey: "Resource_OreVein"),
            new ItemDefinition(ItemType.MedicinalHerb, "MedicinalHerb", "Medicinal Herb",
                ItemCategory.Resource | ItemCategory.Botanical | ItemCategory.Consumable | ItemCategory.Medicine, ItemRarity.Common, spriteKey: "Resource_HerbPatch",
                consume: new[] { new ConsumeEffect(StatKind.Health, 30f) }),
            // No source until Epic 10 Part 2; nothing reads HarvestSpeed yet
            new ItemDefinition(ItemType.ToolPickaxe, "Pickaxe", "Pickaxe",
                ItemCategory.Equipment | ItemCategory.Tool | ItemCategory.Mineral, ItemRarity.Uncommon,
                equipment: new EquipmentBlock(EquipmentSlot.Tool, durability: 100, WearRule.PerUse,
                    new StatModifier(StatKind.HarvestSpeed, ModifierOp.Multiply, 1.5f))),
            // Halves hazard damage; lasts 500 ticks (50 s) in danger (Epic 10 G3)
            new ItemDefinition(ItemType.HazardGear, "HazardGear", "Hazard Gear",
                ItemCategory.Equipment | ItemCategory.Protection, ItemRarity.Uncommon,
                equipment: new EquipmentBlock(EquipmentSlot.Protection, durability: 500, WearRule.PerHazardTick,
                    new StatModifier(StatKind.HazardResistance, ModifierOp.Add, 0.5f))),
            new ItemDefinition(ItemType.Glowstone, "Glowstone", "Glowstone",
                ItemCategory.Resource | ItemCategory.Mineral | ItemCategory.Hazardous, ItemRarity.Rare, spriteKey: "Resource_Glowstone")
        };

        /// <summary>
        /// Tests only: the catalog with <paramref name="extra"/> added, until the returned scope is disposed (item system
        /// I8, the proof that an item needs no code beyond its definition). The tables are swapped whole, between ticks;
        /// tests that use it run serially (Simpiens.Core.Tests).
        /// </summary>
        internal static System.IDisposable ExtendForTest(params ItemDefinition[] extra)
        {
            var previous = _tables;
            var definitions = new List<ItemDefinition>(previous.All);
            definitions.AddRange(extra);
            _tables = new Tables(definitions);
            return new Restore(previous);
        }

        private sealed class Restore : System.IDisposable
        {
            private readonly Tables _previous;
            public Restore(Tables previous) => _previous = previous;
            public void Dispose() => _tables = _previous;
        }

        /// <summary>Every defined item, in id order.</summary>
        public static IReadOnlyList<ItemDefinition> All => _tables.All;

        public static bool IsDefined(ItemType id) => _tables.ById[(byte)id] != null;

        /// <summary>The item's definition, or null for <see cref="ItemType.None"/> and undefined ids.</summary>
        public static ItemDefinition Get(ItemType id) => _tables.ById[(byte)id];

        /// <summary>True when the item is defined and in every group of <paramref name="categories"/>.</summary>
        public static bool Is(ItemType id, ItemCategory categories)
        {
            var definition = _tables.ById[(byte)id];
            return definition != null && definition.Is(categories);
        }

        /// <summary>The items in one group (a single flag), in id order. Built once; never allocates.</summary>
        public static IReadOnlyList<ItemType> InCategory(ItemCategory category)
        {
            uint flag = (uint)category;
            if (flag == 0 || (flag & (flag - 1)) != 0) throw new System.ArgumentException($"One group at a time: {category}.", nameof(category));
            int bit = 0;
            while ((flag >> bit) != 1u) bit++;
            return _tables.ByCategory[bit];
        }

        /// <summary>A code name for logs: the definition's, or the raw id for an undefined one.</summary>
        public static string NameOf(ItemType id) => _tables.ById[(byte)id]?.Name ?? (id == ItemType.None ? "None" : $"Item{(byte)id}");
    }
}
