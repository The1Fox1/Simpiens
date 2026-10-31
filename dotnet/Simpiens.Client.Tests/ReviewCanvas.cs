using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Simpiens.Presentation;

namespace Simpiens.Client.Tests
{
    /// <summary>
    /// A review sheet's canvas (the art sheet, the people sheet): an RGBA image on the CPU, sprites blitted at whole
    /// magnifications, and text in the baked Inter at whole pixels, written to render-output/ for a person or an agent to
    /// look at. Not pinned: what is pinned is the sprites themselves.
    /// </summary>
    internal sealed class ReviewCanvas
    {
        private readonly Dictionary<BitmapFontMetrics, (byte[] Rgba, int Width)[]> _pages = new Dictionary<BitmapFontMetrics, (byte[] Rgba, int Width)[]>();

        public ReviewCanvas(int width, int height, Rgba32 background)
        {
            Width = width;
            Height = height;
            Rgba = new byte[width * height * 4];
            for (int i = 0; i < width * height; i++) Put(i, background);
        }

        public int Width { get; }
        public int Height { get; }
        public byte[] Rgba { get; }

        /// <summary><paramref name="pixels"/> at <paramref name="scale"/>× from <paramref name="x"/>, <paramref name="y"/>, over what is there; on a checker when <paramref name="checker"/> is given.</summary>
        public void Blit(Rgba32[] pixels, int width, int height, int x, int y, int scale, (Rgba32 Light, Rgba32 Dark)? checker = null)
        {
            for (int py = 0; py < height * scale; py++)
                for (int px = 0; px < width * scale; px++)
                {
                    int cx = x + px, cy = y + py;
                    if (cx < 0 || cy < 0 || cx >= Width || cy >= Height) continue;
                    int i = cy * Width + cx;
                    if (checker is { } c) Put(i, ((px / 8 + py / 8) & 1) == 0 ? c.Light : c.Dark);
                    Blend(i, pixels[py / scale * width + px / scale], 255);
                }
        }

        public void Blit(PixelSprite sprite, int x, int y, int scale, (Rgba32 Light, Rgba32 Dark)? checker = null)
            => Blit(sprite.Pixels, sprite.Width, sprite.Height, x, y, scale, checker);

        /// <summary>Text at whole pixels from the line's top-left, each glyph's coverage (its page's alpha) in <paramref name="colour"/>.</summary>
        public void Write(BitmapFontMetrics font, string text, int x, int y, Rgba32 colour)
        {
            var pages = Pages(font);
            int pen = x;
            for (int c = 0; c < text.Length; c++)
            {
                var g = font.Glyph(text[c]);
                var (page, pageWidth) = pages[g.Page];
                for (int gy = 0; gy < g.Height; gy++)
                    for (int gx = 0; gx < g.Width; gx++)
                    {
                        int sx = pen + g.XOffset + gx, sy = y + g.YOffset + gy;
                        if (sx < 0 || sx >= Width || sy < 0 || sy >= Height) continue;
                        Blend(sy * Width + sx, colour, page[((g.Y + gy) * pageWidth + g.X + gx) * 4 + 3]);
                    }
                pen += g.XAdvance + (c + 1 < text.Length ? font.Kerning(text[c], text[c + 1]) : 0);
            }
        }

        public void Save(string fileName) => Png.Write(Path.Combine(AppContext.BaseDirectory, "render-output", fileName), Width, Height, Rgba);

        private (byte[] Rgba, int Width)[] Pages(BitmapFontMetrics font)
        {
            if (_pages.TryGetValue(font, out var pages)) return pages;
            string folder = Path.Combine(AppContext.BaseDirectory, FontSet.ContentFolder, Path.GetDirectoryName(font.Face.CacheFileName));
            pages = font.Pages.Select(page => (Png.Decode(File.ReadAllBytes(Path.Combine(folder, page)), out int width, out _), width)).ToArray();
            _pages[font] = pages;
            return pages;
        }

        private void Put(int i, Rgba32 c)
        {
            Rgba[i * 4] = c.R;
            Rgba[i * 4 + 1] = c.G;
            Rgba[i * 4 + 2] = c.B;
            Rgba[i * 4 + 3] = 255;
        }

        /// <summary><paramref name="c"/> over the pixel, at its own alpha times <paramref name="coverage"/>.</summary>
        private void Blend(int i, Rgba32 c, int coverage)
        {
            float a = c.A / 255f * (coverage / 255f);
            if (a <= 0f) return;
            Rgba[i * 4] = (byte)(c.R * a + Rgba[i * 4] * (1 - a) + 0.5f);
            Rgba[i * 4 + 1] = (byte)(c.G * a + Rgba[i * 4 + 1] * (1 - a) + 0.5f);
            Rgba[i * 4 + 2] = (byte)(c.B * a + Rgba[i * 4 + 2] * (1 - a) + 0.5f);
        }
    }
}
