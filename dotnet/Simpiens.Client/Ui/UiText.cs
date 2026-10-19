using Simpiens.Presentation;

namespace Simpiens.Client.Ui
{
    /// <summary>
    /// The dashboard's text styles: Unity's stylesheet sizes, each one of <see cref="FontSet"/>'s baked faces, drawn
    /// 1:1 (Epic CLIENT text cleanup T2). Gum's size is the tallest glyph's height, so these read about like Unity's
    /// CSS sizes one or two pixels smaller.
    /// </summary>
    public readonly struct UiText
    {
        public readonly int Size;
        public readonly bool Bold;

        public UiText(int size, bool bold)
        {
            Size = size;
            Bold = bold;
        }

        public FontFace Face => new FontFace(FontSet.Family, Size, Bold);

        /// <summary>The header's title, window titles, the dossier's name (Unity's 15-16 px bold).</summary>
        public static readonly UiText Title = new UiText(15, true);
        /// <summary>Panel headers (Unity's 14 px bold).</summary>
        public static readonly UiText Heading = new UiText(14, true);
        /// <summary>Body text (Unity's 12-13 px).</summary>
        public static readonly UiText Body = new UiText(13, false);
        public static readonly UiText BodyBold = new UiText(13, true);
        /// <summary>Status lines (Unity's 12 px).</summary>
        public static readonly UiText Status = new UiText(12, false);
        public static readonly UiText StatusBold = new UiText(12, true);
        /// <summary>Descriptions, events, metrics (Unity's 11 px).</summary>
        public static readonly UiText Small = new UiText(11, false);
        public static readonly UiText SmallBold = new UiText(11, true);
        /// <summary>State badges (Unity's 10 px bold).</summary>
        public static readonly UiText Badge = new UiText(10, true);
    }
}
