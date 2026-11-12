using System.Collections.Generic;
using Simpiens.Simulation.Economy;

namespace Simpiens.Simulation.Items
{
    /// <summary>
    /// Every item the game knows, by id (the item system refactor, 2026-10-26). What an item is doesn't vary by session, so
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

        /// <summary>The game's items. Adding one: an <see cref="ItemType"/> value, a definition here, its art (by its art key), and its nodes.</summary>
        private static ItemDefinition[] Definitions() => new[]
        {
            new ItemDefinition(ItemType.Ration, "Ration", "Ration",
                ItemCategory.Consumable | ItemCategory.Food, ItemRarity.Common,
                consume: new[] { new ConsumeEffect(StatKind.Hunger, -40f) }),
            new ItemDefinition(ItemType.IronOre, "IronOre", "Iron Ore",
                ItemCategory.Resource | ItemCategory.Mineral, ItemRarity.Common),
            new ItemDefinition(ItemType.MedicinalHerb, "MedicinalHerb", "Medicinal Herb",
                ItemCategory.Resource | ItemCategory.Botanical | ItemCategory.Consumable | ItemCategory.Medicine, ItemRarity.Common,
                consume: new[] { new ConsumeEffect(StatKind.Health, 30f) }),
            new ItemDefinition(ItemType.ToolPickaxe, "Pickaxe", "Pickaxe",
                ItemCategory.Equipment | ItemCategory.Tool | ItemCategory.Mineral, ItemRarity.Uncommon,
                // Mines 1.5 times as fast and wears by the mineral unit (Epic 10 Part 2, D36, D37): 40 units, about a week of a
                // miner's work while the stall's ore shelf sets how much it mines, so the forge sees its buyers again
                equipment: new EquipmentBlock(SlotKind.Tool, durability: 40, WearRule.PerHarvest,
                    new StatModifier(StatKind.HarvestSpeed, ModifierOp.Multiply, 1.5f))),
            // Halves hazard damage; lasts 1,500 ticks in danger, about 3.6 game hours (Epic 10 G3; tripled with the 24-hour day's
            // longer ventures, Epic 10 Part 2, T3.2a)
            new ItemDefinition(ItemType.HazardGear, "HazardGear", "Hazard Gear",
                ItemCategory.Equipment | ItemCategory.Protection, ItemRarity.Uncommon,
                equipment: new EquipmentBlock(SlotKind.Body, durability: 1500, WearRule.PerHazardTick,
                    new StatModifier(StatKind.HazardResistance, ModifierOp.Add, 0.5f))),
            new ItemDefinition(ItemType.Glowstone, "Glowstone", "Glowstone",
                ItemCategory.Resource | ItemCategory.Mineral | ItemCategory.Hazardous, ItemRarity.Rare),
            // Made at the still from two herbs; heals 80 at once (Epic 10 Part 2, W2, Q5, D42). Medicine, so the herb reflex
            // drinks it under the treat line; not salvage: the stall doesn't buy it back
            new ItemDefinition(ItemType.HealingDraught, "HealingDraught", "Healing Draught",
                ItemCategory.Consumable | ItemCategory.Medicine, ItemRarity.Uncommon, maxStack: 5,
                consume: new[] { new ConsumeEffect(StatKind.Health, 80f) }),
            // Protection by kind (Epic 10 Part 2, W3, Q5, D47): each cuts its own hazards by three quarters, where hazard gear
            // cuts any by half; worn on the head (the respirator as a mask; Epic 10 Part 3, Q11) and worn out by exposure,
            // as hazard gear is on the body
            new ItemDefinition(ItemType.Respirator, "Respirator", "Respirator",
                ItemCategory.Equipment | ItemCategory.Protection, ItemRarity.Rare,
                equipment: new EquipmentBlock(SlotKind.Head, durability: 1500, WearRule.PerHazardTick,
                    new StatModifier(StatKind.AirResistance, ModifierOp.Add, 0.75f))),
            new ItemDefinition(ItemType.HardHat, "HardHat", "Hard Hat",
                ItemCategory.Equipment | ItemCategory.Protection, ItemRarity.Rare,
                equipment: new EquipmentBlock(SlotKind.Head, durability: 1500, WearRule.PerHazardTick,
                    new StatModifier(StatKind.RockResistance, ModifierOp.Add, 0.75f))),
            // Melee weapons (Epic 10 Part 3, K3.1, Q10), in the weapon slot, on the bare hand's 2 attack a swing every 1.5 s
            // (the combat rule's D2); each wears by the blow landed. Placeholder numbers, tuned by K3's fights
            new ItemDefinition(ItemType.Cudgel, "Cudgel", "Cudgel",
                ItemCategory.Equipment | ItemCategory.Weapon | ItemCategory.Botanical, ItemRarity.Common,
                equipment: new EquipmentBlock(SlotKind.Weapon, durability: 80, WearRule.PerHit,
                    new StatModifier(StatKind.Attack, ModifierOp.Add, 3f),
                    new StatModifier(StatKind.Reach, ModifierOp.Add, 0.1f))),
            new ItemDefinition(ItemType.IronBlade, "IronBlade", "Iron Blade",
                ItemCategory.Equipment | ItemCategory.Weapon | ItemCategory.Mineral, ItemRarity.Uncommon,
                equipment: new EquipmentBlock(SlotKind.Weapon, durability: 120, WearRule.PerHit,
                    new StatModifier(StatKind.Attack, ModifierOp.Add, 8f), // +8 since K3.3's fights (D21): a lone beetle falls to it
                    new StatModifier(StatKind.AttackInterval, ModifierOp.Multiply, 0.9f))),
            new ItemDefinition(ItemType.GlowstoneEdge, "GlowstoneEdge", "Glowstone Edge",
                ItemCategory.Equipment | ItemCategory.Weapon | ItemCategory.Mineral, ItemRarity.Rare,
                equipment: new EquipmentBlock(SlotKind.Weapon, durability: 160, WearRule.PerHit,
                    new StatModifier(StatKind.Attack, ModifierOp.Add, 12f),
                    new StatModifier(StatKind.AttackInterval, ModifierOp.Multiply, 0.9f))),
            // Armour from ore (K3.2, Q11): defense cuts a blow by D / (D + 10) (the combat rule's D4), and wears by the blow taken
            new ItemDefinition(ItemType.IronHelm, "IronHelm", "Iron Helm",
                ItemCategory.Equipment | ItemCategory.Protection | ItemCategory.Mineral, ItemRarity.Uncommon,
                equipment: new EquipmentBlock(SlotKind.Head, durability: 80, WearRule.PerBlowTaken,
                    new StatModifier(StatKind.Defense, ModifierOp.Add, 3f))),
            new ItemDefinition(ItemType.RingCoat, "RingCoat", "Ring Coat",
                ItemCategory.Equipment | ItemCategory.Protection | ItemCategory.Mineral, ItemRarity.Uncommon,
                equipment: new EquipmentBlock(SlotKind.Body, durability: 120, WearRule.PerBlowTaken,
                    new StatModifier(StatKind.Defense, ModifierOp.Add, 6f))),
            // Materials from kills (Epic 10 Part 3, K4.2, Q12): salvage the stall buys and the forge works (K4.4)
            new ItemDefinition(ItemType.Hide, "Hide", "Hide", ItemCategory.Resource | ItemCategory.Creature, ItemRarity.Common),
            new ItemDefinition(ItemType.Chitin, "Chitin", "Chitin", ItemCategory.Resource | ItemCategory.Creature, ItemRarity.Uncommon),
            // Armour from the materials (K4.4): the jerkin between the helm and the ring coat, the plate above it
            new ItemDefinition(ItemType.LeatherJerkin, "LeatherJerkin", "Leather Jerkin",
                ItemCategory.Equipment | ItemCategory.Protection | ItemCategory.Creature, ItemRarity.Common,
                equipment: new EquipmentBlock(SlotKind.Body, durability: 90, WearRule.PerBlowTaken,
                    new StatModifier(StatKind.Defense, ModifierOp.Add, 4f))),
            new ItemDefinition(ItemType.ChitinPlate, "ChitinPlate", "Chitin Plate",
                ItemCategory.Equipment | ItemCategory.Protection | ItemCategory.Creature, ItemRarity.Rare,
                equipment: new EquipmentBlock(SlotKind.Body, durability: 160, WearRule.PerBlowTaken,
                    new StatModifier(StatKind.Defense, ModifierOp.Add, 9f))),
            // Rare pieces (K4.2, Q11: fixed items with a rarity, never random affixes), now and then at a kill
            new ItemDefinition(ItemType.FangKnife, "FangKnife", "Fang Knife",
                ItemCategory.Equipment | ItemCategory.Weapon | ItemCategory.Creature, ItemRarity.Epic,
                equipment: new EquipmentBlock(SlotKind.Weapon, durability: 140, WearRule.PerHit,
                    new StatModifier(StatKind.Attack, ModifierOp.Add, 9f),
                    new StatModifier(StatKind.AttackInterval, ModifierOp.Multiply, 0.8f))),
            new ItemDefinition(ItemType.StoneheartCoat, "StoneheartCoat", "Stoneheart Coat",
                ItemCategory.Equipment | ItemCategory.Protection | ItemCategory.Creature, ItemRarity.Epic,
                equipment: new EquipmentBlock(SlotKind.Body, durability: 220, WearRule.PerBlowTaken,
                    new StatModifier(StatKind.Defense, ModifierOp.Add, 12f))),
            // Ranged arms (Epic 10 Part 3, K8.1, Q15, D44): a range across the gap between bodies, and wear per shot, hit or
            // miss (Q16: no ammunition). Weaker in a hit than a blade, so a blade stays the weapon in reach: the sling 5 to hit
            // at 5 units, the crossbow 9 at 7 and slower
            new ItemDefinition(ItemType.Sling, "Sling", "Sling",
                ItemCategory.Equipment | ItemCategory.Weapon | ItemCategory.Botanical, ItemRarity.Common,
                equipment: new EquipmentBlock(SlotKind.Weapon, durability: 100, WearRule.PerShot,
                    new StatModifier(StatKind.Attack, ModifierOp.Add, 3f),
                    new StatModifier(StatKind.Range, ModifierOp.Add, 5f))),
            new ItemDefinition(ItemType.Crossbow, "Crossbow", "Crossbow",
                ItemCategory.Equipment | ItemCategory.Weapon | ItemCategory.Mineral, ItemRarity.Uncommon,
                equipment: new EquipmentBlock(SlotKind.Weapon, durability: 120, WearRule.PerShot,
                    new StatModifier(StatKind.Attack, ModifierOp.Add, 7f),
                    new StatModifier(StatKind.Range, ModifierOp.Add, 7f),
                    new StatModifier(StatKind.AttackInterval, ModifierOp.Multiply, 1.6f)))
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
