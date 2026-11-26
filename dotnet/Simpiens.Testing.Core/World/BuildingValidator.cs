using System;
using Simpiens.Core.Session.Layouts;
using Simpiens.Simulation.Places;
using Simpiens.Simulation.Spatial;
using Simpiens.Simulation.World;
using Simpiens.Testing.Sessions;

namespace Simpiens.Testing.World
{
    /// <summary>
    /// Buildings as map data (Epic PLACES plan 1, B.1, D2–D5, D8): the building table and lot sizes; the builder's refusals (no
    /// zone or two, an overlap, a door off its edges or crowding a corner or another door, no door, a kind too big for its lot,
    /// off whole units, a kind with no row); its walls written once, less its doors, never over a wall the map has (a building on
    /// a zone's walls adds none and leaves the grid as it was); <see cref="MapDefinition.BuildingAt"/>; and on every shipped map,
    /// each building's doors open onto its interior, its inside is walked to from its zone through them, and with its doorways
    /// walled up there is no way in (B.2: the Dev Map's barracks and empty lot). Runs as a test (ValidatorTests).
    /// </summary>
    public class BuildingValidator
    {
        private static readonly RectF Bounds = new RectF(-30f, -30f, 60f, 60f);

        public void RunAllValidationTests()
        {
            TestLog.Log("[BuildingValidator] Beginning building validation...");
            ValidateTheTable();
            ValidateTheBuilder();
            ValidateTheWalls();
            ValidateBuildingAt();
            ValidateShippedMaps();
            TestLog.Log("<color=green>[BuildingValidator] ALL 5 TESTS PASSED SUCCESSFULLY!</color>");
        }

        private void ValidateTheTable()
        {
            var keys = new System.Collections.Generic.HashSet<string>();
            foreach (BuildingKind kind in Enum.GetValues(typeof(BuildingKind)))
            {
                var row = BuildingTable.Get(kind);
                Assert(row.Kind == kind, $"{kind} has its row.");
                Assert(Latin1(row.Word) && Latin1(row.Title) && Latin1(row.Description) && row.Word.Length > 0 && row.Title.Length > 0, $"{kind}'s words are there, in Latin-1.");
                Assert(keys.Add(row.ArtKey) && Kebab(row.ArtKey), $"{kind}'s art key '{row.ArtKey}' is kebab case and its own.");
                Assert((kind == BuildingKind.None) == (row.Walls == WallMaterial.Foundations) && (kind == BuildingKind.None) == (row.Floor == FloorMaterial.Bare),
                    $"Only the empty lot is foundations and bare ground ({kind}).");
            }
            Assert(BuildingTable.Get((BuildingKind)200).Kind == BuildingKind.None, "A kind with no row reads as the empty lot.");
            Assert(BuildingTable.SizeOf(new RectF(0f, 0f, 8f, 6f)) == LotSize.Small && BuildingTable.SizeOf(new RectF(0f, 0f, 8f, 8f)) == LotSize.Medium
                && BuildingTable.SizeOf(new RectF(0f, 0f, 16f, 6f)) == LotSize.Medium && BuildingTable.SizeOf(new RectF(0f, 0f, 16f, 8f)) == LotSize.Large,
                "Lots by area: 8 x 6 small, 8 x 8 and 16 x 6 medium, 16 x 8 large.");
            TestLog.Log("[PASS] Test 1: The building table: a row a kind, words, art keys, the lot, sizes by area.");
        }

