using Xunit;

namespace Simpiens.Client.Tests
{
    /// <summary>The client starts with no display and draws (Epic CLIENT M0.1).</summary>
    public class ClientBootTests
    {
        [Fact]
        public void TheClientIsBuiltOnKni()
        {
            Assert.Equal("KNI", XnaPlatform.Name);
        }

        [Fact]
        public void TheClientRunsOffscreenAndClearsToTheBackground()
        {
            var game = Offscreen.Start(frames: 3);
            Assert.NotNull(game.GraphicsDevice);

            const int width = 64, height = 32;
            byte[] rgba = FrameCapture.Capture(game, width, height);

            Assert.Equal(width * height * 4, rgba.Length);
            var background = SimpiensGame.Background;
            for (int i = 0; i < width * height; i++)
            {
                Assert.True(rgba[i * 4] == background.R && rgba[i * 4 + 1] == background.G && rgba[i * 4 + 2] == background.B && rgba[i * 4 + 3] == 255,
                    $"pixel {i % width},{i / width} is ({rgba[i * 4]}, {rgba[i * 4 + 1]}, {rgba[i * 4 + 2]}, {rgba[i * 4 + 3]})");
            }
        }
    }
}
