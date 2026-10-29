using System;
using System.Threading.Tasks;
using Simpiens.Simulation;
using Simpiens.Simulation.Economy;
using Simpiens.Simulation.Items;

namespace Simpiens.Testing.Economy
{
    /// <summary>
    /// Loot tables (item system I7, a foundation not yet wired into play): rolls are deterministic on any thread, drop
    /// each entry as often as its weight says (an item's rarity score by default), keep rolls and quantities in their
    /// ranges, nest, refuse broken tables, stop at a full buffer, and allocate nothing. Runs as a test (ValidatorTests).
    /// </summary>
    public class LootValidator
    {
        private const ulong Seed = 424242UL;
        private static readonly EntityId Node = new EntityId(7, 0, 0, 0);

        public void RunAllValidationTests()
        {
            TestLog.Log("[LootValidator] Beginning loot table validation...");

            ValidateRollsAreDeterministic();
            ValidateDropsFollowWeights();
            ValidateRarityIsTheDefaultWeight();
            ValidateRollsAndQuantitiesStayInRange();
            ValidateNestingAndBrokenTables();
            ValidateAFullBufferAndNoAllocation();

            TestLog.Log("<color=green>[LootValidator] ALL 6 TESTS PASSED SUCCESSFULLY!</color>");
        }

        private static LootTables Tables(params LootTable[] tables) => new LootTables(tables);

        private void ValidateRollsAreDeterministic()
        {
            var tables = Tables(new LootTable(1, "salvage", 1, 3,
                LootEntry.ItemDrop(ItemType.IronOre, 1, 3, weight: 600), LootEntry.ItemDrop(ItemType.MedicinalHerb, weight: 300), LootEntry.Nothing(100)));
            var first = new ItemInstance[8];
            var second = new ItemInstance[8];
            for (long tick = 0; tick < 500; tick++)
            {
                int a = LootRoller.Roll(tables, 1, Seed, Node, tick, first);
                int b = LootRoller.Roll(tables, 1, Seed, Node, tick, second);
                Assert(a == b, $"t{tick}: the same inputs dropped {a} and {b} stacks.");
                for (int i = 0; i < a; i++) Assert(first[i] == second[i], $"t{tick}: drop {i} differs.");
            }

            // On other threads, in any order: the same drops
            var expected = new int[200];
            var buffer = new ItemInstance[8];
            for (int t = 0; t < expected.Length; t++) expected[t] = Signature(buffer, LootRoller.Roll(tables, 1, Seed, Node, t, buffer));
            var parallel = new int[expected.Length];
            Parallel.For(0, expected.Length, t =>
            {
                var local = new ItemInstance[8];
                parallel[t] = Signature(local, LootRoller.Roll(tables, 1, Seed, Node, t, local));
            });
            for (int t = 0; t < expected.Length; t++) Assert(parallel[t] == expected[t], $"t{t}: a roll on another thread differs.");
            TestLog.Log("[PASS] Test 1: the same seed, entity and tick drop the same items, on any thread.");
        }

        private void ValidateDropsFollowWeights()
        {
            var tables = Tables(new LootTable(1, "salvage", 1, 1,
                LootEntry.ItemDrop(ItemType.IronOre, weight: 600), LootEntry.ItemDrop(ItemType.MedicinalHerb, weight: 300), LootEntry.Nothing(100)));
            const int Rolls = 100000;
            int ore = 0, herb = 0, nothing = 0;
            var drops = new ItemInstance[2];
            for (long tick = 0; tick < Rolls; tick++)
            {
                int n = LootRoller.Roll(tables, 1, Seed, Node, tick, drops);
                if (n == 0) nothing++;
                else if (drops[0].Type == ItemType.IronOre) ore++;
                else herb++;
            }
            AssertShare(ore, Rolls, 0.6, "ore");
            AssertShare(herb, Rolls, 0.3, "herbs");
            AssertShare(nothing, Rolls, 0.1, "nothing");
            TestLog.Log($"[PASS] Test 2: over {Rolls} rolls, ore {ore}, herbs {herb}, nothing {nothing}: each within 5 standard deviations of its weight.");
        }

        private void ValidateRarityIsTheDefaultWeight()
        {
            // Ore is common (1000), glowstone rare (50): glowstone drops 50 times in 1,050
            var tables = Tables(new LootTable(1, "by rarity", 1, 1, LootEntry.ItemDrop(ItemType.IronOre), LootEntry.ItemDrop(ItemType.Glowstone)));
            Assert(tables.TryGet(1, out var table) && table.TotalWeight == 1050, "Weights default to the items' rarity scores (1000 + 50).");
            const int Rolls = 100000;
            int glowstone = 0;
            var drops = new ItemInstance[2];
            for (long tick = 0; tick < Rolls; tick++)
            {
                if (LootRoller.Roll(tables, 1, Seed, Node, tick, drops) == 1 && drops[0].Type == ItemType.Glowstone) glowstone++;
            }
            AssertShare(glowstone, Rolls, 50.0 / 1050.0, "glowstone");
            TestLog.Log($"[PASS] Test 3: a rare item drops by its rarity score ({glowstone} glowstone in {Rolls}).");
        }

