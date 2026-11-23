using System;
using System.Collections.Generic;
using Simpiens.Cognition.Inventory;
using Simpiens.Simulation.Economy;
using Simpiens.Simulation.Items;

namespace Simpiens.Testing.Economy
{
    /// <summary>
    /// The equipment slots as a table (Epic 10 Part 3, K1.1; seam A3; the producer's Q11): every row is consistent; the
    /// first rows are Q11's reading (head and body in protection's place, the weapon and the tool kept); an item fits a
    /// kind, and a kind with two slots fills the first free one; a new slot needs no code beyond its row; the shared rule of
    /// what a piece takes the place of goes slot by slot (K1.3); and reading allocates nothing. The save (K1.2): every one
    /// of the 16 records goes into a save and comes back, a slot added later loads an older save with nothing in it, and a
    /// save with a piece worn where this game can't wear it is refused. Stats and wear (K1.3): every stat stacks over every
    /// worn piece within its bounds, and every piece wears by its own rule, whichever slot it is in. Runs as a test
    /// (ValidatorTests).
    /// </summary>
    public class EquipmentSlotsValidator
    {
        private const SlotKind Ring = (SlotKind)200;
        private const EquipmentSlot LeftHand = (EquipmentSlot)14;
        private const EquipmentSlot RightHand = (EquipmentSlot)15;
        private const ItemType TestRing = (ItemType)212;
        private const ItemType LeadRing = (ItemType)213;
        private const ItemType WardRing = (ItemType)214;
        private const ItemType TestBlade = (ItemType)215;
        private const ItemType TestMail = (ItemType)216;

        public void RunAllValidationTests()
        {
            TestLog.Log("[EquipmentSlotsValidator] Beginning equipment slot validation...");

            ValidateTheTableIsConsistent();
            ValidateTheFirstRowsAreQ11s();
            ValidateAnItemFitsAKind();
            ValidateASlotIsARow();
            ValidateAPieceTakesThePlaceOnlyOfItsSlots();
            ValidateReadingAllocatesNothing();
            ValidateTheSaveKeepsEveryRecord();
            ValidateANewSlotLoadsAnOlderSaveEmpty();
            ValidateAPieceWornWhereThisGameCantIsRefused();
            ValidateStatsStackWithinTheirBounds();
            ValidateEachPieceWearsByItsOwnRule();

            TestLog.Log("<color=green>[EquipmentSlotsValidator] ALL 11 TESTS PASSED SUCCESSFULLY!</color>");
        }

        private void ValidateTheTableIsConsistent()
        {
            var all = EquipmentSlots.All;
            Assert(all.Count > 0 && all.Count <= EquipmentSlots.Capacity, $"Between one slot and {EquipmentSlots.Capacity}.");
            var names = new HashSet<string>(StringComparer.Ordinal);
            var cells = new HashSet<int>();
            var layers = new HashSet<byte>();
            int previous = -1;
            for (int i = 0; i < all.Count; i++)
            {
                var slot = all[i];
                int id = (byte)slot.Id;
                Assert(id > previous && id < EquipmentSlots.Capacity, $"{slot.Name}: in id order, under {EquipmentSlots.Capacity}.");
                previous = id;
                Assert(EquipmentSlots.IsDefined(slot.Id) && EquipmentSlots.Get(slot.Id) == slot, $"{slot.Name}: found by its id.");
                Assert(!string.IsNullOrEmpty(slot.Name) && names.Add(slot.Name) && !string.IsNullOrEmpty(slot.DisplayName), $"{slot.Name}: named, once.");
                Assert(slot.Kind != SlotKind.None && slot.Region != BodyRegion.None, $"{slot.Name}: takes a kind, on a part of the body.");
                Assert(cells.Add(slot.DollColumn * 64 + slot.DollRow), $"{slot.Name}: its own cell on the paper doll.");
                Assert(layers.Add(slot.DrawLayer), $"{slot.Name}: its own draw layer, so worn sprites stack one way.");
            }
            var shown = EquipmentSlots.InDisplayOrder;
            Assert(shown.Count == all.Count, "Every slot is listed.");
            for (int i = 1; i < shown.Count; i++)
            {
                Assert(shown[i - 1].Order < shown[i].Order || (shown[i - 1].Order == shown[i].Order && shown[i - 1].Id < shown[i].Id), "Lists go by order, then id.");
            }
            Assert(!EquipmentSlots.IsDefined((EquipmentSlot)EquipmentSlots.Capacity) && EquipmentSlots.Get((EquipmentSlot)255) == null, "No slot past the room kept for them.");
            TestLog.Log("[PASS] Test 1: every slot is consistent: in id order under the room kept, named once, with a kind, a region, a cell and a layer of its own.");
        }

