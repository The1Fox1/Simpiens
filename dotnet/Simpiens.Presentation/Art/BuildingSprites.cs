using System;
using System.Collections.Generic;

namespace Simpiens.Presentation.Art
{
    /// <summary>
    /// Buildings' walls and floors (Epic PLACES plan 1, B.3, D9): timber walls, standing and cut away, a lot's foundations (a low
    /// course of pale stone), plank floors and bare ground, each from above (a 16-pixel cell) and in isometric (a 32 by 16 tile,
    /// walls risen as the stone wall is); and a building's sign (B.4, D10), a lettered board on a post, standing as the hearth does. Colours are warm against the stone and flagstone of the zones, so a building reads as
    /// its own place. A generator: it reads no table and no sheet.
    /// </summary>
    public static class BuildingSprites
    {
        public const int Cell = 16;

        /// <summary>A lot's foundations in isometric: a low course of stone, as a cut-away wall's stub.</summary>
        public const int FoundationsRise = 4;

        public const string TimberWall = "Map_Wall_Timber", Foundations = "Map_Wall_Foundations", PlankFloor = "Map_Floor_Planks", BareFloor = "Map_Floor_Bare";
        public const string Sign = "Prop_Sign";
        public const string IsoTimberWall = "Iso_Wall_Timber", IsoTimberWallLow = "Iso_Wall_Timber_Low", IsoFoundations = "Iso_Wall_Foundations",
            IsoPlankFloor = "Iso_Floor_Planks", IsoBareFloor = "Iso_Floor_Bare";

        private static readonly List<PixelSprite> _all = new List<PixelSprite>(9);

        public static IReadOnlyList<PixelSprite> All => _all;

        // Timber: a dark outline, lit and shaded boards, a darker beam on top
        private static readonly Rgba32 WoodOutline = new Rgba32(58, 36, 20), WoodLight = new Rgba32(166, 117, 68), Wood = new Rgba32(139, 94, 52),
            WoodDark = new Rgba32(101, 64, 33), WoodBeam = new Rgba32(118, 78, 42);
        // Plank floor: warm boards, a darker seam between them, knots
        private static readonly Rgba32 Board = new Rgba32(176, 132, 86), BoardAlt = new Rgba32(164, 121, 78), Seam = new Rgba32(112, 76, 45), Knot = new Rgba32(134, 94, 58);
        // Bare ground: packed earth with pebbles
        private static readonly Rgba32 Earth = new Rgba32(122, 104, 84), EarthDark = new Rgba32(104, 88, 70), Pebble = new Rgba32(152, 138, 120);
        // Foundations: pale stone and its mortar
        private static readonly Rgba32 Stone = new Rgba32(180, 174, 162), StoneShade = new Rgba32(150, 144, 134), Mortar = new Rgba32(96, 90, 84);

        static BuildingSprites()
        {
            Add(TimberWall, TimberWallFromAbove(), Cell, Cell);
            Add(Foundations, FoundationsFromAbove(), Cell, Cell);
            Add(PlankFloor, PlanksFromAbove(), Cell, Cell);
            Add(BareFloor, EarthFromAbove(), Cell, Cell);
            const int w = IsometricSprites.TileWidth, h = IsometricSprites.TileHeight;
            Add(IsoTimberWall, TimberBlock(IsometricSprites.WallRise), w, h + IsometricSprites.WallRise);
            Add(IsoTimberWallLow, TimberBlock(IsometricSprites.LowWallRise), w, h + IsometricSprites.LowWallRise);
            Add(IsoFoundations, StoneBlock(FoundationsRise), w, h + FoundationsRise);
            Add(IsoPlankFloor, PlanksDiamond(), w, h);
            Add(IsoBareFloor, EarthDiamond(), w, h);
            Add(Sign, FromPattern(SignPattern), Cell, Cell); // B.4
        }

        /// <summary>The sign: a lettered board on a post, its foot on row 14 as a standing 16-pixel sprite's.</summary>
        private static readonly string[] SignPattern =
        {
            "................",
            "................",
            "..OOOOOOOOOOOO..",
            "..OBBBBBBBBBBO..",
            "..OBddBdBddBBO..",
            "..OBBBBBBBBBBO..",
            "..OBdBddBdddBO..",
            "..OBBBBBBBBBBO..",
            "..OOOOOOOOOOOO..",
            ".......OPO......",
            ".......OPO......",
            ".......OPO......",
            ".......OPO......",
            ".......OPO......",
            "......OOPOO.....",
            "................",
        };

