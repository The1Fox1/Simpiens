using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Simpiens.Presentation;

namespace Simpiens.Client
{
    /// <summary>
    /// The baked fonts' pages as textures, for the map's text (Epic CLIENT text cleanup T3). Read through the platform's
    /// content (<see cref="TitleContainer"/>: next to the game, or Android's assets) and decoded as stored: white glyphs,
    /// coverage in alpha, not premultiplied, tinted when drawn.
    /// </summary>
    public sealed class FontAtlases : IDisposable
    {
        private readonly Dictionary<(FontFace Face, int Page), Texture2D> _pages = new Dictionary<(FontFace, int), Texture2D>();

        public FontAtlases(GraphicsDevice device, FontLibrary fonts, Func<string, Stream> open)
        {
            foreach (var font in fonts.All)
            {
                string folder = FontSet.ContentFolder + "/" + Path.GetDirectoryName(font.Face.CacheFileName).Replace('\\', '/');
                for (int page = 0; page < font.Pages.Count; page++)
                {
                    using var stream = open(folder + "/" + font.Pages[page]);
                    using var bytes = new MemoryStream();
                    stream.CopyTo(bytes);
                    byte[] rgba = Png.Decode(bytes.ToArray(), out int width, out int height);
                    var pixels = new Color[width * height];
                    for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color(rgba[i * 4], rgba[i * 4 + 1], rgba[i * 4 + 2], rgba[i * 4 + 3]);
                    var texture = new Texture2D(device, width, height);
                    texture.SetData(pixels);
                    _pages[(font.Face, page)] = texture;
                }
            }
        }

        public Texture2D Page(FontFace face, int page) => _pages[(face, page)];

        public void Dispose()
        {
            foreach (var texture in _pages.Values) texture.Dispose();
            _pages.Clear();
        }
    }
}
