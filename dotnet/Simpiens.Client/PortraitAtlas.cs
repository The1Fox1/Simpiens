using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Simpiens.Presentation;
using Simpiens.Presentation.Art.People;

namespace Simpiens.Client
{
    /// <summary>
    /// Crawlers' portraits in one texture (Epic UI-Beta B3, D7): <see cref="CrawlerPortraits.Slots"/> slots, each a framed
    /// portrait magnified on the CPU to two whole scales, drawn 1:1 at whole pixels as the icons are, so no texture filter
    /// touches them: <see cref="Scale"/>x for the dossier and the inspector, <see cref="SpeakerScale"/>x beside a scene's
    /// speaker; and its face, at 1x for the lists and at <see cref="FaceScale"/>x for who is in a sector. The scales grow
    /// with the window's height (<see cref="Fit"/>), and every seated portrait is baked again at the new ones. A portrait is baked and written the first time it is shown (<see cref="Show"/>), and again when its
    /// mood, gear or standing changes it; the least recently shown slot is reused.
    /// </summary>
    public sealed class PortraitAtlas : IDisposable
    {
        /// <summary>A framed portrait at 1x, in art pixels.</summary>
        public const int FramedSize = PortraitKit.FramedSize;

        /// <summary>The scales in a window <see cref="BaseHeight"/> rows tall: the dossier's 2x (104 pixels) and the speaker's 1x.</summary>
        public const int BaseScale = 2, BaseSpeakerScale = 1;

        /// <summary>The window's height the base scales are for; the scales grow with it in whole steps.</summary>
        public const int BaseHeight = 720;

        /// <summary>The largest scales: 4x at 1440 rows, the speaker's 2x, a sector's faces 2x.</summary>
        public const int MaxScale = 4, MaxSpeakerScale = 2, MaxFaceScale = 2;

        /// <summary>A face in the lists (Epic UI-Beta B4): the head cropped from the same bake, at 1x.</summary>
        public const int FaceSize = PortraitKit.FaceSize;

        private const int Gutter = 2, Width = 2048;
        private const int LargeCell = FramedSize * MaxScale, SmallCell = FramedSize * MaxSpeakerScale, FaceCell = FaceSize * MaxFaceScale;

        private readonly Rectangle[] _small = new Rectangle[CrawlerPortraits.Slots], _large = new Rectangle[CrawlerPortraits.Slots], _faces = new Rectangle[CrawlerPortraits.Slots];
        private readonly Rectangle[] _scaledFaces = new Rectangle[CrawlerPortraits.Slots];
        private readonly Rgba32[] _framed = new Rgba32[FramedSize * FramedSize], _magnified = new Rgba32[LargeCell * LargeCell], _face = new Rgba32[FaceSize * FaceSize];
        private readonly Color[] _scratch = new Color[LargeCell * LargeCell];

        public PortraitAtlas(GraphicsDevice device)
        {
            // The large cells in rows, then the speaker's, the faces, and the scaled faces; each cell holds its largest scale
            int perLarge = Width / (LargeCell + Gutter), perSmall = Width / (SmallCell + Gutter), perFace = Width / (FaceSize + Gutter);
            int perScaledFace = Width / (FaceCell + Gutter);
            int largeRows = (CrawlerPortraits.Slots + perLarge - 1) / perLarge, smallRows = (CrawlerPortraits.Slots + perSmall - 1) / perSmall;
            int faceRows = (CrawlerPortraits.Slots + perFace - 1) / perFace, scaledFaceRows = (CrawlerPortraits.Slots + perScaledFace - 1) / perScaledFace;
            int smallTop = largeRows * (LargeCell + Gutter), faceTop = smallTop + smallRows * (SmallCell + Gutter);
            int scaledFaceTop = faceTop + faceRows * (FaceSize + Gutter);
            for (int i = 0; i < CrawlerPortraits.Slots; i++)
            {
                _large[i] = new Rectangle(i % perLarge * (LargeCell + Gutter), i / perLarge * (LargeCell + Gutter), LargeCell, LargeCell);
                _small[i] = new Rectangle(i % perSmall * (SmallCell + Gutter), smallTop + i / perSmall * (SmallCell + Gutter), SmallCell, SmallCell);
                _faces[i] = new Rectangle(i % perFace * (FaceSize + Gutter), faceTop + i / perFace * (FaceSize + Gutter), FaceSize, FaceSize);
                _scaledFaces[i] = new Rectangle(i % perScaledFace * (FaceCell + Gutter), scaledFaceTop + i / perScaledFace * (FaceCell + Gutter), FaceCell, FaceCell);
            }
            Texture = new Texture2D(device, Width, scaledFaceTop + scaledFaceRows * (FaceCell + Gutter));
        }

        /// <summary>Screen pixels per art pixel in the dossier and the inspector: 2 in a 720-row window, 3 at 1080, 4 at 1440.</summary>
        public int Scale { get; private set; } = BaseScale;

