using System.Collections.Generic;

namespace Simpiens.Presentation.Art
{
    /// <summary>
    /// The descent gate (Epic 10 Part 3, K10.5c, Q13), a 32-pixel prop standing in the Old Stair's west wall: sealed, its iron
    /// doors riveted in brass with an ember seal between them; breached, a door hanging aside and stairs going down into the
    /// dark. Its base at fifteen sixteenths of its height, so it stands in isometric as a crawler does. A generator: it reads
    /// no table and no sheet.
    /// </summary>
    public static class GateSprites
    {
        public const int Size = 32;
        public const string Sealed = "Prop_Gate";
        public const string Open = "Prop_GateOpen";

        private static readonly List<PixelSprite> _all = new List<PixelSprite>(2);

        public static IReadOnlyList<PixelSprite> All => _all;

        private static readonly Dictionary<char, Rgba32> Palette = new Dictionary<char, Rgba32>
        {
            { '.', Rgba32.Clear },
            { 'O', new Rgba32(24, 22, 28, 255) },   // outline
            { 'U', new Rgba32(150, 144, 134, 255) }, // lit stone
            { 'T', new Rgba32(118, 112, 104, 255) }, // stone
            { 't', new Rgba32(82, 78, 74, 255) },    // shaded stone
            { 'I', new Rgba32(92, 98, 110, 255) },   // iron
            { 'i', new Rgba32(56, 60, 70, 255) },    // the doors' seam
            { 'Y', new Rgba32(200, 168, 88, 255) },  // brass rivets
            { 'E', new Rgba32(255, 140, 60, 255) },  // the ember seal
            { 'k', new Rgba32(6, 6, 10, 255) },      // the dark beyond
        };

        private static readonly string[] SealedPattern =
        {
            "................................",
            "................................",
            "..........OOOOOOOOOOOO..........",
            "........OOUUUUUUUUUUUUOO........",
            ".......OUUTTTTTTTTTTTTUUO.......",
            "......OUTTOOOOOOOOOOOOTTUO......",
            ".....OUTTOIIIIIIIIIIIIOTTUO.....",
            ".....OUTOIIIIIIiiIIIIIIOTUO.....",
            "....OUTTOIYIIIIiiIIIIYIOTTUO....",
            "....OUTTOIIIIIIiiIIIIIIOTTUO....",
            "....OUTTOIIIIIIiiIIIIIIOTTUO....",
            "....OTTTOIIIIIIiiIIIIIIOTTTO....",
            "....OTTTOIYIIIIiiIIIIYIOTTTO....",
            "....OTTtOIIIIIEiiEIIIIIOtTTO....",
            "....OTTtOIIIIEEiiEEIIIIOtTTO....",
            "....OTTtOIIIIIEiiEIIIIIOtTTO....",
            "....OTTTOIYIIIIiiIIIIYIOTTTO....",
            "....OTTTOIIIIIIiiIIIIIIOTTTO....",
            "....OTTtOIIIIIIiiIIIIIIOtTTO....",
            "....OTTtOIIIIIIiiIIIIIIOtTTO....",
            "....OTTTOIYIIIIiiIIIIYIOTTTO....",
            "....OTTTOIIIIIIiiIIIIIIOTTTO....",
            "....OTTtOIIIIIIiiIIIIIIOtTTO....",
            "....OTTtOIIIIIIiiIIIIIIOtTTO....",
            "...OUTTTOiiiiiiiiiiiiiiOTTTUO...",
            "..OUUUUUOOOOOOOOOOOOOOOOUUUUUO..",
            "..OTTTTTTTTTTTTTTTTTTTTTTTTTTO..",
            "..OttttttttttttttttttttttttttO..",
            "..OOOOOOOOOOOOOOOOOOOOOOOOOOOO..",
            "..OOOOOOOOOOOOOOOOOOOOOOOOOOOO..",
            "................................",
            "................................",
        };

        private static readonly string[] OpenPattern =
        {
            "................................",
            "................................",
            "..........OOOOOOOOOOOO..........",
            "........OOUUUUUUUUUUUUOO........",
            ".......OUUTTTTTTTTTTTTUUO.......",
            "......OUTTOOOOOOOOOOOOTTUO......",
            ".....OUTTOkkkkkkkkkkkkOTTUO.....",
            "...OOUTOkkkkkkkkkkkkkkkOTUO.....",
            "..OIOUTTkkkkkkkkkkkkkkkOTTUO....",
            "..OIIOTTkkkkkkkkkkkkkkkOTTUO....",
            "..OIYOTTkkkkkkkkkkkkkkkOTTUO....",
            "..OIIOTTkkkkkkkkkkkkkkkOTTTO....",
            "..OIIOTTkkkkkkkkkkkkkkkOTTTO....",
            "..OIIOTtkkkkkkkkkkkkkkkOtTTO....",
            "..OIYOTtkkkkkkkkkkkkkkkOtTTO....",
            "..OIIOTtkkkkkkkkkkkkkkkOtTTO....",
            "..OIIOTTkkkkkkkkkkkkkkkOTTTO....",
            "..OIIOTTkkkkkkkkkkkkkkkOTTTO....",
            "..OIYOTtkkkkkkkkkkkkkkkOtTTO....",
            "..OIIOTtkkUUUUUUUUUUUkkOtTTO....",
            "..OIIOTTkkkttttttttttkkOTTTO....",
            "..OIIOTTkkkkUUUUUUUUkkkOTTTO....",
            "..OIIOTtkkkkkttttttkkkkOtTTO....",
            "..OOOOTtkkkkkkUUUUkkkkkOtTTO....",
            "...OUTTTkkkkkkkttkkkkkkOTTTUO...",
            "..OUUUUUOOOOOOOOOOOOOOOOUUUUUO..",
            "..OTTTTTTTTTTTTTTTTTTTTTTTTTTO..",
            "..OttttttttttttttttttttttttttO..",
            "..OOOOOOOOOOOOOOOOOOOOOOOOOOOO..",
            "..OOOOOOOOOOOOOOOOOOOOOOOOOOOO..",
            "................................",
            "................................",
        };

        static GateSprites()
        {
            _all.Add(Build(Sealed, SealedPattern));
            _all.Add(Build(Open, OpenPattern));
        }

        private static PixelSprite Build(string name, string[] pattern)
        {
            var pixels = new Rgba32[Size * Size];
            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++) pixels[y * Size + x] = Palette[pattern[y][x]];
            return new PixelSprite(name, Size, Size, pixels);
        }
    }
}
