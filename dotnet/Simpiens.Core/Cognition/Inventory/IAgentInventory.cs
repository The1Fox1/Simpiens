using Simpiens.Simulation.Economy;

namespace Simpiens.Cognition.Inventory
{
    /// <summary>
    /// Read-only abstraction for an agent's inventory and currency.
    /// Implemented by both the live AgentInventory and immutable AgentInventorySnapshot.
    /// </summary>
    public interface IAgentInventory
    {
        int Capacity { get; }
        int Gold { get; }
        int OccupiedSlots { get; }
        ItemInstance GetSlot(int index);
        bool HasItem(ItemType type, int count = 1);
        int GetItemCount(ItemType type);

        /// <summary>
        /// What the crawler has equipped in a slot, beside the pack (protection since Epic 10 G3; weapon and tool reserved):
        /// <see cref="ItemInstance.Empty"/> when nothing. Its <see cref="ItemInstance.Durability"/> counts the wear it still takes.
        /// </summary>
        ItemInstance GetEquipped(Simpiens.Simulation.Items.EquipmentSlot slot);
    }
}
