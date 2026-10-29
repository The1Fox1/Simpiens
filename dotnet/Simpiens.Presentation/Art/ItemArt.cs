using System.Collections.Generic;
using System.Linq;

namespace Simpiens.Presentation.Art
{
    /// <summary>Where an item shows when a crawler wears it (graphics V5): nowhere, or over the hair band, as a helmet.</summary>
    public enum Worn : byte
    {
        None = 0,
        Head = 1,
    }

    /// <summary>How an item looks: its shape, its primary and secondary colours, and how it is worn.</summary>
    public readonly struct ItemLook
    {
        public ItemLook(string shape, Rgba32 primary, Rgba32 secondary, Worn worn = Worn.None)
        {
            Shape = shape;
            Primary = primary;
            Secondary = secondary;
            Worn = worn;
        }

        public string Shape { get; }
        public Rgba32 Primary { get; }
        public Rgba32 Secondary { get; }
        public Worn Worn { get; }
    }

    /// <summary>
    /// Every item's look, by its art key (graphics V3.6): one line an item. The sprite kit makes its menu icon, its map
    /// sprite and its carried marker from it (<see cref="ItemSprites"/>). Keyed by art key, so no item is named in code.
    /// The colours follow the nodes they come from (ore's slate and rust, herbs' teal and lilac, glowstone's amber and cyan).
    /// </summary>
    public static class ItemArt
    {
        private static readonly Dictionary<string, ItemLook> _looks = new Dictionary<string, ItemLook>(System.StringComparer.Ordinal)
        {
            ["ration"] = new ItemLook(ItemShapes.Pack, new Rgba32(194, 140, 84), new Rgba32(220, 38, 28)),
            ["iron-ore"] = new ItemLook(ItemShapes.Chunk, new Rgba32(100, 116, 139), new Rgba32(234, 88, 12)),
            ["medicinal-herb"] = new ItemLook(ItemShapes.Sprig, new Rgba32(45, 212, 191), new Rgba32(216, 180, 254)),
            ["pickaxe"] = new ItemLook(ItemShapes.Pick, new Rgba32(148, 163, 184), new Rgba32(120, 72, 32)),
            ["hazard-gear"] = new ItemLook(ItemShapes.Helmet, new Rgba32(250, 204, 21), new Rgba32(254, 240, 138), Worn.Head),
            ["glowstone"] = new ItemLook(ItemShapes.Chunk, new Rgba32(251, 191, 36), new Rgba32(103, 232, 249)),
        };

        public static bool TryGet(string artKey, out ItemLook look) => _looks.TryGetValue(artKey ?? string.Empty, out look);

        /// <summary>Every look, by art key in ordinal order.</summary>
        public static IEnumerable<KeyValuePair<string, ItemLook>> All => _looks.OrderBy(p => p.Key, System.StringComparer.Ordinal);
    }
}
