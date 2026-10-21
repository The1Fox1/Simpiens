namespace Simpiens.Presentation
{
    /// <summary>The pointer this frame: screen pixels from the top-left, buttons held, wheel notches since the last frame.</summary>
    public struct PointerInput
    {
        public Vec2 Position;
        public bool Left;
        public bool Right;
        public bool Middle;
        /// <summary>Positive: scrolled up (zoom in).</summary>
        public int WheelNotches;
        /// <summary>The pointer is over a UI panel: the map ignores it.</summary>
        public bool OverUi;
    }

    /// <summary>The keys the camera reads: held (WASD or the arrows) or pressed this frame (Home, Esc).</summary>
    public struct KeyInput
    {
        public bool Left;
        public bool Right;
        public bool Up;
        public bool Down;
        public bool HomePressed;
        public bool EscapePressed;
        /// <summary>Tab this frame: the dashboard's panels (the camera ignores it).</summary>
        public bool TabPressed;
    }

    /// <summary>What the frame's input asks of the rest of the view.</summary>
    public struct CameraInputResult
    {
        /// <summary>A left click on the map (pressed and released without dragging) at <see cref="ClickWorld"/>.</summary>
        public bool Clicked;
        public Vec2 ClickWorld;
        /// <summary>The player moved the camera: stop following the selected crawler.</summary>
        public bool StopFollowing;
    }

    /// <summary>
    /// The player's camera controls (Epic CLIENT M1.4), Unity's <c>CameraController</c> rule for rule, on plain input
    /// snapshots so they're tested without devices:
    /// <list type="bullet">
    /// <item><description>scroll zooms toward the cursor;</description></item>
    /// <item><description>right or middle drag pans;</description></item>
    /// <item><description>WASD or the arrows pan;</description></item>
    /// <item><description>Home reframes the whole map;</description></item>
    /// <item><description>Esc or any pan stops following;</description></item>
    /// <item><description>a left click that doesn't drag selects.</description></item>
    /// </list>
    /// Pointer input over a UI panel is ignored. View only: no simulation state.
    /// </summary>
    public sealed class CameraInput
    {
        /// <summary>One scroll notch zooms in to this fraction of the view (out by its inverse).</summary>
        public const float ZoomStep = 0.85f;
        /// <summary>Keyboard pan speed in view heights per second (so it feels the same at any zoom).</summary>
        public const float KeyPanViewsPerSecond = 0.6f;
        /// <summary>A press that moves further than this (pixels) before release is a drag, not a click.</summary>
        public const float ClickSlopPixels = 6f;

        private PointerInput _previous;
        private bool _clickArmed;
        private Vec2 _pressPosition;
        private bool _dragging;

        public CameraInputResult Update(in PointerInput pointer, in KeyInput keys, float deltaSeconds, CameraRig rig, in View view)
        {
            var result = new CameraInputResult();

            if (!pointer.OverUi && pointer.WheelNotches != 0)
            {
                float factor = 1f;
                for (int i = 0; i < System.Math.Abs(pointer.WheelNotches); i++) factor *= pointer.WheelNotches > 0 ? ZoomStep : 1f / ZoomStep;
                rig.ZoomAt(view.ScreenToFloor(pointer.Position), factor);
            }

            // A left click (press and release without dragging, starting on the map) selects
            bool leftPressed = pointer.Left && !_previous.Left;
            bool leftReleased = !pointer.Left && _previous.Left;
            if (leftPressed)
            {
                _clickArmed = !pointer.OverUi;
                _pressPosition = pointer.Position;
            }
            else if (_clickArmed && pointer.Left && (pointer.Position - _pressPosition).SqrMagnitude > ClickSlopPixels * ClickSlopPixels)
            {
                _clickArmed = false;
            }
            if (leftReleased && _clickArmed)
            {
                _clickArmed = false;
                result.Clicked = true;
                result.ClickWorld = view.ScreenToFloor(pointer.Position);
            }

            bool panHeld = pointer.Right || pointer.Middle;
            if (!panHeld)
            {
                _dragging = false;
            }
            else if (!_dragging)
            {
                // A drag starts only on the map, never on a panel
                _dragging = !pointer.OverUi && ((pointer.Right && !_previous.Right) || (pointer.Middle && !_previous.Middle));
            }
            else
            {
                // Grab the map: it moves with the pointer (screen y points down, the view plane's y up)
                float unitsPerPixel = ViewUnitsPerPixel(rig, view);
                var delta = new Vec2((_previous.Position.X - pointer.Position.X) * unitsPerPixel, (pointer.Position.Y - _previous.Position.Y) * unitsPerPixel);
                if (delta.X != 0f || delta.Y != 0f)
                {
                    result.StopFollowing = true;
                    rig.PanOnScreen(delta);
                }
            }

            float dx = (keys.Right ? 1f : 0f) - (keys.Left ? 1f : 0f);
            float dy = (keys.Up ? 1f : 0f) - (keys.Down ? 1f : 0f);
            if (dx != 0f || dy != 0f)
            {
                result.StopFollowing = true;
                var direction = new Vec2(dx, dy).Normalized;
                rig.PanOnScreen(direction * (KeyPanViewsPerSecond * 2f * rig.Size * deltaSeconds));
            }
            if (keys.HomePressed)
            {
                result.StopFollowing = true;
                rig.Reframe();
            }
            if (keys.EscapePressed) result.StopFollowing = true;

            _previous = pointer;
            return result;
        }

        private static float ViewUnitsPerPixel(CameraRig rig, in View view)
            => view.ViewportHeight > 0 ? 2f * rig.Size / view.ViewportHeight : 0f;
    }
}
