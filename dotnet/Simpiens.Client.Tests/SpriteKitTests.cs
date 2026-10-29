using System;
using System.Linq;
using Simpiens.Presentation;
using Simpiens.Presentation.Art;
using Simpiens.Simulation.Items;
using Xunit;

namespace Simpiens.Client.Tests
{
    /// <summary>The sprite kit (graphics V3.5): ramps, painting, outlines and icon plates, deterministic to the byte.</summary>
    public class SpriteKitTests
    {
        private static int Luma(Rgba32 c) => c.R * 299 + c.G * 587 + c.B * 114;

        [Theory]
        [InlineData(234, 88, 12)]
        [InlineData(45, 212, 191)]
        [InlineData(250, 204, 21)]
        [InlineData(100, 116, 139)]
        public void ARampRunsLightToDarkAroundItsBase(byte r, byte g, byte b)
        {
            var ramp = SpriteKit.RampOf(new Rgba32(r, g, b));
            Assert.Equal(new Rgba32(r, g, b), ramp.Base);
            Assert.True(Luma(ramp.Highlight) > Luma(ramp.Base) && Luma(ramp.Base) > Luma(ramp.Shade) && Luma(ramp.Shade) > Luma(ramp.Deep), $"{ramp.Highlight} {ramp.Base} {ramp.Shade} {ramp.Deep}");
            Assert.Equal(ramp.Highlight, SpriteKit.RampOf(new Rgba32(r, g, b)).Highlight); // the same every time
        }

        [Fact]
        public void ARampIsPinnedToTheByte()
        {
            var ramp = SpriteKit.RampOf(new Rgba32(234, 88, 12));
            Assert.Equal(Pinned, (ramp.Highlight, ramp.Shade, ramp.Deep));
        }

        [Fact]
        public void AGreyStaysGrey()
        {
            var ramp = SpriteKit.RampOf(new Rgba32(120, 120, 120));
            foreach (var c in new[] { ramp.Highlight, ramp.Shade, ramp.Deep }) Assert.True(c.R == c.G && c.G == c.B, c.ToString());
        }

        [Fact]
        public void HueSurvivesTheRoundTrip()
        {
            foreach (var c in new[] { new Rgba32(255, 0, 0), new Rgba32(0, 255, 0), new Rgba32(0, 0, 255), new Rgba32(255, 0, 255), new Rgba32(10, 200, 90) })
            {
                SpriteKit.ToHsv(c, out int h, out int s, out int v);
                var back = SpriteKit.FromHsv(h, s, v);
                Assert.True(Math.Abs(back.R - c.R) <= 3 && Math.Abs(back.G - c.G) <= 3 && Math.Abs(back.B - c.B) <= 3, $"{c} -> {h},{s},{v} -> {back}");
            }
        }

        [Fact]
        public void PaintingTakesTheTonesItsLettersName()
        {
            var primary = SpriteKit.RampOf(new Rgba32(200, 50, 50));
            var secondary = SpriteKit.RampOf(new Rgba32(50, 50, 200));
            var pixels = SpriteKit.Paint(new[] { "1.a", "4.d" }, primary, secondary, out int w, out int h);
            Assert.Equal((3, 2), (w, h));
            Assert.Equal(primary.Highlight, pixels[0]);
            Assert.Equal(Rgba32.Clear, pixels[1]);
            Assert.Equal(secondary.Highlight, pixels[2]);
            Assert.Equal(primary.Deep, pixels[3]);
            Assert.Equal(secondary.Deep, pixels[5]);
            Assert.Throws<ArgumentException>(() => SpriteKit.Paint(new[] { "1x" }, primary, secondary, out _, out _));
            Assert.Throws<ArgumentException>(() => SpriteKit.Paint(new[] { "11", "1" }, primary, secondary, out _, out _));
        }

        [Fact]
        public void AnOutlineHugsTheSidesNotTheCorners()
        {
            var dot = new[] { new Rgba32(255, 255, 255) };
            var outlined = SpriteKit.Outlined(dot, 1, 1, SpriteKit.Outline, out int w, out int h);
            Assert.Equal((3, 3), (w, h));
            Assert.Equal(new Rgba32(255, 255, 255), outlined[4]);
            foreach (int side in new[] { 1, 3, 5, 7 }) Assert.Equal(SpriteKit.Outline, outlined[side]);
            foreach (int corner in new[] { 0, 2, 6, 8 }) Assert.Equal(Rgba32.Clear, outlined[corner]);
        }

        [Theory]
        [InlineData(ItemRarity.Common)]
        [InlineData(ItemRarity.Rare)]
        [InlineData(ItemRarity.Legendary)]
        public void AnIconIsAPlateWithItsRarityRimAndTheGlyphCentred(ItemRarity rarity)
        {
            var glyph = Enumerable.Repeat(new Rgba32(1, 2, 3), 4).ToArray();
            var icon = SpriteKit.Icon(glyph, 2, 2, rarity);
            Assert.Equal(16 * 16, icon.Length);
            foreach (int corner in new[] { 0, 15, 15 * 16, 16 * 16 - 1 }) Assert.Equal(Rgba32.Clear, icon[corner]);
            Assert.Equal(RarityColours.Of(rarity), icon[8]); // the top rim
            Assert.Equal(RarityColours.Of(rarity), icon[8 * 16]); // the left rim
            Assert.Equal(SpriteKit.Plate, icon[1 * 16 + 1]);
            Assert.Equal(new Rgba32(1, 2, 3), icon[7 * 16 + 7]);
            Assert.Equal(new Rgba32(1, 2, 3), icon[8 * 16 + 8]);
            Assert.Equal(SpriteKit.Plate, icon[6 * 16 + 6]);
            Assert.Throws<ArgumentException>(() => SpriteKit.Icon(new Rgba32[15 * 15], 15, 15, rarity));
        }

        [Fact]
        public void EachRarityHasItsOwnColour()
            => Assert.Equal(5, Enum.GetValues<ItemRarity>().Select(RarityColours.Of).Distinct().Count());

        /// <summary>The ramp of (234, 88, 12), recorded when the kit was written (2026-10-29); a deliberate change re-pins it, with the art pins.</summary>
        private static readonly (Rgba32, Rgba32, Rgba32) Pinned = (new Rgba32(242, 150, 58), new Rgba32(168, 27, 0), new Rgba32(117, 3, 0));
    }
}
