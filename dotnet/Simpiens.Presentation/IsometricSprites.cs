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
    /// They follow top-down's sprites in the isometric sheet (<see cref="Art.SpriteSheets.Isometric"/>).
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

    /// <summary>
    /// Isometric's art (M3 design D5): the placeholders on 32x16 tiles. Floor tiles are centred on their cells; walls,
    /// props and crawlers stand on their feet, at one art pixel per pixel of their 16x16 sprites. Everything is drawn from
    /// the isometric sheet (<see cref="DrawSheet.Isometric"/>), one batch.
    /// </summary>
    public sealed class IsometricArt : WorldArt
    {
        /// <summary>A 16x16 sprite's feet: the bottom of its last drawn row (row 14).</summary>
        public const float FeetPivot = 15f / 16f;
        private const float Standing = 16f;
        private const DrawSheet Sheet = DrawSheet.Isometric;
        private static SpriteSheet Sprites => SpriteSheets.Isometric;
        private static readonly int FloorIndex = Sprites.IndexOf(IsometricSprites.Floor), RockIndex = Sprites.IndexOf(IsometricSprites.Rock);
        private static readonly int BunkIndex = Sprites.IndexOf(IsometricSprites.Bunk), ShadowIndex = Sprites.IndexOf(IsometricSprites.Shadow);
        private static readonly int RingIndex = Sprites.IndexOf(IsometricSprites.SelectionRing);

        public static IsometricArt Instance { get; } = new IsometricArt();

        private IsometricArt() { }

        public override ArtPiece Tile(TileKind kind)
        {
            switch (kind)
            {
                case TileKind.Floor: return new ArtPiece(FloorIndex, IsometricSprites.TileWidth, IsometricSprites.TileHeight, sheet: Sheet);
                case TileKind.Rock: return new ArtPiece(RockIndex, IsometricSprites.TileWidth, IsometricSprites.TileHeight, sheet: Sheet);
                default: return Wall(IsometricSprites.Wall, IsometricSprites.WallRise);
            }
        }

        /// <summary>A cut-away wall's stub (M3 design D7).</summary>
        public override ArtPiece LowWall => Wall(IsometricSprites.LowWall, IsometricSprites.LowWallRise);

        private static ArtPiece Wall(PixelSprite sprite, int rise)
            => new ArtPiece(Sprites.IndexOf(sprite), sprite.Width, sprite.Height, 0.5f, IsometricSprites.BlockPivotY(IsometricSprites.TileHeight, rise), Sheet);

        public override ArtPiece Resource(Simpiens.Simulation.Economy.ItemType kind)
            => new ArtPiece(Sprites.IndexOf(ProceduralSprites.GetResourceSprite(kind)), Standing, Standing, 0.5f, FeetPivot, Sheet);

        /// <summary>Top-down's two-frame flame and pulse, standing.</summary>
        public override ArtPiece Hearth(float viewSeconds)
        {
            var sprite = ProceduralSprites.GetCampfireSprite((int)(viewSeconds * 6f) & 1);
            float size = Standing * (1f + MathF.Sin(viewSeconds * 4f) * 0.03f);
            return new ArtPiece(Sprites.IndexOf(sprite), size, size, 0.5f, FeetPivot, Sheet);
        }

        public override ArtPiece Bunk => new ArtPiece(BunkIndex, IsometricSprites.BunkWidth, IsometricSprites.BunkHeight + IsometricSprites.BunkRise,
            0.5f, IsometricSprites.BlockPivotY(IsometricSprites.BunkHeight, IsometricSprites.BunkRise), Sheet);

        public override ArtPiece Crawler(in CrawlerPose pose) => new ArtPiece(Sprites.IndexOf(pose.Sprite), Standing * pose.ScaleX, Standing * pose.ScaleY, 0.5f, FeetPivot, Sheet);

        public override ArtPiece CrawlerShadow => new ArtPiece(ShadowIndex, IsometricSprites.Shadow.Width, IsometricSprites.Shadow.Height, sheet: Sheet);

        public override ArtPiece SelectionRing => new ArtPiece(RingIndex, IsometricSprites.SelectionRing.Width, IsometricSprites.SelectionRing.Height, sheet: Sheet);

        public override Vec2 NameOffset => Vec2.Zero;

        /// <summary>Just over the head: a crawler stands one world unit (16 art pixels) tall.</summary>
        public override float NameHeight => 1.125f;
    }
}
