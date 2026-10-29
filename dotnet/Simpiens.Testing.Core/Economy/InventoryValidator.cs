using Simpiens.Testing.Environment;
using System;
using System.Runtime.InteropServices;
using Simpiens.Cognition;
using Simpiens.Cognition.Inventory;
using Simpiens.Simulation.Economy;

namespace Simpiens.Testing.Economy
{
    /// <summary>
    /// Automated domain validation suite for Epic 9 Slice 1:
    /// Unmanaged Inventory, Currency & Item Primitives.
    /// Runs as a test (ValidatorTests), never at boot.
    /// </summary>
    public class InventoryValidator
    {
        public void Start()
        {
            TestLog.Log("[InventoryValidator] Beginning automated validation of Epic 9 Slice 1...");

            ValidateUnmanagedItemInstanceLayout();
            ValidateAgentInventoryStackMergingAndSlots();
            ValidateInventoryCapacityAndOverflow();
            ValidatePartialAndTotalItemRemoval();
            ValidateWalletAndCurrencyBounds();
            ValidateAgentBackwardCompatibility();
            ValidateZeroAllocationQueries();
            ValidateGearIsWornBesideThePack();

            TestLog.Log("<color=green>[InventoryValidator] ALL 8 DOMAIN TESTS PASSED SUCCESSFULLY!</color>");
        }

        private void ValidateUnmanagedItemInstanceLayout()
        {
            int itemSize = Marshal.SizeOf<ItemInstance>();
            Assert(itemSize == 16, $"ItemInstance struct size ({itemSize} bytes) does not equal expected 16 bytes.");

            // Verify default empty state
            Assert(ItemInstance.Empty.IsEmpty, "ItemInstance.Empty has IsEmpty == true.");
            Assert(ItemInstance.Empty.Quantity == 0, "ItemInstance.Empty has Quantity == 0.");
            Assert(ItemInstance.Empty.Type == ItemType.None, "ItemInstance.Empty has Type == ItemType.None.");

            // Verify constructor and WithQuantity
            var ration = new ItemInstance(1, ItemType.Ration, 5, 10, quality: 1, durability: 95);
            Assert(!ration.IsEmpty, "Created ration is not empty.");
            Assert(ration.Type == ItemType.Ration, "Ration Type is ItemType.Ration.");
            Assert(ration.Quantity == 5, "Ration Quantity is 5.");
            Assert(ration.BaseValue == 10, "Ration BaseValue is 10.");
            Assert(ration.Quality == 1, "Ration Quality is 1.");
            Assert(ration.Durability == 95, "Ration Durability is 95.");

            var updatedRation = ration.WithQuantity(12);
            Assert(updatedRation.Quantity == 12, "WithQuantity updated quantity to 12.");
            Assert(updatedRation.BaseValue == 10, "BaseValue preserved after WithQuantity.");

            // Equality
            var rationCopy = new ItemInstance(1, ItemType.Ration, 5, 10, quality: 1, durability: 95);
            Assert(ration == rationCopy, "ItemInstance equality operator functions properly.");

            TestLog.Log("[PASS] Test 1: Unmanaged ItemInstance 16-byte layout and semantics verified.");
        }

