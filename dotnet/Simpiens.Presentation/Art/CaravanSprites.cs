using System.Collections.Generic;

namespace Simpiens.Presentation.Art
{
    /// <summary>
    /// The trade caravan (Epic 10 Part 2, the stretch, D70): a porter hauling a covered handcart, in two walking frames (the
    /// porter's stride and the wheel's spokes alternate), a cell wide so it sorts as any prop. Generated on the CPU from
    /// patterns, as <see cref="ProceduralSprites"/>; a generator, it reads no table and no sheet.
    /// </summary>
    public static class CaravanSprites
    {
        public const int Size = 16;
        public const int Frames = 2;

        private static readonly List<PixelSprite> _all = new List<PixelSprite>(Frames);

        public static IReadOnlyList<PixelSprite> All => _all;

        public static string Name(int frame) => "Prop_Caravan_" + frame;

        private static readonly Dictionary<char, Rgba32> Palette = new Dictionary<char, Rgba32>
        {
            { '.', Rgba32.Clear },
            { 'k', new Rgba32(40, 30, 24, 255) },    // outline
            { 'C', new Rgba32(222, 206, 160, 255) }, // canvas cover
            { 'c', new Rgba32(184, 164, 116, 255) }, // canvas shade
            { 'R', new Rgba32(176, 62, 48, 255) },   // the trader's red stripe
            { 'W', new Rgba32(132, 86, 46, 255) },   // wood
            { 'w', new Rgba32(92, 58, 30, 255) },    // wood shade
            { 'S', new Rgba32(70, 66, 64, 255) },    // wheel rim
            { 's', new Rgba32(150, 142, 132, 255) }, // spokes
            { 'H', new Rgba32(232, 190, 150, 255) }, // the porter's face and hands
            { 'B', new Rgba32(76, 98, 136, 255) },   // the porter's coat
            { 'b', new Rgba32(54, 70, 100, 255) },   // coat shade
        };

        private static readonly string[] Walk0 =
        {
            "................",
            "...kkkkkkk......",
            "..kCCRCCCCk.....",
            ".kCCCRCCCCCk....",
            ".kccccRccccck...",
            ".kWWWWWWWWWWk.kk",
            ".kwwwwwwwwwwkkHk",
            "..kkkSSSkkkk.kBk",
            "....SsSsS...kBBk",
            "....SSsSS..kBBBk",
            "....SsSsS..kBbBH",
            ".....SSS....kBbk",
            "............kb.k",
            "...........kb...",
            "..........kk.kk.",
            "................",
        };

        private static readonly string[] Walk1 =
        {
            "................",
            "...kkkkkkk......",
            "..kCCRCCCCk.....",
            ".kCCCRCCCCCk....",
            ".kccccRccccck...",
            ".kWWWWWWWWWWk.kk",
            ".kwwwwwwwwwwkkHk",
            "..kkkSSSkkkk.kBk",
            "....SSsSS...kBBk",
            "....SsSsS..kBBBk",
            "....SSsSS..kBbBH",
            ".....SSS....kBbk",
            "............k.bk",
            "............kb.k",
            "...........kk.kk",
            "................",
        };

        static CaravanSprites()
        {
            _all.Add(Build(Name(0), Walk0));
            _all.Add(Build(Name(1), Walk1));
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
