using Microsoft.Xna.Framework.Graphics;
using Xunit;

namespace Simpiens.Client.Tests
{
    /// <summary>The client starts with no display and draws (Epic CLIENT M0).</summary>
    public class ClientBootTests
    {
        [Fact]
        public void TheClientIsBuiltOnKni()
        {
            Assert.Equal("KNI", XnaPlatform.Name);
        }

        [Fact]
        public void TheClientRunsOffscreenOnMesasSoftwareRenderer()
        {
            var game = Offscreen.Start(frames: 3);
            Assert.NotNull(game.GraphicsDevice);
            // Pinned so that renders here match CI's pixel for pixel (TestSetup)
            Assert.Contains("llvmpipe", GraphicsAdapter.DefaultAdapter.Description);

            const int width = 64, height = 32;
            byte[] rgba = FrameCapture.Capture(game, width, height);
            Assert.Equal(width * height * 4, rgba.Length);

            // Something other than the background was drawn
            var background = SimpiensGame.Background;
            bool drew = false;
            for (int i = 0; i < width * height && !drew; i++)
            {
                drew = rgba[i * 4] != background.R || rgba[i * 4 + 1] != background.G || rgba[i * 4 + 2] != background.B;
            }
            Assert.True(drew, "the frame is all background");
        }
    }
}