        private void ValidateTheFirstRowsAreQ11s()
        {
            Assert(!EquipmentSlots.IsDefined((EquipmentSlot)0), "Slot 0, protection, is retired (K1.1).");
            Assert(EquipmentSlots.Get(EquipmentSlot.Head)?.Kind == SlotKind.Head && EquipmentSlots.Get(EquipmentSlot.Head).Region == BodyRegion.Head, "The head.");
            Assert(EquipmentSlots.Get(EquipmentSlot.Body)?.Kind == SlotKind.Body && EquipmentSlots.Get(EquipmentSlot.Body).Region == BodyRegion.Torso, "The body.");
            Assert(EquipmentSlots.Get(EquipmentSlot.Weapon)?.Kind == SlotKind.Weapon && EquipmentSlots.Get(EquipmentSlot.Tool)?.Kind == SlotKind.Tool, "The weapon and the tool stay.");
            Assert(EquipmentSlots.All.Count == 4, "Four slots for now (Q11: two new ones, head and body).");

            Assert(EquipmentSlots.Fits(EquipmentSlot.Head, ItemType.HardHat) && EquipmentSlots.Fits(EquipmentSlot.Head, ItemType.Respirator), "The head takes the hard hat and the respirator, worn as a mask.");
            Assert(EquipmentSlots.Fits(EquipmentSlot.Body, ItemType.HazardGear), "The body takes hazard gear.");
            Assert(EquipmentSlots.Fits(EquipmentSlot.Tool, ItemType.ToolPickaxe), "The tool slot takes the pickaxe.");
            Assert(!EquipmentSlots.Fits(EquipmentSlot.Body, ItemType.HardHat) && !EquipmentSlots.Fits(EquipmentSlot.Head, ItemType.Ration), "Nothing else fits them.");

            var body = EquipmentSlots.Get(EquipmentSlot.Body);
            var head = EquipmentSlots.Get(EquipmentSlot.Head);
            var weapon = EquipmentSlots.Get(EquipmentSlot.Weapon);
            Assert(body.DrawLayer < head.DrawLayer && head.DrawLayer < weapon.DrawLayer, "The coat under the helm under the weapon in hand.");
            Assert(head.DollRow < body.DollRow && head.DollColumn == body.DollColumn, "On the doll, the head over the body.");

            // Every item with an equipment block fits a slot the table has
            for (int i = 0; i < ItemCatalog.All.Count; i++)
            {
                var item = ItemCatalog.All[i];
                if (item.Equipment == null) continue;
                Assert(EquipmentSlots.TryFirstOf(item.Equipment.Kind, out var slot) && EquipmentSlots.Fits(slot, item.Id), $"{item.Name} fits a slot.");
            }
            TestLog.Log("[PASS] Test 2: Q11's reading: head and body in protection's place (slot 0 retired), the weapon and the tool kept, and every piece of equipment fits one.");
        }

