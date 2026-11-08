using System;
using System.Collections.Generic;
using Simpiens.Simulation.Economy;
using Simpiens.Simulation.Items;

namespace Simpiens.Testing.Economy
{
    /// <summary>
    /// The recipe book (Epic 10 Part 2, W0.3): every recipe makes a defined item from defined inputs at a station, in a
    /// time; each item has one recipe at most; the lookups agree with the list and never allocate. Runs as a test
    /// (ValidatorTests), never at boot.
    /// </summary>
    public class RecipeValidator
    {
        public void RunAllValidationTests()
        {
            TestLog.Log("[RecipeValidator] Beginning recipe validation...");

            ValidateRecipesAreConsistent();
            ValidateLookups();
            ValidateTheBookRefusesTwoRecipesForOneItem();

            TestLog.Log("<color=green>[RecipeValidator] ALL 3 TESTS PASSED SUCCESSFULLY!</color>");
        }

        private void ValidateRecipesAreConsistent()
        {
            Assert(RecipeBook.All.Count > 0, "The book holds recipes.");
            ItemType previous = ItemType.None;
            foreach (var recipe in RecipeBook.All)
            {
                string name = recipe.ToString();
                Assert(recipe.Output > previous, $"{name}: All is in the order of what recipes make.");
                previous = recipe.Output;

                var output = ItemCatalog.Get(recipe.Output);
                Assert(output != null, $"{name}: makes a defined item.");
                Assert(recipe.OutputCount > 0 && recipe.OutputCount <= output.MaxStack, $"{name}: makes at least one, within a stack.");
                Assert(recipe.Station != StationKind.None && Enum.IsDefined(typeof(StationKind), recipe.Station), $"{name}: worked at a station.");
                Assert(recipe.WorkTicks > 0, $"{name}: takes work.");
                Assert(recipe.InputCount > 0 && recipe.InputCount <= Recipe.MaxInputs, $"{name}: takes one to {Recipe.MaxInputs} inputs.");

                var seen = new HashSet<ItemType>();
                for (int i = 0; i < recipe.InputCount; i++)
                {
                    var input = recipe.GetInput(i);
                    Assert(ItemCatalog.IsDefined(input.Item), $"{name}: input {i} is a defined item.");
                    Assert(input.Count > 0, $"{name}: takes at least one {ItemCatalog.NameOf(input.Item)}.");
                    Assert(seen.Add(input.Item), $"{name}: names {ItemCatalog.NameOf(input.Item)} once.");
                    Assert(input.Item != recipe.Output, $"{name}: isn't made from itself.");
                    Assert(recipe.CountOf(input.Item) == input.Count, $"{name}: CountOf agrees with its inputs.");
                }
            }
            Assert(RecipeBook.For(ItemType.ToolPickaxe)?.Station == StationKind.Forge, "The forge makes the pickaxe (D2).");
            TestLog.Log("  [Passed] Every recipe makes a defined item from defined inputs, at a station, in a time.");
        }

        private void ValidateLookups()
        {
            Assert(RecipeBook.For(ItemType.None) == null && !RecipeBook.IsMade(ItemType.None), "Nothing makes None.");
            foreach (var item in ItemCatalog.All)
            {
                var recipe = RecipeBook.For(item.Id);
                Assert(RecipeBook.IsMade(item.Id) == (recipe != null), $"{item.Name}: IsMade agrees with For.");
                if (recipe != null) Assert(recipe.Output == item.Id, $"{item.Name}: its recipe makes it.");
            }

            int listed = 0;
            foreach (StationKind station in Enum.GetValues(typeof(StationKind)))
            {
                var at = RecipeBook.At(station);
                for (int i = 0; i < at.Count; i++)
                {
                    Assert(at[i].Station == station, $"{at[i]}: listed at its own station.");
                    if (i > 0) Assert(at[i - 1].Output < at[i].Output, $"{station}: its recipes in the order of what they make.");
                }
                listed += at.Count;
            }
            Assert(listed == RecipeBook.All.Count, "Every recipe is listed at exactly one station.");

            RecipeBook.At(StationKind.Forge); // warm
            long before = GC.GetAllocatedBytesForCurrentThread();
            int found = 0;
            for (int i = 0; i < 100; i++)
            {
                found += RecipeBook.At(StationKind.Forge).Count + RecipeBook.At(StationKind.Still).Count;
                if (RecipeBook.For(ItemType.ToolPickaxe) != null) found++;
            }
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert(found > 0 && allocated == 0, $"Recipe lookups allocate nothing ({allocated} bytes).");
            TestLog.Log("  [Passed] Lookups by item and by station agree with the list and allocate nothing.");
        }

        private void ValidateTheBookRefusesTwoRecipesForOneItem()
        {
            bool refused = false;
            try
            {
                using (RecipeBook.ExtendForTest(new Recipe(ItemType.ToolPickaxe, StationKind.Still, 10, new RecipeInput(ItemType.MedicinalHerb, 1)))) { }
            }
            catch (ArgumentException)
            {
                refused = true;
            }
            Assert(refused, "A second recipe for an item is refused (D1).");
            Assert(RecipeBook.For(ItemType.ToolPickaxe).Station == StationKind.Forge, "A refused extension leaves the book as it was.");
            TestLog.Log("  [Passed] An item has one recipe at most.");
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new Exception($"[RecipeValidator ASSERTION FAILED] {message}");
        }
    }
}
