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
        public const string Flask = "flask";
        public const string Mask = "mask";
        public const string Vest = "vest";
        public const string Club = "club";
        public const string Blade = "blade";
        public const string Mail = "mail";
        public const string Pelt = "pelt";
        public const string Plates = "plates";
        public const string Sling = "sling";
        public const string Crossbow = "crossbow";
        public const string Book = "book";
        public const string Mug = "mug";
        public const string Bowl = "bowl";

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
            // A round flask with a cork, full (draughts)
            [Flask] = new[]
            {
                "....abb.....",
                "....bbc.....",
                "....1223....",
                "....1223....",
                "...112233...",
                "..11222233..",
                ".1112222333.",
                ".1122222233.",
                ".1222222333.",
                "..22223333..",
                "...333444...",
                "............",
            },
            // A breathing mask with its two filters (respirators)
            [Mask] = new[]
            {
                "............",
                "............",
                "...111122...",
                "..12222223..",
                ".aab2222baa.",
                "abbb2332bbbc",
                "abbc2332cbbc",
                ".bcc2222ccc.",
                "...223333...",
                "....3344....",
                "............",
                "............",
            },
            // A studded club, its head at the top right (the cudgel; Epic 10 Part 3, K3.1)
            [Club] = new[]
            {
                "......1112..",
                ".....112223.",
                ".....12a2b3.",
                "....1222233.",
                "....12b2a33.",
                "...12223334.",
                "...a23334...",
                "..ab.44.....",
                ".ab.........",
                "ab..........",
                "b...........",
                "............",
            },
            // A leather pouch holding a stone, on two cords looped at the top left (the sling; Epic 10 Part 3, K8.1)
            [Sling] = new[]
            {
                ".aa.........",
                "a..a........",
                "a...a.......",
                ".a...a......",
                "..b...a.....",
                "...b...a....",
                "....b...a...",
                ".....b.1122.",
                "......12223.",
                "......12334.",
                ".......334..",
                "............",
            },
            // A steel prod bowed across the top, its string, and a wooden stock down to the trigger (the crossbow; K8.1)
            // A closed book standing a little turned, its cover in the primary colour, a band and clasp in the secondary, the
            // pages' edge light (the manuals, Epic 11, S3.2)
            [Book] = new[]
            {
                "............",
                "..1111111...",
                "..1222222a..",
                "..12a2222ab.",
                "..12aa222ab.",
                "..1222222ab.",
                "..1bbbbbbab.",
                "..1222222ab.",
                "..1222222ab.",
                "..1222222ab.",
                "..33333333b.",
                "...dddddddd.",
            },
            // A tankard with its foam and handle (the tavern's ale, Epic LIFE-1 plan 2, T.3)
            [Mug] = new[]
            {
                "............",
                "..abbba.....",
                ".abbbbbba...",
                ".12222223...",
                ".122222233..",
                ".12222223.3.",
                ".12222223.3.",
                ".122222233..",
                ".12222223...",
                ".12222223...",
                "..3333334...",
                "............",
            },
            // A bowl of stew (the tavern's supper, T.3)
            [Bowl] = new[]
            {
                "............",
                "............",
                "............",
                "............",
                "..bbbbbbbb..",
                ".1bbcbbcbb3.",
                ".1222222223.",
                "..12222223..",
                "...122223...",
                "....3333....",
                "...334433...",
                "............",
            },
            [Crossbow] = new[]
            {
                "1..........3",
                ".12......23.",
                "..12aaaa23..",
                "....2ab3....",
                ".....ab.....",
                ".....ab.....",
                ".....ab.....",
                ".....ab.....",
                "....abbc....",
                "....bccd....",
                ".....cd.....",
                "............",
            },
            // A straight blade, point at the top right, a crossguard and a wrapped grip (the iron blade, the glowstone edge)
            [Blade] = new[]
            {
                "..........11",
                ".........123",
                "........123.",
                ".......123..",
                "......123...",
                ".....123....",
                "..a.123.....",
                "...a23......",
                "...ba.......",
                "..bc.a......",
                ".bc.........",
                "cd..........",
            },
            // A coat of rings with short sleeves, the rings in dots (the ring coat, worn on the body; K3.2)
            [Mail] = new[]
            {
                "............",
                "..12....23..",
                ".1222..2333.",
                "122a2222a334",
                "12a2a22a2a34",
                ".22a2a2a2a3.",
                "..2a2a2a23..",
                "..22a2a2a3..",
                "..2a2a2a23..",
                "..22a2a2a3..",
                "..23333334..",
                "............",
            },
            // A stretched pelt, its legs out at the corners (a hide; Epic 10 Part 3, K4.2)
            [Pelt] = new[]
            {
                "............",
                ".1........2.",
                "..11122222..",
                "..12222223..",
                ".122a22a233.",
                ".12222222233",
                ".1222a222233",
                "..12222223..",
                "..12222233..",
                "..22333333..",
                ".2........3.",
                "............",
            },
            // Three overlapping shell plates (chitin; K4.2)
            [Plates] = new[]
            {
                "............",
                "....1112....",
                "...112223...",
                "..1122a223..",
                "..1222a2233.",
                ".11122222334",
                ".122a222334.",
                ".122a222334.",
                "..1222233...",
                "...223334...",
                "....334.....",
                "............",
            },
            // A sleeveless suit, open at the neck, with two reflective bands (hazard gear, worn on the body; Epic 10 Part 3, K1.5)
            [Vest] = new[]
            {
                "............",
                "...12..23...",
                "..122..233..",
                ".1222..2333.",
                ".12222.2333.",
                ".1222222333.",
                ".abbbbbbccd.",
                ".1222222333.",
                ".1222222333.",
                ".abbbbbbccd.",
                ".2333333334.",
                "............",
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
