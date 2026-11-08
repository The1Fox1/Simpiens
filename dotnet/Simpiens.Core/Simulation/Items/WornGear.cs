using Simpiens.Cognition.Inventory;
using Simpiens.Simulation.Economy;

namespace Simpiens.Simulation.Items
{
    /// <summary>
    /// Where a piece is worn and what it takes the place of (Epic 10 Part 3, K1.1): the one rule the stall's sales, the
    /// guild's gifts and the scripted players share, so they agree.
    /// <para>
    /// Until K1.3 a crawler wears one protective piece, as it did when protection was a single slot (Epic 10 G3): a piece
    /// of protection takes the place of the protection worn, on the head or the body. K1.1 and K1.2 change the structure
    /// only; K1.3 lets a hard hat and hazard gear be worn together, and changes this rule alone.
    /// </para>
    /// </summary>
    public static class WornGear
    {
        /// <summary>
        /// The slot whose piece <paramref name="item"/> would take the place of, if worn: until K1.3, for protection, the slot
        /// of the protection worn; otherwise (and for protection when none is worn) the first slot of its kind that is
        /// empty, or the first of its kind. False for an item that fits no slot.
        /// </summary>
        public static bool TryReplacing(IAgentInventory inventory, ItemType item, out EquipmentSlot slot)
        {
            var equipment = item != ItemType.None ? ItemCatalog.Get(item)?.Equipment : null;
            slot = default;
            if (equipment == null) return false;
            var slots = EquipmentSlots.All;
            if (inventory != null && ItemCatalog.Is(item, ItemCategory.Protection))
            {
                for (int i = 0; i < slots.Count; i++)
                {
                    var worn = inventory.GetEquipped(slots[i].Id);
                    if (worn.IsEmpty || !ItemCatalog.Is(worn.Type, ItemCategory.Protection)) continue;
                    slot = slots[i].Id;
                    return true;
                }
            }
            bool found = false;
            for (int i = 0; i < slots.Count; i++)
            {
                if (slots[i].Kind != equipment.Kind) continue;
                if (inventory == null || inventory.GetEquipped(slots[i].Id).IsEmpty)
                {
                    slot = slots[i].Id;
                    return true;
                }
                if (!found)
                {
                    slot = slots[i].Id;
                    found = true;
                }
            }
            return found;
        }

        /// <summary>The piece <paramref name="item"/> would take the place of (<see cref="TryReplacing"/>); empty when none.</summary>
        public static ItemInstance Replaced(IAgentInventory inventory, ItemType item)
            => inventory != null && TryReplacing(inventory, item, out var slot) ? inventory.GetEquipped(slot) : ItemInstance.Empty;

        /// <summary>The protection worn (until K1.3 one piece at most, on the head or the body); empty for none.</summary>
        public static ItemInstance Protection(IAgentInventory inventory) => inventory.WornIn(ItemCategory.Protection);
    }
}
