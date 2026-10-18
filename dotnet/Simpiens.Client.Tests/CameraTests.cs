using Simpiens.Core.Session.Layouts;
using Simpiens.Presentation;
using Xunit;

namespace Simpiens.Client.Tests
{
    /// <summary>The camera rig, ported with Unity's CameraRigValidator checks (Epic CLIENT M1.4).</summary>
    public class CameraRigTests
    {
        private const float Eps = 1e-3f;
        private const float Aspect = 16f / 9f;

        private static CameraRig Floor1Rig(int pixelHeight = 0)
        {
            var rig = new CameraRig();
            rig.Configure(Floor1Layout.Definition.CameraFrame, Aspect, pixelHeight);
            return rig;
        }

        private static bool Near(Vec2 a, Vec2 b) => Vec2.Distance(a, b) < 1e-3f;

        [Fact]
        public void ReframeFitsTheWholeMap()
        {
            var rig = Floor1Rig();
            var frame = rig.Frame;
            Assert.True(Near(rig.Position, frame.Center), $"Reframe centres on the map ({rig.Position} vs {frame.Center}).");
            Assert.True(rig.Size * 2f >= frame.Height - Eps && rig.Size * 2f * Aspect >= frame.Width - Eps, "Reframe shows the whole frame.");
            Assert.Equal(rig.MaxSize, rig.Size, 4);
            rig.ZoomAt(new Vec2(5f, 3f), 0.3f);
            rig.Pan(new Vec2(4f, 4f));
            rig.Reframe();
            Assert.True(Near(rig.Position, frame.Center) && SimMath.Approximately(rig.Size, rig.MaxSize), "Reframe restores the whole view after zoom and pan.");
        }

        [Fact]
        public void TheViewNeverLeavesTheFramePlusTheMargin()
        {
            var rig = Floor1Rig();
            var frame = rig.Frame;
            rig.ZoomAt(frame.Center, 0.01f);
            Assert.Equal(CameraRig.MinSize, rig.Size, 4);
            var pushes = new[] { new Vec2(1000f, 0f), new Vec2(-1000f, 0f), new Vec2(0f, 1000f), new Vec2(0f, -1000f) };
            foreach (var push in pushes)
            {
                rig.Pan(push);
                float halfW = rig.Size * Aspect, halfH = rig.Size;
                Assert.True(rig.Position.X - halfW >= frame.XMin - CameraRig.Margin - Eps && rig.Position.X + halfW <= frame.XMax + CameraRig.Margin + Eps
                    && rig.Position.Y - halfH >= frame.YMin - CameraRig.Margin - Eps && rig.Position.Y + halfH <= frame.YMax + CameraRig.Margin + Eps,
                    $"Pan {push}: the view leaves the frame plus the margin.");
            }
            rig.Pan(new Vec2(1000f, 1000f));
            Assert.Equal(frame.XMax + CameraRig.Margin - rig.Size * Aspect, rig.Position.X, 3);
            Assert.Equal(frame.YMax + CameraRig.Margin - rig.Size, rig.Position.Y, 3);
        }

        [Fact]
        public void ZoomKeepsTheCursorPointFixedAndStaysWithinItsLimits()
        {
            var rig = Floor1Rig();
            var cursor = new Vec2(5f, 3f);
            var before = (cursor - rig.Position) * (1f / rig.Size);
            rig.ZoomAt(cursor, 0.5f);
            var after = (cursor - rig.Position) * (1f / rig.Size);
            Assert.True(Near(before, after), $"Zooming in keeps the cursor's world point under it ({before} vs {after}).");
            Assert.Equal(rig.MaxSize * 0.5f, rig.Size, 4);
            rig.ZoomAt(cursor, 100f);
            Assert.Equal(rig.MaxSize, rig.Size, 4);
            rig.ZoomAt(cursor, 0f);
            rig.ZoomAt(cursor, -1f);
            Assert.Equal(rig.MaxSize, rig.Size, 4);
            rig.ZoomAt(cursor, 0.5f);
            var p = rig.Position;
            rig.ZoomAt(cursor, 0.5f);
            rig.ZoomAt(cursor, 2f);
            Assert.True(Near(rig.Position, p), "Zoom in then out about the same point returns to the same view.");
        }

