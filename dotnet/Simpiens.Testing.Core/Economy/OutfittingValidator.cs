using System;
using Simpiens.Cognition.Inventory;
using Simpiens.Simulation.Economy;
using Simpiens.Simulation.Items;
using Simpiens.Simulation.World;

namespace Simpiens.Testing.Economy
{
    /// <summary>
    /// Outfitting by slot, by stats (Epic 10 Part 3, K1.4): a piece is worth what it changes in the wearer's stacked,
    /// bounded stats for a need, with it worn in its slot, never its name or group; asking moves nothing; the best piece in
    /// stock for a slot is chosen by that gain, over what the slot holds or over the slot bare, the first in id order on a
    /// tie; and none of it allocates. Runs as a test (ValidatorTests).
    /// </summary>
    public class OutfittingValidator
    {
        private const ItemType RockVeil = (ItemType)217;
        private static readonly EntityId StallId = new EntityId(0, 0, 0, 0xa1);

        public void RunAllValidationTests()
        {
            TestLog.Log("[OutfittingValidator] Beginning outfitting validation...");

            ValidateAPieceIsWorthWhatItChangesInTheStats();
            ValidateAPieceIsScoredByItsStatsNotItsName();
            ValidateAskingMovesNothing();
            ValidateTheBestInStockForASlot();
            ValidateOutfittingAllocatesNothing();

            TestLog.Log("<color=green>[OutfittingValidator] ALL 5 TESTS PASSED SUCCESSFULLY!</color>");
        }

        private void ValidateAPieceIsWorthWhatItChangesInTheStats()
        {
            var rock = OutfitNeed.Against(HazardKind.FallingRock);
            var air = OutfitNeed.Against(HazardKind.FoulAir);
            var bare = new AgentInventory(4, startingGold: 0);
            Assert(Outfitting.Score(bare, rock) == 0f && Outfitting.Score(bare, default) == 0f, "Bare: nothing stopped; nothing to outfit for scores 0.");
            Assert(Near(Outfitting.Gain(bare, rock, EquipmentSlot.Body, Outfitting.NewPiece(ItemType.HazardGear, Grades.Standard)), 0.5f), "Hazard gear on a bare body: 0.5 against falling rock.");
            Assert(Near(Outfitting.Gain(bare, rock, EquipmentSlot.Head, Outfitting.NewPiece(ItemType.HardHat, Grades.Standard)), 0.75f), "A hard hat on a bare head: 0.75.");

            var geared = new AgentInventory(4, startingGold: 0);
            geared.TryEquip(Outfitting.NewPiece(ItemType.HazardGear, Grades.Standard));
            Assert(Near(Outfitting.Gain(geared, rock, EquipmentSlot.Head, Outfitting.NewPiece(ItemType.HardHat, Grades.Standard)), 0.4f), "A hard hat over hazard gear: 0.4, the rest of the way to the bound of 0.9.");
            Assert(Outfitting.Gain(geared, air, EquipmentSlot.Head, Outfitting.NewPiece(ItemType.HardHat, Grades.Standard)) == 0f, "Against foul air a hard hat is worth nothing.");
            Assert(Near(Outfitting.Gain(geared, air, EquipmentSlot.Head, Outfitting.NewPiece(ItemType.Respirator, Grades.Standard)), 0.4f), "A respirator over hazard gear against foul air: 0.4.");

            var hatted = new AgentInventory(4, startingGold: 0);
            hatted.TryEquip(Outfitting.NewPiece(ItemType.HardHat, Grades.Standard));
            Assert(Near(Outfitting.Gain(hatted, rock, EquipmentSlot.Body, Outfitting.NewPiece(ItemType.HazardGear, Grades.Standard)), 0.15f), "Hazard gear under a hard hat against falling rock: 0.15, not its 0.5.");
            Assert(Outfitting.Gain(hatted, rock, EquipmentSlot.Head, Outfitting.NewPiece(ItemType.Respirator, Grades.Standard)) < 0f, "A respirator in the hard hat's place loses against falling rock.");
            Assert(Near(Outfitting.GainOverBare(hatted, rock, EquipmentSlot.Head, Outfitting.NewPiece(ItemType.HardHat, Grades.Standard)), 0.75f), "A fresh hard hat over the head bare: its whole 0.75.");

            Assert(Near(Outfitting.Gain(bare, OutfitNeed.ForMining, EquipmentSlot.Tool, Outfitting.NewPiece(ItemType.ToolPickaxe, Grades.Standard)), 0.5f), "A pickaxe for mining: half as fast again.");
            Assert(Near(Outfitting.Gain(bare, OutfitNeed.ForMining, EquipmentSlot.Tool, Outfitting.NewPiece(ItemType.ToolPickaxe, Grades.Fine)), 0.55f), "A Fine one: a tenth more (D56).");
            Assert(Near(Outfitting.Gain(bare, rock.And(HazardKind.FoulAir), EquipmentSlot.Body, Outfitting.NewPiece(ItemType.HazardGear, Grades.Standard)), 1f), "Facing two hazards, hazard gear counts against each.");
            TestLog.Log("[PASS] Test 1: a piece is worth what it changes in the wearer's stacked, bounded stats: a hard hat over hazard gear 0.4 against falling rock, hazard gear under a hard hat 0.15.");
        }

