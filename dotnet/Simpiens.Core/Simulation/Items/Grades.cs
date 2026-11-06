namespace Simpiens.Simulation.Items
{
    /// <summary>
    /// The grades a made piece comes in (Epic 10 Part 2, W4, Q8, D55): Standard, Fine and Masterwork, as
    /// <see cref="Simpiens.Simulation.Economy.ItemInstance.Quality"/> names them; shown with the rarity rims Common,
    /// rims a step above their item's own each. An artisan's grade rises with its practice; what a grade does to a piece
    /// is its equipment's.
    /// </summary>
    public static class Grades
    {
        public const byte Standard = 0;
        public const byte Fine = 1;
        public const byte Masterwork = 2;

        /// <summary>A grade's name: "Standard", "Fine", "Masterwork".</summary>
        public static string NameOf(byte grade) => grade switch
        {
            Fine => "Fine",
            >= Masterwork => "Masterwork",
            _ => "Standard"
        };

        /// <summary>
        /// The rarity rim a piece of <paramref name="grade"/> is shown with (D55): a step above its item's own rarity for each
        /// grade, to Legendary (a Fine pickaxe, Uncommon, shows Rare's blue; a Masterwork respirator, Rare, Legendary's amber).
        /// </summary>
        public static ItemRarity RimOf(ItemRarity itemRarity, byte grade)
        {
            int rim = (int)itemRarity + grade;
            return (ItemRarity)(rim < (int)ItemRarity.Legendary ? rim : (int)ItemRarity.Legendary);
        }

        /// <summary>Whether pieces of <paramref name="item"/> come in grades: something worn that a station makes.</summary>
        public static bool Graded(Simpiens.Simulation.Economy.ItemType item) => ItemCatalog.Get(item)?.Equipment != null && RecipeBook.For(item) != null;
    }
}
