using System.Collections.Generic;

namespace Simpiens.Presentation.Art
{
    /// <summary>
    /// The interface's pieces in Lantern &amp; Stone (the UI polish plan, U2): the outpost's stone and the portraits'
    /// metals, lit from the top left as Hearthlight is, generated on the CPU so they pin exactly. Two kinds:
    /// <list type="bullet">
    /// <item><b>Frames</b>, drawn as nine-slices whose corners are <see cref="Corner"/> (or <see cref="FrameCorner"/>)
    /// pixels: a recessed plaque, raised buttons in their states, the slider's groove, and a window's frame in each of the
    /// standing metals. Each edge is uniform along its length and each middle one colour, so a slice stretches cleanly
    /// under any texture filter.</item>
    /// <item><b>Glyphs</b>, white on clear, tinted where they are drawn: coin, notice, shield, stall, pause, play, menu,
    /// sun, moon, close, lantern.</item>
    /// </list>
    /// They live in the icon sheet (<see cref="SpriteSheets.Icons"/>), after the items' icons. The colours are the
    /// client's palette (<c>UiStyle</c>) as art; a generator reads no table or sheet.
    /// </summary>
    public static class UiKit
    {
        /// <summary>The corner of a plaque's or a button's nine-slice, in pixels.</summary>
        public const int Corner = 4;

        /// <summary>The corner of the slider's groove.</summary>
        public const int GrooveCorner = 3;

        /// <summary>The corner of a window's frame: the iron band and its rivets.</summary>
        public const int FrameCorner = 6;

        public const string Plaque = "Ui_Plaque";
        public const string Button = "Ui_Button";
        public const string ButtonHover = "Ui_ButtonHover";
        public const string ButtonPressed = "Ui_ButtonPressed";
        public const string ButtonDisabled = "Ui_ButtonDisabled";
        public const string ButtonActive = "Ui_ButtonActive";
        public const string Primary = "Ui_Primary";
        public const string PrimaryHover = "Ui_PrimaryHover";
        public const string PrimaryPressed = "Ui_PrimaryPressed";
        public const string Groove = "Ui_Groove";
        public const string FrameIron = "Ui_FrameIron";
        public const string FrameBronze = "Ui_FrameBronze";
        public const string FrameSteel = "Ui_FrameSteel";

        public const string Coin = "Ui_Coin";
        public const string Notice = "Ui_Notice";
        public const string Shield = "Ui_Shield";
        public const string Stall = "Ui_Stall";
        public const string Pause = "Ui_Pause";
        public const string Play = "Ui_Play";
        public const string Menu = "Ui_Menu";
        public const string Sun = "Ui_Sun";
        public const string Moon = "Ui_Moon";
        public const string Close = "Ui_Close";
        public const string Lantern = "Ui_Lantern";

        // The palette as art (UiStyle's values): warm grey stone, never brown
        private static readonly Rgba32 StoneDark = Rgb(0x1c1815);
        private static readonly Rgba32 StoneLit = Rgb(0x4c4138);
        private static readonly Rgba32 Shadow = Rgb(0x0d0b09);
        private static readonly Rgba32 Inset = Rgb(0x14110f);
        private static readonly Rgba32 Face = Rgb(0x3a312a);
        private static readonly Rgba32 FaceHover = Rgb(0x463b32);
        private static readonly Rgba32 FacePressed = Rgb(0x2f2823);
        private static readonly Rgba32 FaceLit = Rgb(0x5c4e42);
        private static readonly Rgba32 FaceDisabled = Rgb(0x2a2420);
        private static readonly Rgba32 EdgeDisabled = Rgb(0x342c26);
        private static readonly Rgba32 Amber = Rgb(0xf0a73a);
        private static readonly Rgba32 AmberHover = Rgb(0xf6b65a);
        private static readonly Rgba32 AmberPressed = Rgb(0xd8902a);
        private static readonly Rgba32 AmberLit = Rgb(0xffd28a);
        private static readonly Rgba32 AmberDark = Rgb(0x9a6420);

        /// <summary>Every piece, frames then glyphs, by name.</summary>
        public static IReadOnlyList<PixelSprite> All { get; } = Build();

