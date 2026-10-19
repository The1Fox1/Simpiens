using System;
using System.Collections.Generic;
using System.Globalization;

namespace Simpiens.Presentation
{
    /// <summary>One baked font: a family at a pixel size, regular or bold.</summary>
    public readonly struct FontFace : IEquatable<FontFace>
    {
        public readonly string Family;
        /// <summary>Gum's size: the height of the tallest glyph in pixels ("match character height").</summary>
        public readonly int Size;
        public readonly bool Bold;

        public FontFace(string family, int size, bool bold)
        {
            Family = family;
            Size = size;
            Bold = bold;
        }

        /// <summary>Where the atlas lives under the content folder, by Gum's own naming rule.</summary>
        public string CacheFileName => FontSet.CacheFileName(this);

        public bool Equals(FontFace other) => Family == other.Family && Size == other.Size && Bold == other.Bold;
        public override bool Equals(object obj) => obj is FontFace other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(Family, Size, Bold);
        public override string ToString() => $"{Family} {Size}{(Bold ? " bold" : string.Empty)}";
    }

    /// <summary>
    /// The client's fonts (Epic CLIENT text cleanup): every face is baked offline into a BMFont atlas at its exact
    /// pixel size (dotnet/Simpiens.FontBaker) and drawn 1:1, never scaled. The UI and the map's labels share them. A
    /// change here is a re-bake, committed on its own: the atlases are checked against a fresh bake.
    /// </summary>
    public static class FontSet
    {
        /// <summary>Inter (SIL OFL 1.1), a typeface made for screens at small sizes (producer, 2026-10-18).</summary>
        public const string Family = "Inter";

        /// <summary>Under the game's folder: Gum looks here (its FileManager's "Content/") for "FontCache/...".</summary>
        public const string ContentFolder = "Content";

        /// <summary>Body text.</summary>
        public static readonly int[] RegularSizes = { 10, 11, 12, 13 };

        /// <summary>Titles, buttons and badges (10 to 16), and the map's labels as they grow with the zoom (up to 32).</summary>
        public static readonly int[] BoldSizes = { 10, 11, 12, 13, 14, 15, 16, 20, 24, 28, 32 };

        /// <summary>The characters baked: ASCII and Latin-1 (Gum's default ranges), enough for names with accents.</summary>
        public const string Ranges = "32-126,160-255";

        private static readonly FontFace[] _all = Build();

        public static IReadOnlyList<FontFace> All => _all;

        public static FontFace Regular(int size) => new FontFace(Family, size, bold: false);
        public static FontFace Bold(int size) => new FontFace(Family, size, bold: true);

        /// <summary>
        /// Gum's cache name for a smooth font with no outline, italic or shadow (BmfcSave.GetFontCacheFileNameFor):
        /// "FontCache/Font{size}{family}[_Bold].fnt", spaces in the family as underscores.
        /// </summary>
        public static string CacheFileName(FontFace face)
            => "FontCache/Font" + face.Size.ToString(CultureInfo.InvariantCulture) + face.Family.Replace(' ', '_') + (face.Bold ? "_Bold" : string.Empty) + ".fnt";

        private static FontFace[] Build()
        {
            var faces = new List<FontFace>(RegularSizes.Length + BoldSizes.Length);
            foreach (int size in RegularSizes) faces.Add(Regular(size));
            foreach (int size in BoldSizes) faces.Add(Bold(size));
            return faces.ToArray();
        }
    }
}
