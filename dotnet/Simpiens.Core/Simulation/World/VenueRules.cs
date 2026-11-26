namespace Simpiens.Simulation.World
{
    /// <summary>
    /// Which venues are open (Epic LIFE-1 plan 2, L2.3, D13): a venue is open in its hours, with the station it needs standing
    /// and, where that station takes an artisan (the tavern), kept: its keeper at it on shift (T.4). Computed on the main thread
    /// each tick and published in the snapshot (<c>SharedWorldSnapshot.OpenVenueMask</c>), so cognition reads the same answer. Pure.
    /// </summary>
    public static class VenueRules
    {
        /// <summary>
        /// Bit i set for each of <paramref name="map"/>'s venues open at <paramref name="tick"/>, the stations standing as
        /// <paramref name="builtStations"/> says and kept as <paramref name="keptStations"/> says.
        /// </summary>
        public static uint OpenMask(MapDefinition map, long tick, ulong builtStations, ulong keptStations = 0UL)
        {
            if (map == null) return 0u;
            uint mask = 0u;
            for (int i = 0; i < map.Venues.Count && i < MapDefinition.MaxVenues; i++)
            {
                if (IsOpen(map, map.Venues[i], tick, builtStations, keptStations)) mask |= 1u << i;
            }
            return mask;
        }

        /// <summary>Whether <paramref name="venue"/> is open at <paramref name="tick"/>: in its hours, its station (if any) standing, and kept if it takes a keeper.</summary>
        public static bool IsOpen(MapDefinition map, in VenueDefinition venue, long tick, ulong builtStations, ulong keptStations = 0UL)
        {
            if (!Simpiens.Simulation.Workshops.ShiftBlocks.Covers(venue.Hours, tick)) return false;
            if (venue.Station == null) return true;
            for (int s = 0; s < map.Stations.Count && s < 64; s++)
            {
                if (map.Stations[s].Key != venue.Station) continue;
                if ((builtStations & (1UL << s)) == 0) return false;
                return !Simpiens.Simulation.Items.StationTable.Get(map.Stations[s].Kind).TakesArtisan || (keptStations & (1UL << s)) != 0;
            }
            return false;
        }
    }
}
