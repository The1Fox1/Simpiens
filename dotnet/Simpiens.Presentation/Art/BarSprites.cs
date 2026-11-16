using System.Collections.Generic;

namespace Simpiens.Presentation.Art
{
    /// <summary>
    /// A boss's health bar (Epic 10 Part 3, K10.5b): one small white square, stretched and tinted where it is drawn (its
    /// background dark, its fill by the health it has left). A generator: it reads no table and no sheet.
    /// </summary>
    public static class BarSprites
    {
        public const int Size = 4;
        public const string Name = "Fx_Bar";

        private static readonly List<PixelSprite> _all = new List<PixelSprite>(1);

        public static IReadOnlyList<PixelSprite> All => _all;

        static BarSprites()
        {
            var pixels = new Rgba32[Size * Size];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = new Rgba32(255, 255, 255, 255);
            _all.Add(new PixelSprite(Name, Size, Size, pixels));
        }
    }
}
