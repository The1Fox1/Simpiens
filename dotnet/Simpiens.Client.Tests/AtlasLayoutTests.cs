using System;
using System.Linq;
using Simpiens.Presentation;
using Simpiens.Presentation.Art;
using Xunit;

namespace Simpiens.Client.Tests
{
    /// <summary>The packer (graphics V3.2): every sprite in its cell with its gutter, none overlapping, the same every time, within the texture budget.</summary>
    public class AtlasLayoutTests
    {
        [Theory]
        [InlineData(AtlasOrder.TallestFirst)]
        [InlineData(AtlasOrder.Reversed)]
        public void EverySpriteHasItsOwnCellWithItsGutterAndEverySheetIsWithinTheBudget(AtlasOrder order)
        {
            foreach (var sheet in SpriteSheets.All)
            {
                var sprites = SpriteSheets.Contents(sheet);
                int gutter = SpriteSheets.Smooth(sheet) ? 0 : 2;
                var layout = AtlasLayout.Pack(sprites, gutter, order);
                Assert.InRange(layout.Width, 1, AtlasLayout.MaxTextureSize);
                Assert.InRange(layout.Height, 1, AtlasLayout.MaxTextureSize);
                for (int i = 0; i < sprites.Count; i++)
                {
                    var a = layout[i];
                    Assert.Equal((sprites[i].Width, sprites[i].Height), (a.Width, a.Height));
                    Assert.True(a.X >= 0 && a.Y >= 0 && a.X + a.Width <= layout.Width && a.Y + a.Height <= layout.Height, $"{sheet} {sprites[i].Name} leaves the texture.");
                    for (int j = i + 1; j < sprites.Count; j++)
                    {
                        var b = layout[j];
                        bool apart = a.X + a.Width + gutter <= b.X || b.X + b.Width + gutter <= a.X || a.Y + a.Height + gutter <= b.Y || b.Y + b.Height + gutter <= a.Y;
                        Assert.True(apart, $"{sheet}: {sprites[i].Name} and {sprites[j].Name} are closer than the gutter.");
                    }
                }
            }
        }

        [Fact]
        public void TheSamePacksTheSameWay()
        {
            var sprites = SpriteSheets.Contents(SpriteSheets.All[0]);
            var a = AtlasLayout.Pack(sprites);
            var b = AtlasLayout.Pack(sprites);
            Assert.Equal((a.Width, a.Height), (b.Width, b.Height));
            for (int i = 0; i < sprites.Count; i++) Assert.Equal((a[i].X, a[i].Y), (b[i].X, b[i].Y));
        }

        [Fact]
        public void ASingleSmoothSpriteIsItsOwnTextureAndTooBigIsRefused()
        {
            var fog = AtlasLayout.Pack(new[] { FogCloud.Sprite }, gutter: 0);
            Assert.Equal((FogCloud.Size, FogCloud.Size, 0, 0), (fog.Width, fog.Height, fog[0].X, fog[0].Y));
            var wide = new PixelSprite("Wide", AtlasLayout.MaxTextureSize + 1, 1, new Rgba32[AtlasLayout.MaxTextureSize + 1]);
            Assert.Throws<InvalidOperationException>(() => AtlasLayout.Pack(new[] { wide, wide }.Select((s, i) => i == 0 ? s : new PixelSprite("Wide2", s.Width, 1, s.Pixels)).ToList()));
        }
    }

    /// <summary>
    /// The picture doesn't depend on how the atlas is packed (graphics V3.2, epic F12): the same frame drawn with every
    /// sheet packed tallest first and packed in reverse is the same, byte for byte, at the framing. At an in-between zoom
    /// a pixel centre can still fall within a GPU's float error of a texel's edge (a generic near-tie, measured in
    /// <see cref="SamplingTests.MeasureTies"/>), so a few pixels may differ there: 1 of 480,000 at isometric's 2.5x, where
    /// the zoomed top-down view moved 15,316 before the sampling rules.
    /// </summary>
    public class AtlasIndependenceTests
    {
        private const int Width = 800, Height = 600;

        /// <summary>Pixels an in-between zoom may change with the layout: generic near-ties, a few in a frame.</summary>
        private const int NearTies = 4;

        [Theory]
        [InlineData("top-down", 1f)]
        [InlineData("isometric", 1f)]
        [InlineData("top-down", 0.4f)]
        [InlineData("isometric", 0.4f)]
        public void ThePictureDoesNotDependOnTheAtlasLayout(string mode, float zoom)
        {
            var game = Offscreen.Start(new ClientOptions { View = mode == "top-down" ? ViewMode.TopDown : ViewMode.Isometric, TicksPerUpdate = 100 }, frames: 3);
            if (zoom != 1f)
            {
                var hub = game.Session.Simulation.Host.Current.Map.Labels.First(l => l.Text == "Outpost Hub").Position;
                game.World.Rig.ZoomAt(hub, zoom);
            }
            var packed = FrameCapture.CaptureMap(game, Width, Height);
            using var reversed = new ClientGraphics(game.GraphicsDevice, AtlasOrder.Reversed);
            game.World.LoadContent(reversed);
            byte[] other;
            try { other = FrameCapture.CaptureMap(game, Width, Height); }
            finally { game.World.LoadContent(game.Graphics); }
            int differing = 0;
            for (int i = 0; i < packed.Length; i += 4)
                if (packed[i] != other[i] || packed[i + 1] != other[i + 1] || packed[i + 2] != other[i + 2] || packed[i + 3] != other[i + 3]) differing++;
            int allowed = zoom == 1f ? 0 : NearTies;
            Assert.True(differing <= allowed, $"{mode} at {game.World.View.Zoom}x: {differing} pixels changed with the atlas's layout (allowed {allowed}).");
        }
    }
}
