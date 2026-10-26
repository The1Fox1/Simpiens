using System;
using System.Buffers;
using Simpiens.Cognition.Inventory;
using Simpiens.Simulation.Economy;
using Simpiens.Simulation.Persistence;

namespace Simpiens.Testing.Economy
{
    /// <summary>
    /// Epic 9 §6 item 4 (Milestone 1 completion plan, step 1): the economy's data round-trips through the save serializer.
    /// Items, bounties and stalls are saved as raw memory, so their sizes are pinned here; an inventory round-trips its
    /// purse and slots; and saving and loading into kept buffers and objects allocates nothing.
    /// </summary>
    public class EconomySerializationValidator
    {
        public void RunAllValidationTests()
        {
            TestLog.Log("[EconomySerializationValidator] Beginning economy serialization validation...");

            ValidateSavedLayoutsArePinned();
            ValidateRecordsRoundTrip();
            ValidateInventoryRoundTrips();
            ValidateRoundTripAllocatesNothing();

            TestLog.Log("<color=green>[EconomySerializationValidator] ALL 4 TESTS PASSED SUCCESSFULLY!</color>");
        }

        /// <summary>
        /// A saved record's size is part of the save format. If one of these fails, the struct's layout changed: bump
        /// SessionSaveHeader.CurrentFormatVersion (older saves can no longer be read) and update the pin.
        /// </summary>
        private void ValidateSavedLayoutsArePinned()
        {
            AssertSize<ItemInstance>(16);
            AssertSize<BountyContract>(64);
            AssertSize<TradeStallData>(84); // 52 since Epic 10 G2 (format 3); 56 since G3 (format 4); 84 since item system I5: a goods table of 8, so new items don't change it (format 6)
            TestLog.Log("[PASS] Test 1: ItemInstance, BountyContract and TradeStallData keep their saved sizes (16, 64, 84 bytes).");
        }

        private void ValidateRecordsRoundTrip()
        {
            var item = new ItemInstance(7, ItemType.MedicinalHerb, 3, 12, quality: 2, durability: 61);
            var itemBack = SaveSerializer.Deserialize<ItemInstance>(SaveSerializer.Serialize(item));
            Assert(itemBack.Equals(item), $"An item round-trips ({itemBack} vs {item}).");

            var bounties = SampleBounties();
            var bountiesBack = SaveSerializer.Deserialize<BountyContract[]>(SaveSerializer.Serialize(bounties));
            Assert(bountiesBack != null && bountiesBack.Length == bounties.Length, "The bounty array length round-trips.");
            for (int i = 0; i < bounties.Length; i++)
            {
                Assert(bountiesBack[i].Equals(bounties[i]), $"Bounty {i} round-trips field for field ({bountiesBack[i]} vs {bounties[i]}).");
            }

            var stalls = SampleStalls();
            var stallsBack = SaveSerializer.Deserialize<TradeStallData[]>(SaveSerializer.Serialize(stalls));
            Assert(stallsBack != null && stallsBack.Length == stalls.Length, "The stall array length round-trips.");
            for (int i = 0; i < stalls.Length; i++)
            {
                AssertStallEqual(stallsBack[i], stalls[i], $"Stall {i}");
            }

            TestLog.Log("[PASS] Test 2: items, bounties (claimant, location, status) and stalls (prices, stock, treasury, arrears) round-trip.");
        }

        private void ValidateInventoryRoundTrips()
        {
            var inventory = new AgentInventory(4, startingGold: 37);
            inventory.TryAddItem(new ItemInstance(1, ItemType.Ration, 3, 15), out _);
            inventory.TryAddItem(new ItemInstance(2, ItemType.IronOre, 2, 8, quality: 1, durability: 70), out _);
            inventory.TryAddItem(new ItemInstance(3, ItemType.MedicinalHerb, 1, 12), out _);
            inventory.TryRemoveItem(ItemType.IronOre, 2); // leaves an empty slot between two full ones

            var save = new InventorySave();
            inventory.CaptureTo(save);
            var back = SaveSerializer.Deserialize<InventorySave>(SaveSerializer.Serialize(save));

            // Restore over a pack holding something else: everything is replaced, empty slots included
            var restored = new AgentInventory(4, startingGold: 999);
            restored.TryAddItem(new ItemInstance(9, ItemType.IronOre, 50, 8), out _);
            restored.RestoreFrom(back);

            Assert(restored.Gold == 37, $"The purse round-trips ({restored.Gold}g, expected 37g).");
            for (int i = 0; i < inventory.Capacity; i++)
            {
                Assert(restored.GetSlot(i).Equals(inventory.GetSlot(i)), $"Slot {i} round-trips ({restored.GetSlot(i)} vs {inventory.GetSlot(i)}).");
            }
            Assert(restored.GetItemCount(ItemType.IronOre) == 0, "The ore that was in the restored pack is gone.");

            bool refused = false;
            try
            {
                new AgentInventory(6).RestoreFrom(back);
            }
            catch (ArgumentException)
            {
                refused = true;
            }
            Assert(refused, "A saved pack with another capacity is refused.");

            TestLog.Log("[PASS] Test 3: an inventory round-trips its purse and every slot, and refuses a pack of another size.");
        }

