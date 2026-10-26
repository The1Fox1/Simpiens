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
        /// The gear the crawler wears (Epic 10 G3), beside the pack: <see cref="ItemInstance.Empty"/> when none. Its
        /// <see cref="ItemInstance.Durability"/> counts the ticks it still protects in a hazard sector.
        /// </summary>
        ItemInstance Gear { get; }
    }
}
