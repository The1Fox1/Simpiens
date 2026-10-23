using System;
using Microsoft.Xna.Framework.Input;
using Simpiens.Presentation;

namespace Simpiens.Client
{
    /// <summary>
    /// Reads the mouse and keyboard into the plain snapshots the view's controls work on (Epic CLIENT M1.4): wheel
    /// notches since the last frame, and the keys as actions through <see cref="KeyBindings"/> (review U1), held or
    /// pressed this frame. Read once per update: the presses are edges against the previous read.
    /// </summary>
    public sealed class ClientInput : IClientInputSource
    {
        private const int WheelNotch = 120;

        private static readonly int ActionCount = Enum.GetValues(typeof(InputAction)).Length;

        private readonly bool[] _held = new bool[ActionCount];
        private readonly bool[] _pressed = new bool[ActionCount];
        private int _wheel;
        private bool _hasWheel;
        private KeyboardState _previousKeys;

        public ClientInput(KeyBindings bindings = null) => Bindings = bindings ?? KeyBindings.Default();

        /// <summary>Which keys ask for which actions; rebinding takes effect on the next read.</summary>
        public KeyBindings Bindings { get; }

        public void Read(out PointerInput pointer, out KeyInput keys) => Read(Mouse.GetState(), Keyboard.GetState(), out pointer, out keys);

        /// <summary>The same from given device states (tests: the offscreen window can't be typed into).</summary>
        public void Read(in MouseState mouse, in KeyboardState kb, out PointerInput pointer, out KeyInput keys)
        {
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

            Array.Clear(_held, 0, _held.Length);
            Array.Clear(_pressed, 0, _pressed.Length);
            var bindings = Bindings.All;
            for (int i = 0; i < bindings.Count; i++)
            {
                var binding = bindings[i];
                if (!kb.IsKeyDown(binding.Key)) continue;
                int action = (int)binding.Action;
                _held[action] = true;
                if (!_previousKeys.IsKeyDown(binding.Key)) _pressed[action] = true;
            }
            keys = KeyInput.From(_held, _pressed);
            _previousKeys = kb;
        }
    }
}
