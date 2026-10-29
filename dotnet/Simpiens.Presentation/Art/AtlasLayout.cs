using System;
using System.Collections.Generic;

namespace Simpiens.Presentation.Art
{
    /// <summary>The order sprites are packed in: tallest first (the game's), or its reverse (tests: the picture mustn't depend on it).</summary>
    public enum AtlasOrder : byte
    {
        TallestFirst = 0,
        Reversed = 1,
    }

    /// <summary>Where one sprite sits in its atlas texture, in texels.</summary>
    public readonly struct AtlasCell
    {
        public AtlasCell(int x, int y, int width, int height)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }

        public int X { get; }
        public int Y { get; }
        public int Width { get; }
        public int Height { get; }
    }

    /// <summary>
    /// A sheet's sprites packed into one texture (graphics V3.2, review R5's packer): shelves, tallest sprites first, with
    /// a clear gutter after each, in a texture as near square as a width in steps of 64 allows. Pure and deterministic, so
    /// tests check it without a GPU. A layout over <see cref="MaxTextureSize"/> on a side is refused: 2048 is what every
    /// phone of M4 can load.
    /// </summary>
    public sealed class AtlasLayout
    {
        public const int MaxTextureSize = 2048;
        private const int WidthStep = 64;

        private readonly AtlasCell[] _cells;

        private AtlasLayout(int width, int height, AtlasCell[] cells)
        {
            Width = width;
            Height = height;
            _cells = cells;
        }

        public int Width { get; }
        public int Height { get; }
        public int Count => _cells.Length;

        /// <summary>Where sprite <paramref name="index"/> of the sheet sits.</summary>
        public AtlasCell this[int index] => _cells[index];

        /// <summary>
        /// Packs <paramref name="sprites"/> with <paramref name="gutter"/> clear texels after each. A single sprite with no
        /// gutter (the fog's smooth cloud) is its own texture, exactly: linear sampling then clamps at its edge.
        /// </summary>
        public static AtlasLayout Pack(IReadOnlyList<PixelSprite> sprites, int gutter = 2, AtlasOrder order = AtlasOrder.TallestFirst)
        {
            if (sprites == null || sprites.Count == 0) throw new ArgumentException("An atlas needs sprites.", nameof(sprites));
            if (gutter < 0) throw new ArgumentOutOfRangeException(nameof(gutter));
            if (sprites.Count == 1 && gutter == 0) return Checked(new AtlasLayout(sprites[0].Width, sprites[0].Height, new[] { new AtlasCell(0, 0, sprites[0].Width, sprites[0].Height) }));

            var sequence = new int[sprites.Count];
            long area = 0;
            int widest = 0;
            for (int i = 0; i < sequence.Length; i++)
            {
                sequence[i] = i;
                area += (long)(sprites[i].Width + gutter) * (sprites[i].Height + gutter);
                widest = Math.Max(widest, sprites[i].Width + gutter);
            }
            // Tallest first, then by index: stable, so the same sheet always packs the same way
            Array.Sort(sequence, (a, b) => sprites[a].Height != sprites[b].Height ? sprites[b].Height.CompareTo(sprites[a].Height) : a.CompareTo(b));
            if (order == AtlasOrder.Reversed) Array.Reverse(sequence);

            int width = Math.Max(widest, (int)Math.Ceiling(Math.Sqrt(area)));
            width = (width + WidthStep - 1) / WidthStep * WidthStep;
            var cells = new AtlasCell[sprites.Count];
            int x = 0, y = 0, shelf = 0;
            foreach (int i in sequence)
            {
                var sprite = sprites[i];
                if (x > 0 && x + sprite.Width + gutter > width)
                {
                    y += shelf;
                    x = 0;
                    shelf = 0;
                }
                cells[i] = new AtlasCell(x, y, sprite.Width, sprite.Height);
                x += sprite.Width + gutter;
                shelf = Math.Max(shelf, sprite.Height + gutter);
            }
            return Checked(new AtlasLayout(width, y + shelf, cells));
        }

        private static AtlasLayout Checked(AtlasLayout layout)
        {
            if (layout.Width > MaxTextureSize || layout.Height > MaxTextureSize)
                throw new InvalidOperationException($"An atlas of {layout.Width}x{layout.Height} is over the {MaxTextureSize} texel budget.");
            return layout;
        }
    }
}
