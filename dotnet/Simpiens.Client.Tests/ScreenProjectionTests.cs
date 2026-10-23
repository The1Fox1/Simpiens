using Simpiens.Presentation;
using Xunit;

namespace Simpiens.Client.Tests
{
    /// <summary>Screen space for hard cutscenes (M3 design D9, plan step 3.2).</summary>
    public class ScreenProjectionTests
    {
        [Theory]
        [InlineData(1280, 720, 2f)]
        [InlineData(1920, 1080, 3f)]
        [InlineData(1366, 768, 2f)]
        [InlineData(800, 600, 1f)]
        [InlineData(2560, 1080, 3f)]
        [InlineData(600, 300, 0.8333333f)]
        public void TheScaleStepsWithTheWindowAndTheLetterboxIsCentred(int width, int height, float scale)
        {
            var view = ScreenProjection.Fit(width, height);
            Assert.Equal(scale, view.PixelsPerUnit, 4);
            Assert.Equal(scale, view.Zoom, 4);
            var box = ScreenProjection.Letterbox(view);
            Assert.Equal((width - box.Width) / 2f, box.XMin, 3);
            Assert.Equal((height - box.Height) / 2f, box.YMin, 3);
            Assert.Equal(ScreenProjection.VirtualWidth * scale, box.Width, 3);

            // The virtual screen's corners and centre, y down
            Assert.Equal(new Vec2(box.XMin, box.YMin), view.WorldToScreen(Vec2.Zero));
            Assert.Equal(new Vec2(width / 2f, height / 2f), view.WorldToScreen(new Vec2(320f, 180f)));
            var bottomRight = view.WorldToScreen(new Vec2(640f, 360f));
            Assert.Equal(box.XMax, bottomRight.X, 3);
            Assert.Equal(box.YMax, bottomRight.Y, 3);
            Assert.Equal(new Vec2(100f, 50f).X, view.ScreenToFloor(view.WorldToScreen(new Vec2(100f, 50f))).X, 3);
        }

        [Fact]
        public void ThingsAreDrawnByLayerThenInTheOrderAdded()
        {
            var projection = ScreenProjection.Instance;
            var list = new DrawList();
            list.Add(projection.LayerOf(DrawRole.Crawler), DrawRole.Crawler, projection.Depth(new Vec2(500f, 300f)), 1, 500f, 300f, 16f, 16f, Rgba32.Clear);
            list.Add(projection.LayerOf(DrawRole.Crawler), DrawRole.Crawler, projection.Depth(new Vec2(100f, 100f)), 2, 100f, 100f, 16f, 16f, Rgba32.Clear);
            list.Add(projection.LayerOf(DrawRole.Floor), DrawRole.Floor, projection.Depth(new Vec2(320f, 180f)), 3, 320f, 180f, 640f, 360f, Rgba32.Clear);
            list.Sort();
            Assert.Equal(new[] { 3, 1, 2 }, new[] { list[0].SpriteIndex, list[1].SpriteIndex, list[2].SpriteIndex });
        }
    }
}
