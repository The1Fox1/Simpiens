using System;
using Simpiens.Core.Session.Layouts;
using Simpiens.Presentation;
using Xunit;

namespace Simpiens.Client.Tests
{
    /// <summary>
    /// The isometric projection's maths, before M3 wires it in (plan step 1.6): it fits the same seam as top-down, so the
    /// view, the camera rig and the picker carry it unchanged.
    /// </summary>
    public class IsometricProjectionTests
    {
        private static readonly IsometricProjection Iso = new IsometricProjection();

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        public void FloorPointsRoundTripInEveryOrientation(int quarterTurns)
        {
            var projection = new IsometricProjection(quarterTurns: quarterTurns);
            var view = new View(projection, new ViewCamera(new Vec2(3f, -7f), 64f, 1280, 720));
            var random = new Random(20261020);
            for (int i = 0; i < 1000; i++)
            {
                var p = new Vec2((float)(random.NextDouble() * 100.0 - 50.0), (float)(random.NextDouble() * 100.0 - 50.0));
                var back = projection.ToFloor(projection.ToView(p));
                Assert.True(Vec2.Distance(p, back) < 1e-4f, $"{p} came back as {back}");
                var throughScreen = view.ScreenToFloor(view.WorldToScreen(p));
                Assert.True(Vec2.Distance(p, throughScreen) < 1e-3f, $"{p} came back from the screen as {throughScreen}");
            }
        }

        [Fact]
        public void ATileIsTwoToOneAndHeightLiftsTheSprite()
        {
            var view = new View(Iso, new ViewCamera(Vec2.Zero, Iso.ArtPixelsPerUnit, 800, 600)); // zoom 1: one screen pixel per art pixel
            var origin = view.WorldToScreen(Vec2.Zero);
            Assert.Equal(new Vec2(32f, 16f), view.WorldToScreen(new Vec2(1f, 0f)) - origin); // +x: down and right, half a tile
            Assert.Equal(new Vec2(-32f, 16f), view.WorldToScreen(new Vec2(0f, 1f)) - origin); // +y: down and left
            Assert.Equal(new Vec2(0f, -32f), view.WorldToScreen(Vec2.Zero, 1f) - origin);    // a unit of height: 32 art pixels up
            Assert.Equal(1f, view.Zoom);
        }

        [Fact]
        public void DepthRisesWithScreenYOnTheFloor()
        {
            var view = new View(Iso, new ViewCamera(Vec2.Zero, 32f, 800, 600));
            var random = new Random(7);
            for (int i = 0; i < 1000; i++)
            {
                var a = new Vec2((float)(random.NextDouble() * 60.0 - 30.0), (float)(random.NextDouble() * 60.0 - 30.0));
                var b = new Vec2((float)(random.NextDouble() * 60.0 - 30.0), (float)(random.NextDouble() * 60.0 - 30.0));
                float screen = view.WorldToScreen(b).Y - view.WorldToScreen(a).Y;
                if (MathF.Abs(screen) < 0.01f) continue;
                Assert.Equal(Math.Sign(screen), Math.Sign(Iso.Depth(b) - Iso.Depth(a))); // lower on screen is in front
            }
            Assert.True(Iso.Depth(new Vec2(2f, 2f), 1f) > Iso.Depth(new Vec2(2f, 2f)), "height breaks ties upward");
            Assert.True(Iso.Depth(new Vec2(2f, 2f), 5f) < Iso.Depth(new Vec2(2.1f, 2f)), "but never passes a neighbour in front");
        }

        [Fact]
        public void FramingLandsTheTileGridOnWholePixels()
        {
            var frame = Floor1Layout.Definition.CameraFrame;
            var bounds = Iso.ViewBounds(frame);
            int width = (int)(bounds.Width * Iso.ArtPixelsPerUnit * 1.5f), height = (int)(bounds.Height * Iso.ArtPixelsPerUnit * 1.5f); // room for zoom 1
            var view = View.Framing(Iso, frame, width, height);
            Assert.Equal(Iso.ArtPixelsPerUnit, view.PixelsPerUnit);
            Assert.True(bounds.Width * view.PixelsPerUnit <= width && bounds.Height * view.PixelsPerUnit <= height, "the whole frame is in view");
            for (int x = -3; x <= 3; x++)
            {
                for (int y = -3; y <= 3; y++)
                {
                    var corner = view.WorldToScreen(new Vec2(x + 0.5f, y + 0.5f)); // tile corners sit on half units
                    Assert.Equal(MathF.Round(corner.X), corner.X, 3);
                    Assert.Equal(MathF.Round(corner.Y), corner.Y, 3);
                }
            }
        }

        [Fact]
        public void TheCameraRigCarriesIsometricUnchanged()
        {
            var rig = new CameraRig();
            rig.Configure(Floor1Layout.Definition.CameraFrame, 16f / 9f);
            rig.Projection = Iso;
            var allowed = Iso.ViewBounds(new RectF(rig.Frame.XMin - CameraRig.Margin, rig.Frame.YMin - CameraRig.Margin, rig.Frame.Width + 2f * CameraRig.Margin, rig.Frame.Height + 2f * CameraRig.Margin));

            rig.Reframe();
            Assert.True(rig.Size * 2f >= Iso.ViewBounds(rig.Frame).Height - 1e-3f, "reframing shows the whole projected map");

            rig.ZoomAt(rig.Frame.Center, 0.2f);
            var under = rig.ViewportToWorld(new Vec2(0.25f, 0.7f));
            rig.ZoomAt(under, 0.8f);
            Assert.True(Vec2.Distance(under, rig.ViewportToWorld(new Vec2(0.25f, 0.7f))) < 1e-3f, "zoom keeps the floor point under the cursor");

            var start = rig.Position;
            rig.PanOnScreen(new Vec2(0f, -1f)); // one view unit down the screen, well inside the limits
            Assert.True(Vec2.Distance(Iso.ToView(rig.Position) - Iso.ToView(start), new Vec2(0f, -1f)) < 1e-3f, "a screen pan moves the view along the screen");

            foreach (var push in new[] { new Vec2(1000f, 0f), new Vec2(-1000f, 0f), new Vec2(0f, 1000f), new Vec2(0f, -1000f) })
            {
                rig.PanOnScreen(push);
                var centre = Iso.ToView(rig.Position);
                float halfW = rig.Size * rig.Aspect, halfH = rig.Size;
                Assert.True(centre.X - halfW >= allowed.XMin - 1e-3f && centre.X + halfW <= allowed.XMax + 1e-3f
                    && centre.Y - halfH >= allowed.YMin - 1e-3f && centre.Y + halfH <= allowed.YMax + 1e-3f, $"pan {push} left the projected frame");
            }

        }
    }
}
