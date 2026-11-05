using System.Runtime.InteropServices;

namespace Simpiens.Simulation.Workshops
{
    /// <summary>
    /// An artisan's terms (Epic 10 Part 2, D9): who works which station, at what wage per caravan visit, since when, and
    /// how many visits in a row it has gone unpaid. Saved as raw memory (format 10), in hire order: the order wages are
    /// paid in.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public readonly struct ArtisanContract
    {
        public readonly EntityId CrawlerId;
        public readonly uint JoinTick;
        public readonly ushort Wage;

        /// <summary>The station it works: its index among the map's stations.</summary>
        public readonly byte StationIndex;

        public readonly byte UnpaidVisits;

        public ArtisanContract(EntityId crawlerId, byte stationIndex, ushort wage, uint joinTick, byte unpaidVisits = 0)
        {
            CrawlerId = crawlerId;
            StationIndex = stationIndex;
            Wage = wage;
            JoinTick = joinTick;
            UnpaidVisits = unpaidVisits;
        }

        public ArtisanContract WithWage(ushort wage) => new ArtisanContract(CrawlerId, StationIndex, wage, JoinTick, UnpaidVisits);
        public ArtisanContract WithUnpaidVisits(byte unpaidVisits) => new ArtisanContract(CrawlerId, StationIndex, Wage, JoinTick, unpaidVisits);
    }
}
