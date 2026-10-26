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

        /// <summary>True when the worn gear is in the group.</summary>
        public static bool IsWearing(this IAgentInventory inventory, ItemCategory group) =>
            inventory != null && !inventory.Gear.IsEmpty && ItemCatalog.Is(inventory.Gear.Type, group);
    }
}
