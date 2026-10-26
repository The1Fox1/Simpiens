using Simpiens.Simulation.Items;

namespace Simpiens.Cognition.Inventory
{
    /// <summary>Group questions about a pack, so code asks for a group rather than naming items (.agents/item_system_plan.md §3.10).</summary>
    public static class InventoryGroups
    {
        /// <summary>True when the pack holds at least one item of the group (a single flag).</summary>
        public static bool HasAny(this IAgentInventory inventory, ItemCategory group)
        {
            if (inventory == null) return false;
            var members = ItemCatalog.InCategory(group);
            for (int i = 0; i < members.Count; i++)
            {
                if (inventory.HasItem(members[i])) return true;
            }
            return false;
        }

        /// <summary>The first item of the group the pack holds, in id order; None when it holds none.</summary>
        public static Simpiens.Simulation.Economy.ItemType FirstHeld(this IAgentInventory inventory, ItemCategory group)
        {
            if (inventory == null) return Simpiens.Simulation.Economy.ItemType.None;
            var members = ItemCatalog.InCategory(group);
            for (int i = 0; i < members.Count; i++)
            {
                if (inventory.HasItem(members[i])) return members[i];
            }
            return Simpiens.Simulation.Economy.ItemType.None;
        }

        /// <summary>True when an equipped item is in the group.</summary>
        public static bool IsWearing(this IAgentInventory inventory, ItemCategory group)
        {
            if (inventory == null) return false;
            for (int s = 0; s < AgentInventory.EquipmentSlots; s++)
            {
                var item = inventory.GetEquipped((EquipmentSlot)s);
                if (!item.IsEmpty && ItemCatalog.Is(item.Type, group)) return true;
            }
            return false;
        }
    }
}
