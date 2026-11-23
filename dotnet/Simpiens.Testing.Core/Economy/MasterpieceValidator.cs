using System;
using Simpiens.Cognition.Inventory;
using Simpiens.Simulation;
using Simpiens.Simulation.Economy;
using Simpiens.Simulation.Items;
using Simpiens.Simulation.Persistence;
using Simpiens.Simulation.Workshops;

namespace Simpiens.Testing.Economy
{
    /// <summary>
    /// Masterpieces' foundation (Epic 11, S8.1, D38–D40): an item instance's mark (0 on an ordinary piece, kept through its
    /// copies, never stacked with another), the masterpiece's grade above Masterwork, the record's owners (the first and the
    /// last three), the service's ids and its save, and a save refused when its records are out of order or a piece carries
    /// a mark with no record. Runs as a test (ValidatorTests).
    /// </summary>
    public class MasterpieceValidator
    {
        private static readonly EntityId A = new EntityId(1u, 0u, 0u, 1u);
        private static readonly EntityId B = new EntityId(2u, 0u, 0u, 2u);
        private static readonly EntityId C = new EntityId(3u, 0u, 0u, 3u);
        private static readonly EntityId D = new EntityId(4u, 0u, 0u, 4u);
        private static readonly EntityId E = new EntityId(5u, 0u, 0u, 5u);

        public void RunAllValidationTests()
        {
            TestLog.Log("[MasterpieceValidator] Beginning masterpiece validation...");
            ValidateTheMark();
            ValidateTheGrade();
            ValidateTheOwners();
            ValidateTheService();
            ValidateTheSaveCheck();
            ValidateThroughHands();
            TestLog.Log("<color=green>[MasterpieceValidator] ALL 6 TESTS PASSED SUCCESSFULLY!</color>");
        }

        private static void ValidateTheMark()
        {
            var plain = new ItemInstance(ItemType.IronBlade, 1, 30, quality: Grades.Masterwork, durability: 40);
            Assert(plain.Masterpiece == 0 && !plain.IsMasterpiece, "An ordinary piece carries no mark (the old id's 1 retired).");
            var marked = plain.WithMasterpiece(7);
            Assert(marked.IsMasterpiece && marked.Masterpiece == 7 && marked.Type == ItemType.IronBlade && marked.Durability == 40, "A piece marked keeps everything else.");
            Assert(marked.WithDurability(12).Masterpiece == 7 && marked.WithQuantity(1).Masterpiece == 7, "Its wear and its count keep the mark.");
            Assert(marked != plain && marked.WithMasterpiece(0) == plain, "Two pieces differ by their marks alone.");
            Assert(System.Runtime.CompilerServices.Unsafe.SizeOf<ItemInstance>() == 16, "The instance is still 16 bytes.");

            // Stacking: a stackable good shows the rule (a worn piece stacks to one anyway)
            var pack = new AgentInventory(4, startingGold: 0);
            pack.TryAddItem(new ItemInstance(ItemType.IronOre, 2, 4), out _);
            pack.TryAddItem(new ItemInstance(ItemType.IronOre, 1, 4).WithMasterpiece(3), out _);
            int stacks = 0;
            for (int i = 0; i < pack.Capacity; i++) if (!pack.GetSlot(i).IsEmpty) stacks++;
            Assert(stacks == 2 && pack.GetItemCount(ItemType.IronOre) == 3, "A marked piece stacks only with itself.");
            TestLog.Log("[PASS] The mark: 0 on an ordinary piece, kept through copies, never stacked with another (D39).");
        }

        private static void ValidateTheGrade()
        {
            Assert(Grades.NameOf(Grades.Masterpiece) == "Masterpiece" && Grades.NameOf(Grades.Masterwork) == "Masterwork", "The grades' names.");
            Assert(Grades.RimOf(ItemRarity.Common, Grades.Masterpiece) == ItemRarity.Legendary, "A masterpiece's rim is Legendary's.");
            var blade = ItemCatalog.Get(ItemType.IronBlade).Equipment;
            Assert(blade.DurabilityAt(Grades.Masterpiece) > blade.DurabilityAt(Grades.Masterwork), "A masterpiece lasts longer than a masterwork.");
            var modifier = blade.GetModifier(0);
            Assert(blade.ValueAt(modifier, Grades.Masterpiece) > blade.ValueAt(modifier, Grades.Masterwork), "Its effect is a grade stronger.");
            TestLog.Log("[PASS] The grade: a step above Masterwork by the equipment's own rule, the Legendary rim (D38).");
        }