        private void ValidateAPieceIsScoredByItsStatsNotItsName()
        {
            using (ItemCatalog.ExtendForTest(new ItemDefinition(RockVeil, "RockVeil", "Rock Veil", ItemCategory.Equipment, ItemRarity.Common,
                equipment: new EquipmentBlock(SlotKind.Head, durability: 100, WearRule.PerHazardTick,
                    new StatModifier(StatKind.RockResistance, ModifierOp.Add, 0.75f)))))
            {
                var bare = new AgentInventory(4, startingGold: 0);
                var rock = OutfitNeed.Against(HazardKind.FallingRock);
                Assert(Outfitting.Gain(bare, rock, EquipmentSlot.Head, Outfitting.NewPiece(RockVeil, Grades.Standard)) == Outfitting.Gain(bare, rock, EquipmentSlot.Head, Outfitting.NewPiece(ItemType.HardHat, Grades.Standard)),
                    "A piece in no group, with a hard hat's stats, is worth a hard hat's.");
                Assert(Outfitting.Gain(bare, rock, EquipmentSlot.Tool, Outfitting.NewPiece(ItemType.ToolPickaxe, Grades.Standard)) == 0f, "A pickaxe is worth nothing against a hazard.");
                Assert(Outfitting.Score(bare, OutfitNeed.EveryHazard, new WornSwap(EquipmentSlot.Head, Outfitting.NewPiece(RockVeil, Grades.Standard))) > 0f, "Worn, it protects: by its stats.");
                var both = new TradeStallData(StallId, default)
                    .WithGood(new StallGood(RockVeil, StallRole.Sells, 50, 1))
                    .WithGood(new StallGood(ItemType.HardHat, StallRole.Sells, 60, 1));
                Assert(Outfitting.BestInStock(both, bare, rock, EquipmentSlot.Head, false, out _) == ItemType.HardHat, "Worth the same, the lower id is chosen, whatever the table's order.");
            }
            TestLog.Log("[PASS] Test 2: a piece is scored by its stats, not its name or group: an ungrouped piece with a hard hat's stats is worth a hard hat, and a tie goes to the lower id.");
        }

        private void ValidateAskingMovesNothing()
        {
            var inventory = new AgentInventory(4, startingGold: 0);
            inventory.TryEquip(Outfitting.NewPiece(ItemType.HazardGear, Grades.Standard));
            var swap = new WornSwap(EquipmentSlot.Body, ItemInstance.Empty);
            Assert(CrawlerStats.Get(inventory, StatKind.HazardResistance, 0f, swap) == 0f && CrawlerStats.Get(inventory, StatKind.HazardResistance) == 0.5f, "The body asked bare reads none; asked as it is, the gear's 0.5.");
            Assert(inventory.GetEquipped(EquipmentSlot.Body).Type == ItemType.HazardGear && inventory.GetEquipped(EquipmentSlot.Head).IsEmpty, "Nothing was moved.");
            Assert(CrawlerStats.Get(null, StatKind.HazardResistance, 0f, new WornSwap(EquipmentSlot.Body, Outfitting.NewPiece(ItemType.HazardGear, Grades.Standard))) == 0.5f, "With no inventory, the swapped piece alone.");
            Assert(Outfitting.NewPiece(ItemType.Ration, Grades.Standard).IsEmpty && Outfitting.NewPiece(ItemType.HardHat, Grades.Fine).Durability == 1875, "A new piece is equipment at its grade's life; a ration is none.");
            TestLog.Log("[PASS] Test 3: asking what a piece would do moves nothing; the slot asked bare reads empty.");
        }

