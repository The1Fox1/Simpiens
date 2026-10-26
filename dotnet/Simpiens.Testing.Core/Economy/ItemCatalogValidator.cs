using System;
using System.Collections.Generic;
using Simpiens.Cognition.Inventory;
using Simpiens.Simulation.Economy;
using Simpiens.Simulation.Items;

namespace Simpiens.Testing.Economy
{
    /// <summary>
    /// The item catalog (.agents/item_system_plan.md, I1): every definition is consistent, its groups answer what the
    /// hand-written lists they replaced answered, group queries are ordered and allocation-free, every map's nodes yield
    /// defined items, and rarity scores fall with rarity. Runs as a test (ValidatorTests), never at boot.
    /// </summary>
    public class ItemCatalogValidator
    {
        public void RunAllValidationTests()
        {
            TestLog.Log("[ItemCatalogValidator] Beginning item catalog validation...");

            ValidateDefinitionsAreConsistent();
            ValidateGroupsAnswerAsTheOldListsDid();
            ValidateGroupQueries();
            ValidateMapsYieldDefinedItems();
            ValidateRarityScores();

            TestLog.Log("<color=green>[ItemCatalogValidator] ALL 5 TESTS PASSED SUCCESSFULLY!</color>");
        }

        private void ValidateDefinitionsAreConsistent()
        {
            Assert(ItemCatalog.Get(ItemType.None) == null && !ItemCatalog.IsDefined(ItemType.None), "None is no item.");
            Assert(ItemCatalog.All.Count > 0, "The catalog defines items.");
            var names = new HashSet<string>();
            var economy = EconomyTuning.Default;
            ItemType previous = ItemType.None;
            foreach (var item in ItemCatalog.All)
            {
                Assert(item.Id > previous, $"{item.Name}: All is in id order.");
                previous = item.Id;
                Assert(ReferenceEquals(ItemCatalog.Get(item.Id), item), $"{item.Name}: looked up by its id.");
                Assert(!string.IsNullOrEmpty(item.Name) && names.Add(item.Name), $"{item.Name}: a unique code name.");
                Assert(!string.IsNullOrEmpty(item.DisplayName) && !string.IsNullOrEmpty(item.SpriteKey), $"{item.Name}: a display name and a sprite key.");
                Assert(item.MaxStack > 0, $"{item.Name}: stacks.");
                Assert(item.Categories != ItemCategory.None, $"{item.Name}: in at least one group.");
                Assert(economy.BaseValue(item.Id) > 0, $"{item.Name}: the default economy values it.");

                // Food and medicine are consumables; equipment is exactly one kind, and every kind is equipment
                if (item.Is(ItemCategory.Food) || item.Is(ItemCategory.Medicine)) Assert(item.Is(ItemCategory.Consumable), $"{item.Name}: food and medicine are consumables.");
                int kinds = (item.Is(ItemCategory.Weapon) ? 1 : 0) + (item.Is(ItemCategory.Protection) ? 1 : 0) + (item.Is(ItemCategory.Tool) ? 1 : 0);
                Assert(item.Is(ItemCategory.Equipment) ? kinds == 1 : kinds == 0, $"{item.Name}: equipment is exactly one of weapon, protection or tool.");
            }
            TestLog.Log("[PASS] Test 1: every definition is consistent.");
        }

        /// <summary>I1 is a refactor: over every id, the catalog's answers are the hand-written lists' answers.</summary>
        private void ValidateGroupsAnswerAsTheOldListsDid()
        {
            for (int i = 0; i < ItemCatalog.Capacity; i++)
            {
                var id = (ItemType)i;
                bool wasFood = id == ItemType.None || id == ItemType.Ration;
                bool wasSalvage = id == ItemType.IronOre || id == ItemType.MedicinalHerb || id == ItemType.Glowstone;
                bool wasWearable = id == ItemType.HazardGear;
                Assert(id.IsFoodYield() == wasFood, $"Item {i}: IsFoodYield {id.IsFoodYield()}, was {wasFood}.");
                Assert(id.IsSalvage() == wasSalvage, $"Item {i}: IsSalvage {id.IsSalvage()}, was {wasSalvage}.");
                Assert(AgentInventory.IsWearable(id) == wasWearable, $"Item {i}: IsWearable {AgentInventory.IsWearable(id)}, was {wasWearable}.");
            }
            TestLog.Log("[PASS] Test 2: IsFoodYield, IsSalvage and IsWearable answer as the lists they replaced, for all 256 ids.");
        }

        private void ValidateGroupQueries()
        {
            for (int bit = 0; bit < 32; bit++)
            {
                var group = (ItemCategory)(1u << bit);
                var members = ItemCatalog.InCategory(group);
                int expected = 0;
                foreach (var item in ItemCatalog.All) if (item.Is(group)) expected++;
                Assert(members.Count == expected, $"{group}: {members.Count} members, {expected} defined.");
                for (int i = 0; i < members.Count; i++)
                {
                    Assert(ItemCatalog.Is(members[i], group), $"{group}: {members[i]} is in it.");
                    if (i > 0) Assert(members[i] > members[i - 1], $"{group}: id order.");
                }
            }
            Assert(ItemCatalog.InCategory(ItemCategory.Resource).Count == 3, "Three resources: ore, herbs, glowstone.");

            bool refused = false;
            try { ItemCatalog.InCategory(ItemCategory.Resource | ItemCategory.Mineral); }
            catch (ArgumentException) { refused = true; }
            Assert(refused, "A query names one group.");

            long before = GC.GetAllocatedBytesForCurrentThread();
            int seen = 0;
            for (int n = 0; n < 1000; n++)
            {
                var food = ItemCatalog.InCategory(ItemCategory.Food);
                for (int i = 0; i < food.Count; i++) seen += (int)food[i];
                if (ItemCatalog.Is(ItemType.Glowstone, ItemCategory.Hazardous)) seen++;
            }
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert(allocated == 0 && seen > 0, $"Group queries allocated {allocated} bytes over 1,000 rounds.");
            TestLog.Log("[PASS] Test 3: group queries are in id order, complete, and allocate nothing.");
        }

        private void ValidateMapsYieldDefinedItems()
        {
            var maps = new[] { Simpiens.Core.Session.Layouts.Floor1Layout.Definition, Simpiens.Testing.Sessions.DevMapLayout.Definition };
            foreach (var map in maps)
            {
                for (int i = 0; i < map.ResourceNodes.Count; i++)
                {
                    var node = map.ResourceNodes[i];
                    Assert(node.Item == ItemType.None || ItemCatalog.IsDefined(node.Item), $"{map.Name} node {node.Key}: item {(byte)node.Item} is defined.");
                    Assert(node.Item.IsFoodYield() || ItemCatalog.Is(node.Item, ItemCategory.Resource), $"{map.Name} node {node.Key}: yields food or a resource.");
                }
            }
            TestLog.Log("[PASS] Test 4: every map's nodes yield defined food or resources.");
        }

        private void ValidateRarityScores()
        {
            Assert(ItemRarity.Common.Score() == 1000 && ItemRarity.Uncommon.Score() == 250 && ItemRarity.Rare.Score() == 50
                && ItemRarity.Epic.Score() == 10 && ItemRarity.Legendary.Score() == 2, "The tiers' scores are 1000, 250, 50, 10, 2.");
            Assert(ItemCatalog.Get(ItemType.Glowstone).Rarity == ItemRarity.Rare, "Glowstone is rare.");
            TestLog.Log("[PASS] Test 5: rarity scores fall with rarity.");
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new Exception($"[ItemCatalogValidator ASSERTION FAILED] {message}");
        }
    }
}