        private static void ValidateTheOwners()
        {
            var record = default(MasterpieceRecord);
            Assert(record.OwnerCount == 0 && record.LastOwner == default && record.RecentCount == 0, "Nobody owns a new masterpiece.");
            record.AddOwner(A);
            Assert(record.OwnerCount == 1 && record.FirstOwner == A && record.LastOwner == A && record.RecentCount == 0, "Its first owner.");
            record.AddOwner(A);
            Assert(record.OwnerCount == 1, "The same owner again is not a new one.");
            record.AddOwner(B);
            record.AddOwner(C);
            Assert(record.OwnerCount == 3 && record.LastOwner == C && record.RecentCount == 2 && record.RecentAt(0) == B, "Later owners in order.");
            record.AddOwner(D);
            record.AddOwner(E);
            Assert(record.OwnerCount == 5 && record.FirstOwner == A && record.RecentCount == 3 && record.RecentAt(0) == C && record.RecentAt(1) == D && record.LastOwner == E,
                "The first kept for good, the last three after it.");
            record.AddOwner(default);
            Assert(record.OwnerCount == 5, "Nobody is not an owner.");
            TestLog.Log("[PASS] The owners: the first and the last three (D40).");
        }

        private static void ValidateTheService()
        {
            var service = new MasterpieceService();
            Assert(service.Count == 0 && service.NextId == 1 && !service.TryGet(1, out _) && !service.TryGet(0, out _), "None made yet.");
            ushort first = service.Add(new MasterpieceRecord { Item = ItemType.IronBlade, Maker = A, MadeAt = 100u });
            ushort second = service.Add(new MasterpieceRecord { Item = ItemType.RingCoat, Maker = B, MadeAt = 200u, Place = MasterpiecePlace.Shelf });
            bool found = service.TryGet(2, out var coat);
            Assert(first == 1 && second == 2 && service.Count == 2 && found && coat.Id == 2 && coat.Maker == B, "Ids from 1, in the order made.");
            coat.AddOwner(C);
            coat.Place = MasterpiecePlace.World;
            Assert(service.Set(coat) && service.TryGet(2, out var again) && again.LastOwner == C && again.Place == MasterpiecePlace.World, "A record changed.");
            Assert(!service.Set(new MasterpieceRecord { Id = 9 }), "A record never made can't be set.");
            var saved = service.Capture();
            var restored = new MasterpieceService();
            restored.Restore(saved);
            Assert(restored.Count == 2 && restored.TryGet(1, out var blade) && blade.Maker == A && restored.NextId == 3, "Captured and restored, by id.");
            Assert(MasterpieceService.Check(saved) == null, "A save's records in order are taken.");
            Assert(MasterpieceService.Check(new[] { saved[1], saved[0] }) != null, "Records out of order are refused.");
            Assert(MasterpieceService.Check(new[] { new MasterpieceRecord { Id = 1, Item = ItemType.Ration } }) != null, "A masterpiece of something that isn't worn is refused.");
            restored.Restore(null);
            Assert(restored.Count == 0, "A save without records: none.");
            TestLog.Log("[PASS] The service: ids from 1, records set, captured and checked (D40).");
        }

        private static void ValidateTheSaveCheck()
        {
            var records = new[] { new MasterpieceRecord { Id = 1, Item = ItemType.IronBlade } };
            SessionSave With(ushort mark, MasterpieceRecord[] held) => new SessionSave
            {
                Workshops = new WorkshopSave { Masterpieces = held },
                World = new WorldSave
                {
                    Piles = new PileSave
                    {
                        Piles = new[] { new PileRecord { Id = A, StackCount = 1 } },
                        Stacks = new[] { new ItemInstance(ItemType.IronBlade, 1, 0).WithMasterpiece(mark) }
                    }
                }
            };
            Assert(SaveItems.FirstUnknownMasterpiece(With(1, records)) == null, "A marked piece with its record loads.");
            Assert(SaveItems.FirstUnknownMasterpiece(With(0, null)) == null, "An ordinary piece needs no record.");
            string refused = SaveItems.FirstUnknownMasterpiece(With(2, records));
            Assert(refused != null && refused.Contains("masterpiece 2") && refused.Contains("pile"), $"A mark with no record is refused, with where it is: {refused}");
            Assert(SaveItems.FirstUnknownMasterpiece(With(1, new[] { new MasterpieceRecord { Id = 4, Item = ItemType.IronBlade } })) != null, "Records out of order are refused.");
            TestLog.Log("[PASS] The save's check: a mark with no record, or records out of order, refused (D39).");
        }

