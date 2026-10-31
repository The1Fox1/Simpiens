using MemoryPack;
using Simpiens.Simulation.Guild;

namespace Simpiens.Simulation.Persistence
{
    /// <summary>
    /// The guild in a save (Epic 10 G4), written and read by <see cref="GuildService"/>: its group, its members' contracts
    /// in join order (the order wages are paid in), and the crawlers who can't sign again yet, sorted by id.
    /// </summary>
    [MemoryPackable]
    public sealed partial class GuildSave
    {
        public byte GroupId { get; set; }
        public GuildContract[] Members { get; set; }
        public IdTickRecord[] Cooldowns { get; set; }
    }
}
