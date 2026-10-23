using System;
using Gum.Wireframe;
using Microsoft.Xna.Framework.Input;
using Simpiens.Presentation;

namespace Simpiens.Client.Ui
{
    /// <summary>
    /// Gum's cursor, fed from the game's own input frame (review U1) instead of reading the mouse itself: the map and the
    /// UI see the same pointer each update, a scripted pointer drives both in tests, and touch (M4) will reach both
    /// through the one frame. The rules (push, click, double click, drag threshold) are Gum's own mouse rules, ported
    /// from <c>MonoGameGum.Input.Cursor</c> (MIT) at the commit our Gum package was built from (de21a042), so every Gum
    /// control behaves as it did. <see cref="Feed"/> gives it the frame's pointer; Gum calls <see cref="Activity"/>
    /// during its update, which makes that pointer current.
    /// </summary>
    public sealed class ClientCursor : ICursor
    {
        /// <summary>Gum's: two clicks closer together than this make a double click.</summary>
        public const float MaximumSecondsBetweenClickForDoubleClick = 0.25f;

        /// <summary>Gum's mouse tolerances, in pixels.</summary>
        public const float DoubleClickTolerance = 4f, DragThreshold = 6f;

        private readonly ClickTracker _primaryClicks = new ClickTracker();
        private readonly ClickTracker _secondaryClicks = new ClickTracker();
        private readonly ClickTracker _middleClicks = new ClickTracker();
        private PointerInput _fed, _current, _last;
        private Cursors? _customCursor;

        /// <summary>The frame's pointer, for Gum's next <see cref="Activity"/>.</summary>
        public void Feed(in PointerInput pointer) => _fed = pointer;

        public void Activity(double currentGameTimeTotalSeconds)
        {
            _last = _current;
            _current = _fed;
            _fed.WheelNotches = 0; // a wheel's notches count once, even if Gum updates twice before the next frame
            LastX = X;
            LastY = Y;
            X = (int)_current.Position.X;
            Y = (int)_current.Position.Y;
            double time = currentGameTimeTotalSeconds;
            _primaryClicks.Update(time, PrimaryPush, PrimaryClick, X, Y);
            _secondaryClicks.Update(time, SecondaryPush, SecondaryClick, X, Y);
            _middleClicks.Update(time, MiddlePush, MiddleClick, X, Y);
        }

        public Cursors? CustomCursor
        {
            get => _customCursor;
            set
            {
                _customCursor = value;
                switch (value)
                {
                    case Cursors.SizeNS: Mouse.SetCursor(MouseCursor.SizeNS); break;
                    case Cursors.SizeWE: Mouse.SetCursor(MouseCursor.SizeWE); break;
                    case Cursors.SizeNWSE: Mouse.SetCursor(MouseCursor.SizeNWSE); break;
                    case Cursors.SizeNESW: Mouse.SetCursor(MouseCursor.SizeNESW); break;
                    default: Mouse.SetCursor(MouseCursor.Arrow); break;
                }
            }
        }

        public InputDevice LastInputDevice => InputDevice.Mouse;

        public int X { get; private set; }
        public int Y { get; private set; }
        public int LastX { get; private set; }
        public int LastY { get; private set; }
        public int XChange => X - LastX;
        public int YChange => Y - LastY;

        /// <summary>Gum's world coordinates: its camera's zoom and position (the client leaves them at 1 and 0), from the viewport's corner.</summary>
        public float XRespectingGumZoomAndBounds()
        {
            var renderer = RenderingLibrary.SystemManagers.Default.Renderer;
            int left = renderer.GraphicsDevice?.Viewport.Bounds.Left ?? 0;
            return (X - left) / renderer.Camera.Zoom + renderer.Camera.X;
        }

        public float YRespectingGumZoomAndBounds()
        {
            var renderer = RenderingLibrary.SystemManagers.Default.Renderer;
            int top = renderer.GraphicsDevice?.Viewport.Bounds.Top ?? 0;
            return (Y - top) / renderer.Camera.Zoom + renderer.Camera.Y;
        }

        /// <summary>Notches since the last update; positive scrolled up.</summary>
        public int ScrollWheelChange => _current.WheelNotches;
        public float ZVelocity => ScrollWheelChange;

        public bool PrimaryPush => _current.Left && !_last.Left;
        public bool PrimaryDown => _current.Left;
        public bool PrimaryClick => !_current.Left && _last.Left;
        public bool PrimaryClickNoSlide => PrimaryClick;
        public bool PrimaryDoubleClick => _primaryClicks.DoubleClick;
        public bool PrimaryDoublePush => _primaryClicks.DoublePush;
        public double LastPrimaryClickTime => _primaryClicks.LastClickTime;
        public double LastPrimaryPushTime => _primaryClicks.LastPushTime;

        public bool SecondaryPush => _current.Right && !_last.Right;
        public bool SecondaryDown => _current.Right;
        public bool SecondaryClick => !_current.Right && _last.Right;
        public bool SecondaryDoubleClick => _secondaryClicks.DoubleClick;

        public bool MiddlePush => _current.Middle && !_last.Middle;
        public bool MiddleDown => _current.Middle;
        public bool MiddleClick => !_current.Middle && _last.Middle;
        public bool MiddleDoubleClick => _middleClicks.DoubleClick;

        // Gum's bookkeeping: what is under the pointer and what was pushed, set by Gum's Forms update
        public InteractiveGue VisualOver { get; set; }
        public InteractiveGue VisualRightPushed { get; set; }
        public InteractiveGue WindowOver { get => VisualOver; set => VisualOver = value; }
        public InteractiveGue WindowPushed { get; set; }

        public override string ToString() => $"Cursor at ({X}, {Y}) Push:{PrimaryPush} Down:{PrimaryDown} Click:{PrimaryClick}";

        /// <summary>Gum's double push and double click for one button (its ButtonClickTracker, Gum issue #5293).</summary>
        private sealed class ClickTracker
        {
            private int _pushX, _pushY, _clickX, _clickY;
            private bool _canClickPair; // false after a drag's release, so it can't pair with a later click
            private bool _hasDraggedSincePush; // kept once the pointer left the dead zone, even if it comes back

            public double LastPushTime { get; private set; } = -999;
            public double LastClickTime { get; private set; } = -999;
            public bool DoublePush { get; private set; }
            public bool DoubleClick { get; private set; }

            public void Update(double time, bool push, bool click, int x, int y)
            {
                DoublePush = false;
                DoubleClick = false;
                if (push)
                {
                    DoublePush = time - LastPushTime < MaximumSecondsBetweenClickForDoubleClick && IsWithin(x - _pushX, y - _pushY, DoubleClickTolerance);
                    LastPushTime = time;
                    _pushX = x;
                    _pushY = y;
                    _hasDraggedSincePush = false;
                }
                else if (!IsWithin(x - _pushX, y - _pushY, DragThreshold))
                {
                    _hasDraggedSincePush = true;
                }

                if (click)
                {
                    if (_hasDraggedSincePush)
                    {
                        _canClickPair = false;
                    }
                    else
                    {
                        DoubleClick = _canClickPair && time - LastClickTime < MaximumSecondsBetweenClickForDoubleClick && IsWithin(x - _clickX, y - _clickY, DoubleClickTolerance);
                        _clickX = x;
                        _clickY = y;
                        _canClickPair = true;
                    }
                    LastClickTime = time;
                }
            }

            private static bool IsWithin(int dx, int dy, float distance) => Math.Abs(dx) <= distance && Math.Abs(dy) <= distance;
        }
    }
}
