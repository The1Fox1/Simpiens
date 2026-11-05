using System.Runtime.InteropServices;

namespace Simpiens.Simulation.Workshops
{
    /// <summary>
    /// An artisan's terms (Epic 10 Part 2, D9): who works which station, at what wage per caravan visit, since when, and
    /// how many visits in a row it has gone unpaid; which blocks of the day it works (D12) and whether it broke from its
    /// shift and is off until rested (D16). Saved as raw memory (format 10), in hire order: the order wages are paid in.
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

        /// <summary>The blocks of the day it works, a <see cref="ShiftBlocks"/> mask; none until the player sets them (D12).</summary>
        public readonly byte Shifts;

        /// <summary>It broke from an overlong shift and works none until rested (D16).</summary>
        public readonly bool OnBreak;

        public ArtisanContract(EntityId crawlerId, byte stationIndex, ushort wage, uint joinTick, byte unpaidVisits = 0, byte shifts = 0, bool onBreak = false)
        {
            CrawlerId = crawlerId;
            StationIndex = stationIndex;
            Wage = wage;
            JoinTick = joinTick;
            UnpaidVisits = unpaidVisits;
            Shifts = shifts;
            OnBreak = onBreak;
        }

        public ArtisanContract WithWage(ushort wage) => new ArtisanContract(CrawlerId, StationIndex, wage, JoinTick, UnpaidVisits, Shifts, OnBreak);
        public ArtisanContract WithUnpaidVisits(byte unpaidVisits) => new ArtisanContract(CrawlerId, StationIndex, Wage, JoinTick, unpaidVisits, Shifts, OnBreak);
        public ArtisanContract WithShifts(byte shifts) => new ArtisanContract(CrawlerId, StationIndex, Wage, JoinTick, UnpaidVisits, shifts, OnBreak);
        public ArtisanContract WithOnBreak(bool onBreak) => new ArtisanContract(CrawlerId, StationIndex, Wage, JoinTick, UnpaidVisits, Shifts, onBreak);
    }
}
