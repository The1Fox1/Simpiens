using Gum.Converters;
using Gum.DataTypes;
using Gum.Forms.Controls;
using Gum.Managers;
using Gum.Wireframe;
using Microsoft.Xna.Framework;
using MonoGameGum.GueDeriving;
using RenderingLibrary.Graphics;
using Simpiens.Presentation;

namespace Simpiens.Client.Ui
{
    /// <summary>The dashboard's building blocks in Gum: text in Unity's sizes, stacks, boxes and Unity's pill badges.</summary>
    public static class UiParts
    {
        public static TextRuntime Text(string text, Color color, UiText style)
        {
            var label = new TextRuntime
            {
                Text = text,
                Color = color,
                WidthUnits = DimensionUnitType.RelativeToChildren,
                HeightUnits = DimensionUnitType.RelativeToChildren,
                Width = 0f,
                Height = 0f,
            };
            Style(label, style);
            return label;
        }

        /// <summary>
        /// Gives a text one of the baked faces, drawn 1:1. The three font properties are set under suspended layout, so
        /// Gum looks the face up once instead of once per property (in-between faces aren't baked).
        /// </summary>
        public static void Style(TextRuntime text, UiText style)
        {
            text.SuspendLayout();
            text.Font = FontSet.Family;
            text.FontSize = style.Size;
            text.IsBold = style.Bold;
            text.FontScale = 1f;
            text.ResumeLayout();
        }

        /// <summary>A left-to-right stack sized to its children, centred vertically in its parent.</summary>
        public static ContainerRuntime Row(float spacing)
        {
            var row = new ContainerRuntime
            {
                ChildrenLayout = ChildrenLayout.LeftToRightStack,
                StackSpacing = spacing,
                WidthUnits = DimensionUnitType.RelativeToChildren,
                HeightUnits = DimensionUnitType.RelativeToChildren,
                Width = 0f,
                Height = 0f,
            };
            CentreVertically(row);
            return row;
        }

        /// <summary>A top-to-bottom stack sized to its children.</summary>
        public static ContainerRuntime Column(float spacing)
        {
            return new ContainerRuntime
            {
                ChildrenLayout = ChildrenLayout.TopToBottomStack,
                StackSpacing = spacing,
                WidthUnits = DimensionUnitType.RelativeToChildren,
                HeightUnits = DimensionUnitType.RelativeToChildren,
                Width = 0f,
                Height = 0f,
            };
        }

        public static void CentreVertically(GraphicalUiElement element)
        {
            element.YUnits = GeneralUnitType.PixelsFromMiddle;
            element.YOrigin = VerticalAlignment.Center;
            element.Y = 0f;
        }

        /// <summary>Fills its parent: a solid colour, and optionally a 1 px outline.</summary>
        public static void AddFill(GraphicalUiElement parent, Color fill, Color? border = null)
        {
            var background = new ColoredRectangleRuntime { Color = fill, WidthUnits = DimensionUnitType.RelativeToParent, HeightUnits = DimensionUnitType.RelativeToParent, Width = 0f, Height = 0f };
            parent.Children.Add(background);
            if (border == null) return;
            var outline = new RectangleRuntime { Color = border.Value, WidthUnits = DimensionUnitType.RelativeToParent, HeightUnits = DimensionUnitType.RelativeToParent, Width = 0f, Height = 0f };
            parent.Children.Add(outline);
        }

        /// <summary>Unity's pill badge: a tinted box with a coloured outline around bold text.</summary>
        public static ContainerRuntime Pill(out TextRuntime text, string initial, Color tint, Color border, Color textColor, UiText? style = null)
        {
            var pill = new ContainerRuntime
            {
                WidthUnits = DimensionUnitType.RelativeToChildren,
                HeightUnits = DimensionUnitType.RelativeToChildren,
                Width = 8f,   // right padding
                Height = 3f,  // bottom padding
            };
            AddFill(pill, tint, border);
            text = Text(initial, textColor, style ?? UiText.SmallBold);
            text.X = 8f;
            text.Y = 3f;
            pill.Children.Add(text);
            return pill;
        }

        /// <summary>Sets a text only when it changed: Gum lays a text out again on every assignment (wrapped lines are costly).</summary>
        public static void SetText(TextRuntime text, string value)
        {
            value ??= string.Empty;
            if (!string.Equals(text.Text, value, System.StringComparison.Ordinal)) text.Text = value;
        }

        /// <summary>A colour at an alpha, as Unity's rgba(r, g, b, a) backgrounds.</summary>
        public static Color Alpha(Color color, float alpha) => new Color(color.R, color.G, color.B, (byte)(alpha * 255f + 0.5f));

        public static Button Button(string text, float width, float height = 28f, UiText? style = null)
        {
            var button = new Button { Text = text };
            // Gum's button sizes itself from its text by default: these are fixed sizes, as Unity's were
            button.Visual.WidthUnits = DimensionUnitType.Absolute;
            button.Visual.HeightUnits = DimensionUnitType.Absolute;
            button.Width = width;
            button.Height = height;
            SetTextStyle(button.Visual, style ?? UiText.SmallBold);
            return button;
        }

        /// <summary>A slider whose thumb stands out: sky on a slate track (Gum's thumb is the dark button colour).</summary>
        public static Slider Slider(double minimum, double maximum, double step)
        {
            var slider = new Slider { Minimum = minimum, Maximum = maximum, IsSnapToTickEnabled = true, TicksFrequency = step, SmallChange = step, LargeChange = step * 5 };
            if (slider.Visual is Gum.Forms.DefaultVisuals.V3.SliderVisual visual)
            {
                visual.ThumbInstance.BackgroundColor = UiStyle.Accent;
                visual.TrackBackgroundColor = UiStyle.SliderTrack;
            }
            return slider;
        }

        /// <summary>Gives every text inside a control's visual the same style (Gum's own styles cover the rest).</summary>
        public static void SetTextStyle(GraphicalUiElement visual, UiText style)
        {
            if (visual is TextRuntime text) Style(text, style);
            foreach (var child in visual.Children)
            {
                if (child is GraphicalUiElement element) SetTextStyle(element, style);
            }
        }
    }
}
