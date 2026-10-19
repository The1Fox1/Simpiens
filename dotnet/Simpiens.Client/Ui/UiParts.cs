using Gum.Converters;
using Gum.DataTypes;
using Gum.Forms.Controls;
using Gum.Managers;
using Gum.Wireframe;
using Microsoft.Xna.Framework;
using MonoGameGum.GueDeriving;
using RenderingLibrary.Graphics;

namespace Simpiens.Client.Ui
{
    /// <summary>The dashboard's building blocks in Gum: text in Unity's sizes, stacks, boxes and Unity's pill badges.</summary>
    public static class UiParts
    {
        /// <summary>Gum's built-in font is 18 px; Unity's sizes as scales of it.</summary>
        public const float TitleScale = 0.85f;   // 15-16 px titles
        public const float BodyScale = 0.7f;     // 12-13 px
        public const float SmallScale = 0.64f;   // 11 px

        public static TextRuntime Text(string text, Color color, float scale = BodyScale, bool bold = false)
        {
            var label = new TextRuntime
            {
                Text = text,
                Color = color,
                FontScale = scale,
                IsBold = bold,
                WidthUnits = DimensionUnitType.RelativeToChildren,
                HeightUnits = DimensionUnitType.RelativeToChildren,
                Width = 0f,
                Height = 0f,
            };
            return label;
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
        public static ContainerRuntime Pill(out TextRuntime text, string initial, Color tint, Color border, Color textColor, float scale = SmallScale)
        {
            var pill = new ContainerRuntime
            {
                WidthUnits = DimensionUnitType.RelativeToChildren,
                HeightUnits = DimensionUnitType.RelativeToChildren,
                Width = 8f,   // right padding
                Height = 3f,  // bottom padding
            };
            AddFill(pill, tint, border);
            text = Text(initial, textColor, scale, bold: true);
            text.X = 8f;
            text.Y = 3f;
            pill.Children.Add(text);
            return pill;
        }

        /// <summary>A colour at an alpha, as Unity's rgba(r, g, b, a) backgrounds.</summary>
        public static Color Alpha(Color color, float alpha) => new Color(color.R, color.G, color.B, (byte)(alpha * 255f + 0.5f));

        public static Button Button(string text, float width, float height = 28f, float scale = SmallScale)
        {
            var button = new Button { Text = text };
            // Gum's button sizes itself from its text by default: these are fixed sizes, as Unity's were
            button.Visual.WidthUnits = DimensionUnitType.Absolute;
            button.Visual.HeightUnits = DimensionUnitType.Absolute;
            button.Width = width;
            button.Height = height;
            SetTextScale(button.Visual, scale);
            return button;
        }

        /// <summary>Sets the scale of every text inside a control's visual (Gum's styles set one scale for all).</summary>
        public static void SetTextScale(GraphicalUiElement visual, float scale)
        {
            if (visual is TextRuntime text) text.FontScale = scale;
            foreach (var child in visual.Children)
            {
                if (child is GraphicalUiElement element) SetTextScale(element, scale);
            }
        }

        public static Color Hex(uint rgb) => new Color((int)(rgb >> 16) & 0xff, (int)(rgb >> 8) & 0xff, (int)rgb & 0xff);
    }
}