        /// <summary>
        /// Saving into a kept buffer and loading into kept objects allocates nothing once warm (the house pattern:
        /// GC.GetAllocatedBytesForCurrentThread around the loop).
        /// </summary>
        private void ValidateRoundTripAllocatesNothing()
        {
            var bounties = SampleBounties();
            var stalls = SampleStalls();
            var items = new[] { new ItemInstance(1, ItemType.Ration, 2, 15), new ItemInstance(2, ItemType.IronOre, 5, 8) };
            var inventory = new AgentInventory(4, startingGold: 120);
            inventory.TryAddItem(new ItemInstance(1, ItemType.Ration, 4, 15), out _);
            var target = new AgentInventory(4);

            var buffer = new ArrayBufferWriter<byte>(4096);
            var inventorySave = new InventorySave();
            InventorySave inventoryBack = new InventorySave();
            BountyContract[] bountiesBack = new BountyContract[bounties.Length];
            TradeStallData[] stallsBack = new TradeStallData[stalls.Length];
            ItemInstance[] itemsBack = new ItemInstance[items.Length];
            BountyContract singleBack = default;

            for (int i = 0; i < 10; i++) RoundTrip(); // warm up: generated formatters, pooled writer state

            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 1000; i++) RoundTrip();
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert(allocated == 0, $"1,000 economy round trips into kept buffers allocated {allocated} bytes (expected 0).");
            Assert(target.Gold == 120 && target.GetItemCount(ItemType.Ration) == 4, "The kept inventory holds the restored pack.");
            Assert(bountiesBack[1].Equals(bounties[1]) && singleBack.Equals(bounties[0]), "The kept bounty buffers hold the restored bounties.");
            TestLog.Log("[PASS] Test 4: 1,000 round trips of items, bounties, stalls and an inventory allocate 0 bytes.");

            void RoundTrip()
            {
                buffer.Clear();
                inventory.CaptureTo(inventorySave);
                SaveSerializer.Serialize(buffer, items);
                int itemsEnd = buffer.WrittenCount;
                SaveSerializer.Serialize(buffer, bounties);
                int bountiesEnd = buffer.WrittenCount;
                SaveSerializer.Serialize(buffer, stalls);
                int stallsEnd = buffer.WrittenCount;
                SaveSerializer.Serialize(buffer, bounties[0]);
                int singleEnd = buffer.WrittenCount;
                SaveSerializer.Serialize(buffer, inventorySave);

                var written = buffer.WrittenSpan;
                SaveSerializer.Deserialize(written.Slice(0, itemsEnd), ref itemsBack);
                SaveSerializer.Deserialize(written.Slice(itemsEnd, bountiesEnd - itemsEnd), ref bountiesBack);
                SaveSerializer.Deserialize(written.Slice(bountiesEnd, stallsEnd - bountiesEnd), ref stallsBack);
                SaveSerializer.Deserialize(written.Slice(stallsEnd, singleEnd - stallsEnd), ref singleBack);
                SaveSerializer.Deserialize(written.Slice(singleEnd), ref inventoryBack);
                target.RestoreFrom(inventoryBack);
            }
        }

        private static BountyContract[] SampleBounties()
        {
            var stallId = new EntityId(11u, 12u, 13u, 14u);
            return new[]
            {
                new BountyContract(1, BountyType.GatherResource, 4, ItemType.IronOre, 2, 40, 0, 6000,
                    new Vec2(12.5f, -3.25f), creatorStallId: stallId),
                new BountyContract(2, BountyType.ScoutSector, 5, ItemType.None, 1, 25, 120, 6120,
                    new Vec2(-8f, 20f), new EntityId(21u, 22u, 23u, 24u), stallId, BountyStatus.Claimed),
                new BountyContract(3, BountyType.GatherResource, 4, ItemType.MedicinalHerb, 1, 30, 50, 900,
                    default, default, stallId, BountyStatus.Expired),
            };
        }

        private static TradeStallData[] SampleStalls()
        {
            return new[]
            {
                TestStalls.Stall(new EntityId(11u, 12u, 13u, 14u), new Vec2(0.5f, 1.5f), rationSellPrice: 17, rationStock: 33,
                    oreBuyPrice: 21, oreStock: 4, herbBuyPrice: 26, herbStock: 2, treasuryGold: 412, interactionRadius: 2.75f, inArrears: true),
                TestStalls.Stall(new EntityId(31u, 32u, 33u, 34u), new Vec2(-4f, 9f), treasuryGold: 0),
            };
        }

        private static void AssertStallEqual(in TradeStallData actual, in TradeStallData expected, string label)
        {
            // TradeStallData.Equals compares ids only: compare every field
            Assert(actual.StallEntityId == expected.StallEntityId && actual.Position == expected.Position, $"{label}: id and position round-trip.");
            Assert(actual.RationSellPrice == expected.RationSellPrice && actual.OreBuyPrice == expected.OreBuyPrice && actual.HerbBuyPrice == expected.HerbBuyPrice,
                $"{label}: prices round-trip.");
            Assert(actual.RationStock == expected.RationStock && actual.OreStock == expected.OreStock && actual.HerbStock == expected.HerbStock,
                $"{label}: stock round-trips.");
            Assert(actual.TreasuryGold == expected.TreasuryGold && actual.InteractionRadius == expected.InteractionRadius && actual.InArrears == expected.InArrears,
                $"{label}: treasury, radius and arrears round-trip.");
        }

        private static void AssertSize<T>(int expected) where T : struct
        {
            int size = System.Runtime.CompilerServices.Unsafe.SizeOf<T>();
            Assert(size == expected,
                $"{typeof(T).Name} is {size} bytes, pinned at {expected}: its layout is the save format. Bump SessionSaveHeader.CurrentFormatVersion and update the pin.");
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition)
            {
                throw new Exception($"[ASSERTION FAILED] {message}");
            }
        }
    }
}
