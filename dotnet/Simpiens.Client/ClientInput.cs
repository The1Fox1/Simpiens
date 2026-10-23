using Microsoft.Xna.Framework.Input;
using Simpiens.Presentation;

namespace Simpiens.Client
{
    /// <summary>
    /// Reads the mouse and keyboard into the plain snapshots the view's controls work on (Epic CLIENT M1.4): wheel
    /// notches since the last frame, and Home and Esc as presses rather than holds.
    /// </summary>
    public sealed class ClientInput : IClientInputSource
    {
        private const int WheelNotch = 120;

        private int _wheel;
        private bool _hasWheel;
        private KeyboardState _previousKeys;

        public void Read(out PointerInput pointer, out KeyInput keys)
        {
            var mouse = Mouse.GetState();
            if (!_hasWheel)
            {
                _wheel = mouse.ScrollWheelValue;
                _hasWheel = true;
            }
            int notches = (mouse.ScrollWheelValue - _wheel) / WheelNotch;
            _wheel += notches * WheelNotch;

            pointer = new PointerInput
            {
                Position = new Vec2(mouse.X, mouse.Y),
                Left = mouse.LeftButton == ButtonState.Pressed,
                Right = mouse.RightButton == ButtonState.Pressed,
                Middle = mouse.MiddleButton == ButtonState.Pressed,
                WheelNotches = notches,
            };

            var kb = Keyboard.GetState();
            keys = new KeyInput
            {
                Left = kb.IsKeyDown(Keys.A) || kb.IsKeyDown(Keys.Left),
                Right = kb.IsKeyDown(Keys.D) || kb.IsKeyDown(Keys.Right),
                Up = kb.IsKeyDown(Keys.W) || kb.IsKeyDown(Keys.Up),
                Down = kb.IsKeyDown(Keys.S) || kb.IsKeyDown(Keys.Down),
                HomePressed = kb.IsKeyDown(Keys.Home) && !_previousKeys.IsKeyDown(Keys.Home),
                EscapePressed = kb.IsKeyDown(Keys.Escape) && !_previousKeys.IsKeyDown(Keys.Escape),
                TabPressed = kb.IsKeyDown(Keys.Tab) && !_previousKeys.IsKeyDown(Keys.Tab),
                WallStylePressed = kb.IsKeyDown(Keys.C) && !_previousKeys.IsKeyDown(Keys.C),
                ViewTogglePressed = kb.IsKeyDown(Keys.V) && !_previousKeys.IsKeyDown(Keys.V),
                DemoCutscenePressed = kb.IsKeyDown(Keys.F9) && !_previousKeys.IsKeyDown(Keys.F9),
                TimingsPressed = kb.IsKeyDown(Keys.F3) && !_previousKeys.IsKeyDown(Keys.F3),
                PausePressed = kb.IsKeyDown(Keys.P) && !_previousKeys.IsKeyDown(Keys.P),
                SpeedKey = Pressed(kb, Keys.D1) ? 1 : Pressed(kb, Keys.D2) ? 2 : Pressed(kb, Keys.D3) ? 3 : Pressed(kb, Keys.D4) ? 4 : 0,
                AdvancePressed = (kb.IsKeyDown(Keys.Space) && !_previousKeys.IsKeyDown(Keys.Space)) || (kb.IsKeyDown(Keys.Enter) && !_previousKeys.IsKeyDown(Keys.Enter)),
            };
            _previousKeys = kb;
        }

        private bool Pressed(KeyboardState now, Keys key) => now.IsKeyDown(key) && !_previousKeys.IsKeyDown(key);
    }
}
