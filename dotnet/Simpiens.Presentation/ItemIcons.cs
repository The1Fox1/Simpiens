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
    }
}
