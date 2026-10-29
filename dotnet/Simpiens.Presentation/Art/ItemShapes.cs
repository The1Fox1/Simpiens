using System.Collections.Generic;

namespace Simpiens.Presentation.Art
{
    /// <summary>
    /// Item silhouettes (graphics V3.6), hand-drawn, one per shape and not per item: an item's look (<see cref="ItemArt"/>)
    /// names its shape and colours, and the sprite kit paints it. 12x12 glyphs (outlined to 14x14); the carried marker is
    /// 6x6 (8x8 outlined). Tones: <c>1</c> highlight to <c>4</c> deep in the item's primary colour, <c>a</c>–<c>d</c> in its
    /// secondary, <c>.</c> clear; light falls from the top left.
    /// </summary>
    public static class ItemShapes
    {
        public const string Chunk = "chunk";
        public const string Sprig = "sprig";
        public const string Pack = "pack";
        public const string Helmet = "helmet";
        public const string Pick = "pick";

        public const int GlyphSize = 12;

        /// <summary>Every glyph, by shape.</summary>
        public static IReadOnlyDictionary<string, string[]> All { get; } = new Dictionary<string, string[]>
        {
            // A lump of rock with flecks of what it holds (ore, glowstone)
            [Chunk] = new[]
            {
                "............",
                "......12....",
                "....11222...",
                "...1222b23..",
                "..122b22223.",
                "..12222b223.",
                ".1222222233.",
                ".12b2222b33.",
                ".22222223333",
                "..2222333334",
                "...33333344.",
                "............",
            },
            // A stem with leaves and a flower (herbs)
            [Sprig] = new[]
            {
                "......ab....",
                ".....abba...",
                "......ab....",
                "..12..3.....",
                ".1223.3.12..",
                ".12233.1223.",
                "..23.3.2233.",
                ".....3..33..",
                "...12.3.....",
                "..12233.....",
                "...23.3.....",
                "......3.....",
            },
            // A wrapped bundle with a band (rations)
            [Pack] = new[]
            {
                "............",
                "...111111...",
                "..12222222..",
                ".122222222..",
                ".12bbbbbb23.",
                ".1bccccccd3.",
                ".12bbbbbb23.",
                ".1222222223.",
                ".1222222233.",
                ".2333333333.",
                "..44444444..",
                "............",
            },
            // A hard hat with its lamp (hazard gear)
            [Helmet] = new[]
            {
                "............",
                "....1112....",
                "...112222...",
                "..12222223..",
                "..12aab223..",
                "..12abb223..",
                "..12222233..",
                ".1222222233.",
                "122222223334",
                ".444444444..",
                "............",
                "............",
            },
            // A pick's head on a wooden handle
            [Pick] = new[]
            {
                "..11122.....",
                ".122..223...",
                "12.....223..",
                "1......a223.",
                "......ab.23.",
                ".....ab...3.",
                "....ab......",
                "...ab.......",
                "..ab........",
                ".ab.........",
                "ab..........",
                "b...........",
            },
        };

        /// <summary>A small bundle, in the carried item's colours: what a crawler shows it carries (V5).</summary>
        public static string[] Bundle { get; } =
        {
            ".1111.",
            "122223",
            "122223",
            "222233",
            "233334",
            ".4444.",
        };
    }
}
