using System;
using System.Collections.Generic;
using System.Linq;
using Simpiens.Presentation;
using Simpiens.Presentation.Art;
using Xunit;

namespace Simpiens.Client.Tests
{
    /// <summary>
    /// The art review sheet (graphics V0): every sprite once, magnified by a whole factor on a checker that shows its
    /// transparency, named under it in the baked Inter, a section per sheet. Written on every run to
    /// render-output/art-sheet.png (the pinned image's run copies it to container-output/render-output/), for a person
    /// or an agent to look at before an art change is pinned (<see cref="ArtPinTests"/>).
    /// </summary>
    public class ArtReviewSheetTests
    {
        private const int SheetWidth = 1200, Margin = 16, Gap = 14, TallestCell = 96;
        private static readonly Rgba32 Background = new Rgba32(30, 33, 40), CheckerLight = new Rgba32(72, 76, 86), CheckerDark = new Rgba32(58, 62, 70);
        private static readonly Rgba32 NameColour = new Rgba32(226, 232, 240), TitleColour = new Rgba32(250, 204, 21);

        [Fact]
        public void WriteTheArtReviewSheet()
        {
            var fonts = FontLibrary.LoadFrom(AppContext.BaseDirectory);
            var names = fonts[FontSet.Regular(10)];
            var titles = fonts[FontSet.Bold(12)];

            // Lay out first: each sheet's title, then its sprites not shown yet, in rows
            var shown = new HashSet<PixelSprite>(ReferenceEqualityComparer.Instance);
            var placed = new List<(PixelSprite Sprite, int Scale, int X, int Y, int LabelX, int LabelY)>();
            var titlesAt = new List<(string Text, int X, int Y)>();
            int y = Margin;
            foreach (var sheet in SpriteSheets.All)
            {
                var sprites = SpriteSheets.Contents(sheet).Where(shown.Add).ToList();
                if (sprites.Count == 0) continue;
                titlesAt.Add(($"{sheet} ({sprites.Count})", Margin, y));
                y += titles.LineHeight + 6;
                int x = Margin, rowHeight = 0;
                foreach (var sprite in sprites)
                {
                    int scale = Math.Max(1, TallestCell / Math.Max(sprite.Width, sprite.Height));
                    int width = sprite.Width * scale, height = sprite.Height * scale;
                    int cellWidth = Math.Max(width, names.Measure(sprite.Name));
                    int cellHeight = height + 4 + names.LineHeight;
                    if (x + cellWidth > SheetWidth - Margin && x > Margin)
                    {
                        x = Margin;
                        y += rowHeight + Gap;
                        rowHeight = 0;
                    }
                    placed.Add((sprite, scale, x + (cellWidth - width) / 2, y, x + (cellWidth - names.Measure(sprite.Name)) / 2, y + height + 4));
                    x += cellWidth + Gap;
                    rowHeight = Math.Max(rowHeight, cellHeight);
                }
                y += rowHeight + Gap * 2;
            }
            int sheetHeight = y + Margin - Gap;

            // Every sprite once, and nothing on top of anything else
            int distinct = SpriteSheets.All.SelectMany(SpriteSheets.Contents).Distinct(ReferenceEqualityComparer.Instance).Count();
            Assert.Equal(distinct, placed.Count);
            var boxes = placed.SelectMany(p => new[]
            {
                (p.X, p.Y, p.Sprite.Width * p.Scale, p.Sprite.Height * p.Scale),
                (p.LabelX, p.LabelY, names.Measure(p.Sprite.Name), names.LineHeight),
            }).ToList();
            for (int a = 0; a < boxes.Count; a++)
                for (int b = a + 1; b < boxes.Count; b++)
                    Assert.False(Overlap(boxes[a], boxes[b]), $"Two things overlap on the art sheet at {boxes[a]} and {boxes[b]}.");

            var canvas = new ReviewCanvas(SheetWidth, sheetHeight, Background);
            foreach (var p in placed)
            {
                canvas.Blit(p.Sprite, p.X, p.Y, p.Scale, (CheckerLight, CheckerDark));
                canvas.Write(names, p.Sprite.Name, p.LabelX, p.LabelY, NameColour);
            }
            foreach (var (text, tx, ty) in titlesAt) canvas.Write(titles, text, tx, ty, TitleColour);
            canvas.Save("art-sheet.png");
        }

        private static bool Overlap((int X, int Y, int W, int H) a, (int X, int Y, int W, int H) b)
            => a.X < b.X + b.W && b.X < a.X + a.W && a.Y < b.Y + b.H && b.Y < a.Y + a.H;
    }
}
