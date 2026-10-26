using System;
using System.Collections.Generic;
using Simpiens.Cognition.Inventory;
using Simpiens.Simulation;
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
            ValidateConsumables();
            ValidateEquipmentStats();

            TestLog.Log("<color=green>[ItemCatalogValidator] ALL 7 TESTS PASSED SUCCESSFULLY!</color>");
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
                Assert(item.Is(ItemCategory.Equipment) == (item.Equipment != null), $"{item.Name}: equipment exactly when it has an equipment block.");
                if (item.Equipment != null)
                {
                    var expected = item.Is(ItemCategory.Weapon) ? EquipmentSlot.Weapon : item.Is(ItemCategory.Protection) ? EquipmentSlot.Protection : EquipmentSlot.Tool;
                    Assert(item.Equipment.Slot == expected && item.Equipment.Durability > 0, $"{item.Name}: in the {expected} slot, with a life.");
                }
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
                Assert(id.IsFoodYield() == wasFood, $"Item {i}: IsFoodYield {id.IsFoodYield()}, was {wasFood}.");
                Assert(id.IsSalvage() == wasSalvage, $"Item {i}: IsSalvage {id.IsSalvage()}, was {wasSalvage}.");
                // The one gear slot of G3 took hazard gear only; I4's slots take every equipment item, in its own slot
                bool equipment = ItemCatalog.Is(id, ItemCategory.Equipment);
                Assert(AgentInventory.IsEquipment(id) == equipment, $"Item {i}: IsEquipment {AgentInventory.IsEquipment(id)}, category Equipment {equipment}.");
            }
            TestLog.Log("[PASS] Test 2: IsFoodYield and IsSalvage answer as the lists they replaced, and IsEquipment as the Equipment group, for all 256 ids.");
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

        /// <summary>
        /// I3: an item is consumable exactly when it has effects; food eases hunger and medicine heals. ItemUse reproduces
        /// the code it replaced bit for bit (Max(0, hunger - 40), Min(100, health + 30)), and Consume takes one unit or
        /// refuses.
        /// </summary>
        private void ValidateConsumables()
        {
            foreach (var item in ItemCatalog.All)
            {
                Assert(item.Is(ItemCategory.Consumable) == (item.ConsumeEffectCount > 0), $"{item.Name}: consumable exactly when it has effects.");
                bool easesHunger = false, heals = false;
                for (int i = 0; i < item.ConsumeEffectCount; i++)
                {
                    var effect = item.GetConsumeEffect(i);
                    easesHunger |= effect.Stat == StatKind.Hunger && effect.Amount < 0f;
                    heals |= effect.Stat == StatKind.Health && effect.Amount > 0f;
                }
                if (item.Is(ItemCategory.Food)) Assert(easesHunger, $"{item.Name}: food eases hunger.");
                if (item.Is(ItemCategory.Medicine)) Assert(heals, $"{item.Name}: medicine heals.");
            }

            var ration = ItemCatalog.Get(ItemType.Ration);
            var herb = ItemCatalog.Get(ItemType.MedicinalHerb);
            var crawler = new Simpiens.Cognition.AgentState(new EntityId(1, 0, 0, 0));
            for (int i = 0; i <= 1100; i++)
            {
                float value = i * 0.0937f; // 0 to about 103, off the round numbers
                crawler.Hunger = value;
                Simpiens.Cognition.ItemUse.Apply(crawler, ration);
                Assert(BitConverter.SingleToInt32Bits(crawler.Hunger) == BitConverter.SingleToInt32Bits(Simpiens.SimMath.Max(0f, value - 40f)), $"A ration at hunger {value}: {crawler.Hunger}.");
                crawler.Health = value;
                Simpiens.Cognition.ItemUse.Apply(crawler, herb);
                Assert(BitConverter.SingleToInt32Bits(crawler.Health) == BitConverter.SingleToInt32Bits(Simpiens.SimMath.Min(Simpiens.Cognition.Vitality.MaxHealth, value + 30f)), $"A herb at health {value}: {crawler.Health}.");
            }

            var eater = new Simpiens.Cognition.AgentState(new EntityId(2, 0, 0, 0));
            eater.Inventory.Clear();
            Assert(!Simpiens.Cognition.ItemUse.Consume(eater, ItemType.Ration), "Nothing to eat: refused.");
            eater.Inventory.TryAddItem(new ItemInstance(1, ItemType.Ration, 2, 5), out _);
            eater.Inventory.TryAddItem(new ItemInstance(2, ItemType.IronOre, 1, 8), out _);
            eater.Hunger = 70f;
            Assert(!Simpiens.Cognition.ItemUse.Consume(eater, ItemType.IronOre) && eater.Inventory.GetItemCount(ItemType.IronOre) == 1, "Ore isn't consumed.");
            Assert(Simpiens.Cognition.ItemUse.Consume(eater, ItemType.Ration) && eater.Hunger == 30f && eater.Inventory.GetItemCount(ItemType.Ration) == 1, "A ration: hunger 70 -> 30, one left.");
            TestLog.Log("[PASS] Test 6: consumables are defined by their effects, and using one is exactly the old arithmetic.");
        }

        /// <summary>
        /// I4: a crawler's stats are a base value, then its equipment's Add modifiers, then its Multiply modifiers. Hazard
        /// gear gives HazardResistance exactly 0.5 (so hazard damage keeps G3's float arithmetic); a pickaxe multiplies
        /// HarvestSpeed (reserved); nothing equipped leaves the base.
        /// </summary>
        private void ValidateEquipmentStats()
        {
            var inv = new AgentInventory(4, startingGold: 0);
            Assert(CrawlerStats.Get(inv, StatKind.HazardResistance) == 0f && CrawlerStats.Get(inv, StatKind.HarvestSpeed, 1f) == 1f, "Nothing equipped: the base.");
            inv.TryEquip(new ItemInstance(1, ItemType.HazardGear, 1, 30, durability: 500));
            Assert(BitConverter.SingleToInt32Bits(CrawlerStats.Get(inv, StatKind.HazardResistance)) == BitConverter.SingleToInt32Bits(0.5f), "Hazard gear: resistance exactly 0.5.");
            inv.TryEquip(new ItemInstance(2, ItemType.ToolPickaxe, 1, 30, durability: 100));
            Assert(CrawlerStats.Get(inv, StatKind.HarvestSpeed, 1f) == 1.5f, "A pickaxe: harvest speed x1.5 on a base of 1.");
            Assert(CrawlerStats.Get(inv, StatKind.HarvestSpeed, 2f) == 3f && CrawlerStats.Get(inv, StatKind.HazardResistance) == 0.5f, "Each stat reads only its own modifiers.");
            Assert(CrawlerStats.Get(null, StatKind.Attack, 4f) == 4f, "No inventory: the base.");
            TestLog.Log("[PASS] Test 7: equipment stats are the base, plus adds, times multiplies.");
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new Exception($"[ItemCatalogValidator ASSERTION FAILED] {message}");
        }
    }
}
