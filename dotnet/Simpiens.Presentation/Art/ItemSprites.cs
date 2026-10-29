using System.Collections.Generic;
using Simpiens.Simulation.Items;

namespace Simpiens.Presentation.Art
{
    /// <summary>
    /// Every catalog item's pictures (graphics V3.6), made once by the sprite kit from its look (<see cref="ItemArt"/>):
    /// its menu icon (16x16, its rarity's rim; the icons sheet), its map sprite (the outlined glyph standing at the
    /// bottom of a 16x16 canvas) and its carried marker (8x8; both in the world sheet). An item without a look has none,
    /// and draws the missing art (a test fails on it).
    /// </summary>
    public static class ItemSprites
    {
        public const int MapSize = 16, CarriedSize = 8;

        private static readonly List<PixelSprite> _icons = new List<PixelSprite>(8);
        private static readonly List<PixelSprite> _world = new List<PixelSprite>(16);
        private static readonly List<string> _keys = new List<string>(8);

        static ItemSprites()
        {
            var catalog = ItemCatalog.All;
            for (int i = 0; i < catalog.Count; i++)
            {
                var item = catalog[i];
                if (!ItemArt.TryGet(item.ArtKey, out var look)) continue;
                var primary = SpriteKit.RampOf(look.Primary);
                var secondary = SpriteKit.RampOf(look.Secondary);

                var glyph = SpriteKit.Paint(ItemShapes.All[look.Shape], primary, secondary, out int w, out int h);
                var outlined = SpriteKit.Outlined(glyph, w, h, SpriteKit.Outline, out int ow, out int oh);
                _icons.Add(new PixelSprite(IconName(item.ArtKey), SpriteKit.IconSize, SpriteKit.IconSize, SpriteKit.Icon(outlined, ow, oh, item.Rarity)));
                // Standing on the bottom of its canvas, centred: its lowest opaque row is the canvas's bottom edge
                _world.Add(new PixelSprite(MapName(item.ArtKey), MapSize, MapSize, SpriteKit.Place(outlined, ow, oh, MapSize, MapSize, (MapSize - ow) / 2, MapSize - 1 - LowestOpaqueRow(outlined, ow, oh))));

                var bundle = SpriteKit.Paint(ItemShapes.Bundle, primary, secondary, out int bw, out int bh);
                _world.Add(new PixelSprite(CarriedName(item.ArtKey), CarriedSize, CarriedSize, SpriteKit.Outlined(bundle, bw, bh, SpriteKit.Outline, out _, out _)));
                _keys.Add(item.ArtKey);
            }
        }

        /// <summary>The icons, in the catalog's order.</summary>
        public static IReadOnlyList<PixelSprite> Icons => _icons;

        /// <summary>The map sprites and carried markers, in the catalog's order.</summary>
        public static IReadOnlyList<PixelSprite> World => _world;

        /// <summary>The art keys that have pictures, in the catalog's order.</summary>
        public static IReadOnlyList<string> Keys => _keys;

        private static int LowestOpaqueRow(Rgba32[] pixels, int width, int height)
        {
            for (int y = height - 1; y >= 0; y--)
                for (int x = 0; x < width; x++)
                    if (pixels[y * width + x].A != 0) return y;
            return height - 1;
        }

        public static string IconName(string artKey) => "Item_" + artKey + "_Icon";
        public static string MapName(string artKey) => "Item_" + artKey + "_Map";
        public static string CarriedName(string artKey) => "Item_" + artKey + "_Carried";
    }
}