        private void ValidateAnItemFitsAKind()
        {
            var inventory = new AgentInventory(4, startingGold: 0);
            Assert(inventory.TryEquip(new ItemInstance(ItemType.HardHat, 1, 60, durability: 1500), out var wornIn) && wornIn == EquipmentSlot.Head, "A hard hat goes on the head.");
            Assert(!inventory.TryEquip(new ItemInstance(ItemType.Respirator, 1, 60)), "The head is taken: a respirator is refused.");
            Assert(!inventory.TryEquip(new ItemInstance(ItemType.HazardGear, 1, 40), EquipmentSlot.Head), "Hazard gear doesn't fit the head.");
            Assert(!inventory.TryEquip(new ItemInstance(ItemType.HazardGear, 1, 40), (EquipmentSlot)0), "Nor the retired slot.");
            Assert(!inventory.TryEquip(new ItemInstance(ItemType.HazardGear, 1, 40), (EquipmentSlot)200), "Nor a slot past the room kept.");
            Assert(inventory.TryEquip(new ItemInstance(ItemType.HazardGear, 1, 40), EquipmentSlot.Body), "Hazard gear on the body, beside the hat: the inventory keeps a piece a slot.");
            Assert(inventory.Unequip((EquipmentSlot)200).IsEmpty && !inventory.Wear((EquipmentSlot)200), "A slot past the room holds nothing and wears nothing.");

            // A kind with two slots (two rings): the first free one, in id order
            using (TestSlots())
            using (TestItems())
            {
                var hands = new AgentInventory(4, startingGold: 0);
                Assert(hands.TryEquip(new ItemInstance(TestRing, 1, 5), out var first) && first == LeftHand, "The first ring on the first hand.");
                Assert(hands.TryEquip(new ItemInstance(TestRing, 1, 6), out var second) && second == RightHand, "The second on the other.");
                Assert(!hands.TryEquip(new ItemInstance(TestRing, 1, 7)), "A third is refused: both are taken.");
                Assert(hands.Unequip(LeftHand).BaseValue == 5 && hands.TryEquip(new ItemInstance(TestRing, 1, 7), out var again) && again == LeftHand, "Off one hand, the next goes on it.");
            }
            TestLog.Log("[PASS] Test 3: an item fits a kind: in the first free slot of it, refused in a slot of another kind, a retired one or one past the room; two slots of a kind fill in turn.");
        }

        private void ValidateASlotIsARow()
        {
            using (TestSlots())
            using (TestItems())
            {
                Assert(EquipmentSlots.All.Count == 6 && EquipmentSlots.Get(RightHand).Name == "RightHand", "The new rows are in the table.");
                var inventory = new AgentInventory(4, startingGold: 0);
                Assert(inventory.TryEquip(new ItemInstance(TestRing, 1, 5)) && inventory.TryEquip(new ItemInstance(TestRing, 1, 5, quality: Grades.Fine)), "Two rings on.");
                inventory.TryEquip(new ItemInstance(ItemType.HazardGear, 1, 40));
                // Stats sum every slot: 0.5 from hazard gear, a ring's 0.05 and a Fine one's 0.055 (D56)
                float resistance = CrawlerStats.Get(inventory, StatKind.HazardResistance);
                Assert(Math.Abs(resistance - 0.605f) < 1e-5f, $"Stats sum every slot, the new ones too ({resistance}).");
                var snapshot = inventory.GetSnapshot();
                Assert(snapshot.GetEquipped(RightHand).Quality == Grades.Fine && snapshot.IsWearing(TestRing), "The snapshot carries the new slots.");
                Assert(inventory.IsWearing(TestRing) && !inventory.IsWearing(ItemType.HardHat), "Asking what is worn reads every slot.");
            }
            Assert(EquipmentSlots.All.Count == 4 && !EquipmentSlots.IsDefined(RightHand), "Disposed, the table is the game's again.");

            bool refused = false;
            try
            {
                using (EquipmentSlots.ExtendForTest(new SlotDefinition(EquipmentSlot.Head, "Hat", "Hat", SlotKind.Head, BodyRegion.Head))) { }
            }
            catch (ArgumentException) { refused = true; }
            Assert(refused && EquipmentSlots.Get(EquipmentSlot.Head).Name == "Head", "Two slots with one id are refused, and the table is unchanged.");
            refused = false;
            try
            {
                using (EquipmentSlots.ExtendForTest(new SlotDefinition((EquipmentSlot)EquipmentSlots.Capacity, "Spare", "Spare", SlotKind.Head, BodyRegion.Head))) { }
            }
            catch (ArgumentException) { refused = true; }
            Assert(refused, $"A slot past the room kept ({EquipmentSlots.Capacity}) is refused: it would change the save format.");
            TestLog.Log("[PASS] Test 4: a new slot is a row: its pieces are worn, summed into stats and snapshotted with no code of its own; a duplicate id and one past the room are refused.");
        }

