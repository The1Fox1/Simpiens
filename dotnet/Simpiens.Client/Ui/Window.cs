using Gum.Converters;
using Gum.DataTypes;
using Gum.Forms.Controls;
using Microsoft.Xna.Framework;
using MonoGameGum.GueDeriving;
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
        private static readonly Color TitleColor = UiParts.Hex(0xf8fafc), SubtitleColor = UiParts.Hex(0x94a3b8);

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
            UiParts.AddFill(Veil, new Color(9, 13, 22, 209)); // rgba(9, 13, 22, 0.82)

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
            titles.Children.Add(UiParts.Text(title, TitleColor, UiText.Title));
            titles.Children.Add(UiParts.Text(subtitle, SubtitleColor, UiText.Small));
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

        public bool IsShown
        {
            get => Veil.Visible;
            set => Veil.Visible = value;
        }

        /// <summary>A 1 px line across the window.</summary>
        public static ColoredRectangleRuntime Rule() => new ColoredRectangleRuntime { Color = UiStyle.Border, WidthUnits = DimensionUnitType.RelativeToParent, Width = 0f, Height = 1f };

        /// <summary>Empty space in a column.</summary>
        public static ContainerRuntime Gap(float height) => new ContainerRuntime { Width = 1f, Height = height };

        /// <summary>A sub-header in Unity's muted caps.</summary>
        public static TextRuntime SubHeader(string text) => UiParts.Text(text, UiStyle.TextMuted, UiText.SmallBold);
    }
}
