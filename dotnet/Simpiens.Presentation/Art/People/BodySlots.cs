using System;
using System.Collections.Generic;

namespace Simpiens.Presentation.Art.People
{
    /// <summary>
    /// The world sheet's region for bodies (Epic UI-Beta B2, D1): blank 16x16 cells, a slot of 24 frames for each body,
    /// which the client fills with bakes at run time (<see cref="CrawlerBodies"/> says which body is in which slot). In
    /// the world sheet, so a crawler stays one quad in the world's one batch. Blank by construction, so not pinned: the
    /// bodies' pins are the kit's.
    /// </summary>
    public static class BodySlots
    {
        /// <summary>Bodies the region holds at once; past that, crawlers are drawn as today's figure (D2).</summary>
        public const int Count = 64;

        public const string Prefix = "Body_";

        private static readonly PixelSprite[] _all = Build();

        private static PixelSprite[] Build()
        {
            var blank = new Rgba32[BodyKit.PixelsPerFrame]; // shared: a sprite's pixels are never written
            var all = new PixelSprite[Count * BodyKit.FrameCount];
            for (int slot = 0; slot < Count; slot++)
                for (int frame = 0; frame < BodyKit.FrameCount; frame++)
                    all[slot * BodyKit.FrameCount + frame] = new PixelSprite($"{Prefix}{slot:D2}_{frame:D2}", BodyKit.Size, BodyKit.Size, blank);
            return all;
        }

        /// <summary>Every slot's cells, slot by slot, each in <see cref="BodyKit.FrameIndex"/> order.</summary>
        public static IReadOnlyList<PixelSprite> All => _all;

        /// <summary>The cell of <paramref name="slot"/> for frame <paramref name="frameIndex"/> (<see cref="BodyKit.FrameIndex"/>).</summary>
        public static PixelSprite Cell(int slot, int frameIndex) => _all[slot * BodyKit.FrameCount + frameIndex];

        /// <summary>A cell of the region, not a sprite of its own.</summary>
        public static bool IsSlot(PixelSprite sprite) => sprite != null && sprite.Name.StartsWith(Prefix, StringComparison.Ordinal);
    }
}
