using System;
using System.Collections.Generic;
using Simpiens.Presentation.Art;

namespace Simpiens.Presentation
{
    /// <summary>
    /// Generated isometric placeholders (M3 design D5), for 2:1 tiles of <see cref="TileWidth"/> by <see cref="TileHeight"/>
    /// art pixels: floor and rock diamonds, wall blocks full and low, a bunk, and the ring and shadow that lie on the
    /// floor. Every pixel is decided at its centre against the projection's exact diamond, so floor tiles meet with no gap
    /// and no overlap. Crawlers, the hearth and resource nodes stand as their 16x16 top-down sprites. Colours are top-down's.
    /// They follow the 16x16 sprites in the world sheet (<see cref="Art.SpriteSheets.World"/>).
    /// </summary>
    public static class IsometricSprites
    {
        public const int TileWidth = 32;
        public const int TileHeight = 16;
        /// <summary>A full wall's height in art pixels: one and a half world units at <see cref="TileHeight"/> pixels a unit.</summary>
        public const int WallRise = 24;
        /// <summary>A cut-away wall's stub (M3 design D7).</summary>
        public const int LowWallRise = 4;
        public const int BunkWidth = 24, BunkHeight = 12, BunkRise = 5;
        /// <summary>The trade stall's counter (graphics V5): a block a tile and a half across, with a striped cloth on top.</summary>
        public const int StallWidth = 48, StallHeight = 24, StallRise = 8;
        /// <summary>The workshop row's stations (Epic 10 Part 2, W1.6a): the forge's hearth, the still's copper, a plot's roped square.</summary>
        public const int ForgeWidth = 32, ForgeHeight = 16, ForgeRise = 10;
        public const int StillWidth = 24, StillHeight = 12, StillRise = 16;
        public const int PlotWidth = 32, PlotHeight = 16;
        /// <summary>The drill yard (Epic 11, S3.4): a post standing on a sanded footprint.</summary>
        public const int DrillYardWidth = 24, DrillYardHeight = 12, DrillYardRise = 18;
        /// <summary>The tavern (Epic LIFE-1 plan 2, T.3): its counter standing on a planked footprint.</summary>
        public const int TavernWidth = 32, TavernHeight = 16, TavernRise = 10;

        private static readonly List<PixelSprite> _all = new List<PixelSprite>(8);

        public static IReadOnlyList<PixelSprite> All => _all;

        public static PixelSprite Floor { get; }
        public static PixelSprite Rock { get; }
        public static PixelSprite Wall { get; }
        public static PixelSprite LowWall { get; }
        public static PixelSprite Bunk { get; }
        /// <summary>A gold ellipse on the floor around the selected crawler's feet.</summary>
        public static PixelSprite SelectionRing { get; }
        /// <summary>A soft dark ellipse under a standing crawler.</summary>
        public static PixelSprite Shadow { get; }
        public static PixelSprite Stall { get; }
        public static PixelSprite Forge { get; }
        public static PixelSprite Still { get; }
        public static PixelSprite StationPlot { get; }
        public static PixelSprite DrillYard { get; }
        public static PixelSprite Tavern { get; }

        // Top-down's palette (ProceduralSprites): flagstone, rock, slate walls, the bunk's wood, pillow and blanket
        private static readonly Rgba32 Flagstone = new Rgba32(232, 232, 232), Speckle = new Rgba32(212, 212, 212), Joint = new Rgba32(190, 190, 190);
        private static readonly Rgba32 RockBase = new Rgba32(58, 56, 64), RockDark = new Rgba32(46, 44, 52), RockLight = new Rgba32(74, 72, 82);
        private static readonly Rgba32 Mortar = new Rgba32(30, 41, 59), Bevel = new Rgba32(148, 163, 184), Brick = new Rgba32(71, 85, 105), BrickLit = new Rgba32(96, 110, 131);
        private static readonly Rgba32 Wood = new Rgba32(120, 72, 32), WoodShadow = new Rgba32(78, 45, 18), Pillow = new Rgba32(241, 245, 249), Blanket = new Rgba32(79, 110, 196), BlanketFold = new Rgba32(55, 79, 150);
        private static readonly Rgba32 Gold = new Rgba32(250, 204, 21);

