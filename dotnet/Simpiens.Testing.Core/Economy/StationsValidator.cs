using System;
using System.Linq;
using Simpiens.Simulation.Economy;
using Simpiens.Simulation.Items;
using Simpiens.Simulation.Workshops;
using Simpiens.Testing.Environment;

namespace Simpiens.Testing.Economy
{
    /// <summary>
    /// The station kinds' table (Epic LIFE-1 plan 2, T.1, D17; finding F13): a row for every kind, words in Latin-1, the art
    /// key the kind's name in kebab case, recipes exactly where the recipe book has them, an artisan where there are recipes,
    /// and the rules' words and costs read from the rows. Runs as a test (ValidatorTests).
    /// </summary>
    public class StationsValidator
    {
        public void RunAllValidationTests()
        {
            TestLog.Log("[StationsValidator] Beginning station table validation...");
            ValidateTheRows();
            ValidateWhatReadsThem();
            TestLog.Log("<color=green>[StationsValidator] ALL 2 TESTS PASSED SUCCESSFULLY!</color>");
        }

        private void ValidateTheRows()
        {
            foreach (StationKind kind in Enum.GetValues(typeof(StationKind)))
            {
                if (kind == StationKind.None) continue;
                var row = StationTable.Get(kind);
                Assert(row.Kind == kind, $"{kind}: a row of its own.");
                Assert(!string.IsNullOrEmpty(row.Word) && !string.IsNullOrEmpty(row.Title) && !string.IsNullOrEmpty(row.Description)
                    && (row.Word + row.Title + row.Description).All(c => c < 256), $"{kind}: words, in Latin-1.");
                Assert(row.ArtKey == Kebab(kind.ToString()), $"{kind}: its art key is its name in kebab case ({row.ArtKey}).");
                bool recipes = RecipeBook.All.Any(r => r.Station == kind);
                Assert(row.HasRecipes == recipes, $"{kind}: recipes in its row exactly when the book has some ({row.HasRecipes}, {recipes}).");
                Assert(!row.HasRecipes || row.TakesArtisan, $"{kind}: what makes to order has an artisan.");
            }
            Assert(StationTable.All.Count == Enum.GetValues(typeof(StationKind)).Length - 1, "No row without a kind.");
            Assert(!StationTable.Get(StationKind.DrillYard).TakesArtisan && StationTable.Get(StationKind.DrillYard).Cost == StationCost.Yard, "The drill yard: no artisan, its own price.");
        }

        private void ValidateWhatReadsThem()
        {
            var tuning = EconomyTuning.Default.Workshops;
            foreach (var row in StationTable.All)
            {
                Assert(WorkshopRules.StationName(row.Kind) == row.Word, $"{row.Kind}: the rules' word is its row's.");
                ushort expected = row.Cost == StationCost.Yard ? tuning.DrillYardCost : row.Cost == StationCost.Tavern ? tuning.TavernCost : tuning.ConstructionCost;
                Assert(tuning.CostOf(row.Kind) == expected, $"{row.Kind}: costs its line ({tuning.CostOf(row.Kind)}g).");
            }
        }

        private static string Kebab(string name)
        {
            var text = new System.Text.StringBuilder();
            for (int i = 0; i < name.Length; i++)
            {
                if (char.IsUpper(name[i]) && i > 0) text.Append('-');
                text.Append(char.ToLowerInvariant(name[i]));
            }
            return text.ToString();
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("[StationsValidator] FAILED: " + message);
        }
    }
}
