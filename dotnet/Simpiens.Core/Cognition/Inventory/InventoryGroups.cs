using Simpiens.Simulation.Items;

namespace Simpiens.Cognition.Inventory
{
    /// <summary>Group questions about a pack, so code asks for a group rather than naming items (the item system refactor, 2026-10-26).</summary>
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

        /// <summary>How many items of the group the pack holds (an expedition's provisions, Epic 10 G5).</summary>
        public static int CountOf(this IAgentInventory inventory, ItemCategory group)
        {
            if (inventory == null) return 0;
            var members = ItemCatalog.InCategory(group);
            int count = 0;
            for (int i = 0; i < members.Count; i++) count += inventory.GetItemCount(members[i]);
            return count;
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
        public static bool IsWearing(this IAgentInventory inventory, ItemCategory group) => !inventory.WornIn(group).IsEmpty;

        /// <summary>The first equipped item in the group, in slot id order; empty when none is (Epic 10 Part 3, K1.1).</summary>
        public static Simpiens.Simulation.Economy.ItemInstance WornIn(this IAgentInventory inventory, ItemCategory group)
        {
            if (inventory == null) return Simpiens.Simulation.Economy.ItemInstance.Empty;
            var slots = EquipmentSlots.All;
            for (int s = 0; s < slots.Count; s++)
            {
                var item = inventory.GetEquipped(slots[s].Id);
                if (!item.IsEmpty && ItemCatalog.Is(item.Type, group)) return item;
            }
            return Simpiens.Simulation.Economy.ItemInstance.Empty;
        }

        /// <summary>True when <paramref name="item"/> is equipped in any slot (Epic 10 Part 3, K1.1).</summary>
        public static bool IsWearing(this IAgentInventory inventory, Simpiens.Simulation.Economy.ItemType item)
        {
            if (inventory == null || item == Simpiens.Simulation.Economy.ItemType.None) return false;
            var slots = EquipmentSlots.All;
            for (int s = 0; s < slots.Count; s++)
            {
                if (inventory.GetEquipped(slots[s].Id).Type == item) return true;
            }
            return false;
        }
    }
}
