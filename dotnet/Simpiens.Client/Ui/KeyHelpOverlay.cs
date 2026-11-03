using System.Collections.Generic;
using System.Text;
using Gum.Converters;
using Gum.DataTypes;
using Gum.GueDeriving;
using Gum.Managers;
using Microsoft.Xna.Framework.Input;
using RenderingLibrary.Graphics;
using Simpiens.Presentation;

namespace Simpiens.Client.Ui
{
    /// <summary>
    /// The key help (Epic UI-Beta B5): F1 shows a panel listing every key binding, read from the bindings themselves so it
    /// can't drift from them, an action a line with its keys. F1 again, or Esc's next window close, hides it.
    /// </summary>
    public sealed class KeyHelpOverlay
    {
        public const float Width = 380f;

        private readonly TextRuntime _lines;

        public KeyHelpOverlay(UiLayer ui)
        {
            Root = new ContainerRuntime
            {
                Width = Width,
                HeightUnits = DimensionUnitType.RelativeToChildren,
                Height = 24f,
                XUnits = GeneralUnitType.PixelsFromMiddle,
                XOrigin = HorizontalAlignment.Center,
                YUnits = GeneralUnitType.PixelsFromMiddle,
                YOrigin = VerticalAlignment.Center,
                Visible = false,
            };
            UiParts.AddFrame(Root);
            var column = UiParts.Column(8f);
            column.X = 16f;
            column.Y = 14f;
            column.Children.Add(UiParts.Text("KEYS", UiStyle.Accent, UiText.BodyBold));
            _lines = UiParts.Text(string.Empty, UiStyle.TextLight, UiText.Small);
            column.Children.Add(_lines);
            Root.Children.Add(column);
            ui?.Root.Children.Add(Root);
        }

        public ContainerRuntime Root { get; }

        public bool Shown => Root.Visible;

        /// <summary>What the panel lists, a line per action.</summary>
        public string Text => _lines.Text;

        /// <summary>Shows the bindings, or hides them.</summary>
        public void Toggle(KeyBindings bindings)
        {
            if (Root.Visible)
            {
                Root.Visible = false;
                return;
            }
            UiParts.SetText(_lines, Describe(bindings ?? KeyBindings.Default()));
            Root.Visible = true;
        }

        /// <summary>Every bound action with its keys, in the actions' order: "Pan left: A, Left".</summary>
        public static string Describe(KeyBindings bindings)
        {
            var keys = new SortedDictionary<InputAction, List<Keys>>();
            foreach (var binding in bindings.All)
            {
                if (!keys.TryGetValue(binding.Action, out var list)) keys[binding.Action] = list = new List<Keys>(2);
                list.Add(binding.Key);
            }
            var text = new StringBuilder();
            foreach (var (action, list) in keys)
            {
                if (text.Length > 0) text.Append('\n');
                text.Append(Words(action)).Append(": ");
                for (int i = 0; i < list.Count; i++) text.Append(i > 0 ? ", " : string.Empty).Append(KeyName(list[i]));
            }
            return text.ToString();
        }

        private static string Words(InputAction action) => action switch
        {
            InputAction.PanLeft => "Pan left",
            InputAction.PanRight => "Pan right",
            InputAction.PanUp => "Pan up",
            InputAction.PanDown => "Pan down",
            InputAction.Reframe => "Frame the floor",
            InputAction.Close => "Close a window, stop following",
            InputAction.TogglePanels => "Dashboard or map",
            InputAction.WallStyle => "Wall style",
            InputAction.ToggleView => "Isometric or top-down",
            InputAction.Advance => "Next line",
            InputAction.DemoCutscene => "Demonstration cutscene (development)",
            InputAction.Timings => "Frame timings (development)",
            InputAction.Pause => "Pause",
            InputAction.Speed1 or InputAction.Speed2 or InputAction.Speed3 or InputAction.Speed4
                => System.FormattableString.Invariant($"Speed {Simpiens.Presentation.Dashboard.HeaderModel.Speeds[action - InputAction.Speed1]:0.#}x"),
            InputAction.Help => "These keys",
            _ => action.ToString(),
        };

        private static string KeyName(Keys key) => key switch
        {
            Keys.D1 => "1",
            Keys.D2 => "2",
            Keys.D3 => "3",
            Keys.D4 => "4",
            Keys.Escape => "Esc",
            _ => key.ToString(),
        };
    }
}
