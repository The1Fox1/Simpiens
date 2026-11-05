using System.Collections.Generic;
using Simpiens.Simulation.Economy;

namespace Simpiens.Simulation.Items
{
    /// <summary>
    /// Every recipe the game knows, by what it makes (Epic 10 Part 2, W0.3, D1): static and immutable beside the item
    /// catalog, so any code reads it, on any thread. To make an item: a recipe below, naming its station, its inputs
    /// and its work time.
    /// </summary>
    public static class RecipeBook
    {
        private sealed class Tables
        {
            public readonly Recipe[] ByOutput = new Recipe[ItemCatalog.Capacity];
            public readonly List<Recipe> All = new List<Recipe>(8);
            public readonly Recipe[][] ByStation;

            public Tables(IEnumerable<Recipe> recipes)
            {
                int stations = 0;
                foreach (var recipe in recipes)
                {
                    if (ByOutput[(byte)recipe.Output] != null) throw new System.ArgumentException($"Two recipes make {ItemCatalog.NameOf(recipe.Output)}.");
                    ByOutput[(byte)recipe.Output] = recipe;
                    int index = All.Count;
                    while (index > 0 && All[index - 1].Output > recipe.Output) index--;
                    All.Insert(index, recipe);
                    if ((int)recipe.Station + 1 > stations) stations = (int)recipe.Station + 1;
                }
                // Each station's recipes in the order of what they make, built once: queries never allocate
                ByStation = new Recipe[System.Math.Max(stations, (int)StationKind.Still + 1)][];
                for (int station = 0; station < ByStation.Length; station++)
                {
                    var at = new List<Recipe>();
                    for (int i = 0; i < All.Count; i++)
                    {
                        if ((int)All[i].Station == station) at.Add(All[i]);
                    }
                    ByStation[station] = at.ToArray();
                }
            }
        }

        private static volatile Tables _tables = new Tables(Recipes());

        /// <summary>The game's recipes (Part 2 Q5). Work times are placeholders until W1 measures output.</summary>
        private static Recipe[] Recipes() => new[]
        {
            // D2: two game hours of work at the forge (T3.1; it was a minute, 600 ticks) for three ore, which the stall holds
            // plenty of (F8)
            new Recipe(ItemType.ToolPickaxe, StationKind.Forge, workTicks: (ushort)DayClock.Hours(2), new RecipeInput(ItemType.IronOre, 3)),
            // D42: an hour's work at the still for two herbs
            new Recipe(ItemType.HealingDraught, StationKind.Still, workTicks: (ushort)DayClock.Hours(1), new RecipeInput(ItemType.MedicinalHerb, 2))
        };

        /// <summary>
        /// Tests only: the book with <paramref name="extra"/> added, until the returned scope is disposed, as
        /// <see cref="ItemCatalog"/>'s hook (the proof that a made item needs no code beyond its data).
        /// </summary>
        internal static System.IDisposable ExtendForTest(params Recipe[] extra)
        {
            var previous = _tables;
            var recipes = new List<Recipe>(previous.All);
            recipes.AddRange(extra);
            _tables = new Tables(recipes);
            return new Restore(previous);
        }

        private sealed class Restore : System.IDisposable
        {
            private readonly Tables _previous;
            public Restore(Tables previous) => _previous = previous;
            public void Dispose() => _tables = _previous;
        }

        /// <summary>Every recipe, in the order of what it makes.</summary>
        public static IReadOnlyList<Recipe> All => _tables.All;

        /// <summary>The recipe that makes <paramref name="output"/>, or null when nothing does.</summary>
        public static Recipe For(ItemType output) => _tables.ByOutput[(byte)output];

        public static bool IsMade(ItemType item) => _tables.ByOutput[(byte)item] != null;

        /// <summary>The recipes worked at <paramref name="station"/>, in the order of what they make. Built once; never allocates.</summary>
        public static IReadOnlyList<Recipe> At(StationKind station)
        {
            var byStation = _tables.ByStation;
            return (int)station < byStation.Length ? byStation[(int)station] : System.Array.Empty<Recipe>();
        }
    }
}
