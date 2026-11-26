using System.Collections.Generic;
using Simpiens.Simulation.Life;

namespace Simpiens.Presentation.Art
{
    /// <summary>
    /// What a crawler is doing with its free time, over its head on the map (Epic LIFE-1 plan 3, V.4, D38, from plan 2's T.7): a
    /// flame by the fire, a leaf on a walk, a purse at the stall, crossed blades at sparring, a mug of ale, a bowl of stew, a die;
    /// each seven pixels square with a dark rim, as the role badges are. One for every <see cref="Activity"/> (a test checks
    /// it). A generator: it reads no table and no sheet.
    /// </summary>
    public static class ActivitySprites
    {
        public const int Size = 7;

        private static readonly List<PixelSprite> _all = new List<PixelSprite>(8);
        private static readonly Dictionary<Activity, string> _names = new Dictionary<Activity, string>();

        public static IReadOnlyList<PixelSprite> All => _all;

        private static readonly Rgba32 Rim = new Rgba32(18, 16, 20, 255);

        /// <summary>The sprite's name for <paramref name="activity"/>: "Activity_Ale"; null for none.</summary>
        public static string Name(Activity activity) => _names.TryGetValue(activity, out var name) ? name : null;

        static ActivitySprites()
        {
            // '#' the icon's colour, '+' its second colour, 'o' its rim, '.' clear
            Add(Activity.Fireside, new Rgba32(240, 140, 50, 255), new Rgba32(250, 214, 90, 255), "...o...", "..o#o..", ".o#+#o.", ".o#+#o.", "o#+++#o", "o#+++#o", ".ooooo.");
            Add(Activity.Walk, new Rgba32(110, 180, 90, 255), new Rgba32(70, 120, 60, 255), "....oo.", "..oo##o", ".o###+o", "o##+##o", "o#+##o.", ".o+oo..", "o......");
            Add(Activity.Browse, new Rgba32(200, 150, 80, 255), new Rgba32(240, 200, 90, 255), "..ooo..", ".o#o#o.", "..o#o..", ".o###o.", "o##+##o", "o#####o", ".ooooo.");
            Add(Activity.Spar, new Rgba32(200, 205, 215, 255), new Rgba32(150, 110, 70, 255), "oo...oo", "o#o.o#o", ".o#o#o.", "..o#o..", ".o#o#o.", "o+o.o+o", "oo...oo");
            Add(Activity.Ale, new Rgba32(230, 170, 60, 255), new Rgba32(245, 240, 225, 255), "ooooo..", "o+++o..", "o###ooo", "o###o#o", "o###ooo", "o###o..", "ooooo..");
            Add(Activity.Supper, new Rgba32(160, 110, 70, 255), new Rgba32(200, 120, 70, 255), ".o.o...", "..o.o..", ".......", "ooooooo", "o#+++#o", ".o###o.", "..ooo..");
            Add(Activity.Dice, new Rgba32(240, 236, 226, 255), new Rgba32(30, 26, 30, 255), "ooooooo", "o+###+o", "o#####o", "o##+##o", "o#####o", "o+###+o", "ooooooo");
        }

        private static void Add(Activity activity, Rgba32 colour, Rgba32 second, params string[] rows)
        {
            string name = "Activity_" + activity;
            var pixels = new Rgba32[Size * Size];
            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                {
                    char c = rows[y][x];
                    pixels[y * Size + x] = c == '#' ? colour : c == '+' ? second : c == 'o' ? Rim : default;
                }
            _all.Add(new PixelSprite(name, Size, Size, pixels));
            _names[activity] = name;
        }
    }
}
