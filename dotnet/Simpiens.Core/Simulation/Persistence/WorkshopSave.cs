using MemoryPack;
using Simpiens.Simulation.Workshops;

namespace Simpiens.Simulation.Persistence
{
    /// <summary>
    /// The workshops in a save (Epic 10 Part 2, W0.5), written and read by <see cref="WorkshopService"/>: one state per
    /// station of the map, in the map's order (format 10).
    /// </summary>
    [MemoryPackable]
    public sealed partial class WorkshopSave
    {
        public StationState[] Stations { get; set; }
    }
}