        private void ValidateTheBuilder()
        {
            var footprint = new RectF(-6f, -4f, 12f, 8f);
            var door = new Vec2(0f, -4f);
            Assert(Yard().Building("hall", BuildingKind.Bunkhouse, footprint, door).Build().Buildings.Count == 1, "A building in one zone stands.");
            Refused(b => b.Building("hall", BuildingKind.Bunkhouse, new RectF(-26f, -4f, 12f, 8f), new Vec2(-20f, -4f)), "in no zone");
            Refused(b => b.Building("hall", BuildingKind.Bunkhouse, new RectF(-6f, 16f, 12f, 8f), new Vec2(0f, 16f)), "across two zones", twoZones: true);
            Refused(b => b.Building("hall", BuildingKind.Bunkhouse, footprint, door).Building("shed", BuildingKind.Barracks, new RectF(2f, 0f, 8f, 8f), new Vec2(6f, 8f)), "overlapping another");
            Assert(Yard().Building("hall", BuildingKind.Bunkhouse, footprint, door).Building("shed", BuildingKind.Barracks, new RectF(6f, -4f, 8f, 8f), new Vec2(10f, 4f)).Build().Buildings.Count == 2,
                "Two buildings may share a wall.");
            Refused(b => b.Building("hall", BuildingKind.Bunkhouse, footprint), "with no door");
            Refused(b => b.Building("hall", BuildingKind.Bunkhouse, footprint, new Vec2(0f, -2f)), "with a door off its edges");
            Refused(b => b.Building("hall", BuildingKind.Bunkhouse, footprint, new Vec2(-4f, -4f)), "with a door crowding its corner");
            Refused(b => b.Building("hall", BuildingKind.Bunkhouse, footprint, new Vec2(-6f, -4f)), "with a door on its corner");
            Refused(b => b.Building("hall", BuildingKind.Bunkhouse, footprint, new Vec2(0f, -4f), new Vec2(2f, -4f)), "with two doors overlapping");
            Refused(b => b.Building("hall", BuildingKind.Bunkhouse, new RectF(-4f, -4f, 8f, 6f), new Vec2(0f, -4f)), "a bunkhouse on a small lot");
            Refused(b => b.Building("hall", BuildingKind.Bunkhouse, new RectF(-6.5f, -4f, 12f, 8f), new Vec2(-0.5f, -4f)), "off whole units");
            Refused(b => b.Building("hall", (BuildingKind)200, footprint, door), "of a kind with no row");
            Refused(b => b.Building("hall", BuildingKind.Bunkhouse, footprint, door).Building("hall", BuildingKind.Barracks, new RectF(-20f, -20f, 8f, 8f), new Vec2(-16f, -12f)), "with a key taken");
            Assert(Yard().Lot("lot", new RectF(-4f, -4f, 8f, 6f), new Vec2(0f, -4f)).Build().Buildings[0].IsLot, "An empty lot is a building of kind None.");
            TestLog.Log("[PASS] Test 2: The builder refuses a building in no zone or two, overlapping, with no door, a door off its edges, crowding a corner or another door, a kind too big for its lot, off whole units, of no row, or with a key taken.");
        }

        private void ValidateTheWalls()
        {
            // In the open: its perimeter, 40 cells, less its door's five
            var map = Yard().Building("hall", BuildingKind.Bunkhouse, new RectF(-6f, -4f, 12f, 8f), new Vec2(0f, -4f)).Build();
            var hall = map.Buildings[0];
            Assert(hall.WallCellsAdded == 35, $"In the open, its walls are its perimeter less its door ({hall.WallCellsAdded} cells, not 35).");
            Assert(hall.Sign == new Vec2(4f, -5f), $"Its sign stands outside its door's wall, past the post ({hall.Sign}).");
            var grid = MapMeasure.Rasterized(map);
            Assert(Walkable(grid, new Vec2(0f, -4f)) && !Walkable(grid, new Vec2(3f, -4f)) && !Walkable(grid, new Vec2(-6f, 0f)) && !Walkable(grid, new Vec2(0f, 4f)),
                "Its doorway is open; its posts and walls aren't.");
            Assert(grid.AreConnected(new Vec2(0f, 0f), new Vec2(0f, -10f)) && grid.AreConnected(new Vec2(0f, 0f), new Vec2(15f, 15f)), "Its inside is reached through its door.");
            var shut = Yard().Building("hall", BuildingKind.Bunkhouse, new RectF(-6f, -4f, 12f, 8f), new Vec2(0f, -4f)).Wall(new RectF(-3f, -5f, 6f, 2f)).Build();
            Assert(!MapMeasure.Rasterized(shut).AreConnected(new Vec2(0f, 0f), new Vec2(0f, -10f)), "With its door walled up, its inside can't be reached.");

            // Against the zone's west wall: that side is the zone's; corners once
            var against = Yard().Building("shed", BuildingKind.Barracks, new RectF(-20f, -4f, 8f, 8f), new Vec2(-16f, 4f)).Build();
            Assert(against.Buildings[0].WallCellsAdded == 18, $"Against a zone's wall it adds the other three sides less its door, corners once ({against.Buildings[0].WallCellsAdded}, not 18).");

            // On a zone's walls and doors (the bunkhouse's way): none added, and the grid as it was
            MapDefinitionBuilder Room() => Yard()
                .WallRun(new Vec2(-8f, -12f), new Vec2(8f, -12f)).WallRun(new Vec2(-8f, -12f), new Vec2(-8f, -6f)).WallRun(new Vec2(8f, -12f), new Vec2(8f, -6f))
                .WallRun(new Vec2(-8f, -6f), new Vec2(1f, -6f)).WallRun(new Vec2(7f, -6f), new Vec2(8f, -6f));
            var bare = Room().Build();
            var built = Room().Building("bunks", BuildingKind.Bunkhouse, new RectF(-8f, -12f, 16f, 6f), new Vec2(4f, -6f)).Build();
            Assert(built.Buildings[0].WallCellsAdded == 0 && built.Walls.Count == bare.Walls.Count, $"On the zone's walls and doors it adds none ({built.Buildings[0].WallCellsAdded}).");
            Assert(SameGrid(MapMeasure.Rasterized(bare), MapMeasure.Rasterized(built)), "The grid is as it was.");
            TestLog.Log("[PASS] Test 3: Walls written once: the perimeter less its doors in the open, the zone's side left to it, none on a zone's walls; doors open, posts shut, the inside reached through its door.");
        }

