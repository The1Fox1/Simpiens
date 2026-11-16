using System.Runtime.CompilerServices;
using Simpiens.Simulation.Economy;

namespace Simpiens.Simulation.Items
{
    /// <summary>
    /// A crawler's worn equipment as storage (Epic 10 Part 3, K1.2, seam A3): a record for each slot id the table may define
    /// (<see cref="EquipmentSlots.Capacity"/>), indexed by the id, empty where nothing is worn or no slot is defined.
    /// <para>
    /// Unmanaged, and saved as raw memory with all of its records: a slot added to the table later finds its record empty
    /// in an older save, and loads with nothing in it, with no change of save format. Its size is pinned
    /// (EconomySerializationValidator); a seventeenth slot changes the format.
    /// </para>
    /// </summary>
    [InlineArray(EquipmentSlots.Capacity)]
    public struct WornEquipment
    {
        private ItemInstance _record;
    }
}
