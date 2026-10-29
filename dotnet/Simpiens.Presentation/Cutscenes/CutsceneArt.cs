using System;
using System.Collections.Generic;
using Simpiens.Entities;

namespace Simpiens.Presentation.Cutscenes
{
    /// <summary>
    /// Generated placeholder backdrops for hard cutscenes (M3 design D9), until a designer draws real ones (M7): 160x90
    /// pixel art, drawn four times over the 640x360 virtual screen, from code and pinned by a test.
    /// </summary>
    public static class CutsceneArt
    {
        public const int BackdropWidth = 160, BackdropHeight = 90;
        public const string Outpost = "outpost";
        public const string DeepSeam = "deep-seam";

        private static readonly List<PixelSprite> _backdrops = new List<PixelSprite>(2);
        private static readonly Dictionary<string, PixelSprite> _byName = new Dictionary<string, PixelSprite>();

        /// <summary>Every backdrop, by index in the backdrop sheet (<see cref="DrawSheet.Backdrop"/>).</summary>
        public static IReadOnlyList<PixelSprite> Backdrops => _backdrops;

        static CutsceneArt()
        {
            Add(Outpost, OutpostAtDusk());
            Add(DeepSeam, Cave());
        }

        public static bool TryGetBackdrop(string name, out PixelSprite sprite) => _byName.TryGetValue(name ?? string.Empty, out sprite);

        private static void Add(string name, Rgba32[] pixels)
        {
            var sprite = new PixelSprite("Backdrop_" + name, BackdropWidth, BackdropHeight, pixels);
            _backdrops.Add(sprite);
            _byName[name] = sprite;
        }

        /// <summary>The outpost at dusk: a sky from indigo to ember, a far ridge, the outpost's blocks with lit windows, the ground.</summary>
        private static Rgba32[] OutpostAtDusk()
        {
            var pixels = new Rgba32[BackdropWidth * BackdropHeight];
            int horizon = 62;
            for (int y = 0; y < BackdropHeight; y++)
            {
                for (int x = 0; x < BackdropWidth; x++)
                {
                    Rgba32 c;
                    if (y < horizon)
                    {
                        float t = y / (float)horizon;
                        c = Lerp(new Rgba32(28, 25, 58), new Rgba32(196, 104, 72), t * t);
                        if (y < 30 && Hash(x, y, 7u) % 97 == 0) c = new Rgba32(230, 226, 210); // a few stars
                    }
                    else c = Lerp(new Rgba32(48, 36, 30), new Rgba32(28, 22, 20), (y - horizon) / (float)(BackdropHeight - horizon));
                    // A far ridge
                    int ridge = horizon - 6 - (int)(5f * (0.5f + 0.5f * MathF.Sin(x * 0.07f)) + 3f * (0.5f + 0.5f * MathF.Sin(x * 0.19f + 1.3f)));
                    if (y >= ridge && y < horizon) c = new Rgba32(52, 38, 60);
                    pixels[y * BackdropWidth + x] = c;
                }
            }
            // The outpost: blocks on the horizon with lit windows
            int[,] blocks = { { 52, 14, 16 }, { 68, 10, 22 }, { 80, 18, 12 }, { 100, 12, 18 } };
            for (int b = 0; b < blocks.GetLength(0); b++)
            {
                int left = blocks[b, 0], width = blocks[b, 1], height = blocks[b, 2];
                for (int y = horizon - height; y < horizon; y++)
                {
                    for (int x = left; x < left + width; x++)
                    {
                        bool window = (x - left) % 4 == 2 && (y - (horizon - height)) % 5 == 2 && Hash(x, y, 3u) % 3 != 0;
                        pixels[y * BackdropWidth + x] = window ? new Rgba32(250, 204, 21) : new Rgba32(30, 24, 36);
                    }
                }
            }
            return pixels;
        }

        private const int CaveFloor = 68;

        /// <summary>The deep seam: a dark cave with hanging rock and glowing crystals on its floor.</summary>
        private static Rgba32[] Cave()
        {
            var pixels = new Rgba32[BackdropWidth * BackdropHeight];
            for (int y = 0; y < BackdropHeight; y++)
            {
                for (int x = 0; x < BackdropWidth; x++)
                {
                    var c = Lerp(new Rgba32(18, 16, 26), new Rgba32(40, 34, 46), y / (float)BackdropHeight);
                    if (Hash(x, y, 11u) % 13 == 0) c = new Rgba32(30, 27, 38);
                    // Hanging rock from the ceiling
                    int hang = 6 + (int)(10f * MathF.Abs(MathF.Sin(x * 0.11f)) + 4f * MathF.Abs(MathF.Sin(x * 0.37f)));
                    if (y < hang) c = new Rgba32(14, 12, 20);
                    // The floor, where the characters stand (their feet are at row 69, above the text box)
                    if (y > CaveFloor) c = new Rgba32(26, 22, 30);
                    pixels[y * BackdropWidth + x] = c;
                }
            }
            // Crystal clusters on the floor
            int[] clusters = { 20, 47, 88, 121, 143 };
            foreach (int cx in clusters)
            {
                int height = 6 + (int)(Hash(cx, 0, 5u) % 7);
                var glow = Hash(cx, 1, 5u) % 2 == 0 ? new Rgba32(103, 232, 249) : new Rgba32(192, 132, 252);
                for (int y = CaveFloor - height; y <= CaveFloor; y++)
                {
                    int half = (y - (CaveFloor - height)) / 3;
                    for (int x = cx - half; x <= cx + half; x++)
                    {
                        if (x >= 0 && x < BackdropWidth) pixels[y * BackdropWidth + x] = (x + y) % 3 == 0 ? new Rgba32(230, 250, 255) : glow;
                    }
                }
            }
            return pixels;
        }

