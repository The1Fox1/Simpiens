using System;
using System.Linq;
using Simpiens.Presentation;
using Xunit;

namespace Simpiens.Client.Tests
{
    /// <summary>Isometric in the game (M3 design D8, plan step 1.5; producer P4: isometric by default, V to top-down).</summary>
    public class ViewModeTests
    {
        [Fact]
        public void TheGameStartsIsometric()
        {
            Assert.Same(ViewMode.Isometric, new ClientOptions().View);
            var game = Offscreen.Start(frames: 1);
            Assert.Same(ViewMode.Isometric, game.World.Mode);
            Assert.Same(game.World.Mode.Projection, game.World.Rig.Projection);
            Assert.Same(game.World.Mode.Projection, game.World.Scene.Projection);
            Assert.Same(game.World.Mode.Catalog, game.World.Scene.Catalog);
        }

        [Fact]
        public void VTogglesTheViewKeepingTheCentreAndTheScale()
        {
            var ui = UiDriver.Start();
            var game = ui.Game;
            var hub = game.Session.Simulation.Host.Current.Map.Labels.First(l => l.Text == "Outpost Hub").Position;
            game.World.Rig.ZoomAt(hub, 0.3f);
            game.World.Rig.Pan(hub - game.World.Rig.Position);
            var centre = game.World.Rig.Position;
            float size = game.World.Rig.Size;

            foreach (var next in new[] { ViewMode.TopDown, ViewMode.Isometric })
            {
                ui.Script.Keys = new KeyInput { ViewTogglePressed = true };
                ui.Frame();
                ui.Script.Keys = default;
                Assert.Same(next, game.World.Mode);
                Assert.True((game.World.Rig.Position - centre).Magnitude < 1e-3f, "the same floor point at the centre");
                Assert.Equal(size, game.World.Rig.Size, 4); // the same pixels per art pixel: both catalogues are 16 a unit
            }
        }

        [Theory]
        [InlineData("isometric")]
        [InlineData("top-down")]
        public void TheZoomLimitsHoldAndTheWholeMapFitsAtWholePixels(string mode)
        {
            var view = mode == "isometric" ? ViewMode.Isometric : ViewMode.TopDown;
            var game = Offscreen.Start(new ClientOptions { View = view }, frames: 1, width: 1280, height: 720);
            var rig = game.World.Rig;
            var frame = rig.Frame;

            // Framed at a whole number of screen pixels per art pixel, never below 1:1, on the map's centre
            var framed = rig.ToView(1280, 720);
            Assert.Equal(MathF.Round(framed.Zoom), framed.Zoom, 4);
            Assert.True(framed.Zoom >= 1f);
            Assert.True((rig.Position - frame.Center).Magnitude < 0.5f);

            rig.ZoomAt(frame.Center, 0.01f);
            Assert.Equal(CameraRig.MinSize, rig.Size);
            // The zoom-out limit shows the whole map
            rig.ZoomAt(frame.Center, 1000f);
            Assert.Equal(rig.MaxSize, rig.Size);
            var whole = rig.ToView(1280, 720);
            foreach (var corner in new[] { new Vec2(frame.XMin, frame.YMin), new Vec2(frame.XMax, frame.YMin), new Vec2(frame.XMin, frame.YMax), new Vec2(frame.XMax, frame.YMax) })
            {
                var at = whole.WorldToScreen(corner);
                Assert.InRange(at.X, -0.5f, 1280.5f);
                Assert.InRange(at.Y, -0.5f, 720.5f);
            }

            // A smaller window reframes at whole pixels too
            rig.SetAspect(1000f / 600f);
            rig.SetPixelHeight(600);
            rig.Reframe();
            var reframed = rig.ToView(1000, 600);
            Assert.Equal(MathF.Round(reframed.Zoom), reframed.Zoom, 4);
            Assert.True(reframed.Zoom >= 1f);
        }
    }
}
