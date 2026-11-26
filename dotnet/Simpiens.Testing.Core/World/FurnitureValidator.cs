using System;
using Simpiens.Core.Session.Layouts;
using Simpiens.Simulation.Places;
using Simpiens.Simulation.World;
using Simpiens.Testing.Sessions;

namespace Simpiens.Testing.World
{
    /// <summary>
    /// Furniture as a table and map data (Epic PLACES plan 1, F.1, D11–D12): the furniture table (a row a kind, words, art keys,
    /// footprints, only a table blocks, seats and beds where they belong, beauty and comfort in bounds); the builder's refusals (in
    /// a wall, in no zone, over another piece, a kind with no row, a key taken); a placement's footprint, seats and beds, turned
    /// and not, and the zone and building it stands in; and on every shipped map, each piece's seats and beds clear of every wall
    /// and in its zone. Runs as a test (ValidatorTests).
    /// </summary>
    public class FurnitureValidator
    {
        private static readonly RectF Bounds = new RectF(-30f, -30f, 60f, 60f);

        public void RunAllValidationTests()
        {
            TestLog.Log("[FurnitureValidator] Beginning furniture validation...");
            ValidateTheTable();
            ValidateTheBuilder();
            ValidateAPlacement();
            ValidateShippedMaps();
            TestLog.Log("<color=green>[FurnitureValidator] ALL 4 TESTS PASSED SUCCESSFULLY!</color>");
        }

        private void ValidateTheTable()
        {
            var keys = new System.Collections.Generic.HashSet<string>();
            foreach (FurnitureKind kind in Enum.GetValues(typeof(FurnitureKind)))
            {
                var row = FurnitureTable.Get(kind);
                if (kind == FurnitureKind.None)
                {
                    Assert(row == null, "None has no row.");
                    continue;
                }
                Assert(row != null && row.Kind == kind, $"{kind} has its row.");
                Assert(Latin1(row.Word) && Latin1(row.Title) && row.Word.Length > 0 && row.Title.Length > 0, $"{kind}'s words are there, in Latin-1.");
                Assert(keys.Add(row.ArtKey) && Kebab(row.ArtKey), $"{kind}'s art key '{row.ArtKey}' is kebab case and its own.");
                Assert(row.Width > 0f && row.Depth > 0f && row.Width <= 4f && row.Depth <= 4f, $"{kind}'s footprint is a few units at most ({row.Width} x {row.Depth}).");
                foreach (var seat in row.Seats) Assert(Math.Abs(seat.X) <= row.Width * 0.5f && Math.Abs(seat.Y) <= row.Depth * 0.5f, $"{kind}'s seats lie on it.");
                foreach (var bed in row.Beds) Assert(Math.Abs(bed.X) <= row.Width * 0.5f && Math.Abs(bed.Y) <= row.Depth * 0.5f, $"{kind}'s beds lie on it.");
                Assert((row.Beds.Count > 0) == (row.BedFor != BedFor.None), $"{kind} has beds exactly when they are for someone.");
                Assert(!(row.Blocks && (row.Seats.Count > 0 || row.Beds.Count > 0)), $"{kind}: a piece that blocks has nobody on it.");
                Assert(row.Beauty >= -10 && row.Beauty <= 10 && row.Comfort >= -10 && row.Comfort <= 10, $"{kind}'s beauty and comfort are within -10..10.");
            }
            Assert(FurnitureTable.Get(FurnitureKind.Table).Blocks && FurnitureTable.Get((FurnitureKind)200) == null, "A table blocks; a kind with no row has none.");
            foreach (var row in FurnitureTable.All) Assert(!row.Blocks || row.Kind == FurnitureKind.Table, $"Only a table blocks in plan 1 ({row.Kind}).");
            TestLog.Log("[PASS] Test 1: The furniture table: a row a kind, words, art keys, footprints, seats and beds on their pieces, only a table blocks.");
        }

        private void ValidateTheBuilder()
        {
            var map = Yard().Furniture("table", FurnitureKind.Table, new Vec2(0f, 0f)).Furniture("stool", FurnitureKind.Stool, new Vec2(1.2f, 0f)).Build();
            Assert(map.Furniture.Count == 2 && map.TryGetFurniture("stool", out var stool) && stool.Kind == FurnitureKind.Stool && !map.TryGetFurniture("chair", out _), "Pieces stand, found by their keys.");
            Refused(b => b.Furniture("table", FurnitureKind.Table, new Vec2(-19.8f, 0f)), "in a wall");
            Refused(b => b.Furniture("table", FurnitureKind.Table, new Vec2(25f, 0f)), "in no zone");
            Refused(b => b.Furniture("table", FurnitureKind.Table, new Vec2(0f, 0f)).Furniture("stool", FurnitureKind.Stool, new Vec2(0.6f, 0f)), "over another piece");
            Refused(b => b.Furniture("thing", (FurnitureKind)200, new Vec2(0f, 0f)), "of a kind with no row");
            Refused(b => b.Furniture("thing", FurnitureKind.None, new Vec2(0f, 0f)), "of no kind");
            Refused(b => b.Furniture("table", FurnitureKind.Table, new Vec2(0f, 0f)).Furniture("table", FurnitureKind.Stool, new Vec2(4f, 0f)), "with a key taken");
            Refused(b => b.Building("hall", BuildingKind.Bunkhouse, new RectF(-6f, -4f, 12f, 8f), new Vec2(0f, -4f)).Furniture("bench", FurnitureKind.Bench, new Vec2(5.6f, 0f), turned: true),
                "in a building's wall");
            TestLog.Log("[PASS] Test 2: The builder refuses a piece in a wall (a zone's or a building's), in no zone, over another, of no row, or with a key taken.");
        }

