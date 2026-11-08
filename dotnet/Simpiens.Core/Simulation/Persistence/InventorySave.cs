using MemoryPack;
using Simpiens.Simulation.Economy;

namespace Simpiens.Simulation.Persistence
{
    /// <summary>
    /// A crawler's pack in a save: the purse and every slot, empty ones included, so the slot count is the inventory's
    /// capacity. Written and read by <see cref="Cognition.Inventory.AgentInventory"/>.
    /// </summary>
    [MemoryPackable]
    public sealed partial class InventorySave
    {
        public int Gold { get; set; }
        public ItemInstance[] Slots { get; set; }

        /// <summary>The equipment beside the pack, one record per slot id, indexed by it (format 11, Epic 10 Part 3, K1.1: 16 records, as the slot table has room for; format 5 held protection, weapon and tool; format 4 one gear item).</summary>
        public ItemInstance[] Equipped { get; set; }
    }
}
