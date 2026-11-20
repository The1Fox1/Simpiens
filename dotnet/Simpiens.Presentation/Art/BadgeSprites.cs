using System.Collections.Generic;

namespace Simpiens.Presentation.Art
{
    /// <summary>
    /// A guild member's role badge over its body on the map (Epic 11, S5.8, D24): a shield for the front, an arrow for the back,
    /// a cross for a carrier, a star for a leader, each seven pixels square with a dark rim so it reads on any floor. A
    /// generator: it reads no table and no sheet.
    /// </summary>
    public static class BadgeSprites
    {
        public const int Size = 7;
        public const string Front = "Badge_Front", Back = "Badge_Back", Carrier = "Badge_Carrier", Leader = "Badge_Leader";

        private static readonly List<PixelSprite> _all = new List<PixelSprite>(4);

        public static IReadOnlyList<PixelSprite> All => _all;

        private static readonly Rgba32 Rim = new Rgba32(18, 16, 20, 255);

        static BadgeSprites()
        {
            // '#' the badge's colour, 'o' its rim, '.' clear
            _all.Add(Make(Front, new Rgba32(150, 180, 214, 255), "ooooooo", "o#####o", "o#####o", "o#####o", ".o###o.", "..o#o..", "...o..."));
            _all.Add(Make(Back, new Rgba32(140, 200, 120, 255), "...ooo.", "....o#o", "...o##o", "..o#oo.", ".o#o...", "o#o....", ".o....."));
            _all.Add(Make(Carrier, new Rgba32(232, 96, 84, 255), "..ooo..", "..o#o..", "ooo#ooo", "o#####o", "ooo#ooo", "..o#o..", "..ooo.."));
            _all.Add(Make(Leader, new Rgba32(240, 190, 80, 255), "...o...", "..o#o..", "oo###oo", "o#####o", ".o###o.", "o#o.o#o", "oo...oo"));
        }

        private static PixelSprite Make(string name, Rgba32 colour, params string[] rows)
        {
            var pixels = new Rgba32[Size * Size];
            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                {
                    char c = rows[y][x];
                    pixels[y * Size + x] = c == '#' ? colour : c == 'o' ? Rim : default;
                }
            return new PixelSprite(name, Size, Size, pixels);
        }
    }
}