        private void ValidateAPieceTakesThePlaceOnlyOfItsSlots()
        {
            var inventory = new AgentInventory(4, startingGold: 0);
            Assert(WornGear.TryReplacing(inventory, ItemType.Respirator, out var slot) && slot == EquipmentSlot.Head && WornGear.Replaced(inventory, ItemType.Respirator).IsEmpty,
                "Nothing worn: a respirator would go on the empty head, in nobody's place.");
            inventory.TryEquip(new ItemInstance(ItemType.HazardGear, 1, 40, durability: 1500));
            Assert(WornGear.TryReplacing(inventory, ItemType.Respirator, out slot) && slot == EquipmentSlot.Head && WornGear.Replaced(inventory, ItemType.Respirator).IsEmpty,
                "Hazard gear worn: a respirator still goes on the empty head, in nobody's place (K1.3).");
            Assert(inventory.TryEquip(new ItemInstance(ItemType.HardHat, 1, 60, durability: 1500), out var wornIn) && wornIn == EquipmentSlot.Head,
                "A hard hat goes on over the hazard gear.");
            Assert(inventory.IsWearing(ItemType.HardHat) && inventory.IsWearing(ItemType.HazardGear), "Both are worn together.");
            Assert(WornGear.Protection(inventory).Type == ItemType.HardHat, "The first protection worn, in id order, is the head's.");
            Assert(WornGear.TryReplacing(inventory, ItemType.Respirator, out slot) && slot == EquipmentSlot.Head && WornGear.Replaced(inventory, ItemType.Respirator).Type == ItemType.HardHat,
                "A respirator would take the hard hat's place, the piece in its own slot, and leave the hazard gear.");
            Assert(WornGear.Replaced(inventory, ItemType.HazardGear).Type == ItemType.HazardGear, "More hazard gear would take the hazard gear's place.");
            Assert(WornGear.TryReplacing(inventory, ItemType.ToolPickaxe, out slot) && slot == EquipmentSlot.Tool && WornGear.Replaced(inventory, ItemType.ToolPickaxe).IsEmpty,
                "A pickaxe goes in the free tool slot, in nobody's place.");
            inventory.TryEquip(new ItemInstance(ItemType.ToolPickaxe, 1, 30, durability: 40));
            Assert(WornGear.Replaced(inventory, ItemType.ToolPickaxe).Type == ItemType.ToolPickaxe, "With one worn, another pickaxe would take its place.");
            Assert(!WornGear.TryReplacing(inventory, ItemType.Ration, out _) && WornGear.Replaced(inventory, ItemType.Ration).IsEmpty, "A ration is worn nowhere.");
            TestLog.Log("[PASS] Test 5: the shared rule: a piece goes in a free slot of its kind, or takes the place of the piece in it, and only that piece: a hard hat and hazard gear are worn together.");
        }

        private void ValidateReadingAllocatesNothing()
        {
            var inventory = new AgentInventory(4, startingGold: 0);
            inventory.TryEquip(new ItemInstance(ItemType.HazardGear, 1, 40, durability: 1500));
            inventory.TryEquip(new ItemInstance(ItemType.ToolPickaxe, 1, 30, durability: 40));
            CrawlerStats.Get(inventory, StatKind.HarvestSpeed, 1f); // warm up
            WornGear.TryReplacing(inventory, ItemType.Respirator, out _);
            long before = GC.GetAllocatedBytesForCurrentThread();
            float total = 0f;
            int found = 0;
            for (int i = 0; i < 10000; i++)
            {
                total += CrawlerStats.Get(inventory, StatKind.HarvestSpeed, 1f) + CrawlerStats.HazardResistance(inventory, Simpiens.Simulation.World.HazardKind.Flooding);
                if (WornGear.TryReplacing(inventory, ItemType.Respirator, out _) && inventory.IsWearing(ItemType.HazardGear) && EquipmentSlots.TryFirstOf(SlotKind.Head, out _)) found++;
                total += inventory.GetEquipped(EquipmentSlots.All[i % EquipmentSlots.All.Count].Id).Durability;
            }
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert(allocated == 0 && total > 0f && found == 10000, $"10,000 reads allocated {allocated} bytes.");
            TestLog.Log("[PASS] Test 6: reading the table, the worn slots, the stats over them and the shared rule allocate nothing.");
        }

