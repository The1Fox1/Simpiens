using System.Collections.Generic;

namespace Simpiens.Presentation.Art
{
    /// <summary>
    /// Furniture (Epic PLACES plan 1, F.5): a table, a stool, a chair and a bench, in warm wood, each a sprite seen from the front
    /// and a little above as the hearth and the cairn are, so one sprite serves both view modes; its foot on row 14 of 16 as a
    /// standing 16-pixel sprite's (the bench is two cells wide). Bunks keep their own sprites (their beds' bunks draw them). A
    /// generator: it reads no table and no sheet.
    /// </summary>
    public static class FurnitureSprites
    {
        public const string Table = "Furniture_Table", Stool = "Furniture_Stool", Chair = "Furniture_Chair", Bench = "Furniture_Bench";

        private static readonly List<PixelSprite> _all = new List<PixelSprite>(4);

        public static IReadOnlyList<PixelSprite> All => _all;

        private static readonly Dictionary<char, Rgba32> Palette = new Dictionary<char, Rgba32>
        {
            { '.', Rgba32.Clear },
            { 'O', new Rgba32(58, 36, 20) },    // outline
            { 'T', new Rgba32(186, 140, 90) },  // a lit top
            { 'G', new Rgba32(160, 118, 76) },  // the top's grain
            { 't', new Rgba32(118, 78, 42) },   // its front edge
            { 'L', new Rgba32(101, 64, 33) },   // legs
        };

        private static readonly string[] TablePattern =
        {
            "................",
            "................",
            "................",
            "................",
            "..OOOOOOOOOOOO..",
            ".OTTTTTTTTTTTTO.",
            ".OTGGTTTTGGTTTO.",
            ".OTTTTTTTTTTTTO.",
            ".OttttttttttttO.",
            "..OOOOOOOOOOOO..",
            "..OLO......OLO..",
            "..OLO......OLO..",
            "..OLO......OLO..",
            "..OLO......OLO..",
            "..OOO......OOO..",
            "................",
        };

        private static readonly string[] StoolPattern =
        {
            "................",
            "................",
            "................",
            "................",
            "................",
            "................",
            "................",
            "................",
            "....OOOOOOOO....",
            "...OTTTTTTTTO...",
            "...OttttttttO...",
            "....OOOOOOOO....",
            ".....OL..LO.....",
            ".....OL..LO.....",
            ".....OO..OO.....",
            "................",
        };

        private static readonly string[] ChairPattern =
        {
            "................",
            "................",
            "................",
            ".....OOOOOO.....",
            ".....OTGGTO.....",
            ".....OTTTTO.....",
            ".....OTGGTO.....",
            ".....OTTTTO.....",
            "....OOOOOOOO....",
            "...OTTTTTTTTO...",
            "...OttttttttO...",
            "....OOOOOOOO....",
            ".....OL..LO.....",
            ".....OL..LO.....",
            ".....OO..OO.....",
            "................",
        };

        private static readonly string[] BenchPattern =
        {
            "................................",
            "................................",
            "................................",
            "................................",
            "................................",
            "................................",
            "................................",
            "................................",
            "..OOOOOOOOOOOOOOOOOOOOOOOOOOOO..",
            ".OTTTTGGTTTTTTTTTTTTTTGGTTTTTTO.",
            ".OttttttttttttttttttttttttttttO.",
            "..OOOOOOOOOOOOOOOOOOOOOOOOOOOO..",
            "...OLO..................OLO.....",
            "...OLO..................OLO.....",
            "...OOO..................OOO.....",
            "................................",
        };

        static FurnitureSprites()
        {
            Add(Table, TablePattern);
            Add(Stool, StoolPattern);
            Add(Chair, ChairPattern);
            Add(Bench, BenchPattern);
        }

        private static void Add(string name, string[] rows)
        {
            int width = rows[0].Length, height = rows.Length;
            var pixels = new Rgba32[width * height];
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    pixels[y * width + x] = Palette[rows[y][x]];
            _all.Add(new PixelSprite(name, width, height, pixels));
        }
    }
}