        private void ValidateBuildingAt()
        {
            var map = Yard().Building("hall", BuildingKind.Bunkhouse, new RectF(-6f, -4f, 12f, 8f), new Vec2(0f, -4f))
                .Building("shed", BuildingKind.Barracks, new RectF(6f, -4f, 8f, 8f), new Vec2(10f, 4f)).Build();
            Assert(map.BuildingAt(new Vec2(0f, 0f)) == 0 && map.BuildingAt(new Vec2(10f, 0f)) == 1, "Inside each, its own.");
            Assert(map.BuildingAt(new Vec2(-5.4f, 3.4f)) == 0, "Its inside runs to its walls' faces.");
            Assert(map.BuildingAt(new Vec2(0f, -4f)) == -1 && map.BuildingAt(new Vec2(6f, 0f)) == -1 && map.BuildingAt(new Vec2(-6f, 0f)) == -1, "In a doorway or on a wall, none.");
            Assert(map.BuildingAt(new Vec2(0f, -10f)) == -1, "Outside, none.");
            Assert(map.TryGetBuilding("shed", out var shed) && shed.Kind == BuildingKind.Barracks && !map.TryGetBuilding("barn", out _), "Found by its key.");
            Assert(new MapDefinitionBuilder("Empty", Bounds).Build().BuildingAt(new Vec2(0f, 0f)) == -1, "A map with none has none anywhere.");
            TestLog.Log("[PASS] Test 4: BuildingAt: inside each building its own; on a wall, in a doorway or outside, none.");
        }

        private void ValidateShippedMaps()
        {
            int doors = 0, buildings = 0;
            foreach (var map in new[] { Floor1Layout.Definition, DevMapLayout.Definition })
            {
                var grid = MapMeasure.Rasterized(map);
                for (int i = 0; i < map.Buildings.Count; i++)
                {
                    var b = map.Buildings[i];
                    Assert(map.TryGetSector(b.SectorId, out var zone), $"{map.Name}'s '{b.Key}' is in a zone.");
                    Assert(map.BuildingAt(b.Sign) < 0 && map.FindSector(b.Sign).Id != 0 && !WallRasterizer.CircleOverlapsWall(map, b.Sign, 0.05f),
                        $"{map.Name}'s '{b.Key}' sign stands outside it, in a zone (its own, or the next for a building filling its own), clear of every wall ({b.Sign}).");
                    var walks = MapMeasure.Walks(grid, zone.TargetPoint);
                    var firstInside = b.Doors[0] + Inward(b.Footprint, b.Doors[0]);
                    Assert(MapMeasure.WalkTo(grid, walks, firstInside) >= 0f, $"{map.Name}'s '{b.Key}' is walked into from its zone.");
                    buildings++;
                    for (int d = 0; d < b.Doors.Count; d++)
                    {
                        var inward = Inward(b.Footprint, b.Doors[d]);
                        var inside = b.Doors[d] + inward;
                        var outside = b.Doors[d] - inward;
                        Assert(Walkable(grid, b.Doors[d]) && Walkable(grid, inside) && Walkable(grid, outside) && grid.AreConnected(inside, outside),
                            $"{map.Name}'s '{b.Key}' door at {b.Doors[d]} opens onto its inside.");
                        Assert(map.BuildingAt(inside) == i, $"{map.Name}'s '{b.Key}': a step in through its door is inside it.");
                        doors++;
                    }

                    // No way in but its doors: with every doorway walled up, its inside is cut off from its zone
                    var shut = MapMeasure.Rasterized(map);
                    for (int d = 0; d < b.Doors.Count; d++) WallUp(shut, b.Footprint, b.Doors[d]);
                    shut.EnsureRegions();
                    Assert(!shut.AreConnected(firstInside, b.Doors[0] - Inward(b.Footprint, b.Doors[0])), $"{map.Name}'s '{b.Key}' has no way in but its doors.");
                }
            }
            Assert(buildings >= 3, $"The shipped maps hold buildings to check ({buildings}).");
            // Floor 1's bunkhouse (B.5, D6): it fills its room on its walls and doors, so it adds no wall and moves nothing
            Assert(Floor1Layout.Definition.TryGetBuilding(Floor1Layout.BunkhouseBuilding, out var bunkhouse) && bunkhouse.WallCellsAdded == 0
                && bunkhouse.SectorId == Floor1Layout.BunkhouseSectorId && bunkhouse.Row.Title == "Bunkhouse", "Floor 1's bunkhouse fills its room and adds no wall.");
            for (int i = 0; i < Floor1Layout.Definition.Pois.Count; i++)
            {
                var poi = Floor1Layout.Definition.Pois[i];
                if (poi.Kind == PoiKind.Bunk) Assert(bunkhouse.Contains(poi.Position), $"Bunk '{poi.Key}' is inside the bunkhouse.");
            }
            TestLog.Log($"[PASS] Test 5: On the shipped maps, every building is walked into from its zone through its doors, and has no other way in ({buildings} buildings, {doors} doors).");
        }

