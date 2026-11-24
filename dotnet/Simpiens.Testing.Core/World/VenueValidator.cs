using System;
using Simpiens.Core.Session.Layouts;
using Simpiens.Simulation;
using Simpiens.Simulation.Creatures;
using Simpiens.Simulation.Life;
using Simpiens.Simulation.World;
using Simpiens.Testing.Environment;

namespace Simpiens.Testing.World
{
    /// <summary>
    /// Venues as map data (Epic LIFE-1 plan 2, L2.3, D13; the epic's §2.5, "no leisure in danger"): on the outpost floor every
    /// venue stands outside the hazard sectors and every lair's reach, its seats inside its sector and out of the reach too;
    /// every activity offered has a row, and each recreation it offers somewhere to stand or sit. Which venues are open
    /// follows their hours and the stations they need. Runs as a test (ValidatorTests).
    /// </summary>
    public class VenueValidator
    {
        public void RunAllValidationTests()
        {
            TestLog.Log("[VenueValidator] Beginning venue validation...");
            ValidateNoLeisureInDanger();
            ValidateWhatEachOffers();
            ValidateWhichAreOpen();
            TestLog.Log("<color=green>[VenueValidator] ALL 3 TESTS PASSED SUCCESSFULLY!</color>");
        }

        private void ValidateNoLeisureInDanger()
        {
            var map = Floor1Layout.Definition;
            Assert(map.Venues.Count >= 4, $"The outpost's venues: the hearth, the stall, the Garden, the yard ({map.Venues.Count}).");
            foreach (var venue in map.Venues)
            {
                Assert(map.TryGetSector(venue.SectorId, out var sector), $"{venue.Key}: in a sector of the map.");
                Assert(!sector.Hazard.IsHazardous, $"{venue.Key}: not in a hazard sector ({sector.Name}).");
                foreach (var lair in map.Lairs) Assert(!LairReach.Holds(lair, venue.Position), $"{venue.Key}: outside {lair.Key}'s reach.");
                foreach (var seat in venue.Seats)
                {
                    Assert(sector.Bounds.Contains(seat), $"{venue.Key}: its seat {seat} inside {sector.Name}.");
                    foreach (var lair in map.Lairs) Assert(!LairReach.Holds(lair, seat), $"{venue.Key}: its seat {seat} outside {lair.Key}'s reach.");
                }
            }
        }

        private void ValidateWhatEachOffers()
        {
            var map = Floor1Layout.Definition;
            foreach (var venue in map.Venues)
            {
                int offered = 0;
                for (int id = 1; id < ActivityTable.Capacity; id++)
                {
                    if (!venue.Offers((Activity)id)) continue;
                    offered++;
                    var row = ActivityTable.Get((Activity)id);
                    Assert(row != null, $"{venue.Key}: offers activity {id}, which has a row.");
                    if (row.IsRecreation) Assert(venue.Seats.Count >= Math.Max(1, (int)row.CompanyMax), $"{venue.Key}: room for {row.Id} ({venue.Seats.Count} seats).");
                }
                Assert(offered > 0, $"{venue.Key}: offers something.");
            }
            Assert(Find(map, Floor1Layout.GardenVenue).Offers(Activity.Walk) && Find(map, Floor1Layout.StallVenue).Offers(Activity.Browse)
                && Find(map, Floor1Layout.HearthVenue).Offers(Activity.Fireside) && Find(map, Floor1Layout.YardVenue).Offers(Activity.Spar), "Each offers its own.");
            Assert(Find(map, Floor1Layout.YardVenue).Access == VenueAccess.GuildMembers, "The yard is the guild's members'.");
            var tavern = Find(map, Floor1Layout.TavernVenue);
            Assert(tavern.Offers(Activity.Ale) && tavern.Offers(Activity.Supper) && tavern.Offers(Activity.Dice) && tavern.Station == Floor1Layout.Tavern
                && tavern.SectorId == Floor1Layout.TavernSectorId && tavern.Seats.Count == 8, "The tavern: ale, supper and dice at eight seats, at its counter, in its room (T.4).");
        }

        private void ValidateWhichAreOpen()
        {
            var map = Floor1Layout.Definition;
            int garden = Index(map, Floor1Layout.GardenVenue), yard = Index(map, Floor1Layout.YardVenue);
            int drillYard = -1;
            for (int s = 0; s < map.Stations.Count; s++) if (map.Stations[s].Key == Floor1Layout.DrillYard) drillYard = s;
            for (long tick = 0; tick < DayClock.TicksPerDay; tick += DayClock.TicksPerHour)
            {
                uint none = VenueRules.OpenMask(map, tick, 0UL), built = VenueRules.OpenMask(map, tick, 1UL << drillYard);
                Assert((none & (1u << garden)) != 0, $"The Garden is open at all hours (D26), at {DayClock.MinuteOfDay(tick) / 60}:00.");
                Assert((none & (1u << yard)) == 0 && (built & (1u << yard)) != 0, "The yard is open only while the drill yard stands.");
            }
            Assert(VenueRules.OpenMask(null, 0, 0UL) == 0u, "No map, no venues.");
            int tavern = Index(map, Floor1Layout.TavernVenue), counter = -1;
            for (int s = 0; s < map.Stations.Count; s++) if (map.Stations[s].Key == Floor1Layout.Tavern) counter = s;
            Assert((VenueRules.OpenMask(map, 0, 0UL, 0UL) & (1u << tavern)) == 0, "No tavern standing: shut.");
            Assert((VenueRules.OpenMask(map, 0, 1UL << counter, 0UL) & (1u << tavern)) == 0, "Standing but not kept: shut.");
            Assert((VenueRules.OpenMask(map, 0, 1UL << counter, 1UL << counter) & (1u << tavern)) != 0, "Standing and kept: open.");
        }

        private static VenueDefinition Find(MapDefinition map, string key) => map.Venues[Index(map, key)];

        private static int Index(MapDefinition map, string key)
        {
            for (int i = 0; i < map.Venues.Count; i++) if (map.Venues[i].Key == key) return i;
            throw new InvalidOperationException($"[VenueValidator] FAILED: no venue {key}.");
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("[VenueValidator] FAILED: " + message);
        }
    }
}
