using Simpiens.Simulation.Economy;

namespace Simpiens.Simulation.Items
{
    /// <summary>The outpost's stations (Epic 10 Part 2, Q2): where a recipe is worked. Saved; never reuse a value.</summary>
    public enum StationKind : byte
    {
        None = 0,
        /// <summary>Ore and glowstone into tools and gear.</summary>
        Forge = 1,
        /// <summary>The Alchemy Still: herbs into draughts.</summary>
        Still = 2,
        /// <summary>The guildhall's drill yard (Epic 11, S3.4, Q10, D10): no recipes and no artisan; guild members drill there.</summary>
        DrillYard = 3,
        /// <summary>The tavern (Epic LIFE-1 plan 2, T.3, D18): kept by a keeper on its shifts; no recipes; a venue for the evening.</summary>
        Tavern = 4
    }

    /// <summary>One of a recipe's inputs: an item and how many of it one make takes.</summary>
    public readonly struct RecipeInput
    {
        public readonly ItemType Item;
        public readonly byte Count;

        public RecipeInput(ItemType item, byte count)
        {
            Item = item;
            Count = count;
        }
    }

    /// <summary>
    /// How an item is made (Epic 10 Part 2, W0.3): at which station, from what, and after how many ticks of an artisan's
    /// work. Keyed by what it makes (Part 2 D1): an item has at most one recipe. Immutable, the same in every session, as
    /// an item's definition is; what a made item sells for is the session's economy.
    /// </summary>
    public sealed class Recipe
    {
        /// <summary>The most inputs a recipe takes.</summary>
        public const int MaxInputs = 3;

        private readonly RecipeInput[] _inputs;

        public Recipe(ItemType output, StationKind station, ushort workTicks, params RecipeInput[] inputs) : this(output, 1, station, workTicks, inputs) { }

        public Recipe(ItemType output, byte outputCount, StationKind station, ushort workTicks, params RecipeInput[] inputs)
        {
            Output = output;
            OutputCount = outputCount;
            Station = station;
            WorkTicks = workTicks;
            _inputs = inputs != null ? (RecipeInput[])inputs.Clone() : System.Array.Empty<RecipeInput>();
        }

        /// <summary>What it makes.</summary>
        public ItemType Output { get; }

        /// <summary>How many of it one make gives.</summary>
        public byte OutputCount { get; }

        public StationKind Station { get; }

        /// <summary>Ticks of an artisan's work one make takes.</summary>
        public ushort WorkTicks { get; }

        /// <summary>The level a make demands (Epic 11, S1.3, Q8, D5): an artisan above it learns less from each. An Apprentice's 3 unless the row says.</summary>
        public byte Challenge { get; init; } = 3;

        public int InputCount => _inputs.Length;
        public RecipeInput GetInput(int index) => _inputs[index];

        /// <summary>How many of <paramref name="item"/> one make takes; 0 when it isn't an input.</summary>
        public int CountOf(ItemType item)
        {
            for (int i = 0; i < _inputs.Length; i++)
            {
                if (_inputs[i].Item == item) return _inputs[i].Count;
            }
            return 0;
        }

        public override string ToString() => ItemCatalog.NameOf(Output);
    }
}
