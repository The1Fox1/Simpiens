using Simpiens.Cognition.Inventory;
using Simpiens.Simulation.Economy;

namespace Simpiens.Simulation.Items
{
    /// <summary>
    /// Where a piece is worn and what it takes the place of (Epic 10 Part 3, K1.1): the one rule the stall's sales, the
    /// guild's gifts and the scripted players share, so they agree.
    /// <para>
    /// A piece goes in a slot of its kind and takes the place only of what is in that slot (K1.3): a hard hat on the head
    /// and hazard gear on the body are worn together, and a respirator takes the hard hat's place.
    /// </para>
    /// </summary>
    public static class WornGear
    {
        /// <summary>
        /// The slot <paramref name="item"/> would go in, if worn: the first slot of its kind that is empty, or (all of them
        /// taken) the first of its kind, whose piece it would take the place of. False for an item that fits no slot.
        /// </summary>
        public static bool TryReplacing(IAgentInventory inventory, ItemType item, out EquipmentSlot slot)
        {
            var equipment = item != ItemType.None ? ItemCatalog.Get(item)?.Equipment : null;
            slot = default;
            if (equipment == null) return false;
            var slots = EquipmentSlots.All;
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

        /// <summary>
        /// The first protective piece worn, in the slot table's id order (the head's before the body's); empty for none. A
        /// crawler may wear several since K1.3: what shows one piece (the map's worn sprite, until K1.5 draws every slot)
        /// shows this one.
        /// </summary>
        public static ItemInstance Protection(IAgentInventory inventory) => inventory.WornIn(ItemCategory.Protection);
    }
}
