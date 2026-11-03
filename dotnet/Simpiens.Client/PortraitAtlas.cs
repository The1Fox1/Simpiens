using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Simpiens.Presentation;
using Simpiens.Presentation.Art.People;

namespace Simpiens.Client
{
    /// <summary>
    /// Crawlers' portraits in one texture (Epic UI-Beta B3, D7): <see cref="CrawlerPortraits.Slots"/> slots, each a framed
    /// portrait at 1x and magnified on the CPU to <see cref="Scale"/>x, drawn 1:1 at whole pixels as the icons are, so no
    /// texture filter touches them. A portrait is baked and written the first time it is shown (<see cref="Show"/>), and
    /// again when its mood, gear or standing changes it; the least recently shown slot is reused.
    /// </summary>
    public sealed class PortraitAtlas : IDisposable
    {
        /// <summary>Screen pixels per art pixel in the dossier (and B4's inspector): a 52-pixel framed portrait is 104.</summary>
        public const int Scale = 2;

        /// <summary>A framed portrait at 1x (B4's cards, the scene speakers), and at <see cref="Scale"/>x.</summary>
        public const int SmallSize = PortraitKit.FramedSize, Size = SmallSize * Scale;

        private const int Gutter = 2, Width = 1024;

        private readonly Rectangle[] _small = new Rectangle[CrawlerPortraits.Slots], _large = new Rectangle[CrawlerPortraits.Slots];
        private readonly Rgba32[] _framed = new Rgba32[SmallSize * SmallSize], _magnified = new Rgba32[Size * Size];
        private readonly Color[] _scratch = new Color[Size * Size];

        public PortraitAtlas(GraphicsDevice device)
        {
            // The 2x cells in rows, then the 1x cells
            int perLarge = Width / (Size + Gutter), perSmall = Width / (SmallSize + Gutter);
            int largeRows = (CrawlerPortraits.Slots + perLarge - 1) / perLarge, smallRows = (CrawlerPortraits.Slots + perSmall - 1) / perSmall;
            for (int i = 0; i < CrawlerPortraits.Slots; i++)
            {
                _large[i] = new Rectangle(i % perLarge * (Size + Gutter), i / perLarge * (Size + Gutter), Size, Size);
                _small[i] = new Rectangle(i % perSmall * (SmallSize + Gutter), largeRows * (Size + Gutter) + i / perSmall * (SmallSize + Gutter), SmallSize, SmallSize);
            }
            Texture = new Texture2D(device, Width, largeRows * (Size + Gutter) + smallRows * (SmallSize + Gutter));
        }

        public Texture2D Texture { get; }

        /// <summary>Which portrait is in which slot.</summary>
        public CrawlerPortraits Table { get; } = new CrawlerPortraits();

        /// <summary>Where <paramref name="key"/>'s portrait is in <see cref="Texture"/> at <paramref name="scale"/> (1 or <see cref="Scale"/>), baked now if it wasn't.</summary>
        public Rectangle Show(in PortraitKey key, int scale)
        {
            int slot = Table.Show(key);
            while (Table.TryTakePending(out int pending))
            {
                Table.BakeSlot(pending, _framed);
                Write(_small[pending], _framed);
                PortraitKit.Magnify(_framed, SmallSize, Scale, _magnified);
                Write(_large[pending], _magnified);
            }
            return scale == 1 ? _small[slot] : _large[slot];
        }

        private void Write(Rectangle cell, ReadOnlySpan<Rgba32> pixels)
        {
            int count = cell.Width * cell.Height;
            for (int i = 0; i < count; i++)
            {
                var p = pixels[i];
                _scratch[i] = new Color(p.R, p.G, p.B, p.A);
            }
            Texture.SetData(0, cell, _scratch, 0, count);
        }

        public void Dispose() => Texture.Dispose();
    }
}