        private static Rgba32 Lerp(Rgba32 a, Rgba32 b, float t)
        {
            t = SimMath.Clamp01(t);
            return new Rgba32((byte)(a.R + (b.R - a.R) * t), (byte)(a.G + (b.G - a.G) * t), (byte)(a.B + (b.B - a.B) * t));
        }

        private static uint Hash(int x, int y, uint salt)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263) ^ salt;
                h = (h ^ (h >> 13)) * 1274126177u;
                return h ^ (h >> 16);
            }
        }
    }

    /// <summary>
    /// A hard cutscene's picture as a draw list in screen space (<see cref="ScreenProjection"/>): the backdrop over the
    /// whole virtual screen, then its characters standing in their slots above the text box, sliding in from the side,
    /// mirrored when they face left, those not speaking dimmed. Engine-free and changes nothing.
    /// </summary>
    public sealed class CutsceneStage
    {
        /// <summary>A crawler sprite's pixels a virtual unit: six, so a 16x16 placeholder stands 96 units tall.</summary>
        public const float CharacterScale = 6f;

        /// <summary>Characters are drawn with top-down's crawler art (graphics V2), each state's id resolved once.</summary>
        private static readonly Art.ArtCatalog Characters = Art.TopDownArtTable.Catalog;
        private static readonly Art.ArtId[] Crawlers = CrawlerIds();

        private static Art.ArtId[] CrawlerIds()
        {
            var states = (AgentVisualState[])System.Enum.GetValues(typeof(AgentVisualState));
            var ids = new Art.ArtId[states.Length];
            foreach (var state in states) ids[(int)state] = Characters.Id(Art.ArtKeys.Crawler(state));
            return ids;
        }
        /// <summary>Where characters' feet rest: just above the text box (<see cref="TextBox"/>).</summary>
        public const float FeetY = 278f;
        /// <summary>The text box's place on the virtual screen, along its bottom.</summary>
        public static readonly RectF TextBox = new RectF(16f, 282f, 608f, 70f);

        private static readonly Rgba32 White = new Rgba32(255, 255, 255, 255);
        private static readonly Rgba32 Dimmed = new Rgba32(150, 150, 165, 255);

        public DrawList Items { get; } = new DrawList();

        public static float SlotX(StageSlot slot) => slot == StageSlot.Left ? 150f : slot == StageSlot.Right ? 490f : 320f;

        /// <summary>Where a character slides in from: off the screen's nearer side.</summary>
        public static float OffstageX(StageSlot slot) => slot == StageSlot.Right ? ScreenProjection.VirtualWidth + 60f : -60f;

        public void Build(CutscenePlayer player)
        {
            Items.Clear();
            var projection = ScreenProjection.Instance;
            if (player != null && CutsceneArt.TryGetBackdrop(player.Backdrop, out var backdrop))
            {
                Items.Add(projection.LayerOf(DrawRole.Floor), DrawRole.Floor, 0f, Art.SpriteSheets.Backdrop.IndexOf(backdrop), ScreenProjection.VirtualWidth * 0.5f, ScreenProjection.VirtualHeight * 0.5f,
                    ScreenProjection.VirtualWidth, ScreenProjection.VirtualHeight, White, 0f, DrawSheet.Backdrop);
            }
            if (player == null) return;

            var stage = player.Stage;
            for (int i = 0; i < stage.Count; i++)
            {
                var staged = stage[i];
                float t = player.SlideIn(staged);
                t = 1f - (1f - t) * (1f - t); // eases to a stop
                float x = OffstageX(staged.Slot) + (SlotX(staged.Slot) - OffstageX(staged.Slot)) * t;
                // Top-down's crawler art, magnified (its sprites at their own size)
                var art = Crawlers[(int)staged.Pose];
                int index = Characters.Sprite(art, CrawlerAppearance.AnimationFrame(staged.Pose, player.Time));
                var sprite = Art.SpriteSheets.Contents(Characters[art].Sheet)[index];
                var tint = player.Speaker == null || player.Speaker == staged.Character.Id ? White : Dimmed;
                ref var item = ref Items.Add(projection.LayerOf(DrawRole.Crawler), DrawRole.Crawler, 0f, index, x, FeetY,
                    sprite.Width * CharacterScale, sprite.Height * CharacterScale, tint, 0f, Characters[art].Sheet);
                item.PivotY = Art.IsometricArtTable.FeetPivot;
                item.FlipX = staged.FacingLeft;
            }
            Items.Sort();
        }
    }
}
