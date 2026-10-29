using System;
using Simpiens.Simulation.Items;

namespace Simpiens.Presentation.Art
{
    /// <summary>Four tones of one colour, light to dark: what a silhouette's tone letters are painted with.</summary>
    public readonly struct Ramp
    {
        public Ramp(Rgba32 highlight, Rgba32 baseTone, Rgba32 shade, Rgba32 deep)
        {
            Highlight = highlight;
            Base = baseTone;
            Shade = shade;
            Deep = deep;
        }

        public Rgba32 Highlight { get; }
        public Rgba32 Base { get; }
        public Rgba32 Shade { get; }
        public Rgba32 Deep { get; }

        /// <summary>Tone 1 (highlight) to 4 (deep).</summary>
        public Rgba32 this[int tone] => tone switch { 1 => Highlight, 2 => Base, 3 => Shade, 4 => Deep, _ => throw new ArgumentOutOfRangeException(nameof(tone)) };
    }

    /// <summary>
    /// The sprite kit (graphics V3.5): deterministic operations that turn a hand-drawn silhouette and an item's colours
    /// into finished pixel art: a four-tone ramp from one colour, painting, an outline, and the icon's plate with its
    /// rarity rim. Integer arithmetic throughout, so the same inputs make the same bytes on any machine (pinned by name in
    /// the art pins).
    /// </summary>
    public static class SpriteKit
    {
        /// <summary>The outline every sprite shares (the crawlers' since Unity).</summary>
        public static readonly Rgba32 Outline = new Rgba32(20, 24, 33);

        /// <summary>An icon's plate: the dashboard's dark slate.</summary>
        public static readonly Rgba32 Plate = new Rgba32(30, 41, 59);

        public const int IconSize = 16;

        /// <summary>
        /// A ramp from <paramref name="baseColour"/>: lighter tones less saturated and warmer (hue toward yellow), darker
        /// ones more saturated and cooler (toward blue), the usual pixel-art ramp. The base tone is the colour itself.
        /// </summary>
        public static Ramp RampOf(Rgba32 baseColour)
        {
            ToHsv(baseColour, out int h, out int s, out int v);
            var highlight = FromHsv(Toward(h, 60, 10, s), s * 80 / 100, v + (255 - v) * 40 / 100);
            var shade = FromHsv(Toward(h, 240, 10, s), Math.Min(255, s * 110 / 100), v * 72 / 100);
            var deep = FromHsv(Toward(h, 240, 18, s), Math.Min(255, s * 115 / 100), v * 50 / 100);
            return new Ramp(highlight, baseColour, shade, deep);
        }

        /// <summary>
        /// Paints <paramref name="shape"/>, rows from the top: <c>1</c>–<c>4</c> take <paramref name="primary"/>'s tones,
        /// <c>a</c>–<c>d</c> <paramref name="secondary"/>'s, <c>.</c> is clear. Any other letter, or a ragged row, is refused.
        /// </summary>
        public static Rgba32[] Paint(string[] shape, in Ramp primary, in Ramp secondary, out int width, out int height)
        {
            if (shape == null || shape.Length == 0) throw new ArgumentException("A shape has rows.", nameof(shape));
            height = shape.Length;
            width = shape[0].Length;
            var pixels = new Rgba32[width * height];
            for (int y = 0; y < height; y++)
            {
                if (shape[y].Length != width) throw new ArgumentException($"Row {y} is {shape[y].Length} wide, not {width}.", nameof(shape));
                for (int x = 0; x < width; x++)
                {
                    char c = shape[y][x];
                    pixels[y * width + x] = c switch
                    {
                        '.' => Rgba32.Clear,
                        >= '1' and <= '4' => primary[c - '0'],
                        >= 'a' and <= 'd' => secondary[c - 'a' + 1],
                        _ => throw new ArgumentException($"'{c}' at {x}, {y} is no tone (1-4, a-d, or '.').", nameof(shape)),
                    };
                }
            }
            return pixels;
        }

        /// <summary>
        /// The pixels on a canvas one larger on every side, with a 1-pixel outline in <paramref name="colour"/> on every
        /// clear pixel that touches an opaque one along a side (not a corner).
        /// </summary>
        public static Rgba32[] Outlined(Rgba32[] pixels, int width, int height, Rgba32 colour, out int outWidth, out int outHeight)
        {
            outWidth = width + 2;
            outHeight = height + 2;
            var result = Place(pixels, width, height, outWidth, outHeight, 1, 1);
            var outlined = (Rgba32[])result.Clone();
            for (int y = 0; y < outHeight; y++)
                for (int x = 0; x < outWidth; x++)
                {
                    if (result[y * outWidth + x].A != 0) continue;
                    if (Opaque(result, outWidth, outHeight, x - 1, y) || Opaque(result, outWidth, outHeight, x + 1, y)
                        || Opaque(result, outWidth, outHeight, x, y - 1) || Opaque(result, outWidth, outHeight, x, y + 1))
                        outlined[y * outWidth + x] = colour;
                }
            return outlined;
        }

