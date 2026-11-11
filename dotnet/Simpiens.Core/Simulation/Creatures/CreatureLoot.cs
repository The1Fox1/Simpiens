using Simpiens.Simulation.Economy;
using Simpiens.Simulation.Items;

namespace Simpiens.Simulation.Creatures
{
    /// <summary>
    /// What each kind drops at its kill (Epic 10 Part 3, K4.2, Q12): its material, sometimes salvage, now and then a rare
    /// piece, as loot tables (item system I7) rolled by <see cref="LootRoller"/> on (seed, the creature, the tick of its
    /// death). A kind names its table by id (<see cref="CreatureDefinition.LootTableId"/>). Placeholder weights, tuned by play.
    /// </summary>
    public static class CreatureLoot
    {
        public const ushort Rat = 1, Beetle = 2, Spinner = 3;

        /// <summary>The most stacks a kill drops.</summary>
        public const int MaxDrops = 4;

        public static LootTables Tables { get; } = new LootTables(
            // A rat: its hide mostly, a herb from its burrow sometimes, its fang now and then
            new LootTable(Rat, "rat", 1, 1,
                LootEntry.ItemDrop(ItemType.Hide, 1, 1, weight: 70),
                LootEntry.ItemDrop(ItemType.MedicinalHerb, 1, 1, weight: 18),
                LootEntry.ItemDrop(ItemType.FangKnife, 1, 1, weight: 2),
                LootEntry.Nothing(10)),
            // A beetle: chitin, twice over at times; ore from its gut sometimes; its stoneheart now and then
            new LootTable(Beetle, "beetle", 1, 2,
                LootEntry.ItemDrop(ItemType.Chitin, 1, 2, weight: 72),
                LootEntry.ItemDrop(ItemType.IronOre, 1, 1, weight: 22),
                LootEntry.ItemDrop(ItemType.StoneheartCoat, 1, 1, weight: 3),
                LootEntry.Nothing(3)),
            // A spinner (the gate's approach, K10): chitin from its legs, a herb from its web
            new LootTable(Spinner, "spinner", 1, 1,
                LootEntry.ItemDrop(ItemType.Chitin, 1, 1, weight: 60),
                LootEntry.ItemDrop(ItemType.MedicinalHerb, 1, 1, weight: 30),
                LootEntry.Nothing(10)));
    }
}
