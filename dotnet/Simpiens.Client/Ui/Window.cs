using Gum.Converters;
using Gum.DataTypes;
using Gum.Forms.Controls;
using Microsoft.Xna.Framework;
using Gum.GueDeriving;
using RenderingLibrary.Graphics;

namespace Simpiens.Client.Ui
{
    /// <summary>
    /// Unity's modal window over the map: a dark veil from under the header to the bottom (it takes the clicks, so the
    /// map gets none while a window is open), and a bordered window centred on it with a title row and a close button.
    /// </summary>
    public sealed class Window
    {
        public const float Padding = 16f;

        public Window(string name, string title, string subtitle, float width, Color border)
        {
            Veil = new ContainerRuntime
            {
                Name = name,
                WidthUnits = DimensionUnitType.RelativeToParent,
                HeightUnits = DimensionUnitType.RelativeToParent,
                Width = 0f,
                Height = -HeaderBar.Height,
                Y = HeaderBar.Height,
                HasEvents = true,
                Visible = false,
            };
            UiParts.AddFill(Veil, UiStyle.Veil);

            Frame = new ContainerRuntime
            {
                Width = width,
                HeightUnits = DimensionUnitType.RelativeToChildren,
                Height = Padding,
                XUnits = GeneralUnitType.PixelsFromMiddle,
                XOrigin = HorizontalAlignment.Center,
                YUnits = GeneralUnitType.PixelsFromMiddle,
                YOrigin = VerticalAlignment.Center,
                HasEvents = true,
            };
            UiParts.AddFill(Frame, UiStyle.Panel, border);
            Veil.Children.Add(Frame);

            Body = UiParts.Column(0f);
            Body.X = Padding;
            Body.Y = Padding;
            Body.WidthUnits = DimensionUnitType.Absolute;
            Body.Width = width - 2f * Padding;
            Frame.Children.Add(Body);

            var titleRow = new ContainerRuntime { WidthUnits = DimensionUnitType.RelativeToParent, Width = 0f, HeightUnits = DimensionUnitType.RelativeToChildren, Height = 10f };
            var titles = UiParts.Column(2f);
            titles.Children.Add(UiParts.Text(title, UiStyle.TextTitle, UiText.Title));
            titles.Children.Add(UiParts.Text(subtitle, UiStyle.TextSecondary, UiText.Small));
            titleRow.Children.Add(titles);
            CloseButton = UiParts.Button("X", 28f, 24f);
            CloseButton.Visual.XUnits = GeneralUnitType.PixelsFromLarge;
            CloseButton.Visual.XOrigin = HorizontalAlignment.Right;
            titleRow.Children.Add(CloseButton.Visual);
            Body.Children.Add(titleRow);
            Body.Children.Add(Rule());
            Body.Children.Add(Gap(12f));
        }

        /// <summary>The veil over the map: the window's root, shown while it is open.</summary>
        public ContainerRuntime Veil { get; }

        /// <summary>The window itself.</summary>
        public ContainerRuntime Frame { get; }

        /// <summary>The window's content, top to bottom.</summary>
        public ContainerRuntime Body { get; }

        public Button CloseButton { get; }

        /// <summary>
        /// Keeps the window on whole pixels (graphics V4.3): centred in the area under the header, a window of odd height
        /// would sit half a pixel down, and with it its icons, which then sample on texel edges. Moves it up or left by
        /// the fraction, from where it would sit unsnapped, so the offset never drifts. Call after a layout (each bind).
        /// </summary>
        public void SnapToPixels()
        {
            float left = Frame.AbsoluteLeft - Frame.X, top = Frame.AbsoluteTop - Frame.Y;
            float x = System.MathF.Floor(left) - left, y = System.MathF.Floor(top) - top;
            if (Frame.X != x) Frame.X = x;
            if (Frame.Y != y) Frame.Y = y;
        }

        public bool IsShown
        {
            get => Veil.Visible;
            set => Veil.Visible = value;
        }

        /// <summary>A 1 px line across the window.</summary>
        public static RectangleRuntime Rule() => new RectangleRuntime { IsFilled = true, StrokeWidth = 0, FillColor = UiStyle.Border, WidthUnits = DimensionUnitType.RelativeToParent, Width = 0f, Height = 1f };

        /// <summary>Empty space in a column.</summary>
        public static ContainerRuntime Gap(float height) => new ContainerRuntime { Width = 1f, Height = height };

        /// <summary>A sub-header in Unity's muted caps.</summary>
        public static TextRuntime SubHeader(string text) => UiParts.Text(text, UiStyle.TextMuted, UiText.SmallBold);
    }
}
