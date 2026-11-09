using System;
using Simpiens.Simulation.Economy;
using Simpiens.Simulation.Items;

namespace Simpiens.Testing.Economy
{
    /// <summary>
    /// The catalogues' test hooks for any test project (Epic 10 Part 3, K1.6): the item catalogue and the slot table with
    /// rows added until the returned scope is disposed, swapped whole between ticks. Tests that use them run serially. Test
    /// only, never shipped: the core keeps the hooks internal.
    /// </summary>
    public static class TestCatalogs
    {
        /// <summary>The item catalogue with <paramref name="extra"/> added.</summary>
        public static IDisposable Items(params ItemDefinition[] extra) => ItemCatalog.ExtendForTest(extra);

        /// <summary>The slot table with <paramref name="extra"/> added.</summary>
        public static IDisposable Slots(params SlotDefinition[] extra) => EquipmentSlots.ExtendForTest(extra);

        /// <summary>
        /// The proof's feet slot and boots (K1.6): slot 5 of a new kind, on the feet, under every other piece; boots that fit
        /// it, cut any hazard's harm by a fifth and wear out by exposure. Both catalogues, until disposed.
        /// </summary>
        public static IDisposable FeetAndBoots()
        {
            var slots = Slots(new SlotDefinition(Feet, "Feet", "Feet", FeetKind, BodyRegion.Feet) { DrawLayer = 5, DollColumn = 1, DollRow = 2, Order = 4 });
            var items = Items(new ItemDefinition(Boots, "Boots", "Boots", ItemCategory.Equipment, ItemRarity.Common,
                equipment: new EquipmentBlock(FeetKind, durability: 600, WearRule.PerHazardTick,
                    new StatModifier(StatKind.HazardResistance, ModifierOp.Add, 0.2f))));
            return new Both(items, slots);
        }

        public const EquipmentSlot Feet = (EquipmentSlot)5;
        public const SlotKind FeetKind = (SlotKind)201;
        public const ItemType Boots = (ItemType)218;

        private sealed class Both : IDisposable
        {
            private readonly IDisposable _first, _second;
            public Both(IDisposable first, IDisposable second)
            {
                _first = first;
                _second = second;
            }

            public void Dispose()
            {
                _first.Dispose();
                _second.Dispose();
            }
        }
    }
}