        private static Rgba32[] FromPattern(string[] rows)
        {
            var pixels = new Rgba32[Cell * Cell];
            for (int y = 0; y < Cell; y++)
            {
                for (int x = 0; x < Cell; x++)
                {
                    pixels[y * Cell + x] = rows[y][x] switch
                    {
                        'O' => WoodOutline,
                        'B' => Board,
                        'd' => Seam,
                        'P' => WoodBeam,
                        _ => Rgba32.Clear,
                    };
                }
            }
            return pixels;
        }

        private static void Add(string name, Rgba32[] pixels, int width, int height) => _all.Add(new PixelSprite(name, width, height, pixels));

        // --- From above (16 x 16) ---

        /// <summary>A timber wall's top: an outline, three boards along the wall with seams between, staggered joints, a lit top edge.</summary>
        private static Rgba32[] TimberWallFromAbove()
        {
            var pixels = new Rgba32[Cell * Cell];
            for (int y = 0; y < Cell; y++)
            {
                for (int x = 0; x < Cell; x++)
                {
                    Rgba32 c;
                    if (x == 0 || y == 0 || x == Cell - 1 || y == Cell - 1) c = WoodOutline;
                    else if (y == 1) c = WoodLight;
                    else if (y % 5 == 0) c = WoodDark; // a seam between boards
                    else
                    {
                        int board = y / 5;
                        c = (x + board * 5) % 8 == 0 ? WoodDark : board == 1 ? Wood : WoodBeam;
                    }
                    pixels[y * Cell + x] = c;
                }
            }
            return pixels;
        }

        /// <summary>A lot's foundations from above: pale stone blocks in mortar, a course of four rows offset by half a block.</summary>
        private static Rgba32[] FoundationsFromAbove()
        {
            var pixels = new Rgba32[Cell * Cell];
            for (int y = 0; y < Cell; y++)
            {
                for (int x = 0; x < Cell; x++)
                {
                    int course = y / 4;
                    bool joint = y % 4 == 3 || (x + (course & 1) * 3) % 6 == 5;
                    pixels[y * Cell + x] = joint ? Mortar : IsometricSprites.Hash(x, y, 0x5Fu) % 7 == 0 ? StoneShade : Stone;
                }
            }
            return pixels;
        }

        /// <summary>Planks from above: four boards a cell, each four pixels deep, a seam under each, joints staggered, a knot here and there.</summary>
        private static Rgba32[] PlanksFromAbove()
        {
            var pixels = new Rgba32[Cell * Cell];
            for (int y = 0; y < Cell; y++)
            {
                for (int x = 0; x < Cell; x++)
                {
                    int board = y / 4;
                    Rgba32 c;
                    if (y % 4 == 3 || (x + board * 6) % 16 == 0) c = Seam;
                    else c = IsometricSprites.Hash(x, y, 0x3Du) % 29 == 0 ? Knot : (board & 1) == 0 ? Board : BoardAlt;
                    pixels[y * Cell + x] = c;
                }
            }
            return pixels;
        }

        /// <summary>Bare earth from above: packed earth, darker patches and pebbles.</summary>
        private static Rgba32[] EarthFromAbove()
        {
            var pixels = new Rgba32[Cell * Cell];
            for (int y = 0; y < Cell; y++)
                for (int x = 0; x < Cell; x++)
                    pixels[y * Cell + x] = EarthAt(x, y);
            return pixels;
        }

        private static Rgba32 EarthAt(int x, int y)
        {
            uint h = IsometricSprites.Hash(x, y, 0x77u) % 13;
            return h == 0 ? Pebble : h <= 3 ? EarthDark : Earth;
        }

        // --- Isometric (32 x 16 tiles) ---

