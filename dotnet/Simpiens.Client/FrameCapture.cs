using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Simpiens.Client
{
    /// <summary>
    /// Draws a frame of the game into an off-screen target and reads its pixels back: what render tests compare with
    /// their reference images, and what an agent looks at.
    /// </summary>
    public static class FrameCapture
    {
        /// <summary>The whole frame, map and UI, as RGBA bytes, row by row from the top. The UI is laid out for the window: capture at the window's size.</summary>
        public static byte[] Capture(SimpiensGame game, int width, int height) => Read(game, width, height, withUi: true);

        /// <summary>The map alone, without the UI over it (the map's render references).</summary>
        public static byte[] CaptureMap(SimpiensGame game, int width, int height) => Read(game, width, height, withUi: false);

        private static byte[] Read(SimpiensGame game, int width, int height, bool withUi)
        {
            var device = game.GraphicsDevice;
            using var target = new RenderTarget2D(device, width, height);
            device.SetRenderTarget(target);
            if (withUi) game.DrawFrame(width, height);
            else game.DrawScene(width, height);
            device.SetRenderTarget(null);

            var pixels = new Color[width * height];
            target.GetData(pixels);
            var rgba = new byte[pixels.Length * 4];
            for (int i = 0; i < pixels.Length; i++)
            {
                rgba[i * 4] = pixels[i].R;
                rgba[i * 4 + 1] = pixels[i].G;
                rgba[i * 4 + 2] = pixels[i].B;
                rgba[i * 4 + 3] = pixels[i].A;
            }
            return rgba;
        }
    }
}
