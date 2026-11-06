namespace Simpiens.Simulation.Items
{
    /// <summary>
    /// The grades a made piece comes in (Epic 10 Part 2, W4, Q8, D55): Standard, Fine and Masterwork, as
    /// <see cref="Simpiens.Simulation.Economy.ItemInstance.Quality"/> names them; shown with the rarity rims Common,
    /// Uncommon and Rare. An artisan's grade rises with its practice; what a grade does to a piece is its equipment's.
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

        /// <summary>The rarity rim a grade is shown with (D55): Common, Uncommon, Rare.</summary>
        public static ItemRarity RimOf(byte grade) => grade switch
        {
            Fine => ItemRarity.Uncommon,
            >= Masterwork => ItemRarity.Rare,
            _ => ItemRarity.Common
        };
    }
}
