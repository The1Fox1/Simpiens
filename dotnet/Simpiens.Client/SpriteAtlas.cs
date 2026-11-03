using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Simpiens.Presentation;
using Simpiens.Presentation.Art;

namespace Simpiens.Client
{
    /// <summary>
    /// A sprite sheet packed into one texture (Epic CLIENT M0; packed by shelves since graphics V3.2, <see cref="AtlasLayout"/>),
    /// so a sheet draws in one batch. Sprites are found by their index in the sheet.
    /// </summary>
    public sealed class SpriteAtlas : IDisposable
    {
        private readonly Rectangle[] _sources;

        /// <param name="smooth">Linear sampling (the fog); pixel art is point-sampled.</param>
        /// <param name="gutter">Clear texels after each sprite. A single smooth sprite takes none, so linear sampling clamps at its own edge.</param>
        /// <param name="order">The packing order; the picture doesn't depend on it (tests pack in reverse to prove it).</param>
        public SpriteAtlas(GraphicsDevice device, IReadOnlyList<PixelSprite> sprites, bool smooth = false, int gutter = 2, AtlasOrder order = AtlasOrder.TallestFirst)
        {
            Sampling = smooth ? SamplerState.LinearClamp : SamplerState.PointClamp;
            var layout = AtlasLayout.Pack(sprites, gutter, order);
            int width = layout.Width, height = layout.Height;
            var pixels = new Color[width * height];
            _sources = new Rectangle[sprites.Count];
            for (int i = 0; i < sprites.Count; i++)
            {
                var sprite = sprites[i];
                var cell = layout[i];
                _sources[i] = new Rectangle(cell.X, cell.Y, cell.Width, cell.Height);
                for (int y = 0; y < sprite.Height; y++)
                {
                    for (int x = 0; x < sprite.Width; x++)
                    {
                        var p = sprite.Pixels[y * sprite.Width + x];
                        pixels[(cell.Y + y) * width + cell.X + x] = new Color(p.R, p.G, p.B, p.A);
                    }
                }
            }

            Texture = new Texture2D(device, width, height);
            Texture.SetData(pixels);
        }

        public Texture2D Texture { get; }

        public SamplerState Sampling { get; }

        public Rectangle Source(int spriteIndex) => _sources[spriteIndex];

        /// <summary>
        /// Writes <paramref name="pixels"/> over sprite <paramref name="spriteIndex"/>'s cell (a body baked into the world
        /// sheet's region, UI-Beta B2), through <paramref name="scratch"/>, at least the cell's size. Only for cells the
        /// sheet keeps blank for this: the art's own sprites are never written.
        /// </summary>
        public void Write(int spriteIndex, ReadOnlySpan<Rgba32> pixels, Color[] scratch)
        {
            var cell = _sources[spriteIndex];
            int count = cell.Width * cell.Height;
            if (pixels.Length < count || scratch.Length < count) throw new ArgumentException($"A cell of {cell.Width}x{cell.Height} needs {count} pixels.");
            for (int i = 0; i < count; i++)
            {
                var p = pixels[i];
                scratch[i] = new Color(p.R, p.G, p.B, p.A);
            }
            Texture.SetData(0, cell, scratch, 0, count);
        }

        public void Dispose() => Texture.Dispose();
    }
}