        private void ValidateTheSaveKeepsEveryRecord()
        {
            var inventory = new AgentInventory(4, startingGold: 7);
            inventory.TryEquip(new ItemInstance(ItemType.HardHat, 1, 60, quality: Grades.Fine, durability: 1234));
            inventory.TryEquip(new ItemInstance(ItemType.HazardGear, 1, 40, durability: 99));
            inventory.TryEquip(new ItemInstance(ItemType.ToolPickaxe, 1, 30, quality: Grades.Masterwork, durability: 17));
            var back = RoundTrip(inventory);
            for (int id = 0; id < EquipmentSlots.Capacity; id++)
            {
                var slot = (EquipmentSlot)id;
                Assert(back.GetEquipped(slot) == inventory.GetEquipped(slot), $"Slot {id} comes back as it went.");
            }
            Assert(back.GetEquipped(EquipmentSlot.Head).Durability == 1234 && back.GetEquipped(EquipmentSlot.Head).Quality == Grades.Fine
                && back.GetEquipped(EquipmentSlot.Tool).Quality == Grades.Masterwork && back.Gold == 7, "With their grades and wear.");

            var save = new Simpiens.Simulation.Persistence.InventorySave();
            inventory.CaptureTo(save);
            int bytes = Simpiens.Simulation.Persistence.SaveSerializer.Serialize(save).Length;
            var bare = new Simpiens.Simulation.Persistence.InventorySave();
            new AgentInventory(4, startingGold: 7).CaptureTo(bare);
            Assert(Simpiens.Simulation.Persistence.SaveSerializer.Serialize(bare).Length == bytes, "A bare crawler saves as many bytes as a geared one: all 16 records, always.");
            TestLog.Log("[PASS] Test 7: the save keeps all 16 records, filled or not, with their grades and wear.");
        }

        private void ValidateANewSlotLoadsAnOlderSaveEmpty()
        {
            // A save made with today's table, loaded after two slots were added
            var inventory = new AgentInventory(4, startingGold: 0);
            inventory.TryEquip(new ItemInstance(ItemType.HazardGear, 1, 40, durability: 1500));
            var save = new Simpiens.Simulation.Persistence.InventorySave();
            inventory.CaptureTo(save);
            byte[] bytes = Simpiens.Simulation.Persistence.SaveSerializer.Serialize(save);
            using (TestSlots())
            using (TestItems())
            {
                var loaded = Simpiens.Simulation.Persistence.SaveSerializer.Deserialize<Simpiens.Simulation.Persistence.InventorySave>(bytes);
                Assert(Simpiens.Simulation.Persistence.SaveItems.FirstMisworn(SaveOf(loaded)) == null, "The older save loads.");
                var back = new AgentInventory(4, startingGold: 0);
                back.RestoreFrom(loaded);
                Assert(back.GetEquipped(EquipmentSlot.Body).Type == ItemType.HazardGear, "What was worn is worn.");
                Assert(back.GetEquipped(LeftHand).IsEmpty && back.GetEquipped(RightHand).IsEmpty, "The new slots load with nothing in them.");
                Assert(back.TryEquip(new ItemInstance(TestRing, 1, 5), out var wornIn) && wornIn == LeftHand, "And take a piece after.");
            }
            TestLog.Log("[PASS] Test 8: a slot added to the table loads an older save with nothing in it, and no change of format.");
        }

