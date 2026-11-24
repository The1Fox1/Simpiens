namespace Simpiens.Simulation.World
{
    /// <summary>
    /// Which venues are open (Epic LIFE-1 plan 2, L2.3, D13): a venue is open in its hours, with the station it needs standing.
    /// (The tavern also needs its keeper at the counter on shift: plan 2's T.4.) Computed on the main thread each tick and
    /// published in the snapshot (<c>SharedWorldSnapshot.OpenVenueMask</c>), so cognition reads the same answer. Pure.
    /// </summary>
    public static class VenueRules
    {
        /// <summary>Bit i set for each of <paramref name="map"/>'s venues open at <paramref name="tick"/>, the stations standing as <paramref name="builtStations"/> says.</summary>
        public static uint OpenMask(MapDefinition map, long tick, ulong builtStations)
        {
            if (map == null) return 0u;
            uint mask = 0u;
            for (int i = 0; i < map.Venues.Count && i < MapDefinition.MaxVenues; i++)
            {
                if (IsOpen(map, map.Venues[i], tick, builtStations)) mask |= 1u << i;
            }
            return mask;
        }

        /// <summary>Whether <paramref name="venue"/> is open at <paramref name="tick"/>: in its hours, its station (if any) standing.</summary>
        public static bool IsOpen(MapDefinition map, in VenueDefinition venue, long tick, ulong builtStations)
        {
            if (!Simpiens.Simulation.Workshops.ShiftBlocks.Covers(venue.Hours, tick)) return false;
            if (venue.Station == null) return true;
            for (int s = 0; s < map.Stations.Count && s < 64; s++)
            {
                if (map.Stations[s].Key == venue.Station) return (builtStations & (1UL << s)) != 0;
            }
            return false;
        }
    }
}