        static IsometricSprites()
        {
            Floor = Add("Iso_Floor", FloorPixels(), TileWidth, TileHeight);
            Rock = Add("Iso_Rock", RockPixels(), TileWidth, TileHeight);
            Wall = Add("Iso_Wall", WallPixels(WallRise), TileWidth, TileHeight + WallRise);
            LowWall = Add("Iso_Wall_Low", WallPixels(LowWallRise), TileWidth, TileHeight + LowWallRise);
            Bunk = Add("Iso_Bunk", BunkPixels(), BunkWidth, BunkHeight + BunkRise);
            SelectionRing = Add("Iso_Selection_Ring", EllipsePixels(22, 12, 11f, 6f, ring: true, Gold), 22, 12);
            Shadow = Add("Iso_Shadow", EllipsePixels(12, 6, 6f, 3f, ring: false, new Rgba32(0, 0, 0, 90)), 12, 6);
            Stall = Add("Iso_Stall", StallPixels(), StallWidth, StallHeight + StallRise);
            Forge = Add("Iso_Forge", ForgePixels(), ForgeWidth, ForgeHeight + ForgeRise);
            Still = Add("Iso_Still", StillPixels(), StillWidth, StillHeight + StillRise);
            StationPlot = Add("Iso_StationPlot", PlotPixels(), PlotWidth, PlotHeight);
            DrillYard = Add("Iso_DrillYard", DrillYardPixels(), DrillYardWidth, DrillYardHeight + DrillYardRise); // Epic 11, S3.4
            Tavern = Add("Iso_Tavern", TavernPixels(), TavernWidth, TavernHeight + TavernRise); // Epic LIFE-1 plan 2, T.3
        }

        /// <summary>The pivot of a block <paramref name="rise"/> pixels tall on a <paramref name="height"/>-pixel footprint: its footprint's centre.</summary>
        public static float BlockPivotY(int height, int rise) => (rise + height * 0.5f) / (height + rise);

        private static PixelSprite Add(string name, Rgba32[] pixels, int width, int height)
        {
            var sprite = new PixelSprite(name, width, height, pixels);
            _all.Add(sprite);
            return sprite;
        }

        /// <summary>True when the pixel's centre lies inside the 2:1 diamond of <paramref name="width"/> by <paramref name="height"/> centred at <paramref name="cx"/>, <paramref name="cy"/>.</summary>
        private static bool InDiamond(int x, int y, float cx, float cy, int width, int height)
            => MathF.Abs(x + 0.5f - cx) / (width * 0.5f) + MathF.Abs(y + 0.5f - cy) / (height * 0.5f) < 1f;

