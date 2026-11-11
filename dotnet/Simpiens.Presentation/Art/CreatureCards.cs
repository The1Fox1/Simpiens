using System;
using Simpiens.Presentation.Art.People;
using Simpiens.Simulation.Creatures;

namespace Simpiens.Presentation.Art
{
    /// <summary>
    /// A creature's card for the inspector (Epic 10 Part 3, K2.5), in a portrait's place: its kind's first frame from
    /// <see cref="CreatureSprites"/>, three art pixels to one, centred over a cave's dark with its feet on the floor, in a
    /// two-pixel frame the colour of dried blood, <see cref="PortraitKit.FramedSize"/> square like a framed portrait, so the
    /// portrait atlas magnifies and draws it the same way. Generated on the CPU; nothing is allocated.
    /// </summary>
    public static class CreatureCards
    {
        public const int Size = PortraitKit.Size, FramedSize = PortraitKit.FramedSize;

        /// <summary>Art pixels of the card per pixel of the creature's sprite.</summary>
        public const int Scale = 3;

        public static readonly Rgba32 FrameOuter = new Rgba32(64, 30, 26), FrameInner = new Rgba32(156, 70, 52);
        private static readonly Rgba32 Dark = new Rgba32(30, 27, 30), Dusk = new Rgba32(40, 35, 37), Floor = new Rgba32(56, 48, 42), FloorEdge = new Rgba32(74, 64, 54);

        // The card's floor, where the creature's feet rest
        private const int FloorRow = 40;

        /// <summary>The card of <paramref name="kind"/> into <paramref name="into"/> (<see cref="FramedSize"/> square); a kind with no sprite gets the empty card.</summary>
        public static void BakeFramed(CreatureKind kind, Span<Rgba32> into)
        {
            var sprite = SpriteOf(kind);
            int minX = CreatureSprites.Size, minY = CreatureSprites.Size, maxX = -1, maxY = -1;
            if (sprite != null)
            {
                for (int y = 0; y < sprite.Height; y++)
                    for (int x = 0; x < sprite.Width; x++)
                    {
                        if (sprite.Pixels[y * sprite.Width + x].A == 0) continue;
                        if (x < minX) minX = x;
                        if (x > maxX) maxX = x;
                        if (y < minY) minY = y;
                        if (y > maxY) maxY = y;
                    }
            }
            int left = maxX >= 0 ? (Size - (maxX - minX + 1) * Scale) / 2 - minX * Scale : 0;
            int top = maxY >= 0 ? FloorRow + 2 - (maxY + 1) * Scale : 0;
            for (int y = 0; y < FramedSize; y++)
                for (int x = 0; x < FramedSize; x++)
                {
                    int i = y * FramedSize + x;
                    if (x == 0 || y == 0 || x == FramedSize - 1 || y == FramedSize - 1) { into[i] = FrameOuter; continue; }
                    if (x == 1 || y == 1 || x == FramedSize - 2 || y == FramedSize - 2) { into[i] = FrameInner; continue; }
                    int cx = x - 2, cy = y - 2;
                    var back = cy > FloorRow ? Floor : cy == FloorRow ? FloorEdge : ((cx + cy) % 7 == 0 && cy < FloorRow - 6 ? Dusk : Dark);
                    into[i] = back;
                    if (maxX < 0) continue;
                    int sx = cx - left, sy = cy - top;
                    if (sx < 0 || sy < 0) continue;
                    sx /= Scale;
                    sy /= Scale;
                    if (sx >= sprite.Width || sy >= sprite.Height) continue;
                    var p = sprite.Pixels[sy * sprite.Width + sx];
                    if (p.A != 0) into[i] = p;
                }
        }

        private static PixelSprite SpriteOf(CreatureKind kind)
        {
            var key = CreatureCatalog.Get(kind)?.ArtKey;
            if (key == null) return null;
            string name = CreatureSprites.Name(key, 0);
            var all = CreatureSprites.All;
            for (int i = 0; i < all.Count; i++) if (all[i].Name == name) return all[i];
            return null;
        }
    }
}
