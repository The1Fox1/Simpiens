namespace Simpiens.Presentation
{
    public enum TextAnchor
    {
        /// <summary>The text is centred on the point, both ways (Unity's <c>TextAnchor.MiddleCenter</c>).</summary>
        MiddleCenter,
        /// <summary>The text sits on the point, centred across it (Unity's <c>TextAnchor.LowerCenter</c>).</summary>
        LowerCenter,
    }

    /// <summary>
    /// Text in the world in the <see cref="PixelFont"/> (Epic CLIENT M1.2): one draw item per glyph, sized in world units.
    /// The text's corner snaps to its own pixel grid, so with the camera on whole pixels every glyph keeps its shape.
    /// </summary>
    public static class WorldText
    {
        /// <summary>Map labels and other world text: one font pixel is an eighth of a cell (two screen pixels at zoom 1).</summary>
        public const float LabelPixel = 0.125f;

        public static void Add(DrawList list, string text, Vec2 at, float pixelSize, Rgba32 color, int layer, TextAnchor anchor = TextAnchor.MiddleCenter)
        {
            if (string.IsNullOrEmpty(text)) return;
            float width = PixelFont.MeasurePixels(text) * pixelSize;
            float height = PixelFont.GlyphHeight * pixelSize;
            float left = Snap(at.X - width * 0.5f, pixelSize);
            float bottom = Snap(anchor == TextAnchor.LowerCenter ? at.Y : at.Y - height * 0.5f, pixelSize);
            float centreY = bottom + height * 0.5f;
            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] == ' ') continue;
                float centreX = left + (i * PixelFont.Advance + PixelFont.GlyphWidth * 0.5f) * pixelSize;
                list.Add(layer, 0f, PixelFont.Glyph(text[i]).Index, centreX, centreY, PixelFont.GlyphWidth * pixelSize, height, color, 0f, DrawSheet.Font);
            }
        }

        private static float Snap(float value, float step) => SimMath.Round(value / step) * step;
    }
}
