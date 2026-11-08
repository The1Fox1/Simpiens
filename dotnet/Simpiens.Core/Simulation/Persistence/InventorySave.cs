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

        /// <summary>
        /// The equipment beside the pack: a record for each of the 16 slot ids the table may define, indexed by the id, all
        /// of them saved as raw memory (format 11, Epic 10 Part 3, K1.2), so a slot added later loads with nothing in it.
        /// Format 5 held protection, weapon and tool; format 4 one gear item.
        /// </summary>
        public Simpiens.Simulation.Items.WornEquipment Equipped { get; set; }
    }
}
