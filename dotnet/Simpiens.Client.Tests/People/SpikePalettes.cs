using System.Collections.Generic;
using Simpiens.Presentation;
using Simpiens.Simulation.People;

namespace Simpiens.Client.Tests.People
{
    /// <summary>
    /// Epic UI-Beta B0 spike: a colour for each colour option of <see cref="LookCatalog"/>, by key. Test-only (plan D1):
    /// what passes the producer's look moves into Simpiens.Presentation/Art/People at B2 and B3.
    /// </summary>
    internal static class SpikePalettes
    {
        // Light to deep, evenly spaced in value, warm to neutral in hue; the delvers' two of their own last
        private static readonly Dictionary<string, Rgba32> Skin = new Dictionary<string, Rgba32>
        {
            ["tone-1"] = new Rgba32(255, 224, 199), ["tone-2"] = new Rgba32(241, 200, 166), ["tone-3"] = new Rgba32(227, 179, 140),
            ["tone-4"] = new Rgba32(208, 157, 116), ["tone-5"] = new Rgba32(188, 134, 94), ["tone-6"] = new Rgba32(163, 111, 74),
            ["tone-7"] = new Rgba32(138, 90, 60), ["tone-8"] = new Rgba32(113, 72, 48), ["tone-9"] = new Rgba32(90, 57, 40),
            ["tone-10"] = new Rgba32(68, 44, 32), ["ruddy"] = new Rgba32(201, 118, 92), ["ash"] = new Rgba32(146, 146, 156),
        };

        private static readonly Dictionary<string, Rgba32> Hair = new Dictionary<string, Rgba32>
        {
            ["black"] = new Rgba32(40, 34, 36), ["dark-brown"] = new Rgba32(70, 46, 32), ["brown"] = new Rgba32(106, 70, 44),
            ["light-brown"] = new Rgba32(152, 110, 70), ["auburn"] = new Rgba32(142, 62, 36), ["red"] = new Rgba32(190, 80, 40),
            ["blond"] = new Rgba32(222, 188, 112), ["grey"] = new Rgba32(152, 150, 152), ["white"] = new Rgba32(226, 222, 214),
            ["teal"] = new Rgba32(44, 150, 150), ["violet"] = new Rgba32(124, 72, 172), ["rose"] = new Rgba32(212, 102, 142),
        };

        private static readonly Dictionary<string, Rgba32> Eyes = new Dictionary<string, Rgba32>
        {
            ["brown"] = new Rgba32(96, 58, 34), ["hazel"] = new Rgba32(132, 104, 52), ["green"] = new Rgba32(60, 132, 72),
            ["blue"] = new Rgba32(64, 112, 196), ["grey"] = new Rgba32(118, 132, 146), ["amber"] = new Rgba32(196, 134, 40),
        };

        private static readonly Dictionary<string, Rgba32> Clothes = new Dictionary<string, Rgba32>
        {
            ["ochre"] = new Rgba32(196, 148, 52), ["rust"] = new Rgba32(166, 76, 42), ["moss"] = new Rgba32(98, 122, 60),
            ["teal"] = new Rgba32(52, 122, 122), ["slate"] = new Rgba32(92, 106, 126), ["navy"] = new Rgba32(46, 62, 112),
            ["plum"] = new Rgba32(112, 60, 104), ["wine"] = new Rgba32(128, 38, 56), ["sand"] = new Rgba32(204, 184, 142),
            ["umber"] = new Rgba32(112, 82, 56), ["charcoal"] = new Rgba32(62, 62, 70), ["cream"] = new Rgba32(226, 216, 190),
        };

        public static Rgba32 SkinOf(in CrawlerLook look) => Skin[LookCatalog.KeyOf(LookGene.SkinTone, look.SkinTone)];
        public static Rgba32 HairOf(in CrawlerLook look) => Hair[LookCatalog.KeyOf(LookGene.HairColour, look.HairColour)];
        public static Rgba32 EyesOf(in CrawlerLook look) => Eyes[LookCatalog.KeyOf(LookGene.EyeColour, look.EyeColour)];
        public static Rgba32 PrimaryOf(in CrawlerLook look) => Clothes[LookCatalog.KeyOf(LookGene.ClothesPrimary, look.ClothesPrimary)];
        public static Rgba32 SecondaryOf(in CrawlerLook look) => Clothes[LookCatalog.KeyOf(LookGene.ClothesSecondary, look.ClothesSecondary)];

        /// <summary>Every colour option has a colour here (the sheet would throw on a missing key).</summary>
        public static bool CoversCatalogue()
        {
            foreach (var o in LookCatalog.Options(LookGene.SkinTone)) if (!Skin.ContainsKey(o.Key)) return false;
            foreach (var o in LookCatalog.Options(LookGene.HairColour)) if (!Hair.ContainsKey(o.Key)) return false;
            foreach (var o in LookCatalog.Options(LookGene.EyeColour)) if (!Eyes.ContainsKey(o.Key)) return false;
            foreach (var o in LookCatalog.Options(LookGene.ClothesPrimary)) if (!Clothes.ContainsKey(o.Key)) return false;
            return true;
        }
    }
}