        private static PixelSprite[] Build() => new[]
        {
            Bevelled(Plaque, 12, StoneDark, Shadow, StoneLit),
            Bevelled(Button, 12, Face, FaceLit, Shadow),
            Bevelled(ButtonHover, 12, FaceHover, FaceLit, Shadow),
            Bevelled(ButtonPressed, 12, FacePressed, Shadow, FaceLit),
            Bevelled(ButtonDisabled, 12, FaceDisabled, EdgeDisabled, Shadow),
            Bevelled(ButtonActive, 12, FaceHover, FaceLit, Shadow, underline: Amber),
            Bevelled(Primary, 12, Amber, AmberLit, AmberDark),
            Bevelled(PrimaryHover, 12, AmberHover, AmberLit, AmberDark),
            Bevelled(PrimaryPressed, 12, AmberPressed, AmberDark, AmberLit),
            Bevelled(Groove, 9, Inset, Shadow, StoneLit),
            Frame(FrameIron, Rgb(0x4a4d52), Rgb(0x7d8289), Rgb(0x34363a), Rgb(0xb8bcc2)),
            Frame(FrameBronze, Rgb(0x6e4a22), Rgb(0xb9803f), Rgb(0x4a3116), Rgb(0xe8c48c)),
            Frame(FrameSteel, Rgb(0x34495f), Rgb(0x5a7fa6), Rgb(0x233242), Rgb(0xa8c4e0)),
            Glyph(Coin, "..####..", ".#....#.", "#..##..#", "#.#..#.#", "#.#..#.#", "#..##..#", ".#....#.", "..####.."),
            Glyph(Notice, "######..", "#....#..", "#.##.#..", "#....#..", "#.##.#..", "#....#..", "#.##.###", "########"),
            Glyph(Shield, "########", "#......#", "#..##..#", "#.####.#", "#..##..#", ".#....#.", "..#..#..", "...##..."),
            Glyph(Stall, "########", "#.#.#.#.", "########", ".#....#.", ".#....#.", ".#....#.", ".######.", ".#....#."),
            Glyph(Pause, "##..##", "##..##", "##..##", "##..##", "##..##", "##..##"),
            Glyph(Play, "#.....", "##....", "###...", "####..", "###...", "##....", "#....."),
            Glyph(Menu, "######", "......", "######", "......", "######"),
            Glyph(Sun, "...#...", ".#...#.", "..###..", "#.###.#", "..###..", ".#...#.", "...#..."),
            Glyph(Moon, "..###..", ".##....", "##.....", "##.....", "##.....", ".##....", "..###.."),
            Glyph(Close, "#.....#", ".#...#.", "..#.#..", "...#...", "..#.#..", ".#...#.", "#.....#"),
            Glyph(Lantern, "..##..", ".####.", "##..##", "#.##.#", "#.##.#", "##..##", ".####.", "..##.."),
        };

        /// <summary>
        /// A square piece with a one-pixel bevel and chamfered corners: <paramref name="upLeft"/> along the top and left,
        /// <paramref name="downRight"/> along the bottom and right (lit up-left for raised, shadowed up-left for recessed),
        /// <paramref name="fill"/> inside, and an optional line just inside the bottom edge (an active button's lamplight).
        /// </summary>
        private static PixelSprite Bevelled(string name, int size, Rgba32 fill, Rgba32 upLeft, Rgba32 downRight, Rgba32? underline = null)
        {
            var pixels = new Rgba32[size * size];
            int last = size - 1;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    Rgba32 p = fill;
                    if (y == 0 || x == 0) p = upLeft;
                    if (y == last || x == last) p = downRight;
                    if (x == 1 && y == 1) p = upLeft;
                    if (x == last - 1 && y == last - 1) p = downRight;
                    if (underline.HasValue && y == last - 2 && x >= 2 && x <= last - 2) p = underline.Value;
                    pixels[y * size + x] = p;
                }
            // Chamfered corners: the outermost pixel and its two neighbours on each corner are clear
            foreach (var (cx, cy, dx, dy) in new[] { (0, 0, 1, 1), (last, 0, -1, 1), (0, last, 1, -1), (last, last, -1, -1) })
            {
                pixels[cy * size + cx] = Rgba32.Clear;
                pixels[cy * size + cx + dx] = Rgba32.Clear;
                pixels[(cy + dy) * size + cx] = Rgba32.Clear;
            }
            return new PixelSprite(name, size, size, pixels);
        }

        /// <summary>
        /// A window's frame, 16 square with a <see cref="FrameCorner"/>-pixel band: a dark outer line, the metal lit along its top and left and
        /// darkened along its bottom and right, a dark inner line, then the panel; a rivet at each corner.
        /// </summary>
        private static PixelSprite Frame(string name, Rgba32 metal, Rgba32 lit, Rgba32 dark, Rgba32 rivet)
        {
            const int size = 16, last = size - 1;
            var pixels = new Rgba32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    int ring = System.Math.Min(System.Math.Min(x, y), System.Math.Min(last - x, last - y)); // 0 at the edge
                    bool farSide = last - x == ring || last - y == ring; // the bottom or right of this ring
                    Rgba32 p = ring switch
                    {
                        0 => Shadow,
                        1 => farSide ? dark : lit,
                        2 or 3 => metal,
                        4 => Shadow,
                        _ => StoneDark,
                    };
                    if (ring == 3 && farSide) p = dark;
                    pixels[y * size + x] = p;
                }
            pixels[0] = pixels[last] = pixels[last * size] = pixels[last * size + last] = Rgba32.Clear;
            foreach (var (rx, ry) in new[] { (2, 2), (last - 3, 2), (2, last - 3), (last - 3, last - 3) })
            {
                pixels[ry * size + rx] = rivet;
                pixels[ry * size + rx + 1] = metal;
                pixels[(ry + 1) * size + rx] = metal;
                pixels[(ry + 1) * size + rx + 1] = Shadow;
            }
            return new PixelSprite(name, size, size, pixels);
        }

        /// <summary>A white glyph from rows of '#' (ink) and '.' (clear).</summary>
        private static PixelSprite Glyph(string name, params string[] rows)
        {
            int width = rows[0].Length, height = rows.Length;
            var pixels = new Rgba32[width * height];
            var white = new Rgba32(255, 255, 255);
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    pixels[y * width + x] = rows[y][x] == '#' ? white : Rgba32.Clear;
            return new PixelSprite(name, width, height, pixels);
        }

        private static Rgba32 Rgb(uint rgb) => new Rgba32((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb);
    }
}
