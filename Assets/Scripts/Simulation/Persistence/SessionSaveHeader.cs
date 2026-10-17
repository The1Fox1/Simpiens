using MemoryPack;

namespace Simpiens.Simulation.Persistence
{
    /// <summary>
    /// The first block of a session save: what the save is, and whether this build can load it. A load refuses a
    /// save whose format version, session or map fingerprint differs; saves are not migrated (Milestone 1).
    /// The seed, the tick and the id allocator's counter are all a save needs to resume randomness and entity ids,
    /// because <see cref="SimRandom"/> keeps no stream state.
    /// </summary>
    [MemoryPackable]
    public sealed partial class SessionSaveHeader
    {
        /// <summary>Bumped whenever a saved type's layout changes: unmanaged records are saved as memory copies.</summary>
        public const int CurrentFormatVersion = 1;

        public int FormatVersion { get; set; } = CurrentFormatVersion;
        public string BuildVersion { get; set; }
        public string SessionId { get; set; }
        public ulong MapFingerprint { get; set; }
        public ulong Seed { get; set; }
        public long Tick { get; set; }
        public long EntityIdCounter { get; set; }
    }
}