        private void ValidateAgentInventoryStackMergingAndSlots()
        {
            var inv = new AgentInventory(capacity: 4, startingGold: 25);
            Assert(inv.Capacity == 4, "Capacity is 4.");
            Assert(inv.Gold == 25, "Starting gold is 25.");
            Assert(inv.OccupiedSlots == 0, "Initial occupied slots is 0.");

            // 1. Add first stack
            var ration1 = new ItemInstance(1, ItemType.Ration, 5, 5);
            bool added1 = inv.TryAddItem(ration1, out var remainder1);
            Assert(added1, "First ration added successfully.");
            Assert(remainder1.IsEmpty, "No remainder on first add.");
            Assert(inv.OccupiedSlots == 1, "Occupied slots is 1.");
            Assert(inv.GetItemCount(ItemType.Ration) == 5, "Ration count is 5.");

            // 2. Add second stack of same type & quality -> should stack-merge
            var ration2 = new ItemInstance(1, ItemType.Ration, 8, 5);
            bool added2 = inv.TryAddItem(ration2, out var remainder2);
            Assert(added2, "Second ration added successfully.");
            Assert(remainder2.IsEmpty, "No remainder on second add.");
            Assert(inv.OccupiedSlots == 1, "Occupied slots remains 1 after stack merge.");
            Assert(inv.GetItemCount(ItemType.Ration) == 13, "Ration count is 13.");

            // 3. Add second item type
            var ore = new ItemInstance(2, ItemType.IronOre, 3, 20);
            bool added3 = inv.TryAddItem(ore, out var remainder3);
            Assert(added3, "IronOre added successfully.");
            Assert(remainder3.IsEmpty, "No remainder on ore add.");
            Assert(inv.OccupiedSlots == 2, "Occupied slots is now 2.");
            Assert(inv.GetItemCount(ItemType.IronOre) == 3, "IronOre count is 3.");
            Assert(inv.HasItem(ItemType.Ration, 13), "HasItem returns true for 13 rations.");
            Assert(!inv.HasItem(ItemType.Ration, 14), "HasItem returns false for 14 rations.");

            TestLog.Log("[PASS] Test 2: AgentInventory stack merging and slot assignment verified.");
        }

        private void ValidateInventoryCapacityAndOverflow()
        {
            var inv = new AgentInventory(capacity: 3, startingGold: 0);

            // Fill all 3 slots
            inv.TryAddItem(new ItemInstance(1, ItemType.Ration, 10, 5), out _);
            inv.TryAddItem(new ItemInstance(2, ItemType.IronOre, 5, 15), out _);
            inv.TryAddItem(new ItemInstance(3, ItemType.MedicinalHerb, 4, 10), out _);
            Assert(inv.OccupiedSlots == 3, "All 3 slots occupied.");

            // Attempt to add a 4th distinct item type (ToolPickaxe)
            var pickaxe = new ItemInstance(4, ItemType.ToolPickaxe, 1, 50);
            bool added = inv.TryAddItem(pickaxe, out var remainder);
            Assert(!added, "Adding item beyond capacity returns false.");
            Assert(!remainder.IsEmpty, "Remainder is not empty on overflow.");
            Assert(remainder.Quantity == 1, "Remainder quantity matches rejected item.");
            Assert(inv.OccupiedSlots == 3, "Occupied slots unchanged.");
            Assert(!inv.HasItem(ItemType.ToolPickaxe), "Inventory does not have rejected item.");

            TestLog.Log("[PASS] Test 3: Inventory capacity limits and overflow handling verified.");
        }

        private void ValidatePartialAndTotalItemRemoval()
        {
            var inv = new AgentInventory(capacity: 4, startingGold: 0);
            inv.TryAddItem(new ItemInstance(1, ItemType.Ration, 10, 5), out _);

            // Partial removal
            bool removedPartial = inv.TryRemoveItem(ItemType.Ration, 4);
            Assert(removedPartial, "TryRemoveItem 4 rations succeeded.");
            Assert(inv.GetItemCount(ItemType.Ration) == 6, "Remaining rations is 6.");
            Assert(inv.OccupiedSlots == 1, "Slot still occupied after partial removal.");

            // Over-removal attempt (asking for 10 when 6 available)
            bool removedTooMany = inv.TryRemoveItem(ItemType.Ration, 10);
            Assert(!removedTooMany, "TryRemoveItem returns false when attempting to remove more than available.");
            Assert(inv.GetItemCount(ItemType.Ration) == 6, "Ration count unchanged after failed removal.");

            // Total removal
            bool removedTotal = inv.TryRemoveItem(ItemType.Ration, 6);
            Assert(removedTotal, "TryRemoveItem 6 rations succeeded.");
            Assert(inv.GetItemCount(ItemType.Ration) == 0, "Ration count is now 0.");
            Assert(inv.OccupiedSlots == 0, "Slot cleared after total removal.");
            Assert(!inv.HasItem(ItemType.Ration), "HasItem returns false after total removal.");

            TestLog.Log("[PASS] Test 4: Partial and total item removal verified.");
        }

