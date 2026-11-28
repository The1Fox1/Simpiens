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
    /// follows their hours and the stations they need. A venue may seat its crawlers on furniture (Epic PLACES plan 1, F.3, D14):
    /// its seats are its pieces' seats in its order, each piece in its sector; the tavern's stools sit where its seats were. Runs
    /// as a test (ValidatorTests).
    /// </summary>
    public class VenueValidator
    {
        public void RunAllValidationTests()
        {
            TestLog.Log("[VenueValidator] Beginning venue validation...");
            ValidateNoLeisureInDanger();
            ValidateWhatEachOffers();
            ValidateWhichAreOpen();
            ValidateSeatsOnFurniture();
            TestLog.Log("<color=green>[VenueValidator] ALL 4 TESTS PASSED SUCCESSFULLY!</color>");
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

        private void ValidateSeatsOnFurniture()
        {
            var map = Floor1Layout.Definition;
            int seated = 0;
            foreach (var venue in map.Venues)
            {
                if (venue.SeatedOn.Count == 0) continue;
                int s = 0;
                foreach (var key in venue.SeatedOn)
                {
                    Assert(map.TryGetFurniture(key, out var piece) && piece.SectorId == venue.SectorId, $"'{venue.Key}' seats crawlers on '{key}', in its sector.");
                    for (int i = 0; i < piece.SeatCount; i++, s++) Assert(venue.Seats[s] == piece.SeatAt(i), $"'{venue.Key}' seat {s} is '{key}' seat {i}.");
                }
                Assert(s == venue.Seats.Count, $"'{venue.Key}' has its pieces' seats and no others ({venue.Seats.Count}, not {s}).");
                seated++;
            }
            Assert(map.TryGetVenue(Floor1Layout.TavernVenue, out var tavern) && tavern.SeatedOn.Count == 8 && tavern.Seats.Count == 8, "The tavern seats its crawlers on its eight stools.");
            // Its stools stand 1.8 from their tables' centres since the tables block (F.4, D15), each within the venue's reach
            for (int i = 0; i < tavern.Seats.Count; i++)
            {
                var table = Floor1Layout.TavernTables[i / 4];
                Assert(Math.Abs(Vec2.Distance(tavern.Seats[i], table) - 1.8f) < 0.001f && Vec2.Distance(tavern.Seats[i], tavern.Position) + VenueDefinition.SeatRadius <= tavern.Radius,
                    $"The tavern's seat {i} stands 1.8 from its table, within the venue ({tavern.Seats[i]}).");
            }

            // The builder refuses seats of its own beside seats on furniture, a piece the map hasn't, and a piece with no seat
            Refused(b => b.Furniture("stool", Simpiens.Simulation.Places.FurnitureKind.Stool, new Vec2(0f, 0f))
                .Venue("v", new Vec2(0f, 0f), 2f, new[] { new Vec2(1f, 0f) }, new[] { Activity.Ale }, seatedOn: new[] { "stool" }), "seats of its own and on furniture");
            Refused(b => b.Venue("v", new Vec2(0f, 0f), 2f, null, new[] { Activity.Ale }, seatedOn: new[] { "stool" }), "a piece the map hasn't");
            Refused(b => b.Furniture("table", Simpiens.Simulation.Places.FurnitureKind.Table, new Vec2(0f, 0f))
                .Venue("v", new Vec2(0f, 0f), 2f, null, new[] { Activity.Ale }, seatedOn: new[] { "table" }), "a piece with no seat");
            TestLog.Log($"[PASS] Test 4: Seats on furniture: {seated} venue(s) seat crawlers on their pieces, in order; the tavern's stools round its tables; the builder's refusals.");
        }

        private static void Refused(Action<MapDefinitionBuilder> add, string what)
        {
            var b = new MapDefinitionBuilder("Yard", new RectF(-10f, -10f, 20f, 20f));
            b.Sector(1, "Yard", "", new RectF(-8f, -8f, 16f, 16f));
            add(b);
            try
            {
                b.Build();
            }
            catch (InvalidOperationException)
            {
                return;
            }
            throw new InvalidOperationException("[VenueValidator] FAILED: The builder refuses a venue with " + what + ".");
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