        private void ValidateAPlacement()
        {
            var map = Yard().Building("hall", BuildingKind.Bunkhouse, new RectF(-6f, -4f, 12f, 8f), new Vec2(0f, -4f))
                .Furniture("bench", FurnitureKind.Bench, new Vec2(-2f, 0f))
                .Furniture("turned", FurnitureKind.Bench, new Vec2(2f, 0f), turned: true)
                .Furniture("bunk", FurnitureKind.Bunk, new Vec2(0f, -10f)).Build();
            map.TryGetFurniture("bench", out var bench);
            map.TryGetFurniture("turned", out var turned);
            map.TryGetFurniture("bunk", out var bunk);
            Assert(bench.Footprint == new RectF(-2.9f, -0.3f, 1.8f, 0.6f) && turned.Footprint == new RectF(1.7f, -0.9f, 0.6f, 1.8f), $"A footprint by its row, its sides swapped when turned ({bench.Footprint}, {turned.Footprint}).");
            Assert(bench.SeatCount == 2 && bench.SeatAt(0) == new Vec2(-2.45f, 0f) && bench.SeatAt(1) == new Vec2(-1.55f, 0f), "A bench's two seats along it.");
            Assert(turned.SeatAt(0) == new Vec2(2f, -0.45f) && turned.SeatAt(1) == new Vec2(2f, 0.45f), "Turned, its seats run the other way.");
            Assert(bunk.BedCount == 1 && bunk.BedAt(0) == new Vec2(0f, -10f) && bunk.SeatCount == 0 && bunk.Row.BedFor == BedFor.Residents, "A bunk's bed at its centre, for residents.");
            Assert(bench.Building == 0 && turned.Building == 0 && bunk.Building == -1 && bench.SectorId == 1 && bunk.SectorId == 1, "Each knows its zone and the building it stands in.");
            TestLog.Log("[PASS] Test 3: A placement: its footprint, seats and beds by its row, turned or not; its zone and building.");
        }

        private void ValidateShippedMaps()
        {
            int pieces = 0;
            foreach (var map in new[] { Floor1Layout.Definition, DevMapLayout.Definition })
            {
                for (int i = 0; i < map.Furniture.Count; i++)
                {
                    var piece = map.Furniture[i];
                    for (int s = 0; s < piece.SeatCount; s++)
                        Assert(!WallRasterizer.CircleOverlapsWall(map, piece.SeatAt(s), 0.25f) && map.FindSector(piece.SeatAt(s)).Id == piece.SectorId, $"{map.Name}'s '{piece.Key}' seat {s} is clear of walls, in its zone.");
                    for (int b = 0; b < piece.BedCount; b++)
                        Assert(!WallRasterizer.CircleOverlapsWall(map, piece.BedAt(b), 0.25f) && map.FindSector(piece.BedAt(b)).Id == piece.SectorId, $"{map.Name}'s '{piece.Key}' bed {b} is clear of walls, in its zone.");
                    pieces++;
                }
            }
            TestLog.Log($"[PASS] Test 4: On the shipped maps, every piece's seats and beds are clear of walls and in its zone ({pieces} pieces).");
        }

        // --- Helpers ---

        private static MapDefinitionBuilder Yard()
        {
            var b = new MapDefinitionBuilder("Yard", Bounds);
            b.Sector(1, "Yard", "", new RectF(-20f, -20f, 40f, 40f));
            b.WallRun(new Vec2(-20f, -20f), new Vec2(20f, -20f)).WallRun(new Vec2(-20f, 20f), new Vec2(20f, 20f))
             .WallRun(new Vec2(-20f, -20f), new Vec2(-20f, 20f)).WallRun(new Vec2(20f, -20f), new Vec2(20f, 20f));
            return b;
        }

        private static void Refused(Action<MapDefinitionBuilder> add, string what)
        {
            var b = Yard();
            add(b);
            try
            {
                b.Build();
            }
            catch (InvalidOperationException)
            {
                return;
            }
            throw new InvalidOperationException("[FurnitureValidator] FAILED: The builder refuses a piece " + what + ".");
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
            if (!condition) throw new InvalidOperationException("[FurnitureValidator] FAILED: " + message);
        }
    }
}
