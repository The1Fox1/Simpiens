using MemoryPack;

namespace Simpiens.Simulation.Persistence
{
    /// <summary>
    /// A whole session at one tick (Milestone 1 save/load): the header, then one block per owner. Every block is plain
    /// data in a fixed order (records sorted by id, crawlers in spawn order), so capturing the same state twice gives the
    /// same bytes whatever order its dictionaries were filled in.
    /// Not saved: work in progress (intents, plans, and the bunk claims and contention that depend on them: crawlers
    /// decide afresh after a load), derived state (snapshots, the spatial and nav grids, the agent and world registries),
    /// and what the session definition provides (the map and the economy tuning).
    /// </summary>
    [MemoryPackable]
    public sealed partial class SessionSave
    {
        public SessionSaveHeader Header { get; set; }

        /// <summary>One per crawler, in spawn order (the order they tick in).</summary>
        public CrawlerSave[] Crawlers { get; set; }

        public EconomySave Economy { get; set; }
        public WorldSave World { get; set; }
        public GroupSave Groups { get; set; }
        public ChronicleSave Chronicle { get; set; }
    }
}
