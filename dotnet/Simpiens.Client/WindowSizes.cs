using Microsoft.Xna.Framework;
using static System.FormattableString;

namespace Simpiens.Client
{
    /// <summary>
    /// The window's sizes the header's menu offers (the producer, 2026-11-03), all 16:9: the first default, the game's
    /// default, and one a 1440-row screen holds with room for its panel and title bar, even at a 1.05 scale (2560 by
    /// 1440 can't: that is full screen's). A window dragged to another size keeps it, and none of these is lit then.
    /// </summary>
    public static class WindowSizes
    {
        public static readonly Point[] All = { new Point(1280, 720), new Point(1920, 1080), new Point(2240, 1260) };

        /// <summary>The window a game opens in, unless the player picked another or the command line asks for one.</summary>
        public static Point Default => All[1];

        /// <summary>A size's words: "1920 × 1080".</summary>
        public static string Text(Point size) => Invariant($"{size.X} × {size.Y}");
    }
}
