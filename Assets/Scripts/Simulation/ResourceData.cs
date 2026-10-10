using Simpiens.Simulation.Economy;

namespace Simpiens.Simulation
{
    /// <summary>
    /// Authoritative per-node resource state, owned and mutated only by SimulationManager (single writer).
    /// </summary>
    public class ResourceData
    {
        public UnityEngine.GUID EntityId;
        public int RemainingYield;

        /// <summary>Item produced per successful harvest. Ration nodes relieve hunger instead of filling inventory.</summary>
        public ItemType YieldItem = ItemType.Ration;

        /// <summary>Yield restored on regrowth. Defaults to the initial RemainingYield when left at 0.</summary>
        public int MaxYield;

        /// <summary>Ticks a depleted node stays dormant before regrowing. 0 = never regrows (destroyed on depletion).</summary>
        public uint RegrowTicks;

        /// <summary>Tick at which the node was depleted; only meaningful while IsDormant.</summary>
        public uint DepletedAtTick;

        public bool IsDormant;
    }
}