        [Fact]
        public void FollowConvergesKeepsUpAndIsFrameRateIndependent()
        {
            var rig = Floor1Rig();
            rig.ZoomAt(rig.Frame.Center, 0.2f);
            var target = new Vec2(4f, -8f); // the Bunkhouse
            float previous = Vec2.Distance(rig.Position, target);
            for (int i = 0; i < 120; i++)
            {
                rig.Follow(target, 1f / 60f);
                float d = Vec2.Distance(rig.Position, target);
                Assert.True(d <= previous + Eps, $"Follow moved away from the target at step {i}.");
                previous = d;
            }
            Assert.True(previous < 0.05f, $"Follow reaches the target within 2 s ({previous} away).");

            var a = Floor1Rig(); a.ZoomAt(a.Frame.Center, 0.2f);
            var b = Floor1Rig(); b.ZoomAt(b.Frame.Center, 0.2f);
            a.Follow(target, 0.5f);
            for (int i = 0; i < 30; i++) b.Follow(target, 1f / 60f);
            Assert.True(Vec2.Distance(a.Position, b.Position) < 0.01f, "Follow is frame-rate independent.");

            var m = Floor1Rig();
            m.ZoomAt(m.Frame.Center, 0.2f);
            m.ResetFollow();
            var walker = new Vec2(-10f, 0f);
            for (int i = 0; i < 60; i++)
            {
                walker += new Vec2(20f / 60f, 0f);
                m.Follow(walker, 1f / 60f);
            }
            Assert.True(Vec2.Distance(m.Position, walker) < 0.05f, "Follow keeps up with a target moving 20 units/s.");
        }

        [Fact]
        public void ANewScreenShapeKeepsTheViewWithinTheLimits()
        {
            var rig = Floor1Rig();
            rig.SetAspect(1f);
            Assert.True(rig.MaxSize * 2f >= rig.Frame.Width - Eps, "A narrower screen raises the whole-map size to fit the width.");
            rig.Reframe();
            Assert.True(rig.Size * 2f * rig.Aspect >= rig.Frame.Width - Eps);
            rig.SetAspect(3f);
            Assert.True(rig.Size <= rig.MaxSize + Eps && rig.Size >= CameraRig.MinSize - Eps);
        }

        [Fact]
        public void WithAPixelHeightReframeShowsTheWholeMapAtWholePixelsPerArtPixel()
        {
            var rig = new CameraRig();
            rig.Configure(Floor1Layout.Definition.CameraFrame, 800f / 600f, 600);
            Assert.True(rig.Size >= rig.FitSize - Eps, "The whole map is still visible.");
            var camera = rig.ToCamera(800, 600);
            Assert.Equal(0f, camera.PixelsPerUnit % TopDownCamera.BasePixelsPerUnit, 3);
            rig.ZoomAt(rig.Frame.Center, 10f);
            Assert.Equal(rig.MaxSize, rig.Size, 4); // that crisp view is the zoom-out limit
        }
    }

    /// <summary>The camera controls on input snapshots (Epic CLIENT M1.4): Unity's CameraController rules.</summary>
    public class CameraInputTests
    {
        private static (CameraRig rig, CameraInput input) Setup()
        {
            var rig = new CameraRig();
            rig.Configure(Floor1Layout.Definition.CameraFrame, 800f / 600f, 600);
            return (rig, new CameraInput());
        }

        private static CameraInputResult Step(CameraInput input, CameraRig rig, PointerInput pointer, KeyInput keys = default, float dt = 1f / 60f)
            => input.Update(pointer, keys, dt, rig, rig.ToCamera(800, 600));