        /// <summary>A timber block over one cell, <paramref name="rise"/> pixels tall: a beam on top, vertical boards on its faces (lit left, shaded right), a rail across every eight pixels, a dark outline.</summary>
        private static Rgba32[] TimberBlock(int rise)
        {
            int width = IsometricSprites.TileWidth, footprint = IsometricSprites.TileHeight, height = footprint + rise;
            var pixels = new Rgba32[width * height];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (!IsometricSprites.InBlock(x, y, width, footprint, rise)) continue;
                    bool edge = !IsometricSprites.InBlock(x - 1, y, width, footprint, rise) || !IsometricSprites.InBlock(x + 1, y, width, footprint, rise)
                        || !IsometricSprites.InBlock(x, y - 1, width, footprint, rise) || !IsometricSprites.InBlock(x, y + 1, width, footprint, rise);
                    Rgba32 c;
                    if (edge) c = WoodOutline;
                    else if (IsometricSprites.InDiamond(x, y, width * 0.5f, footprint * 0.5f, width, footprint)) c = WoodBeam; // the top
                    else
                    {
                        float dx = MathF.Abs(x + 0.5f - width * 0.5f) / (width * 0.5f);
                        float topEdge = footprint * 0.5f + footprint * 0.5f * (1f - dx);
                        int down = (int)(y + 0.5f - topEdge);
                        int along = x < width / 2 ? x : x - width / 2;
                        bool seam = along % 4 == 0 || (down % 8 == 7 && rise > IsometricSprites.LowWallRise);
                        c = seam ? WoodDark : x < width / 2 ? WoodLight : Wood;
                    }
                    pixels[y * width + x] = c;
                }
            }
            return pixels;
        }

        /// <summary>A low block of pale stone over one cell: a lit top, courses of three pixels on its faces, mortar between.</summary>
        private static Rgba32[] StoneBlock(int rise)
        {
            int width = IsometricSprites.TileWidth, footprint = IsometricSprites.TileHeight, height = footprint + rise;
            var pixels = new Rgba32[width * height];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (!IsometricSprites.InBlock(x, y, width, footprint, rise)) continue;
                    bool edge = !IsometricSprites.InBlock(x - 1, y, width, footprint, rise) || !IsometricSprites.InBlock(x + 1, y, width, footprint, rise)
                        || !IsometricSprites.InBlock(x, y - 1, width, footprint, rise) || !IsometricSprites.InBlock(x, y + 1, width, footprint, rise);
                    Rgba32 c;
                    if (edge) c = Mortar;
                    else if (IsometricSprites.InDiamond(x, y, width * 0.5f, footprint * 0.5f, width, footprint))
                    {
                        // The top: blocks in mortar, staggered
                        int row = y / 3;
                        c = y % 3 == 2 || (x + (row & 1) * 4) % 8 == 7 ? Mortar : Stone;
                    }
                    else c = x < width / 2 ? Stone : StoneShade;
                    pixels[y * width + x] = c;
                }
            }
            return pixels;
        }

        /// <summary>A plank floor's diamond: four boards along one floor axis, a seam between them, joints staggered along them, a joint along the tile's two upper edges as flagstone has.</summary>
        private static Rgba32[] PlanksDiamond()
        {
            int width = IsometricSprites.TileWidth, height = IsometricSprites.TileHeight;
            var pixels = new Rgba32[width * height];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (!IsometricSprites.InDiamond(x, y, width * 0.5f, height * 0.5f, width, height)) continue;
                    // The pixel on the floor: s along one axis, t along the other, each -0.5..0.5 across the cell
                    float a = (x + 0.5f - width * 0.5f) / (width * 0.5f), b = (y + 0.5f - height * 0.5f) / (height * 0.5f);
                    float s = (a + b) * 0.5f, t = (b - a) * 0.5f;
                    float across = (t + 0.5f) * 4f;
                    int board = Math.Clamp((int)across, 0, 3);
                    float along = (s + 0.5f) * 2f + board * 0.5f;
                    bool seam = across - MathF.Floor(across) > 0.8f || along - MathF.Floor(along) > 0.94f;
                    bool upperEdge = y < height / 2 && !IsometricSprites.InDiamond(x, y - 1, width * 0.5f, height * 0.5f, width, height);
                    pixels[y * width + x] = upperEdge || seam ? Seam : IsometricSprites.Hash(x, y, 0x3Du) % 31 == 0 ? Knot : (board & 1) == 0 ? Board : BoardAlt;
                }
            }
            return pixels;
        }

        /// <summary>Bare ground's diamond: packed earth with pebbles.</summary>
        private static Rgba32[] EarthDiamond()
        {
            int width = IsometricSprites.TileWidth, height = IsometricSprites.TileHeight;
            var pixels = new Rgba32[width * height];
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    if (IsometricSprites.InDiamond(x, y, width * 0.5f, height * 0.5f, width, height)) pixels[y * width + x] = EarthAt(x, y);
            return pixels;
        }
    }
}
