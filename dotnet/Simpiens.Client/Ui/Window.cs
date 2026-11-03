using Gum.Converters;
using Gum.DataTypes;
using Gum.Forms.Controls;
using Microsoft.Xna.Framework;
using Gum.GueDeriving;
using RenderingLibrary.Graphics;

namespace Simpiens.Client.Ui
{
    /// <summary>
    /// A modal window over the map (Unity's, in Lantern &amp; Stone since the UI polish plan, U5): a veil from under the
    /// header to the bottom (it takes the clicks, so the map gets none while a window is open), and a stone window centred
    /// on it in a riveted frame whose metal says what the window is about (bronze for the stall, iron for the bounty
    /// board, blued steel for the guild, as the portraits' frames say standing), with a title band holding its glyph, its
    /// title and subtitle, and a drawn close button.
    /// </summary>
    public sealed class Window
    {
        public const float Padding = 16f;

        /// <summary>The frame's band: the kit's frame corner.</summary>
        private const float Band = Simpiens.Presentation.Art.UiKit.FrameCorner;

        /// <summary>How tall the title band is, inside the frame.</summary>
        private const float TitleBand = 54f;

        /// <param name="frame">The frame's kit piece (<see cref="Simpiens.Presentation.Art.UiKit.FrameBronze"/>, …).</param>
        /// <param name="glyph">The title's kit glyph.</param>
        public Window(string name, string title, string subtitle, float width, string frame, string glyph)
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
            // A hard shadow down and right (light from the top left), then the frame, then the title band's stone
            Frame.Children.Add(new RectangleRuntime { IsFilled = true, StrokeWidth = 0, FillColor = new Color(0, 0, 0, 110), WidthUnits = DimensionUnitType.RelativeToParent, HeightUnits = DimensionUnitType.RelativeToParent, Width = 0f, Height = 0f, X = 4f, Y = 4f });
            UiParts.AddKitFill(Frame, frame, Simpiens.Presentation.Art.UiKit.FrameCorner, UiStyle.Panel);
            if (UiStyle.Kit == null) UiParts.AddFill(Frame, Color.Transparent, UiStyle.ControlBorder);
            Frame.Children.Add(BandLine(UiStyle.Header, Band, TitleBand));
            Frame.Children.Add(BandLine(UiStyle.Hex(0x0d0b09), Band + TitleBand, 1f));
            Frame.Children.Add(BandLine(UiStyle.Hex(0x5a3c1a), Band + TitleBand + 1f, 1f)); // lamplight under the band
            Veil.Children.Add(Frame);

            Body = UiParts.Column(0f);
            Body.X = Padding;
            Body.Y = Band + 7f;
            Body.WidthUnits = DimensionUnitType.Absolute;
            Body.Width = width - 2f * Padding;
            Frame.Children.Add(Body);

            var titleRow = new ContainerRuntime { WidthUnits = DimensionUnitType.RelativeToParent, Width = 0f, Height = TitleBand - 14f };
            var mark = UiParts.Glyph(glyph, UiStyle.Accent);
            UiParts.CentreVertically(mark);
            titleRow.Children.Add(mark);
            var titles = UiParts.Column(2f);
            UiParts.CentreVertically(titles);
            titles.X = mark.Width + 10f;
            titles.Children.Add(UiParts.Text(title, UiStyle.TextTitle, UiText.Title));
            titles.Children.Add(UiParts.Text(subtitle, UiStyle.TextSecondary, UiText.Small));
            titleRow.Children.Add(titles);
            CloseButton = UiParts.GlyphButton(string.Empty, Simpiens.Presentation.Art.UiKit.Close, 28f, out var cross, 28f);
            cross.Color = UiStyle.Text;
            CloseButton.Visual.XUnits = GeneralUnitType.PixelsFromLarge;
            CloseButton.Visual.XOrigin = HorizontalAlignment.Right;
            UiParts.CentreVertically(CloseButton.Visual);
            titleRow.Children.Add(CloseButton.Visual);
            Body.Children.Add(titleRow);
            Body.Children.Add(Gap(TitleBand + Band + 16f - (Band + 7f) - (TitleBand - 14f)));
        }

        /// <summary>A band across the window inside its frame, <paramref name="height"/> tall at <paramref name="y"/>.</summary>
        private static RectangleRuntime BandLine(Color color, float y, float height)
            => new RectangleRuntime { IsFilled = true, StrokeWidth = 0, FillColor = color, WidthUnits = DimensionUnitType.RelativeToParent, Width = -2f * Band, X = Band, Y = y, Height = height };

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
