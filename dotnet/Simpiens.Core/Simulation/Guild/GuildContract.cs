using System.Runtime.InteropServices;

namespace Simpiens.Simulation.Guild
{
    /// <summary>
    /// One member's terms with the guild (Epic 10 G4): its wage per caravan visit, how many visits in a row it went
    /// unpaid, and when it signed. The membership itself is an Epic 8 group membership; these are the guild's own terms.
    /// Unmanaged, saved as raw memory.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public readonly struct GuildContract
    {
        public readonly EntityId CrawlerId;
        public readonly uint JoinTick;
        public readonly ushort Wage;
        public readonly byte UnpaidVisits;
        private readonly byte _pad;

        public GuildContract(EntityId crawlerId, ushort wage, uint joinTick, byte unpaidVisits = 0)
        {
            CrawlerId = crawlerId;
            Wage = wage;
            JoinTick = joinTick;
            UnpaidVisits = unpaidVisits;
            _pad = 0;
        }

        public GuildContract WithWage(ushort wage) => new GuildContract(CrawlerId, wage, JoinTick, UnpaidVisits);
        public GuildContract WithUnpaidVisits(byte unpaidVisits) => new GuildContract(CrawlerId, Wage, JoinTick, unpaidVisits);
    }
}