        private void ValidateTheBestInStockForASlot()
        {
            var stall = new TradeStallData(StallId, default)
                .WithGood(new StallGood(ItemType.HazardGear, StallRole.Sells, 30, 2))
                .WithGood(new StallGood(ItemType.HardHat, StallRole.Sells, 60, 1))
                .WithGood(new StallGood(ItemType.Respirator, StallRole.Sells, 60, 1))
                .WithGood(new StallGood(ItemType.ToolPickaxe, StallRole.Sells, 40, 0));
            var rock = OutfitNeed.Against(HazardKind.FallingRock);
            var flood = OutfitNeed.Against(HazardKind.Flooding);
            var bare = new AgentInventory(4, startingGold: 0);
            Assert(Outfitting.BestInStock(stall, bare, rock, EquipmentSlot.Head, overBare: false, out float gain) == ItemType.HardHat && Near(gain, 0.75f), "The head against falling rock: the hard hat.");
            Assert(Outfitting.BestInStock(stall, bare, flood, EquipmentSlot.Head, false, out _) == ItemType.Respirator, "Against flooding: the respirator.");
            Assert(Outfitting.BestInStock(stall, bare, rock, EquipmentSlot.Body, false, out _) == ItemType.HazardGear, "The body: hazard gear.");
            Assert(Outfitting.BestInStock(stall, bare, OutfitNeed.ForMining, EquipmentSlot.Tool, false, out gain) == ItemType.None && gain == 0f, "A pickaxe out of stock is no choice.");
            Assert(Outfitting.BestInStock(stall, bare, default, EquipmentSlot.Head, false, out _) == ItemType.None, "Nothing to outfit for, nothing chosen.");

            var noRespirators = stall.WithGood(new StallGood(ItemType.Respirator, StallRole.Sells, 60, 0));
            Assert(Outfitting.BestInStock(noRespirators, bare, OutfitNeed.Against(HazardKind.FoulAir), EquipmentSlot.Head, false, out _) == ItemType.None, "Out of respirators, nothing on the head cuts foul air.");

            var hatted = new AgentInventory(4, startingGold: 0);
            hatted.TryEquip(new ItemInstance(1, ItemType.HardHat, 1, 60, durability: 500));
            Assert(Outfitting.BestInStock(stall, hatted, rock, EquipmentSlot.Head, overBare: false, out _) == ItemType.None, "A hard hat worn: no other piece gains against falling rock.");
            Assert(Outfitting.BestInStock(stall, hatted, rock, EquipmentSlot.Head, overBare: true, out gain) == ItemType.HardHat && Near(gain, 0.75f), "Worn down, it is refreshed: the best over the head bare.");

            // A Masterwork piece on the shelf counts at its grade (D57)
            var graded = stall.WithStocked(ItemType.Respirator, 1, Grades.Masterwork);
            Assert(Outfitting.BestInStock(graded, bare, flood, EquipmentSlot.Head, false, out gain) == ItemType.Respirator && Near(gain, 0.9f), $"A Masterwork respirator on the shelf: 0.9 ({gain}).");
            TestLog.Log("[PASS] Test 4: the best piece in stock for a slot, by its gain over what the slot holds or over it bare; out of stock is no choice; the shelf's best grade counts.");
        }

        private void ValidateOutfittingAllocatesNothing()
        {
            var stall = new TradeStallData(StallId, default)
                .WithGood(new StallGood(ItemType.HazardGear, StallRole.Sells, 30, 2))
                .WithGood(new StallGood(ItemType.HardHat, StallRole.Sells, 60, 1));
            var inventory = new AgentInventory(4, startingGold: 0);
            inventory.TryEquip(Outfitting.NewPiece(ItemType.HazardGear, Grades.Standard));
            var need = OutfitNeed.Against(HazardKind.FallingRock).And(HazardKind.FoulAir);
            Outfitting.BestInStock(stall, inventory, need, EquipmentSlot.Head, false, out _); // warm up
            var every = OutfitNeed.EveryHazard;
            long before = GC.GetAllocatedBytesForCurrentThread();
            float total = 0f;
            for (int i = 0; i < 10000; i++)
            {
                Outfitting.BestInStock(stall, inventory, need, EquipmentSlot.Head, false, out float gain);
                total += gain + Outfitting.Score(inventory, every);
            }
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert(allocated == 0 && total > 0f, $"10,000 choices allocated {allocated} bytes.");
            TestLog.Log("[PASS] Test 5: scoring and choosing allocate nothing (cognition asks off the main thread).");
        }

        private static bool Near(float a, float b) => Math.Abs(a - b) < 1e-5f;

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new Exception($"[OutfittingValidator ASSERTION FAILED] {message}");
        }
    }
}