        private static void ValidateThroughHands()
        {
            var service = new MasterpieceService();
            var stall = new TradeStallData(new EntityId(9u, 0u, 0u, 9u), Vec2.Zero, 100u).WithGood(new StallGood(ItemType.IronBlade, StallRole.Sells, 40, 0));
            var plain = service.PieceFor(ItemType.IronBlade, Grades.Masterwork, 40, out ushort none);
            Assert(none == 0 && !plain.IsMasterpiece && plain.Quality == Grades.Masterwork, "No masterpiece on the shelf: an ordinary Masterwork piece.");
            ushort id = service.Add(new MasterpieceRecord { Item = ItemType.IronBlade, Place = MasterpiecePlace.Shelf });
            service.Add(new MasterpieceRecord { Item = ItemType.IronBlade, Place = MasterpiecePlace.Held });
            Assert(service.OnShelf(ItemType.IronBlade) == id && service.OnShelf(ItemType.RingCoat) == 0, "The shelf's masterpiece of an item; a held one isn't on it.");
            var fine = service.PieceFor(ItemType.IronBlade, Grades.Fine, 40, out ushort notYet);
            Assert(notYet == 0 && fine.Quality == Grades.Fine, "Only a Masterwork take hands out a masterpiece (the shelf's best is its grade).");
            var piece = service.PieceFor(ItemType.IronBlade, Grades.Masterwork, 40, out ushort handed);
            var blade = ItemCatalog.Get(ItemType.IronBlade).Equipment;
            Assert(handed == id && piece.Masterpiece == id && piece.Quality == Grades.Masterpiece && piece.Durability == blade.DurabilityAt(Grades.Masterpiece),
                "A Masterwork take hands out the masterpiece, at its grade and life, marked.");
            Assert(service.TryGet(id, out var still) && still.Place == MasterpiecePlace.Shelf, "Nothing changes until it is handed out.");
            service.HandOut(id, A);
            Assert(service.TryGet(id, out var out_) && out_.Place == MasterpiecePlace.World && out_.LastOwner == A && service.OnShelf(ItemType.IronBlade) == 0, "Handed out: off the shelf, its owner.");
            service.TakenUp(id, B);
            Assert(service.TryGet(id, out var taken) && taken.OwnerCount == 2 && taken.LastOwner == B, "Taken up from the ground: its next owner.");

            var wearer = new AgentInventory(4, startingGold: 0);
            wearer.TryEquip(piece);
            service.CountKill(wearer, named: false, default, 0);
            service.CountKill(wearer, named: true, C, 2);
            Assert(service.TryGet(id, out var bloodied) && bloodied.Kills == 2 && bloodied.NamedSlain == 1 && bloodied.SlainVictim == C && bloodied.SlainRank == 2,
                "Kills while it is worn; a named creature's by whom it was named for and its rank.");
            service.CountKill(new AgentInventory(4, startingGold: 0), named: false, default, 0);
            Assert(service.TryGet(id, out var same) && same.Kills == 2, "A kill without it counts nothing on it.");

            var back = service.HandBack(stall, piece.WithDurability(3));
            Assert(back.TryGetGood(ItemType.IronBlade, out var good) && good.Stock == 1 && good.Masterwork == 1 && good.Worn == 0, "Handed back: onto the shelf whole, among the Masterwork pieces.");
            Assert(service.TryGet(id, out var shelved) && shelved.Place == MasterpiecePlace.Shelf, "On the shelf again.");
            var worn = service.HandBack(stall, plain.WithDurability(3));
            Assert(worn.TryGetGood(ItemType.IronBlade, out var mend) && mend.Worn == 1 && mend.Masterwork == 0, "An ordinary piece goes onto the worn shelf, as before.");
            var elsewhere = new TradeStallData(new EntityId(8u, 0u, 0u, 8u), Vec2.Zero, 100u);
            Assert(service.HandBack(elsewhere, piece).Equals(elsewhere), "A stall that doesn't trade it takes nothing.");
            TestLog.Log("[PASS] Through hands: the shelf's masterpiece first, handed out, taken up, its kills, handed back whole (D43, D44).");
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException($"[MasterpieceValidator] FAILED: {message}");
        }
    }
}