        /// <summary>
        /// <paramref name="pixels"/> on a clear canvas of <paramref name="canvasWidth"/> by <paramref name="canvasHeight"/>,
        /// their top-left at <paramref name="left"/>, <paramref name="top"/>. Clear pixels may fall off the canvas; an
        /// opaque one can't.
        /// </summary>
        public static Rgba32[] Place(Rgba32[] pixels, int width, int height, int canvasWidth, int canvasHeight, int left, int top)
        {
            var canvas = new Rgba32[canvasWidth * canvasHeight];
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    int cx = left + x, cy = top + y;
                    var p = pixels[y * width + x];
                    if (cx < 0 || cy < 0 || cx >= canvasWidth || cy >= canvasHeight)
                    {
                        if (p.A != 0) throw new ArgumentException("An opaque pixel leaves the canvas.");
                        continue;
                    }
                    canvas[cy * canvasWidth + cx] = p;
                }
            return canvas;
        }

        /// <summary>
        /// A menu icon: a 16x16 plate with a 1-pixel rim in <paramref name="rarity"/>'s colour (its corners clear), and
        /// <paramref name="glyph"/> (at most 14x14, outlined) centred on it.
        /// </summary>
        public static Rgba32[] Icon(Rgba32[] glyph, int width, int height, ItemRarity rarity)
        {
            if (width > IconSize - 2 || height > IconSize - 2) throw new ArgumentException($"A glyph of {width}x{height} doesn't fit inside an icon's rim.");
            var rim = RarityColours.Of(rarity);
            var icon = new Rgba32[IconSize * IconSize];
            for (int y = 0; y < IconSize; y++)
                for (int x = 0; x < IconSize; x++)
                {
                    bool edgeX = x == 0 || x == IconSize - 1, edgeY = y == 0 || y == IconSize - 1;
                    icon[y * IconSize + x] = edgeX && edgeY ? Rgba32.Clear : edgeX || edgeY ? rim : Plate;
                }
            int left = (IconSize - width) / 2, top = (IconSize - height) / 2;
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    var p = glyph[y * width + x];
                    if (p.A != 0) icon[(top + y) * IconSize + left + x] = p;
                }
            return icon;
        }

        private static bool Opaque(Rgba32[] pixels, int width, int height, int x, int y)
            => x >= 0 && y >= 0 && x < width && y < height && pixels[y * width + x].A != 0;

        /// <summary><paramref name="hue"/> moved toward <paramref name="target"/> by at most <paramref name="degrees"/>, the short way round; a grey keeps its hue.</summary>
        private static int Toward(int hue, int target, int degrees, int saturation)
        {
            if (saturation == 0) return hue;
            int difference = (target - hue + 540) % 360 - 180;
            int step = Math.Clamp(difference, -degrees, degrees);
            return (hue + step + 360) % 360;
        }

        /// <summary>Hue in whole degrees (0–359), saturation and value 0–255.</summary>
        public static void ToHsv(Rgba32 c, out int hue, out int saturation, out int value)
        {
            int max = Math.Max(c.R, Math.Max(c.G, c.B)), min = Math.Min(c.R, Math.Min(c.G, c.B)), delta = max - min;
            value = max;
            saturation = max == 0 ? 0 : (delta * 255 + max / 2) / max;
            if (delta == 0) { hue = 0; return; }
            int sixty;
            if (max == c.R) sixty = 60 * (c.G - c.B);
            else if (max == c.G) sixty = 60 * (c.B - c.R) + 120 * delta;
            else sixty = 60 * (c.R - c.G) + 240 * delta;
            int degrees = sixty / delta;
            if (sixty % delta != 0 && sixty < 0) degrees--; // floor, not truncation
            hue = (degrees % 360 + 360) % 360;
        }

        /// <summary>The colour of a hue in whole degrees, saturation and value 0–255.</summary>
        public static Rgba32 FromHsv(int hue, int saturation, int value)
        {
            value = Math.Clamp(value, 0, 255);
            saturation = Math.Clamp(saturation, 0, 255);
            if (saturation == 0) return new Rgba32((byte)value, (byte)value, (byte)value);
            int region = hue / 60 % 6, remainder = hue % 60 * 255 / 60;
            int p = value * (255 - saturation) / 255;
            int q = value * (255 - saturation * remainder / 255) / 255;
            int t = value * (255 - saturation * (255 - remainder) / 255) / 255;
            return region switch
            {
                0 => new Rgba32((byte)value, (byte)t, (byte)p),
                1 => new Rgba32((byte)q, (byte)value, (byte)p),
                2 => new Rgba32((byte)p, (byte)value, (byte)t),
                3 => new Rgba32((byte)p, (byte)q, (byte)value),
                4 => new Rgba32((byte)t, (byte)p, (byte)value),
                _ => new Rgba32((byte)value, (byte)p, (byte)q),
            };
        }
    }

    /// <summary>The colour each rarity shows in (graphics D3): icons' rims. The dashboard's palette: slate, green, blue, purple, amber.</summary>
    public static class RarityColours
    {
        public static Rgba32 Of(ItemRarity rarity) => rarity switch
        {
            ItemRarity.Uncommon => new Rgba32(34, 197, 94),
            ItemRarity.Rare => new Rgba32(59, 130, 246),
            ItemRarity.Epic => new Rgba32(168, 85, 247),
            ItemRarity.Legendary => new Rgba32(245, 158, 11),
            _ => new Rgba32(148, 163, 184),
        };
    }
}