        /// <summary>Blocks the cells of a doorway: its gap, post to post, across its wall line (three rows, as a wall on a half unit blocks).</summary>
        private static void WallUp(StaticNavGrid grid, RectF footprint, Vec2 door)
        {
            bool horizontal = SimMath.Approximately(door.Y, footprint.YMin) || SimMath.Approximately(door.Y, footprint.YMax);
            float half = BuildingDefinition.DoorSpan * 0.5f;
            for (float t = -half + 0.5f; t < half; t += 1f)
            {
                for (float across = -1f; across <= 1f; across += 1f)
                {
                    var point = horizontal ? new Vec2(door.X + t, door.Y + across) : new Vec2(door.X + across, door.Y + t);
                    if (grid.WorldToCell(point, out int x, out int y)) grid.SetWall(x, y);
                }
            }
        }

        // --- Helpers ---

        /// <summary>An open yard: one zone over most of the map with its walls on its edges, or two side by side (the north half its own).</summary>
        private static MapDefinitionBuilder Yard(bool twoZones = false)
        {
            var b = new MapDefinitionBuilder("Yard", Bounds);
            if (twoZones)
            {
                b.Sector(1, "South", "", new RectF(-20f, -20f, 40f, 20f));
                b.Sector(2, "North", "", new RectF(-20f, 0f, 40f, 20f));
            }
            else
            {
                b.Sector(1, "Yard", "", new RectF(-20f, -20f, 40f, 40f));
            }
            b.WallRun(new Vec2(-20f, -20f), new Vec2(20f, -20f)).WallRun(new Vec2(-20f, 20f), new Vec2(20f, 20f))
             .WallRun(new Vec2(-20f, -20f), new Vec2(-20f, 20f)).WallRun(new Vec2(20f, -20f), new Vec2(20f, 20f));
            return b;
        }

        private static void Refused(Action<MapDefinitionBuilder> add, string what, bool twoZones = false)
        {
            var b = Yard(twoZones);
            add(b);
            try
            {
                b.Build();
            }
            catch (InvalidOperationException)
            {
                return;
            }
            throw new InvalidOperationException("[BuildingValidator] FAILED: The builder refuses a building " + what + ".");
        }

        private static Vec2 Inward(RectF footprint, Vec2 door)
        {
            if (SimMath.Approximately(door.Y, footprint.YMin)) return new Vec2(0f, 1f);
            if (SimMath.Approximately(door.Y, footprint.YMax)) return new Vec2(0f, -1f);
            if (SimMath.Approximately(door.X, footprint.XMin)) return new Vec2(1f, 0f);
            return new Vec2(-1f, 0f);
        }

        private static bool Walkable(StaticNavGrid grid, Vec2 point) => grid.WorldToCell(point, out int x, out int y) && grid.IsCellWalkable(x, y);

        private static bool SameGrid(StaticNavGrid a, StaticNavGrid b)
        {
            if (a.Width != b.Width || a.Height != b.Height) return false;
            for (int y = 0; y < a.Height; y++)
                for (int x = 0; x < a.Width; x++)
                    if (a.IsCellWalkable(x, y) != b.IsCellWalkable(x, y)) return false;
            return true;
        }

        private static bool Latin1(string text)
        {
            foreach (char c in text) if (c > 0xFF) return false;
            return true;
        }

        private static bool Kebab(string key)
        {
            if (string.IsNullOrEmpty(key) || key[0] == '-' || key[^1] == '-') return false;
            foreach (char c in key) if (!(c >= 'a' && c <= 'z') && c != '-') return false;
            return true;
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("[BuildingValidator] FAILED: " + message);
        }
    }
}