        private void ValidateAPieceWornWhereThisGameCantIsRefused()
        {
            // A ring worn on a hand that this game's table doesn't have: made with the test slots, loaded without them
            Simpiens.Simulation.Persistence.InventorySave ringed;
            using (TestSlots())
            using (TestItems())
            {
                var inventory = new AgentInventory(4, startingGold: 0);
                inventory.TryEquip(new ItemInstance(TestRing, 1, 5));
                ringed = new Simpiens.Simulation.Persistence.InventorySave();
                inventory.CaptureTo(ringed);
            }
            using (TestItems())
            {
                string refusal = Simpiens.Simulation.Persistence.SaveItems.FirstMisworn(SaveOf(ringed));
                Assert(refusal != null && refusal.Contains("slot 14"), $"A piece in a slot this game doesn't have is refused ({refusal}).");
            }

            var misfit = new Simpiens.Simulation.Persistence.InventorySave { Slots = new ItemInstance[4] };
            var records = default(WornEquipment);
            records[(int)EquipmentSlot.Body] = new ItemInstance(ItemType.HardHat, 1, 60);
            misfit.Equipped = records;
            string wrong = Simpiens.Simulation.Persistence.SaveItems.FirstMisworn(SaveOf(misfit));
            Assert(wrong != null && wrong.Contains("HardHat") && wrong.Contains("Body"), $"A hard hat in the body slot is refused ({wrong}).");

            records = default;
            records[0] = new ItemInstance(ItemType.HazardGear, 1, 40);
            misfit.Equipped = records;
            Assert(Simpiens.Simulation.Persistence.SaveItems.FirstMisworn(SaveOf(misfit)) != null, "So is hazard gear in retired slot 0, where format 10 kept protection.");
            TestLog.Log("[PASS] Test 9: a save with a piece in a slot this game doesn't have, or in a slot of another kind, is refused before anything loads.");
        }

        private void ValidateStatsStackWithinTheirBounds()
        {
            // A hard hat and hazard gear together: the gear's 0.5 against any hazard, the hat's 0.75 against falling rock
            var inventory = new AgentInventory(4, startingGold: 0);
            inventory.TryEquip(new ItemInstance(ItemType.HazardGear, 1, 40, durability: 1500));
            inventory.TryEquip(new ItemInstance(ItemType.HardHat, 1, 60, durability: 1500));
            var rock = Simpiens.Simulation.World.HazardKind.FallingRock;
            var air = Simpiens.Simulation.World.HazardKind.FoulAir;
            Assert(CrawlerStats.Get(inventory, StatKind.HazardResistance) == 0.5f && CrawlerStats.KindResistance(inventory, rock) == 0.75f, "Each piece's stat, read on its own.");
            Assert(CrawlerStats.HazardResistance(inventory, rock) == StatRules.MaxResistance, $"Against falling rock they add up, held at the most protection stops ({CrawlerStats.HazardResistance(inventory, rock)}).");
            Assert(CrawlerStats.HazardResistance(inventory, air) == 0.5f, "Against foul air only the gear counts: the hat isn't made against it.");

            // A Masterwork respirator reaches the bound alone (0.75, a fifth more at two grades up: 0.9)
            var masterwork = new AgentInventory(4, startingGold: 0);
            masterwork.TryEquip(new ItemInstance(ItemType.Respirator, 1, 60, quality: Grades.Masterwork, durability: 1500));
            Assert(Math.Abs(CrawlerStats.Get(masterwork, StatKind.AirResistance) - 0.9f) < 1e-5f, "A Masterwork respirator stops nine tenths of the air hazards.");

            using (TestSlots())
            using (TestItems())
            {
                // Two wards of 0.5 on the hands and hazard gear's 0.5: 1.5 summed, held at 0.9
                var warded = new AgentInventory(4, startingGold: 0);
                warded.TryEquip(new ItemInstance(WardRing, 1, 5));
                warded.TryEquip(new ItemInstance(WardRing, 1, 5));
                warded.TryEquip(new ItemInstance(ItemType.HazardGear, 1, 40, durability: 1500));
                Assert(CrawlerStats.Get(warded, StatKind.HazardResistance) == StatRules.MaxResistance, "Resistance from three slots is held at its bound.");

                // Two lead rings: mining x0.01 twice, held at a tenth; defense -5 twice, held at none
                var leaden = new AgentInventory(4, startingGold: 0);
                leaden.TryEquip(new ItemInstance(LeadRing, 1, 5));
                leaden.TryEquip(new ItemInstance(LeadRing, 1, 5));
                Assert(CrawlerStats.Get(leaden, StatKind.HarvestSpeed, 1f) == 0.1f && CrawlerStats.MiningSpeed(leaden, ItemType.IronOre) == 0.1f, "Harvest speed multiplied down is held at a tenth: work never stops.");
                Assert(CrawlerStats.Get(leaden, StatKind.Defense) == 0f && CrawlerStats.Get(leaden, StatKind.Attack, 2f) == 2f, "Defense taken below none is held at none; attack, untouched, is its base.");
            }
            Assert(StatRules.Of(StatKind.Hunger).Min == float.MinValue && StatRules.Of(StatKind.Hunger).Max == float.MaxValue, "A stat with no row is unbounded.");
            TestLog.Log("[PASS] Test 10: every stat stacks over every worn piece, base plus adds times multiplies, then within its bounds: a hard hat over hazard gear stops 0.9 of falling rock, and nothing takes a stat past its bounds.");
        }