        /// <summary>Screen pixels per art pixel beside a scene's speaker: 1 in a 720-row window, 2 from 1080.</summary>
        public int SpeakerScale { get; private set; } = BaseSpeakerScale;

        /// <summary>The dossier's and the inspector's portrait, in screen pixels.</summary>
        public int Size => FramedSize * Scale;

        /// <summary>The speaker's portrait, in screen pixels.</summary>
        public int SpeakerSize => FramedSize * SpeakerScale;

        /// <summary>Screen pixels per art pixel of a face in who is in a sector: the speaker's step, 1 in a 720-row window, 2 from 1080.</summary>
        public int FaceScale { get; private set; } = 1;

        /// <summary>A face at <see cref="FaceScale"/>x, in screen pixels.</summary>
        public int ScaledFaceSize => FaceSize * FaceScale;

        /// <summary>
        /// The scales for a window <paramref name="windowHeight"/> rows tall: the base ones scaled with the height, rounded
        /// to whole steps (half up), never below the base nor above the largest.
        /// </summary>
        public static (int Scale, int SpeakerScale, int FaceScale) ScalesFor(int windowHeight)
            => (Step(BaseScale, windowHeight, MaxScale), Step(BaseSpeakerScale, windowHeight, MaxSpeakerScale), Step(1, windowHeight, MaxFaceScale));

        private static int Step(int baseScale, int windowHeight, int max)
            => Math.Clamp((int)Math.Round(baseScale * (double)windowHeight / BaseHeight, MidpointRounding.AwayFromZero), baseScale, max);

        /// <summary>
        /// Takes the scales for a window <paramref name="windowHeight"/> rows tall; when they change, every seated portrait
        /// is baked again at them, and the views must lay out for the new <see cref="Size"/> (true).
        /// </summary>
        public bool Fit(int windowHeight)
        {
            var (scale, speaker, face) = ScalesFor(windowHeight);
            if (scale == Scale && speaker == SpeakerScale && face == FaceScale) return false;
            Scale = scale;
            SpeakerScale = speaker;
            FaceScale = face;
            for (int slot = 0; slot < CrawlerPortraits.Slots; slot++)
            {
                if (Table.TryKeyIn(slot, out _)) Bake(slot);
            }
            return true;
        }

        public Texture2D Texture { get; }

        /// <summary>Which portrait is in which slot.</summary>
        public CrawlerPortraits Table { get; } = new CrawlerPortraits();

        /// <summary>Where <paramref name="key"/>'s portrait is in <see cref="Texture"/> at <see cref="Scale"/>x, baked now if it wasn't.</summary>
        public Rectangle Show(in PortraitKey key) => At(_large[Seat(key)], Size);

        /// <summary>Where <paramref name="key"/>'s portrait is in <see cref="Texture"/> at <see cref="SpeakerScale"/>x, baked now if it wasn't.</summary>
        public Rectangle ShowSpeaker(in PortraitKey key) => At(_small[Seat(key)], SpeakerSize);

        /// <summary>Where <paramref name="key"/>'s face (the lists') is in <see cref="Texture"/>, baked now if its portrait wasn't.</summary>
        public Rectangle ShowFace(in PortraitKey key) => _faces[Seat(key)];

        /// <summary>Where <paramref name="key"/>'s face is at <see cref="FaceScale"/>x (who is in a sector), baked now if its portrait wasn't.</summary>
        public Rectangle ShowScaledFace(in PortraitKey key) => At(_scaledFaces[Seat(key)], ScaledFaceSize);

        // The key's slot, every pending bake written
        private int Seat(in PortraitKey key)
        {
            int slot = Table.Show(key);
            while (Table.TryTakePending(out int pending)) Bake(pending);
            return slot;
        }

        // A slot's portrait at the two scales, and its face at 1x and at its scale
        private void Bake(int slot)
        {
            Table.BakeSlot(slot, _framed);
            PortraitKit.Magnify(_framed, FramedSize, SpeakerScale, _magnified);
            Write(At(_small[slot], SpeakerSize), _magnified);
            PortraitKit.Magnify(_framed, FramedSize, Scale, _magnified);
            Write(At(_large[slot], Size), _magnified);
            Table.TryKeyIn(slot, out var baked);
            PortraitKit.Face(_framed, baked.Standing, _face);
            Write(_faces[slot], _face);
            PortraitKit.Magnify(_face, FaceSize, FaceScale, _magnified);
            Write(At(_scaledFaces[slot], ScaledFaceSize), _magnified);
        }

        // The top left of a cell, size square: where a portrait at a scale smaller than the cell's largest is
        private static Rectangle At(Rectangle cell, int size) => new Rectangle(cell.X, cell.Y, size, size);

        private void Write(Rectangle cell, ReadOnlySpan<Rgba32> pixels)
        {
            int count = cell.Width * cell.Height;
            for (int i = 0; i < count; i++)
            {
                var p = pixels[i];
                _scratch[i] = new Color(p.R, p.G, p.B, p.A);
            }
            Texture.SetData(0, cell, _scratch, 0, count);
        }

        public void Dispose() => Texture.Dispose();
    }
}
