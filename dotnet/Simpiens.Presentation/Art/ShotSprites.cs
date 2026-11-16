using System.Collections.Generic;

namespace Simpiens.Presentation.Art
{
    /// <summary>
    /// Shots in flight (Epic 10 Part 3, K8.4a): a crawler's stone or bolt, a dark slug with a glint, and a spinner's spit, a
    /// green gob. Small, so they read as things flying between bodies, never as props. Generated on the CPU from patterns, as
    /// <see cref="CaravanSprites"/>; a generator, it reads no table and no sheet.
    /// </summary>
    public static class ShotSprites
    {
        public const int Size = 8;

        public const string ShotName = "Fx_Shot";
        public const string SpitName = "Fx_Spit";

        private static readonly List<PixelSprite> _all = new List<PixelSprite>(2);

        public static IReadOnlyList<PixelSprite> All => _all;

        private static readonly Dictionary<char, Rgba32> Palette = new Dictionary<char, Rgba32>
        {
            { '.', Rgba32.Clear },
            { 'k', new Rgba32(28, 24, 22, 255) },    // outline
            { 'S', new Rgba32(96, 92, 88, 255) },    // the slug
            { 's', new Rgba32(62, 58, 56, 255) },    // its shade
            { 'W', new Rgba32(236, 232, 220, 255) }, // the glint
            { 'G', new Rgba32(150, 206, 72, 255) },  // spit
            { 'g', new Rgba32(96, 150, 44, 255) },   // its shade
            { 'L', new Rgba32(214, 244, 140, 255) }, // its shine
        };

        private static readonly string[] Shot =
        {
            "........",
            "..kkk...",
            ".kWSSk..",
            ".kSSsk..",
            ".kSssk..",
            "..kkk...",
            "........",
            "........",
        };

        private static readonly string[] Spit =
        {
            "........",
            "..kkkk..",
            ".kLGGgk.",
            ".kGGGgk.",
            ".kGgggk.",
            "..kkkk..",
            "...kk...",
            "........",
        };

        static ShotSprites()
        {
            _all.Add(Build(ShotName, Shot));
            _all.Add(Build(SpitName, Spit));
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
