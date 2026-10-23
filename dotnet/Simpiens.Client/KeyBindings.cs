using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework.Input;
using Simpiens.Presentation;

namespace Simpiens.Client
{
    /// <summary>One key and the action it asks for.</summary>
    public readonly struct KeyBinding
    {
        public KeyBinding(Keys key, InputAction action)
        {
            Key = key;
            Action = action;
        }

        public Keys Key { get; }
        public InputAction Action { get; }
    }

    /// <summary>
    /// Which keys ask for which <see cref="InputAction"/> (review U1): data, not code, so a key can be rebound. A key asks
    /// for one action; an action can have several keys (WASD and the arrows).
    /// </summary>
    public sealed class KeyBindings
    {
        private readonly List<KeyBinding> _bindings;

        public KeyBindings(IEnumerable<KeyBinding> bindings)
        {
            _bindings = new List<KeyBinding>();
            foreach (var binding in bindings ?? throw new ArgumentNullException(nameof(bindings))) Bind(binding.Key, binding.Action);
        }

        /// <summary>The game's keys: WASD or the arrows pan, Home reframes, Esc closes, Tab the panels, C and V the walls and the view, Space or Enter advance a cutscene, P pauses, 1 to 4 the speed; F9 and F3 in development builds.</summary>
        public static KeyBindings Default() => new KeyBindings(new[]
        {
            new KeyBinding(Keys.A, InputAction.PanLeft), new KeyBinding(Keys.Left, InputAction.PanLeft),
            new KeyBinding(Keys.D, InputAction.PanRight), new KeyBinding(Keys.Right, InputAction.PanRight),
            new KeyBinding(Keys.W, InputAction.PanUp), new KeyBinding(Keys.Up, InputAction.PanUp),
            new KeyBinding(Keys.S, InputAction.PanDown), new KeyBinding(Keys.Down, InputAction.PanDown),
            new KeyBinding(Keys.Home, InputAction.Reframe),
            new KeyBinding(Keys.Escape, InputAction.Close),
            new KeyBinding(Keys.Tab, InputAction.TogglePanels),
            new KeyBinding(Keys.C, InputAction.WallStyle),
            new KeyBinding(Keys.V, InputAction.ToggleView),
            new KeyBinding(Keys.Space, InputAction.Advance), new KeyBinding(Keys.Enter, InputAction.Advance),
            new KeyBinding(Keys.F9, InputAction.DemoCutscene),
            new KeyBinding(Keys.F3, InputAction.Timings),
            new KeyBinding(Keys.P, InputAction.Pause),
            new KeyBinding(Keys.D1, InputAction.Speed1), new KeyBinding(Keys.D2, InputAction.Speed2),
            new KeyBinding(Keys.D3, InputAction.Speed3), new KeyBinding(Keys.D4, InputAction.Speed4),
        });

        public IReadOnlyList<KeyBinding> All => _bindings;

        /// <summary>Makes <paramref name="key"/> ask for <paramref name="action"/>, instead of whatever it asked for before.</summary>
        public void Bind(Keys key, InputAction action)
        {
            Unbind(key);
            _bindings.Add(new KeyBinding(key, action));
        }

        /// <summary>Frees <paramref name="key"/>; false when it wasn't bound.</summary>
        public bool Unbind(Keys key) => _bindings.RemoveAll(b => b.Key == key) > 0;

        /// <summary>The action <paramref name="key"/> asks for, if it is bound.</summary>
        public bool TryGetAction(Keys key, out InputAction action)
        {
            for (int i = 0; i < _bindings.Count; i++)
            {
                if (_bindings[i].Key != key) continue;
                action = _bindings[i].Action;
                return true;
            }
            action = default;
            return false;
        }
    }
}
