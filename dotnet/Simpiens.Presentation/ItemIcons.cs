using Simpiens.Presentation.Art;
using Simpiens.Simulation.Economy;
using Simpiens.Simulation.Items;

namespace Simpiens.Presentation
{
    /// <summary>
    /// An item's icon key (<c>item/&lt;art key&gt;/icon</c>, graphics V4.2), for the dashboard's models: they name an icon,
    /// and the client draws it. Built once per item and kept, so a model's refresh builds no string.
    /// </summary>
    public static class ItemIcons
    {
        private static readonly string[] _keys = new string[256];
        private static readonly string[] _graded = new string[256 * 3];

        /// <summary>The icon key of <paramref name="item"/>; null for no item, or one the catalog doesn't define.</summary>
        public static string Of(ItemType item)
        {
            if (item == ItemType.None) return null;
            var key = _keys[(byte)item];
            if (key != null) return key;
            var definition = ItemCatalog.Get(item);
            if (definition == null) return null;
            return _keys[(byte)item] = ArtKeys.Item(definition.ArtKey, ItemPicture.Icon);
        }

        /// <summary>The icon key of a piece of <paramref name="item"/> at <paramref name="grade"/> (Epic 10 Part 2, W4): its rim a step rarer a grade.</summary>
        public static string Of(ItemType item, byte grade)
        {
            if (grade == Grades.Standard || !Grades.Graded(item)) return Of(item);
            int at = (byte)item * 3 + (grade < Grades.Masterwork ? grade : Grades.Masterwork);
            var key = _graded[at];
            if (key != null) return key;
            return _graded[at] = ArtKeys.Item(ItemSprites.GradedKey(ItemCatalog.Get(item).ArtKey, grade), ItemPicture.Icon);
        }
    }
}
