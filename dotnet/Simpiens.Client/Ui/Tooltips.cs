using System.Collections.Generic;
using Gum.DataTypes;
using Gum.GueDeriving;
using Gum.Wireframe;
using Microsoft.Xna.Framework;

namespace Simpiens.Client.Ui
{
    /// <summary>
    /// Tooltips (Epic UI-Beta B5): Gum has none. An element registered with a tip shows it in a small panel beside the
    /// cursor after <see cref="Delay"/> seconds over it (or over a child of it); moving off hides it. The panel takes no
    /// clicks, so it never steals one. One per UI layer, over everything else.
    /// </summary>
    public sealed class Tooltips
    {
        /// <summary>Seconds over an element before its tip shows.</summary>
        public const double Delay = 0.5;

        private const float Offset = 18f, Padding = 8f;

        private readonly UiLayer _ui;
        private readonly Dictionary<GraphicalUiElement, string> _tips = new Dictionary<GraphicalUiElement, string>(ReferenceEqualityComparer.Instance);
        private readonly TextRuntime _text;
        private GraphicalUiElement _over;
        private double _since;

        public Tooltips(UiLayer ui)
        {
            _ui = ui;
            Panel = new ContainerRuntime { WidthUnits = DimensionUnitType.RelativeToChildren, HeightUnits = DimensionUnitType.RelativeToChildren, Width = Padding * 2f, Height = Padding * 2f, Visible = false };
            UiParts.AddFill(Panel, UiParts.Alpha(UiStyle.Panel, 0.97f), UiStyle.Border);
            _text = UiParts.Text(string.Empty, UiStyle.TextLight, UiText.Small);
            _text.X = Padding;
            _text.Y = Padding;
            Panel.Children.Add(_text);
        }

        /// <summary>The tip's panel; the shell adds it last, over everything.</summary>
        public ContainerRuntime Panel { get; }

        public bool Shown => Panel.Visible;
        public string Text => _text.Text;

        /// <summary>Gives <paramref name="element"/> a tip (null removes it). It must take events, as buttons do.</summary>
        public void Register(GraphicalUiElement element, string tip)
        {
            if (element == null) return;
            if (tip == null) _tips.Remove(element);
            else _tips[element] = tip;
        }

        /// <summary>After the UI's update: what the cursor is over, how long, and the tip if it is due.</summary>
        public void Update(double seconds, int screenWidth, int screenHeight)
        {
            var over = Registered(_ui?.Cursor.WindowOver);
            if (!ReferenceEquals(over, _over))
            {
                _over = over;
                _since = 0;
                Panel.Visible = false;
                return;
            }
            if (over == null) return;
            _since += seconds;
            if (_since < Delay || !over.Visible) return;
            UiParts.SetText(_text, _tips[over]);
            float x = _ui.Cursor.X + Offset, y = _ui.Cursor.Y + Offset;
            float width = _text.AbsoluteWidth + Padding * 2f, height = _text.AbsoluteHeight + Padding * 2f;
            Panel.X = MathHelper.Clamp(x, 0f, System.Math.Max(0f, screenWidth - width));
            Panel.Y = MathHelper.Clamp(y, 0f, System.Math.Max(0f, screenHeight - height));
            Panel.Visible = true;
        }

        // The element, or the nearest of its parents, that has a tip
        private GraphicalUiElement Registered(GraphicalUiElement element)
        {
            for (var e = element; e != null; e = e.Parent as GraphicalUiElement)
                if (_tips.ContainsKey(e)) return e;
            return null;
        }
    }
}