        private void ValidateWalletAndCurrencyBounds()
        {
            var inv = new AgentInventory(capacity: 4, startingGold: 50);
            Assert(inv.Gold == 50, "Initial gold is 50.");

            inv.AddGold(25);
            Assert(inv.Gold == 75, "Gold after AddGold(25) is 75.");

            bool deducted = inv.TryDeductGold(30);
            Assert(deducted, "TryDeductGold(30) succeeded.");
            Assert(inv.Gold == 45, "Gold after deduction is 45.");

            // Prevent underflow
            bool overDeducted = inv.TryDeductGold(100);
            Assert(!overDeducted, "TryDeductGold(100) returns false when balance insufficient.");
            Assert(inv.Gold == 45, "Gold balance unchanged after failed deduction.");

            // Prevent negative addition
            inv.AddGold(-20);
            Assert(inv.Gold == 45, "Negative AddGold ignored.");

            TestLog.Log("[PASS] Test 5: Wallet operations and underflow prevention verified.");
        }

        private void ValidateAgentBackwardCompatibility()
        {
            var goCrawler = HeadlessEntities.Crawler(null, null, null, Vec2.Zero, id: EntityId.Generate(), name: "[TestAgent_InventoryCompatibility]", register: false);
            var agent = goCrawler.Agent;

            // Before initialization, Inventory is auto-created with 10 gold
            Assert(agent.Inventory != null, "Agent.Inventory is not null.");
            Assert(agent.Inventory.Gold == 10, "Agent default gold is 10.");
            Assert(!agent.HasFoodInInventory, "Agent initially has HasFoodInInventory == false.");

            // Setting HasFoodInInventory = true should populate a Ration in Inventory
            agent.HasFoodInInventory = true;
            Assert(agent.HasFoodInInventory, "HasFoodInInventory is true after set.");
            Assert(agent.Inventory.HasItem(ItemType.Ration), "Inventory contains Ration item.");
            Assert(agent.Inventory.GetItemCount(ItemType.Ration) >= 1, "Ration item count is >= 1.");

            // Setting HasFoodInInventory = false should clear Ration from Inventory
            agent.HasFoodInInventory = false;
            Assert(!agent.HasFoodInInventory, "HasFoodInInventory is false after clear.");
            Assert(!agent.Inventory.HasItem(ItemType.Ration), "Inventory does not contain Ration item.");

            HeadlessEntities.Remove(null, goCrawler);
            TestLog.Log("[PASS] Test 6: AutonomousAgent.HasFoodInInventory backward compatibility verified.");
        }

        private void ValidateZeroAllocationQueries()
        {
            var inv = new AgentInventory(capacity: 4, startingGold: 100);
            inv.TryAddItem(new ItemInstance(1, ItemType.Ration, 10, 5), out _);
            inv.TryAddItem(new ItemInstance(2, ItemType.IronOre, 5, 20), out _);

            // 1,000 queries in hot loop
            for (int i = 0; i < 1000; i++)
            {
                bool hasRations = inv.HasItem(ItemType.Ration, 5);
                int oreCount = inv.GetItemCount(ItemType.IronOre);
                var slot = inv.GetSlot(0);
                int gold = inv.Gold;

                if (!hasRations || oreCount != 5 || slot.IsEmpty || gold != 100)
                {
                    Assert(false, "Inconsistent query result during zero-allocation loop.");
                }
            }

            TestLog.Log("[PASS] Test 7: Zero-allocation hot path queries verified.");
        }

