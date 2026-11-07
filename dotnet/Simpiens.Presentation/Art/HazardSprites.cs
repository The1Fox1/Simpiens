using System;
using System.Collections.Generic;

namespace Simpiens.Presentation.Art
{
    /// <summary>
    /// The hazards' animations (Epic 10 Part 2, the stretch, D69), generated on the CPU as every sprite is: foul air's puffs
    /// rising and thinning, falling rock's pebble and its dust, and the flood's ripples spreading, the last both round
    /// (top-down) and flattened onto isometric's floor. Frames run in order; <see cref="HazardEffects"/> says where and
    /// when they play. A generator: it reads no table and no sheet (<c>ArtInitialisationTests</c>).
    /// </summary>
    public static class HazardSprites
    {
        /// <summary>The puffs' and the ripples' canvas: big enough to read at the whole floor's zoom, drawn 1:1 as all art is.</summary>
        public const int Size = 24;
        /// <summary>The pebble's canvas, a crawler's 16 pixels.</summary>
        public const int RockSize = 16;
        public const int IsoRippleWidth = 32, IsoRippleHeight = 16;
        public const int PuffFrames = 4, RockFrames = 6, RippleFrames = 4;

        private static readonly List<PixelSprite> _all = new List<PixelSprite>(PuffFrames + RockFrames + 2 * RippleFrames);

        public static IReadOnlyList<PixelSprite> All => _all;

        public static string PuffName(int frame) => "Fx_FoulAir_" + frame;
        public static string RockName(int frame) => "Fx_Rockfall_" + frame;
        public static string RippleName(int frame) => "Fx_Ripple_" + frame;
        public static string IsoRippleName(int frame) => "Iso_Fx_Ripple_" + frame;

        public static string[] Names(Func<int, string> name, int frames)
        {
            var names = new string[frames];
            for (int i = 0; i < frames; i++) names[i] = name(i);
            return names;
        }

        static HazardSprites()
        {
            for (int i = 0; i < PuffFrames; i++) _all.Add(Puff(i));
            for (int i = 0; i < RockFrames; i++) _all.Add(Rock(i));
            for (int i = 0; i < RippleFrames; i++) _all.Add(Ripple(RippleName(i), Size, Size, i));
            for (int i = 0; i < RippleFrames; i++) _all.Add(Ripple(IsoRippleName(i), IsoRippleWidth, IsoRippleHeight, i));
        }

        // Foul air: a sickly green puff that rises a little, swells and thins over its four frames
        private static PixelSprite Puff(int frame)
        {
            var pixels = new Rgba32[Size * Size];
            float radius = 4.5f + frame * 1.75f;
            float cx = 11.5f, cy = 16.5f - frame * 2f;
            int alpha = 230 - frame * 45;
            var body = new Rgba32(150, 190, 52, 255);
            var core = new Rgba32(206, 232, 96, 255);
            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                {
                    float d = MathF.Sqrt((x + 0.5f - cx) * (x + 0.5f - cx) + (y + 0.5f - cy) * (y + 0.5f - cy));
                    if (d > radius) continue;
                    // Dithered thinning at the rim, so the edge reads as gas, not a disc
                    if (d > radius - 1.25f && ((x + y + frame) & 1) == 1) continue;
                    var colour = d < radius * 0.45f ? core : body;
                    pixels[y * Size + x] = new Rgba32(colour.R, colour.G, colour.B, (byte)alpha);
                }
            return new PixelSprite(PuffName(frame), Size, Size, pixels);
        }

        // Falling rock: a pebble falling to the floor (frames 0-2), then a cloud of dust spreading and settling (3-5)
        private static PixelSprite Rock(int frame)
        {
            const int n = RockSize;
            var pixels = new Rgba32[n * n];
            var outline = new Rgba32(58, 52, 48, 255);
            var stone = new Rgba32(132, 124, 118, 255);
            var light = new Rgba32(186, 178, 170, 255);
            void Pebble(int left, int top)
            {
                for (int y = top; y < top + 4; y++)
                    for (int x = left; x < left + 4; x++)
                    {
                        bool edge = x == left || x == left + 3 || y == top || y == top + 3;
                        bool corner = (x == left || x == left + 3) && (y == top || y == top + 3);
                        if (corner) continue;
                        pixels[y * n + x] = edge ? outline : (x == left + 1 && y == top + 1 ? light : stone);
                    }
            }
            if (frame < 3)
            {
                Pebble(6, frame * 5);
                return new PixelSprite(RockName(frame), n, n, pixels);
            }
            int spread = frame - 2; // 1, 2, 3
            int alpha = 235 - (spread - 1) * 60;
            var dust = new Rgba32(200, 180, 146, (byte)alpha);
            float rx = 2.5f + spread * 2f, ry = 1.5f + spread * 0.75f, cx = 8f, cy = 13.5f;
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float nx = (x + 0.5f - cx) / rx, ny = (y + 0.5f - cy) / ry;
                    if (nx * nx + ny * ny > 1f || ((x + y + spread) % 4) == 0) continue;
                    pixels[y * n + x] = dust;
                }
            Pebble(6, 12); // come to rest
            return new PixelSprite(RockName(frame), n, n, pixels);
        }

        // The flood: a pale ring spreading outward and fading, on a canvas of the floor's own proportions
        private static PixelSprite Ripple(string name, int width, int height, int frame)
        {
            var pixels = new Rgba32[width * height];
            float t = (frame + 1f) / RippleFrames;
            float rx = width * 0.5f * t - 0.5f, ry = height * 0.5f * t - 0.5f;
            int alpha = 245 - frame * 45;
            var ring = new Rgba32(120, 192, 240, (byte)alpha);
            float cx = width * 0.5f, cy = height * 0.5f;
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    float nx = (x + 0.5f - cx) / MathF.Max(rx, 0.5f), ny = (y + 0.5f - cy) / MathF.Max(ry, 0.5f);
                    float d = MathF.Sqrt(nx * nx + ny * ny);
                    float band = 1f / MathF.Max(MathF.Min(rx, ry), 1f); // about a pixel at this ring's size
                    if (MathF.Abs(d - 1f) <= band) pixels[y * width + x] = ring;
                }
            return new PixelSprite(name, width, height, pixels);
        }
    }
}
