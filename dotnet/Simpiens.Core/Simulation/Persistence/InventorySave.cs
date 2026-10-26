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

        /// <summary>The worn gear, beside the pack (Epic 10 G3, format 4); empty when none.</summary>
        public ItemInstance Gear { get; set; }
    }
}
