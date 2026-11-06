using MemoryPack;
using Simpiens.Simulation.Guild;

namespace Simpiens.Simulation.Persistence
{
    /// <summary>
    /// The guild in a save (Epic 10 G4), written and read by <see cref="GuildService"/>: its group, its members' contracts
    /// in join order (the order wages are paid in), and the crawlers who can't sign again yet, sorted by id. From format
    /// 9 (G5), the expedition out; from format 10 (Epic 10 Part 2, W5, D60), the guild's parties in slot order, each with
    /// its expedition and its roster.
    /// </summary>
    [MemoryPackable]
    public sealed partial class GuildSave
    {
        public byte GroupId { get; set; }
        public GuildContract[] Members { get; set; }
        public IdTickRecord[] Cooldowns { get; set; }
        public GuildPartySave[] Parties { get; set; }
    }

    /// <summary>One of the guild's parties in a save (Epic 10 Part 2, W5, D60): its slot, its group, its expedition and its roster in the order it was sent.</summary>
    [MemoryPackable]
    public sealed partial class GuildPartySave
    {
        public byte Slot { get; set; }
        public byte GroupId { get; set; }
        public ExpeditionRecord Expedition { get; set; }
        public ExpeditionMember[] Members { get; set; }
    }
}