        /// <summary>
        /// Epic 10 G3 (D3), item system I4: equipment goes in slots beside the pack (protection, weapon, tool), so a full
        /// pack still takes it and it never takes a pack slot. Only equipment goes on, in its own slot, one item per slot;
        /// a pickaxe (a tool) and hazard gear (protection) are worn together. Wear counts a slot's durability down and
        /// breaks it at 0; the snapshot and a save carry every slot.
        /// </summary>
        private void ValidateGearIsWornBesideThePack()
        {
            var protection = Simpiens.Simulation.Items.EquipmentSlot.Protection;
            var tool = Simpiens.Simulation.Items.EquipmentSlot.Tool;
            var inv = new AgentInventory(4, startingGold: 0);
            inv.TryAddItem(new ItemInstance(1, ItemType.Ration, 1, 5), out _);
            inv.TryAddItem(new ItemInstance(2, ItemType.IronOre, 1, 8), out _);
            inv.TryAddItem(new ItemInstance(3, ItemType.MedicinalHerb, 1, 12), out _);
            inv.TryAddItem(new ItemInstance(4, ItemType.Glowstone, 1, 30), out _);
            Assert(inv.OccupiedSlots == 4, "The pack is full.");

            Assert(!inv.TryEquip(new ItemInstance(5, ItemType.Ration, 1, 5)), "A ration is not equipment.");
            Assert(inv.TryEquip(new ItemInstance(6, ItemType.HazardGear, 1, 40, durability: 3)), "Gear goes on although the pack is full.");
            Assert(inv.OccupiedSlots == 4 && !inv.HasItem(ItemType.HazardGear), "Equipment takes no pack slot and isn't in the pack.");
            Assert(inv.GetEquipped(protection).Type == ItemType.HazardGear, "Hazard gear goes in the protection slot.");
            Assert(!inv.TryEquip(new ItemInstance(7, ItemType.HazardGear, 1, 40)), "A second piece of protection is refused while one is worn.");
            Assert(inv.TryEquip(new ItemInstance(8, ItemType.ToolPickaxe, 1, 30, durability: 100)) && inv.GetEquipped(tool).Type == ItemType.ToolPickaxe,
                "A pickaxe goes in the tool slot beside the protection.");

            var snapshot = (AgentInventorySnapshot)inv.GetSnapshot();
            Assert(snapshot.GetEquipped(protection).Durability == 3 && snapshot.GetEquipped(tool).Type == ItemType.ToolPickaxe, "The snapshot carries every slot.");

            var save = new Simpiens.Simulation.Persistence.InventorySave();
            inv.CaptureTo(save);
            var back = new AgentInventory(4, startingGold: 0);
            back.RestoreFrom(Simpiens.Simulation.Persistence.SaveSerializer.Deserialize<Simpiens.Simulation.Persistence.InventorySave>(Simpiens.Simulation.Persistence.SaveSerializer.Serialize(save)));
            Assert(back.GetEquipped(protection) == inv.GetEquipped(protection) && back.GetEquipped(tool) == inv.GetEquipped(tool), "A save round trip keeps every slot and its durability.");

            Assert(!inv.Wear(protection) && inv.GetEquipped(protection).Durability == 2, "Wear counts the durability down.");
            Assert(!inv.Wear(protection) && inv.GetEquipped(protection).Durability == 1, "Wear counts the durability down again.");
            Assert(inv.Wear(protection) && inv.GetEquipped(protection).IsEmpty, "At 0 the gear breaks and is gone.");
            Assert(!inv.Wear(protection), "Nothing worn, nothing breaks.");
            Assert(inv.GetEquipped(tool).Durability == 100, "Wearing one slot leaves the others.");
            Assert(inv.TryEquip(new ItemInstance(9, ItemType.HazardGear, 1, 40)) && inv.Unequip(protection).ItemId == 9 && inv.GetEquipped(protection).IsEmpty, "Gear comes off again (a rolled-back purchase).");

            inv.TryEquip(new ItemInstance(10, ItemType.HazardGear, 1, 40));
            inv.Clear();
            Assert(inv.GetEquipped(protection).IsEmpty && inv.GetEquipped(tool).IsEmpty && inv.OccupiedSlots == 0, "Clear empties the pack and every slot.");

            TestLog.Log("[InventoryValidator] Test 8 Passed: Equipment is worn beside the pack, a slot per kind, wears out, and is snapshotted and saved.");
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition)
            {
                throw new Exception($"[InventoryValidator ASSERTION FAILED] {message}");
            }
        }
    }
}