        private void ValidateEachPieceWearsByItsOwnRule()
        {
            System.Span<ItemType> broke = stackalloc ItemType[EquipmentSlots.Capacity];
            var inventory = new AgentInventory(4, startingGold: 0);
            inventory.TryEquip(new ItemInstance(ItemType.HazardGear, 1, 40, durability: 1500));
            inventory.TryEquip(new ItemInstance(ItemType.HardHat, 1, 60, durability: 2));
            inventory.TryEquip(new ItemInstance(ItemType.ToolPickaxe, 1, 30, durability: 40));

            Assert(inventory.WearBy(WearRule.PerHazardTick, broke, out int brokeCount) == 2 && brokeCount == 0, "A tick in danger wears both the hat and the gear.");
            Assert(inventory.GetEquipped(EquipmentSlot.Head).Durability == 1 && inventory.GetEquipped(EquipmentSlot.Body).Durability == 1499 && inventory.GetEquipped(EquipmentSlot.Tool).Durability == 40,
                "Each by a unit; the pickaxe, which wears by the harvest, is untouched.");
            Assert(inventory.WearBy(WearRule.PerHarvest, broke, out brokeCount) == 1 && inventory.GetEquipped(EquipmentSlot.Tool).Durability == 39 && inventory.GetEquipped(EquipmentSlot.Body).Durability == 1499,
                "A mineral unit mined wears the pickaxe alone.");
            Assert(inventory.WearBy(WearRule.PerHazardTick, broke, out brokeCount) == 2 && brokeCount == 1 && broke[0] == ItemType.HardHat && inventory.GetEquipped(EquipmentSlot.Head).IsEmpty,
                "The hat's last unit breaks it, and it's named among the broken.");
            Assert(inventory.WearBy(WearRule.PerHit, broke, out brokeCount) == 0 && inventory.WearBy(WearRule.None, broke, out _) == 0, "Nothing worn wears by a hit, and nothing wears by no rule.");

            using (TestSlots())
            using (TestItems())
            {
                // A blade wears per blow landed, mail per blow taken (D7); a ring wearing by exposure on a new slot wears too
                var fighter = new AgentInventory(4, startingGold: 0);
                fighter.TryEquip(new ItemInstance(TestBlade, 1, 10, durability: 2));
                fighter.TryEquip(new ItemInstance(TestMail, 1, 10, durability: 1));
                fighter.TryEquip(new ItemInstance(WardRing, 1, 5, durability: 9));
                var hit = new Simpiens.Simulation.Combat.SwingOutcome(true, 0.7f, 3f);
                var miss = new Simpiens.Simulation.Combat.SwingOutcome(false, 0.7f, 0f);
                Assert(!Simpiens.Simulation.Combat.CombatRules.WearOnHit(miss, fighter) && !Simpiens.Simulation.Combat.CombatRules.WearOnBlowTaken(miss, fighter), "A miss wears nothing, on either side.");
                Assert(!Simpiens.Simulation.Combat.CombatRules.WearOnHit(hit, fighter) && fighter.GetEquipped(EquipmentSlot.Weapon).Durability == 1 && fighter.GetEquipped(EquipmentSlot.Body).Durability == 1,
                    "A blow landed wears the blade, not the mail.");
                Assert(Simpiens.Simulation.Combat.CombatRules.WearOnBlowTaken(hit, fighter) && fighter.GetEquipped(EquipmentSlot.Body).IsEmpty && fighter.GetEquipped(EquipmentSlot.Weapon).Durability == 1,
                    "A blow taken wears the mail, its last unit breaking it; the blade is untouched.");
                Assert(fighter.WearBy(WearRule.PerHazardTick, broke, out _) == 1 && fighter.GetEquipped(LeftHand).Durability == 8, "A piece in a new slot wears by its rule, with no code of its own.");
            }
            TestLog.Log("[PASS] Test 11: every piece wears by its own rule, whichever slot it is in: exposure wears a hat and gear together, the harvest the pickaxe, a blow landed the blade and a blow taken the mail.");
        }

