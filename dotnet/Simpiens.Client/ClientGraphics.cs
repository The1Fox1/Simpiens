using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Simpiens.Presentation;
using Simpiens.Presentation.Cutscenes;

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

        public ClientGraphics(GraphicsDevice device)
        {
            Device = device;
            _batch = new SpriteBatch(device);
            // By DrawSheet value: top-down's art, the fog's cloud, every sprite with the isometric placeholders, the backdrops
            _sheets = new[]
            {
                new SpriteAtlas(device, ProceduralSprites.All),
                new SpriteAtlas(device, new[] { FogCloud.Sprite }, smooth: true, gutter: 0),
                new SpriteAtlas(device, WorldSprites.All),
                new SpriteAtlas(device, CutsceneArt.Backdrops),
            };
            Renderer = new WorldRenderer(_batch, _sheets);
            // The baked fonts (Simpiens.FontBaker), through the platform's content: the map's text is drawn from them
            Fonts = FontLibrary.Load(TitleContainer.OpenStream);
            FontPages = new FontAtlases(device, Fonts, TitleContainer.OpenStream);
        }

        public GraphicsDevice Device { get; }
        public WorldRenderer Renderer { get; }
        public FontLibrary Fonts { get; }
        public FontAtlases FontPages { get; }

        public void Dispose()
        {
            foreach (var sheet in _sheets) sheet.Dispose();
            FontPages.Dispose();
            _batch.Dispose();
        }
    }
}
