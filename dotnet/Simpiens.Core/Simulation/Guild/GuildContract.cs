using System.Runtime.InteropServices;

namespace Simpiens.Simulation.Guild
{
    /// <summary>
    /// One member's terms with the guild (Epic 10 G4): its wage per caravan visit, how many visits in a row it went
    /// unpaid, when it signed, and the bunk kept for it in the barracks (Epic 10 Part 2, W5.2, D62). The membership itself
    /// is an Epic 8 group membership; these are the guild's own terms. Unmanaged, saved as raw memory.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public readonly struct GuildContract
    {
        public readonly EntityId CrawlerId;
        public readonly uint JoinTick;
        public readonly ushort Wage;
        public readonly byte UnpaidVisits;

        /// <summary>Its bunk in the barracks, by its place in the map's barracks from 1; 0 for none (the struct's spare byte).</summary>
        public readonly byte Bunk;

        public GuildContract(EntityId crawlerId, ushort wage, uint joinTick, byte unpaidVisits = 0, byte bunk = 0)
        {
            CrawlerId = crawlerId;
            Wage = wage;
            JoinTick = joinTick;
            UnpaidVisits = unpaidVisits;
            Bunk = bunk;
        }

        public bool HasBunk => Bunk != 0;

        public GuildContract WithWage(ushort wage) => new GuildContract(CrawlerId, wage, JoinTick, UnpaidVisits, Bunk);
        public GuildContract WithUnpaidVisits(byte unpaidVisits) => new GuildContract(CrawlerId, Wage, JoinTick, unpaidVisits, Bunk);
    }
}
