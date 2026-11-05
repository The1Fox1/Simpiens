using MemoryPack;
using Simpiens.Simulation.Workshops;

namespace Simpiens.Simulation.Persistence
{
    /// <summary>
    /// The workshops in a save (Epic 10 Part 2, W0.5), written and read by <see cref="WorkshopService"/>: one state per
    /// station of the map, in the map's order; the artisans' contracts in hire order (W1.2), and the crawlers who can't
    /// be hired again yet, sorted by id (format 10).
    /// </summary>
    [MemoryPackable]
    public sealed partial class WorkshopSave
    {
        public StationState[] Stations { get; set; }
        public ArtisanContract[] Artisans { get; set; }
        public IdTickRecord[] Cooldowns { get; set; }
    }
}
