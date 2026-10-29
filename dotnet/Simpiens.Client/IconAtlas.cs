using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Simpiens.Presentation;
using Simpiens.Presentation.Art;

namespace Simpiens.Client
{
    /// <summary>
    /// The menus' icons in one texture (graphics V4.1): the icons sheet at 1x and magnified by <see cref="Scale"/> on the CPU
    /// when the client starts, and packed (<see cref="AtlasLayout"/>). The dashboard draws them 1:1 at whole pixels, as it draws its
    /// text from baked fonts (F8), so no texture filter can blur or shift them. Icons are found by art key
    /// (<see cref="IconArtTable"/>); one without art gets the missing art.
    /// </summary>
    public sealed class IconAtlas : IDisposable
    {
        /// <summary>Screen pixels per art pixel in the menus (graphics D2): a 16x16 icon is 32x32.</summary>
        public const int Scale = 2;

        /// <summary>An icon's size on screen in the windows.</summary>
        public const int Size = SpriteKit.IconSize * Scale;

        /// <summary>An icon's size where room is short (the header, the dossier's gear line): its art at 1x.</summary>
        public const int SmallSize = SpriteKit.IconSize;

        private readonly Rectangle[] _sources; // the sheet's sprites at 1x then at Scale
        private readonly ArtCatalog _catalog = IconArtTable.Catalog;

        public IconAtlas(GraphicsDevice device)
        {
            var sheet = SpriteSheets.Icons;
            var magnified = new List<PixelSprite>(sheet.Count);
            foreach (var sprite in sheet) magnified.Add(sprite);
            foreach (var sprite in sheet) magnified.Add(Magnified(sprite));
            var layout = AtlasLayout.Pack(magnified);
            var pixels = new Color[layout.Width * layout.Height];
            _sources = new Rectangle[magnified.Count];
            for (int i = 0; i < magnified.Count; i++)
            {
                var sprite = magnified[i];
                var cell = layout[i];
                _sources[i] = new Rectangle(cell.X, cell.Y, cell.Width, cell.Height);
                for (int y = 0; y < sprite.Height; y++)
                    for (int x = 0; x < sprite.Width; x++)
                    {
                        var p = sprite.Pixels[y * sprite.Width + x];
                        pixels[(cell.Y + y) * layout.Width + cell.X + x] = new Color(p.R, p.G, p.B, p.A);
                    }
            }
            Texture = new Texture2D(device, layout.Width, layout.Height);
            Texture.SetData(pixels);
        }

        public Texture2D Texture { get; }

        /// <summary>
        /// Where the icon <paramref name="iconKey"/> (an <c>item/&lt;art key&gt;/icon</c> key) is in <see cref="Texture"/>, at
        /// <paramref name="scale"/> (1 or <see cref="Scale"/>); the missing art for a key without one.
        /// </summary>
        public Rectangle Source(string iconKey, int scale = Scale)
        {
            var id = iconKey != null && _catalog.TryId(iconKey, out var found) ? found : _catalog.Missing;
            return _sources[_catalog.Sprite(id, 0) + (scale == 1 ? 0 : SpriteSheets.Icons.Count)];
        }

        public void Dispose() => Texture.Dispose();

        private static PixelSprite Magnified(PixelSprite sprite)
        {
            int width = sprite.Width * Scale, height = sprite.Height * Scale;
            var pixels = new Rgba32[width * height];
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    pixels[y * width + x] = sprite.Pixels[y / Scale * sprite.Width + x / Scale];
            return new PixelSprite(sprite.Name, width, height, pixels);
        }
    }
}