        private static AgentInventory RoundTrip(AgentInventory inventory)
        {
            var save = new Simpiens.Simulation.Persistence.InventorySave();
            inventory.CaptureTo(save);
            var loaded = Simpiens.Simulation.Persistence.SaveSerializer.Deserialize<Simpiens.Simulation.Persistence.InventorySave>(Simpiens.Simulation.Persistence.SaveSerializer.Serialize(save));
            var back = new AgentInventory(inventory.Capacity, startingGold: 0);
            back.RestoreFrom(loaded);
            return back;
        }

        private static Simpiens.Simulation.Persistence.SessionSave SaveOf(Simpiens.Simulation.Persistence.InventorySave inventory) => new Simpiens.Simulation.Persistence.SessionSave
        {
            Crawlers = new[] { new Simpiens.Simulation.Persistence.CrawlerSave { Name = "Tester", Inventory = inventory } }
        };

        /// <summary>Two hands for rings: a kind with two slots (Q11's "a kind may later have several slots").</summary>
        private static IDisposable TestSlots() => EquipmentSlots.ExtendForTest(
            new SlotDefinition(LeftHand, "LeftHand", "Left Hand", Ring, BodyRegion.Hands) { DrawLayer = 50, DollColumn = 0, DollRow = 2, Order = 4 },
            new SlotDefinition(RightHand, "RightHand", "Right Hand", Ring, BodyRegion.Hands) { DrawLayer = 51, DollColumn = 2, DollRow = 2, Order = 5 });

        private static IDisposable TestItems() => ItemCatalog.ExtendForTest(
            new ItemDefinition(TestRing, "TestRing", "Test Ring", ItemCategory.Equipment, ItemRarity.Common,
                equipment: new EquipmentBlock(Ring, durability: 100, WearRule.None,
                    new StatModifier(StatKind.HazardResistance, ModifierOp.Add, 0.05f))),
            // Takes mining and defense down, to show the bounds hold
            new ItemDefinition(LeadRing, "LeadRing", "Lead Ring", ItemCategory.Equipment, ItemRarity.Common,
                equipment: new EquipmentBlock(Ring, durability: 100, WearRule.None,
                    new StatModifier(StatKind.HarvestSpeed, ModifierOp.Multiply, 0.01f),
                    new StatModifier(StatKind.Defense, ModifierOp.Add, -5f))),
            new ItemDefinition(WardRing, "WardRing", "Ward Ring", ItemCategory.Equipment, ItemRarity.Common,
                equipment: new EquipmentBlock(Ring, durability: 100, WearRule.PerHazardTick,
                    new StatModifier(StatKind.HazardResistance, ModifierOp.Add, 0.5f))),
            new ItemDefinition(TestBlade, "TestBlade", "Test Blade", ItemCategory.Equipment, ItemRarity.Common,
                equipment: new EquipmentBlock(SlotKind.Weapon, durability: 2, WearRule.PerHit,
                    new StatModifier(StatKind.Attack, ModifierOp.Add, 3f))),
            new ItemDefinition(TestMail, "TestMail", "Test Mail", ItemCategory.Equipment, ItemRarity.Common,
                equipment: new EquipmentBlock(SlotKind.Body, durability: 1, WearRule.PerBlowTaken,
                    new StatModifier(StatKind.Defense, ModifierOp.Add, 4f))));

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new Exception($"[EquipmentSlotsValidator ASSERTION FAILED] {message}");
        }
    }
}
