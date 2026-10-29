using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Simpiens.Presentation;

namespace Simpiens.Client
{
    /// <summary>
    /// Every generated sprite packed into one texture (Epic CLIENT M0), so a frame draws in one batch. Sprites sit on a
    /// grid with a clear gutter between cells, and are found by their index in the sheet.
    /// </summary>
    public sealed class SpriteAtlas : IDisposable
    {
        private readonly Rectangle[] _sources;

        /// <param name="smooth">Linear sampling (the fog); pixel art is point-sampled.</param>
        /// <param name="gutter">Clear pixels between cells. A single smooth sprite takes none, so linear sampling clamps at its own edge.</param>
        public SpriteAtlas(GraphicsDevice device, IReadOnlyList<PixelSprite> sprites, bool smooth = false, int gutter = 2)
        {
            Sampling = smooth ? SamplerState.LinearClamp : SamplerState.PointClamp;
            int cell = 0;
            for (int i = 0; i < sprites.Count; i++) cell = Math.Max(cell, Math.Max(sprites[i].Width, sprites[i].Height));
            int pitch = cell + gutter;
            int columns = Math.Max(1, (int)Math.Ceiling(Math.Sqrt(sprites.Count)));
            int rows = (sprites.Count + columns - 1) / columns;
            int width = columns * pitch, height = Math.Max(1, rows) * pitch;

            var pixels = new Color[width * height];
            _sources = new Rectangle[sprites.Count];
            for (int i = 0; i < sprites.Count; i++)
            {
                var sprite = sprites[i];
                int left = i % columns * pitch, top = i / columns * pitch;
                _sources[i] = new Rectangle(left, top, sprite.Width, sprite.Height);
                for (int y = 0; y < sprite.Height; y++)
                {
                    for (int x = 0; x < sprite.Width; x++)
                    {
                        var p = sprite.Pixels[y * sprite.Width + x];
                        pixels[(top + y) * width + left + x] = new Color(p.R, p.G, p.B, p.A);
                    }
                }
            }

            Texture = new Texture2D(device, width, height);
            Texture.SetData(pixels);
        }

        public Texture2D Texture { get; }

        public SamplerState Sampling { get; }

        public Rectangle Source(int spriteIndex) => _sources[spriteIndex];

        public void Dispose() => Texture.Dispose();
    }
}
