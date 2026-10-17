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
    }
}