        [Fact]
        public void ScrollZoomsTowardTheCursor()
        {
            var (rig, input) = Setup();
            var at = new Vec2(200f, 150f);
            var worldUnder = rig.ToCamera(800, 600).ScreenToWorld(at);
            float size = rig.Size;
            Step(input, rig, new PointerInput { Position = at, WheelNotches = 1 });
            Assert.Equal(size * CameraInput.ZoomStep, rig.Size, 3);
            var stillUnder = rig.ToCamera(800, 600).ScreenToWorld(at);
            Assert.True(Vec2.Distance(worldUnder, stillUnder) < 0.1f, $"the point under the cursor moved ({worldUnder} -> {stillUnder})");

            Step(input, rig, new PointerInput { Position = at, WheelNotches = 1, OverUi = true });
            Assert.Equal(size * CameraInput.ZoomStep, rig.Size, 3); // over a panel the map ignores the wheel
        }

        [Fact]
        public void AShortLeftClickSelectsAndADragDoesNot()
        {
            var (rig, input) = Setup();
            var p = new Vec2(400f, 300f);
            Step(input, rig, new PointerInput { Position = p, Left = true });
            var click = Step(input, rig, new PointerInput { Position = p + new Vec2(3f, 2f) });
            Assert.True(click.Clicked);
            Assert.True(Vec2.Distance(rig.ToCamera(800, 600).ScreenToWorld(p + new Vec2(3f, 2f)), click.ClickWorld) < 1e-3f);

            Step(input, rig, new PointerInput { Position = p, Left = true });
            Step(input, rig, new PointerInput { Position = p + new Vec2(20f, 0f), Left = true }); // beyond the 6 px slop
            Assert.False(Step(input, rig, new PointerInput { Position = p + new Vec2(20f, 0f) }).Clicked);
        }

        [Fact]
        public void RightDragGrabsTheMapAndStopsFollowing()
        {
            var (rig, input) = Setup();
            rig.ZoomAt(rig.Frame.Center, 0.3f);
            var start = rig.Position;
            Step(input, rig, new PointerInput { Position = new Vec2(400f, 300f), Right = true });
            var result = Step(input, rig, new PointerInput { Position = new Vec2(420f, 300f), Right = true });
            Assert.True(result.StopFollowing);
            Assert.True(rig.Position.X < start.X, "dragging right moves the view left (the map follows the pointer)");

            float y = rig.Position.Y;
            Step(input, rig, new PointerInput { Position = new Vec2(420f, 280f), Right = true }); // pointer up 20 px
            Assert.True(rig.Position.Y < y, "dragging up moves the view down (the map follows the pointer)");
        }

        [Fact]
        public void KeysPanHomeReframesAndEscStopsFollowing()
        {
            var (rig, input) = Setup();
            rig.ZoomAt(rig.Frame.Center, 0.3f);
            var start = rig.Position;
            var result = Step(input, rig, default, new KeyInput { Right = true, Up = true }, dt: 0.5f);
            Assert.True(result.StopFollowing);
            Assert.True(rig.Position.X > start.X && rig.Position.Y > start.Y);

            Step(input, rig, default, new KeyInput { HomePressed = true });
            Assert.Equal(rig.MaxSize, rig.Size, 4);
            Assert.True(Vec2.Distance(rig.Position, rig.Frame.Center) < 1e-3f);

            Assert.True(Step(input, rig, default, new KeyInput { EscapePressed = true }).StopFollowing);
            Assert.False(Step(input, rig, default).StopFollowing);
        }
    }

    /// <summary>The devices themselves: offscreen the window never has focus, so the game never reads them on its own.</summary>
    public class ClientInputTests
    {
        [Fact]
        public void TheMouseAndKeyboardReadIntoSnapshotsOnARunningGame()
        {
            Offscreen.Start(frames: 1);
            var input = new ClientInput();
            input.Read(out var pointer, out var keys);
            input.Read(out var again, out _);

            Assert.Equal(0, pointer.WheelNotches); // the first read sets the wheel's origin
            Assert.Equal(0, again.WheelNotches);
            Assert.False(keys.HomePressed || keys.EscapePressed);
            Assert.False(pointer.OverUi);
        }
    }
}