        /// <summary>A fixed per-pixel hash (view only), for speckles.</summary>
        private static uint Hash(int x, int y, uint salt)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263) ^ salt;
                h = (h ^ (h >> 13)) * 1274126177u;
                return h ^ (h >> 16);
            }
        }

        /// <summary>Flagstone, tinted per sector when drawn; a joint along its two upper edges, so each seam is one pixel.</summary>
        private static Rgba32[] FloorPixels()
        {
            var pixels = new Rgba32[TileWidth * TileHeight];
            for (int y = 0; y < TileHeight; y++)
            {
                for (int x = 0; x < TileWidth; x++)
                {
                    if (!InDiamond(x, y, TileWidth * 0.5f, TileHeight * 0.5f, TileWidth, TileHeight)) continue;
                    bool upperEdge = y < TileHeight / 2 && !InDiamond(x, y - 1, TileWidth * 0.5f, TileHeight * 0.5f, TileWidth, TileHeight);
                    pixels[y * TileWidth + x] = upperEdge ? Joint : Hash(x, y, 0xF1u) % 11 == 0 ? Speckle : Flagstone;
                }
            }
            return pixels;
        }

        private static Rgba32[] RockPixels()
        {
            var pixels = new Rgba32[TileWidth * TileHeight];
            for (int y = 0; y < TileHeight; y++)
            {
                for (int x = 0; x < TileWidth; x++)
                {
                    if (!InDiamond(x, y, TileWidth * 0.5f, TileHeight * 0.5f, TileWidth, TileHeight)) continue;
                    uint h = Hash(x, y, 0x2Bu) % 9;
                    pixels[y * TileWidth + x] = h == 0 ? RockDark : h == 1 ? RockLight : RockBase;
                }
            }
            return pixels;
        }

        /// <summary>
        /// A slate block over one cell, <paramref name="rise"/> pixels tall: a lit top face (the cell's diamond, raised),
        /// a lit left face and a shaded right face with staggered courses of brick, and a mortar outline.
        /// </summary>
        private static Rgba32[] WallPixels(int rise)
        {
            int width = TileWidth, height = TileHeight + rise;
            var pixels = new Rgba32[width * height];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (!InBlock(x, y, width, TileHeight, rise)) continue;
                    bool edge = !InBlock(x - 1, y, width, TileHeight, rise) || !InBlock(x + 1, y, width, TileHeight, rise)
                        || !InBlock(x, y - 1, width, TileHeight, rise) || !InBlock(x, y + 1, width, TileHeight, rise);
                    Rgba32 colour;
                    if (edge) colour = Mortar;
                    else if (InDiamond(x, y, width * 0.5f, TileHeight * 0.5f, width, TileHeight)) colour = Bevel; // the top face
                    else
                    {
                        // A face: courses of 6 pixels below the top face's lower edge, bricks 8 long, staggered by course
                        float dx = MathF.Abs(x + 0.5f - width * 0.5f) / (width * 0.5f);
                        float topEdge = TileHeight * 0.5f + TileHeight * 0.5f * (1f - dx);
                        int down = (int)(y + 0.5f - topEdge);
                        int course = down / 6;
                        int along = (x < width / 2 ? x : x - width / 2) + (course & 1) * 4;
                        bool joint = down % 6 == 5 || along % 8 == 0;
                        colour = joint ? Mortar : x < width / 2 ? BrickLit : Brick;
                    }
                    pixels[y * width + x] = colour;
                }
            }
            return pixels;
        }

        /// <summary>Inside the block: the cell's diamond swept from <paramref name="rise"/> pixels up down to the floor.</summary>
        private static bool InBlock(int x, int y, int width, int footprintHeight, int rise)
        {
            if (x < 0 || x >= width || y < 0 || y >= footprintHeight + rise) return false;
            float dx = MathF.Abs(x + 0.5f - width * 0.5f) / (width * 0.5f);
            if (dx >= 1f) return false;
            float reach = footprintHeight * 0.5f * (1f - dx);
            float py = y + 0.5f;
            return py > footprintHeight * 0.5f - reach && py < footprintHeight * 0.5f + rise + reach;
        }

        /// <summary>The stall's counter: a wooden block with a red-and-cream striped cloth over its top.</summary>
        private static Rgba32[] StallPixels()
        {
            int width = StallWidth, height = StallHeight + StallRise;
            var red = new Rgba32(220, 38, 38);
            var cream = new Rgba32(254, 243, 199);
            var pixels = new Rgba32[width * height];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (!InBlock(x, y, width, StallHeight, StallRise)) continue;
                    Rgba32 colour;
                    if (InDiamond(x, y, width * 0.5f, StallHeight * 0.5f, width, StallHeight)) colour = (x + 2 * y) / 6 % 2 == 0 ? red : cream;
                    else colour = x < width / 2 ? Wood : WoodShadow;
                    pixels[y * width + x] = colour;
                }
            }
            return pixels;
        }

        /// <summary>The forge: a stone hearth with a bed of coals glowing on its top.</summary>
        private static Rgba32[] ForgePixels()
        {
            int width = ForgeWidth, height = ForgeHeight + ForgeRise;
            var stone = new Rgba32(96, 110, 131);
            var stoneDark = new Rgba32(71, 85, 105);
            var coal = new Rgba32(234, 88, 12);
            var coalDark = new Rgba32(153, 27, 27);
            var hot = new Rgba32(250, 204, 21);
            var pixels = new Rgba32[width * height];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (!InBlock(x, y, width, ForgeHeight, ForgeRise)) continue;
                    Rgba32 colour;
                    if (InDiamond(x, y, width * 0.5f, ForgeHeight * 0.5f, width * 6 / 10, ForgeHeight * 6 / 10))
                        colour = Hash(x, y, 0xF0F0u) % 7 == 0 ? hot : Hash(x, y, 0x0F0Fu) % 3 == 0 ? coalDark : coal;
                    else if (InDiamond(x, y, width * 0.5f, ForgeHeight * 0.5f, width, ForgeHeight)) colour = stone;
                    else colour = x < width / 2 ? stone : stoneDark;
                    pixels[y * width + x] = colour;
                }
            }
            return pixels;
        }

        /// <summary>
        /// The still: a squat stone firebox, its fire showing in the front, with a round copper pot on it; a pipe leaves the
        /// pot's neck and comes down at its side, a drop of draught at its end.
        /// </summary>
        private static Rgba32[] StillPixels()
        {
            int width = StillWidth, height = StillHeight + StillRise;
            const int boxRise = 6, boxTop = StillRise - boxRise; // the firebox stands on the station's footprint
            var stone = new Rgba32(96, 110, 131);
            var stoneDark = new Rgba32(71, 85, 105);
            var fire = new Rgba32(234, 88, 12);
            var hot = new Rgba32(250, 204, 21);
            var lit = new Rgba32(217, 119, 6);
            var copper = new Rgba32(180, 83, 9);
            var shade = new Rgba32(120, 53, 15);
            var rim = new Rgba32(69, 26, 3);
            var pipe = new Rgba32(148, 163, 184);
            var pipeDark = new Rgba32(100, 116, 139);
            var draught = new Rgba32(103, 232, 249);
            var pixels = new Rgba32[width * height];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (!InBlock(x, y - boxTop, width, StillHeight, boxRise)) continue;
                    bool top = InDiamond(x, y - boxTop, width * 0.5f, StillHeight * 0.5f, width, StillHeight);
                    pixels[y * width + x] = top ? stone : x < width / 2 ? stone : stoneDark;
                }
            }
            // The fire's mouth, in the firebox's lit face
            for (int y = 20; y <= 22; y++)
                for (int x = 4; x <= 7; x++) pixels[y * width + x] = y == 20 ? rim : (x + y) % 3 == 0 ? hot : fire;

            // The pot: round, lit from the top left
            const float cx = 11f, cy = 11f, rx = 7f, ry = 6.5f;
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float nx = (x + 0.5f - cx) / rx, ny = (y + 0.5f - cy) / ry;
                    float d = nx * nx + ny * ny;
                    if (d > 1f) continue;
                    float light = nx + ny;
                    pixels[y * width + x] = d > 0.75f ? rim : light < -0.6f ? lit : light > 0.5f ? shade : copper;
                }
            }
            // Its neck, and the pipe from it, over and down the right side
            for (int y = 2; y <= 5; y++) { pixels[y * width + 10] = copper; pixels[y * width + 11] = shade; }
            for (int x = 10; x <= 20; x++) { pixels[1 * width + x] = pipe; pixels[2 * width + x] = x <= 11 ? copper : pipeDark; }
            for (int y = 1; y <= 13; y++) { pixels[y * width + 20] = pipe; pixels[y * width + 21] = pipeDark; }
            pixels[14 * width + 20] = draught;
            return pixels;
        }

        /// <summary>
        /// The drill yard (Epic 11, S3.4): a sanded footprint, and on it a wooden post with a straw body bound with rope and
        /// its arms out, lit from the top left.
        /// </summary>
        private static Rgba32[] DrillYardPixels()
        {
            int width = DrillYardWidth, height = DrillYardHeight + DrillYardRise;
            var sand = new Rgba32(176, 156, 118);
            var sandDark = new Rgba32(146, 126, 92);
            var wood = new Rgba32(120, 72, 32);
            var woodDark = new Rgba32(78, 45, 18);
            var straw = new Rgba32(214, 186, 112);
            var strawDark = new Rgba32(168, 138, 70);
            var rope = new Rgba32(161, 98, 7);
            var pixels = new Rgba32[width * height];
            // The sanded footprint, on the floor
            for (int y = 0; y < DrillYardHeight; y++)
                for (int x = 0; x < width; x++)
                    if (InDiamond(x, y, width * 0.5f, DrillYardHeight * 0.5f, width, DrillYardHeight))
                        pixels[(y + DrillYardRise) * width + x] = Hash(x, y, 7u) % 5 == 0 ? sandDark : sand;
            // The post, from the footprint's centre up
            int cx = width / 2, foot = DrillYardRise + DrillYardHeight / 2;
            for (int y = 6; y <= foot; y++) { pixels[y * width + cx - 1] = wood; pixels[y * width + cx] = woodDark; }
            // Its arms
            for (int x = cx - 7; x <= cx + 6; x++) { pixels[9 * width + x] = wood; pixels[10 * width + x] = woodDark; }
            // The straw body, round, bound twice with rope
            for (int y = 2; y <= 16; y++)
            {
                for (int x = cx - 4; x <= cx + 3; x++)
                {
                    float nx = (x + 0.5f - cx) / 4f, ny = (y + 0.5f - 9f) / 7.5f;
                    if (nx * nx + ny * ny > 1f) continue;
                    pixels[y * width + x] = y == 6 || y == 13 ? rope : nx + ny * 0.5f > 0.2f ? strawDark : straw;
                }
            }
            return pixels;
        }

        /// <summary>
        /// The tavern (Epic LIFE-1 plan 2, T.3): a planked footprint, a counter raised on it (its top in light wood, its faces in
        /// shade), and two tankards on the counter, foam on top.
        /// </summary>
        private static Rgba32[] TavernPixels()
        {
            int width = TavernWidth, height = TavernHeight + TavernRise;
            var plank = new Rgba32(120, 84, 48);
            var plankDark = new Rgba32(96, 64, 34);
            var top = new Rgba32(168, 116, 64);
            var face = new Rgba32(120, 72, 32);
            var faceDark = new Rgba32(78, 45, 18);
            var pewter = new Rgba32(150, 152, 160);
            var foam = new Rgba32(244, 232, 196);
            var pixels = new Rgba32[width * height];
            // The planked footprint, on the floor
            for (int y = 0; y < TavernHeight; y++)
                for (int x = 0; x < width; x++)
                    if (InDiamond(x, y, width * 0.5f, TavernHeight * 0.5f, width, TavernHeight))
                        pixels[(y + TavernRise) * width + x] = (x + y) % 4 == 0 ? plankDark : plank;
            // The counter: a smaller diamond lifted by the rise, its faces dropped to the floor
            int counterWidth = 22, counterHeight = 10;
            float cx = width * 0.5f, cy = TavernRise + TavernHeight * 0.5f - 2f;
            for (int x = 0; x < width; x++)
            {
                int lowest = -1;
                for (int y = 0; y < height; y++)
                {
                    if (!InDiamond(x, y, cx, cy - TavernRise + 2f, counterWidth, counterHeight)) continue;
                    pixels[y * width + x] = top;
                    lowest = y;
                }
                if (lowest < 0) continue;
                for (int y = lowest + 1; y <= lowest + TavernRise - 2 && y < height; y++) pixels[y * width + x] = x < cx ? face : faceDark;
            }
            // Two tankards on the counter
            foreach (int mx in new[] { (int)cx - 6, (int)cx + 3 })
            {
                int my = (int)(cy - TavernRise + 1f);
                for (int y = my - 3; y <= my; y++) { pixels[y * width + mx] = pewter; pixels[y * width + mx + 1] = pewter; }
                pixels[(my - 4) * width + mx] = foam;
                pixels[(my - 4) * width + mx + 1] = foam;
            }
            return pixels;
        }

        /// <summary>A station's empty plot: a roped square on the floor with a stake at each corner, where one may be built.</summary>
        private static Rgba32[] PlotPixels()
        {
            int width = PlotWidth, height = PlotHeight;
            var rope = new Rgba32(161, 98, 7);
            var stake = new Rgba32(120, 72, 32);
            var pixels = new Rgba32[width * height];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    bool edge = InDiamond(x, y, width * 0.5f, height * 0.5f, width, height) && !InDiamond(x, y, width * 0.5f, height * 0.5f, width - 6, height - 3);
                    if (!edge) continue;
                    bool corner = x < 3 || x >= width - 3 || y < 2 || y >= height - 2;
                    if (corner) pixels[y * width + x] = stake;
                    else if ((x / 2) % 2 == 0) pixels[y * width + x] = rope;
                }
            }
            return pixels;
        }

        /// <summary>A low wooden frame with a blanket on top and the pillow at its far end.</summary>
        private static Rgba32[] BunkPixels()
        {
            int width = BunkWidth, height = BunkHeight + BunkRise;
            var pixels = new Rgba32[width * height];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (!InBlock(x, y, width, BunkHeight, BunkRise)) continue;
                    Rgba32 colour;
                    if (InDiamond(x, y, width * 0.5f, BunkHeight * 0.5f, width, BunkHeight))
                        colour = y < 4 ? Pillow : (x + y) % 5 == 0 ? BlanketFold : Blanket;
                    else colour = x < width / 2 ? Wood : WoodShadow;
                    pixels[y * width + x] = colour;
                }
            }
            return pixels;
        }

        /// <summary>A 2:1 ellipse with radii <paramref name="rx"/>, <paramref name="ry"/>: filled, or a thin ring.</summary>
        private static Rgba32[] EllipsePixels(int width, int height, float rx, float ry, bool ring, Rgba32 colour)
        {
            var pixels = new Rgba32[width * height];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float dx = (x + 0.5f - width * 0.5f) / rx, dy = (y + 0.5f - height * 0.5f) / ry;
                    float d = MathF.Sqrt(dx * dx + dy * dy);
                    if (d < 1f && (!ring || d > 0.78f)) pixels[y * width + x] = colour;
                }
            }
            return pixels;
        }
    }
}
