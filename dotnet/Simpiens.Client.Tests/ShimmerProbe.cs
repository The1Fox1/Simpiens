using System;
using System.Collections.Generic;
using System.Linq;
using Simpiens.Entities;
using Simpiens.Presentation;
using Simpiens.Presentation.Art;
using Xunit;

namespace Simpiens.Client.Tests
{
    /// <summary>
    /// A measurement, not a gate (graphics V2.5, plan item P7): does a crawler's silhouette change shape as it moves by
    /// fractions of a screen pixel? Point sampling is modelled on the CPU as the renderer does it (each screen pixel's
    /// centre takes the texel under it). A silhouette that only shifts is stable; one whose shape changes shimmers.
    /// Run: <c>-method "*ShimmerProbe*" -explicit only -showLiveOutput</c>.
    /// </summary>
    public class ShimmerProbe
    {
        private readonly ITestOutputHelper _output;

        public ShimmerProbe(ITestOutputHelper output) => _output = output;

        [Theory(Explicit = true)]
        [InlineData("top-down")]
        [InlineData("isometric")]
        public void MeasureShimmer(string mode)
        {
            var catalog = mode == "top-down" ? ViewMode.TopDown.Catalog : ViewMode.Isometric.Catalog;
            var art = catalog.Id(ArtKeys.Crawler(AgentVisualState.Walking));
            var sheet = SpriteSheets.Contents(catalog[art].Sheet);
            var sprite = sheet[catalog.Sprite(art, 0)];
            var piece = catalog.Piece(art, 0, 1f, 1f); // no squash: only the motion across pixels
            foreach (int zoom in new[] { 1, 2, 3 })
            {
                float width = piece.Width * zoom, height = piece.Height * zoom;
                var shapes = new HashSet<string>();
                int fewest = int.MaxValue, most = 0;
                for (int step = 0; step < 16; step++)
                {
                    float left = 100f + step / 16f - width * piece.PivotX, top = 100f + step / 16f - height * piece.PivotY;
                    var (shape, count) = Silhouette(sprite, left, top, width, height);
                    shapes.Add(shape);
                    fewest = Math.Min(fewest, count);
                    most = Math.Max(most, count);
                }
                _output.WriteLine($"{mode}, zoom {zoom}: the crawler is {width:0.##}x{height:0.##} px for {sprite.Width}x{sprite.Height} texels; over 16 sub-pixel steps it takes {shapes.Count} shape(s), {fewest}-{most} pixels");
            }
        }

        /// <summary>The opaque screen pixels, relative to the first pixel the quad covers, as a string; and how many.</summary>
        private static (string Shape, int Count) Silhouette(PixelSprite sprite, float left, float top, float width, float height)
        {
            int x0 = (int)MathF.Floor(left), y0 = (int)MathF.Floor(top);
            var lit = new List<string>();
            for (int y = y0; y <= (int)MathF.Ceiling(top + height); y++)
                for (int x = x0; x <= (int)MathF.Ceiling(left + width); x++)
                {
                    float u = (x + 0.5f - left) / width, v = (y + 0.5f - top) / height;
                    if (u < 0f || u >= 1f || v < 0f || v >= 1f) continue;
                    int tx = (int)(u * sprite.Width), ty = (int)(v * sprite.Height);
                    if (sprite.Pixels[ty * sprite.Width + tx].A > 0) lit.Add($"{x - x0},{y - y0}");
                }
            // Relative to the silhouette's own corner, so a pure shift gives the same shape
            int minX = lit.Min(p => int.Parse(p.Split(',')[0])), minY = lit.Min(p => int.Parse(p.Split(',')[1]));
            var shape = string.Join(";", lit.Select(p => p.Split(',')).Select(p => $"{int.Parse(p[0]) - minX},{int.Parse(p[1]) - minY}"));
            return (shape, lit.Count);
        }
    }
}