        private void ValidateRollsAndQuantitiesStayInRange()
        {
            var tables = Tables(new LootTable(1, "ranges", 2, 4, LootEntry.ItemDrop(ItemType.IronOre, 3, 5)));
            var drops = new ItemInstance[8];
            bool sawTwo = false, sawFour = false, sawThree = false, sawFive = false;
            for (long tick = 0; tick < 2000; tick++)
            {
                int n = LootRoller.Roll(tables, 1, Seed, Node, tick, drops);
                Assert(n >= 2 && n <= 4, $"t{tick}: {n} rolls, not 2 to 4.");
                sawTwo |= n == 2;
                sawFour |= n == 4;
                for (int i = 0; i < n; i++)
                {
                    Assert(drops[i].Quantity >= 3 && drops[i].Quantity <= 5, $"t{tick}: {drops[i].Quantity} units, not 3 to 5.");
                    sawThree |= drops[i].Quantity == 3;
                    sawFive |= drops[i].Quantity == 5;
                }
            }
            Assert(sawTwo && sawFour && sawThree && sawFive, "Both ends of each range come up.");
            TestLog.Log("[PASS] Test 4: rolls and quantities stay in their ranges, both ends included.");
        }

        private void ValidateNestingAndBrokenTables()
        {
            var tables = Tables(
                new LootTable(1, "outer", 1, 1, LootEntry.NestedTable(2, 1)),
                new LootTable(2, "inner", 1, 1, LootEntry.ItemDrop(ItemType.MedicinalHerb, 2, 2)));
            var drops = new ItemInstance[4];
            for (long tick = 0; tick < 50; tick++)
            {
                Assert(LootRoller.Roll(tables, 1, Seed, Node, tick, drops) == 1 && drops[0].Type == ItemType.MedicinalHerb && drops[0].Quantity == 2, "A nested table is rolled in its place.");
            }
            Assert(LootRoller.Roll(tables, 99, Seed, Node, 0, drops) == 0, "An unknown table drops nothing.");

            Refused(() => Tables(new LootTable(1, "a", 1, 1, LootEntry.NestedTable(2, 1)), new LootTable(2, "b", 1, 1, LootEntry.NestedTable(1, 1))), "a cycle");
            Refused(() => Tables(new LootTable(1, "a", 1, 1, LootEntry.NestedTable(3, 1))), "a missing nested table");
            Refused(() => Tables(new LootTable(1, "a", 1, 1, LootEntry.Nothing(1)), new LootTable(1, "b", 1, 1, LootEntry.Nothing(1))), "two tables with one id");
            Refused(() => Tables(
                new LootTable(1, "d1", 1, 1, LootEntry.NestedTable(2, 1)), new LootTable(2, "d2", 1, 1, LootEntry.NestedTable(3, 1)),
                new LootTable(3, "d3", 1, 1, LootEntry.NestedTable(4, 1)), new LootTable(4, "d4", 1, 1, LootEntry.NestedTable(5, 1)),
                new LootTable(5, "d5", 1, 1, LootEntry.Nothing(1))), $"nesting deeper than {LootRoller.MaxDepth}");
            Refused(() => new LootTable(1, "unknown", 1, 1, LootEntry.ItemDrop((ItemType)77)), "an item the catalog doesn't have");
            Refused(() => new LootTable(1, "weightless", 1, 1, LootEntry.Nothing(0)), "an entry that weighs nothing");
            Refused(() => new LootTable(1, "backwards", 3, 1, LootEntry.Nothing(1)), "fewer most rolls than least");
            Refused(() => new LootTable(1, "quantities", 1, 1, LootEntry.ItemDrop(ItemType.IronOre, 4, 2)), "a quantity range backwards");
            TestLog.Log("[PASS] Test 5: nested tables roll in place; broken tables are refused when built.");
        }

        private void ValidateAFullBufferAndNoAllocation()
        {
            var tables = Tables(new LootTable(1, "many", 6, 6, LootEntry.ItemDrop(ItemType.IronOre)));
            var small = new ItemInstance[3];
            Assert(LootRoller.Roll(tables, 1, Seed, Node, 0, small) == 3, "A full buffer stops the drops.");

            var buffer = new ItemInstance[8];
            LootRoller.Roll(tables, 1, Seed, Node, 0, buffer); // warm up
            long before = GC.GetAllocatedBytesForCurrentThread();
            int total = 0;
            for (long tick = 0; tick < 1000; tick++) total += LootRoller.Roll(tables, 1, Seed, Node, tick, buffer);
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert(allocated == 0 && total == 6000, $"1,000 rolls allocated {allocated} bytes ({total} drops).");
            TestLog.Log("[PASS] Test 6: a full buffer stops the drops, and rolling allocates nothing.");
        }

        private static int Signature(ItemInstance[] drops, int count)
        {
            int h = count;
            for (int i = 0; i < count; i++) h = h * 31 + ((int)drops[i].Type << 8) + drops[i].Quantity;
            return h;
        }

        private static void AssertShare(int hits, int rolls, double expected, string what)
        {
            double sigma = Math.Sqrt(expected * (1 - expected) / rolls);
            double share = (double)hits / rolls;
            Assert(Math.Abs(share - expected) <= 5 * sigma, $"{what}: {share:P2} of rolls, expected {expected:P2} (5 sigma = {5 * sigma:P2}).");
        }

        private static void Refused(Action build, string what)
        {
            bool refused = false;
            try { build(); }
            catch (ArgumentException) { refused = true; }
            Assert(refused, $"A table with {what} is refused.");
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new Exception($"[LootValidator ASSERTION FAILED] {message}");
        }
    }
}
