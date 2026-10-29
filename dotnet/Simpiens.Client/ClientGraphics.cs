using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Simpiens.Presentation;
using Simpiens.Presentation.Art;

namespace Simpiens.Client
{
    /// <summary>
    /// What every screen draws with (Epic CLIENT): one sprite batch, the sprite sheets by <see cref="DrawSheet"/> value,
    /// the renderer, and the baked fonts' metrics and pages. The shell makes it once the device exists; the world view
    /// and the cutscene screen share it.
    /// </summary>
    public sealed class ClientGraphics : IDisposable
    {
        private readonly SpriteBatch _batch;
        private readonly SpriteAtlas[] _sheets;

        /// <param name="order">How the sheets are packed; the picture doesn't depend on it (graphics V3.2: tests pack in reverse to prove it).</param>
        public ClientGraphics(GraphicsDevice device, AtlasOrder order = AtlasOrder.TallestFirst)
        {
            Device = device;
            _batch = new SpriteBatch(device);
            // By DrawSheet value, from the sheets' table; a smooth sheet (the fog's cloud) takes no gutter, so linear sampling
            // clamps at its own edge
            _sheets = new SpriteAtlas[SpriteSheets.All.Count];
            foreach (var sheet in SpriteSheets.All)
            {
                bool smooth = SpriteSheets.Smooth(sheet);
                _sheets[(int)sheet] = new SpriteAtlas(device, SpriteSheets.Contents(sheet), smooth, gutter: smooth ? 0 : 2, order);
            }
            Renderer = new WorldRenderer(_batch, _sheets);
            Icons = new IconAtlas(device);
            // The baked fonts (Simpiens.FontBaker), through the platform's content: the map's text is drawn from them
            Fonts = FontLibrary.Load(TitleContainer.OpenStream);
            FontPages = new FontAtlases(device, Fonts, TitleContainer.OpenStream);
        }

        public GraphicsDevice Device { get; }
        public WorldRenderer Renderer { get; }
        /// <summary>The menus' item icons (graphics V4), magnified and drawn 1:1.</summary>
        public IconAtlas Icons { get; }
        public FontLibrary Fonts { get; }
        public FontAtlases FontPages { get; }

        public void Dispose()
        {
            foreach (var sheet in _sheets) sheet.Dispose();
            Icons.Dispose();
            FontPages.Dispose();
            _batch.Dispose();
        }
    }
}
