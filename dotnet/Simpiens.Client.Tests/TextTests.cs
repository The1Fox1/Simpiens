using System.Linq;
using Simpiens.Core.Session.Layouts;
using Simpiens.Presentation;
using Xunit;

namespace Simpiens.Client.Tests
{
    /// <summary>The pixel font and world text (Epic CLIENT M1.2).</summary>
    public class TextTests
    {
        [Fact]
        public void TheFontCoversPrintableAsciiAndFallsBackToAQuestionMark()
        {
            Assert.Equal(95, PixelFont.Glyphs.Length);
            for (int i = 0; i < PixelFont.Glyphs.Length; i++)
            {
                Assert.Equal(i, PixelFont.Glyphs[i].Index);
                Assert.Equal((5, 7), (PixelFont.Glyphs[i].Width, PixelFont.Glyphs[i].Height));
            }
            Assert.All(PixelFont.Glyph(' ').Pixels, p => Assert.Equal(Rgba32.Clear, p));
            Assert.Contains(PixelFont.Glyph('A').Pixels, p => p.A == 255);
            Assert.Same(PixelFont.Glyph('?'), PixelFont.Glyph('é'));
            Assert.Equal(PixelFont.Glyph('?'), PixelFont.Glyph('\n'));
            // Every visible glyph has ink; no two letters are the same picture
            var letters = Enumerable.Range('!', '~' - '!' + 1).Select(c => PixelFont.Glyph((char)c)).ToList();
            Assert.All(letters, g => Assert.Contains(g.Pixels, p => p.A == 255));
            Assert.Equal(letters.Count, letters.Select(g => string.Concat(g.Pixels.Select(p => p.A > 0 ? '#' : '.'))).Distinct().Count());
        }

        [Fact]
        public void TextIsCentredOnItsPointWithOneItemPerVisibleGlyph()
        {
            var list = new DrawList();
            const float pixel = WorldText.LabelPixel;
            WorldText.Add(list, "Ore Quarry", new Vec2(-16f, 4.6f), pixel, new Rgba32(255, 0, 0), RenderLayer.Labels);

            Assert.Equal(9, list.Count); // 10 characters, one space
            float left = list[0].X - PixelFont.GlyphWidth * pixel * 0.5f;
            float right = list[list.Count - 1].X + PixelFont.GlyphWidth * pixel * 0.5f;
            Assert.Equal(PixelFont.MeasurePixels("Ore Quarry") * pixel, right - left, 4);
            Assert.InRange((left + right) * 0.5f, -16f - pixel, -16f + pixel); // centred, to within the font pixel it snaps to
            Assert.Equal(4.6f, list[0].Y, 1);
            Assert.All(Enumerable.Range(0, list.Count).Select(i => list[i]), item =>
            {
                Assert.Equal(DrawSheet.Font, item.Sheet);
                Assert.Equal(0f, item.X / pixel % 0.5f, 3);        // glyph centres on the font's half-pixel grid
            });
        }

        [Fact]
        public void TheOutpostSceneDrawsItsEightRoomNames()
        {
            using var sim = Simpiens.Hosting.HeadlessSimulation.Create(Simpiens.Simulation.SimulationSettings.DefaultSeed);
            sim.Respawn(Simpiens.Core.Session.OutpostSession.SessionId);
            var scene = new WorldScene();
            scene.Build(sim.Host.Current.Map, sim.Registry.ActiveEntities, 0, 0f);

            var map = Floor1Layout.Definition;
            Assert.Equal(8, map.Labels.Count);
            int glyphs = map.Labels.Sum(l => l.Text.Count(c => c != ' '));
            var labels = Enumerable.Range(0, scene.Items.Count).Select(i => scene.Items[i]).Where(i => DrawSortKey.Layer(i.SortKey) == RenderLayer.Labels).ToList();
            Assert.Equal(glyphs, labels.Count);
            Assert.All(labels, l => Assert.Equal(DrawSheet.Font, l.Sheet));
        }
    }
}
