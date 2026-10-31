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

        /// <summary>The session host's: who comes in next (Epic 10 G1, drifters).</summary>
        public PopulationSave Population { get; set; }
    }

    /// <summary>
    /// The drifters' schedule (the next arrival's tick, -1 for none, and how many have come), and everyone who has left,
    /// sorted by id: the chronicle still names them after they go (Epic UI-Beta B1, plan D4).
    /// </summary>
    [MemoryPackable]
    public sealed partial class PopulationSave
    {
        public long NextDrifterTick { get; set; }
        public int DriftersArrived { get; set; }
        public DepartedSave[] Departed { get; set; }
    }

    /// <summary>A crawler who has left the session: who it was and what it looked like.</summary>
    [MemoryPackable]
    public sealed partial class DepartedSave
    {
        public EntityId Id { get; set; }
        public string GivenName { get; set; }
        public Simpiens.Simulation.People.CrawlerLook Look { get; set; }
    }
}
