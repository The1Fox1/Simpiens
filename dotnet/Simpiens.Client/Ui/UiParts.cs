using Gum.Converters;
using Gum.DataTypes;
using Gum.Forms.Controls;
using Gum.Managers;
using Gum.Wireframe;
using Microsoft.Xna.Framework;
using Gum.GueDeriving;
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
            var background = new RectangleRuntime { IsFilled = true, StrokeWidth = 0, FillColor = fill, WidthUnits = DimensionUnitType.RelativeToParent, HeightUnits = DimensionUnitType.RelativeToParent, Width = 0f, Height = 0f };
            parent.Children.Add(background);
            if (border == null) return;
            var outline = new RectangleRuntime { IsFilled = false, StrokeWidth = 1, StrokeColor = border.Value, WidthUnits = DimensionUnitType.RelativeToParent, HeightUnits = DimensionUnitType.RelativeToParent, Width = 0f, Height = 0f };
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

        /// <summary>
        /// An item's icon (graphics V4): <see cref="IconAtlas.Size"/> square, drawn 1:1 from the icon atlas (so at whole
        /// pixels it is the art, whatever Gum's texture filter). <paramref name="iconKey"/> null hides it.
        /// </summary>
        public static SpriteRuntime Icon(IconAtlas atlas, string iconKey, int scale = IconAtlas.Scale)
        {
            int size = scale == 1 ? IconAtlas.SmallSize : IconAtlas.Size;
            var icon = new SpriteRuntime
            {
                Texture = atlas.Texture,
                TextureAddress = TextureAddress.Custom,
                WidthUnits = DimensionUnitType.Absolute,
                HeightUnits = DimensionUnitType.Absolute,
                Width = size,
                Height = size,
            };
            SetIcon(icon, atlas, iconKey);
            return icon;
        }

        /// <summary>
        /// A crawler's face (Epic UI-Beta B4): <see cref="PortraitAtlas.FaceSize"/> square, drawn 1:1 from the portrait
        /// atlas, in a box that takes clicks (a click finds the crawler on the map). Hidden until <see cref="SetFace"/> shows one.
        /// </summary>
        public static ContainerRuntime FaceBox(PortraitAtlas atlas, out SpriteRuntime face)
        {
            var box = new ContainerRuntime { Width = PortraitAtlas.FaceSize, Height = PortraitAtlas.FaceSize, HasEvents = true, Visible = false };
            face = new SpriteRuntime
            {
                Texture = atlas?.Texture,
                TextureAddress = TextureAddress.Custom,
                WidthUnits = DimensionUnitType.Absolute,
                HeightUnits = DimensionUnitType.Absolute,
                Width = PortraitAtlas.FaceSize,
                Height = PortraitAtlas.FaceSize,
            };
            box.Children.Add(face);
            return box;
        }

        /// <summary>Shows <paramref name="key"/>'s face in <paramref name="box"/>, baked now if it wasn't; hides it when there is none to show.</summary>
        public static void SetFace(ContainerRuntime box, SpriteRuntime face, PortraitAtlas atlas, bool hasFace, in Simpiens.Presentation.Art.People.PortraitKey key)
        {
            bool shown = hasFace && atlas != null;
            if (box.Visible != shown) box.Visible = shown;
            if (!shown) return;
            var source = atlas.ShowFace(key);
            if (face.SourceRectangle != source) face.SourceRectangle = source;
        }

        /// <summary>Points <paramref name="icon"/> at <paramref name="iconKey"/>'s art, at the scale its size says, or hides it for null.</summary>
        public static void SetIcon(SpriteRuntime icon, IconAtlas atlas, string iconKey)
        {
            icon.Visible = iconKey != null;
            if (iconKey != null) icon.SourceRectangle = atlas.Source(iconKey, icon.Width <= IconAtlas.SmallSize ? 1 : IconAtlas.Scale);
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
            StoneSkin.Apply(button);
            return button;
        }

        /// <summary>A window's main action: the amber primary button (one per window).</summary>
        public static Button PrimaryButton(string text, float width, float height = 28f, UiText? style = null)
        {
            var button = Button(text, width, height, style);
            StoneSkin.Apply(button, ButtonLook.Primary);
            return button;
        }

        /// <summary>
        /// Fills <paramref name="parent"/> with the kit's <paramref name="piece"/> as a nine-slice whose corners are
        /// <paramref name="corner"/> pixels (the UI polish plan, U4); a plain <paramref name="fallback"/> fill without a kit.
        /// </summary>
        public static void AddKitFill(GraphicalUiElement parent, string piece, int corner, Color fallback, Color? fallbackBorder = null)
        {
            var kit = UiStyle.Kit;
            if (kit == null)
            {
                AddFill(parent, fallback, fallbackBorder);
                return;
            }
            var source = kit.Kit(piece);
            parent.Children.Add(new Gum.GueDeriving.NineSliceRuntime
            {
                Texture = kit.Texture,
                TextureAddress = TextureAddress.Custom,
                TextureLeft = source.X,
                TextureTop = source.Y,
                TextureWidth = source.Width,
                TextureHeight = source.Height,
                CustomFrameTextureCoordinateWidth = corner,
                WidthUnits = DimensionUnitType.RelativeToParent,
                HeightUnits = DimensionUnitType.RelativeToParent,
                Width = 0f,
                Height = 0f,
            });
        }

        /// <summary>A panel's iron frame round its stone (the UI polish plan, U8): a plain panel with a border without a kit.</summary>
        public static void AddFrame(GraphicalUiElement parent, string piece = Simpiens.Presentation.Art.UiKit.FrameIron)
            => AddKitFill(parent, piece, Simpiens.Presentation.Art.UiKit.FrameCorner, UiStyle.Panel, UiStyle.Border);

        /// <summary>A box sunk into a panel: the kit's recessed plaque, or a plain inset without a kit.</summary>
        public static void AddInset(GraphicalUiElement parent)
            => AddKitFill(parent, Simpiens.Presentation.Art.UiKit.Plaque, Simpiens.Presentation.Art.UiKit.Corner, UiStyle.Inset, UiStyle.Border);

        /// <summary>
        /// A recessed stone plaque <paramref name="height"/> tall, as wide as its content plus <paramref name="padding"/>
        /// each side: the content goes in the returned row, centred vertically.
        /// </summary>
        public static ContainerRuntime Plaque(out ContainerRuntime content, float height, float padding = 10f, float spacing = 8f)
        {
            var plaque = new ContainerRuntime { WidthUnits = DimensionUnitType.RelativeToChildren, Width = padding, Height = height };
            CentreVertically(plaque);
            AddKitFill(plaque, Simpiens.Presentation.Art.UiKit.Plaque, Simpiens.Presentation.Art.UiKit.Corner, UiStyle.Panel);
            content = Row(spacing);
            content.X = padding;
            plaque.Children.Add(content);
            return plaque;
        }

        /// <summary>A kit glyph (white art) tinted <paramref name="tint"/>, at <paramref name="scale"/>, drawn 1:1; hidden without a kit.</summary>
        public static SpriteRuntime Glyph(string piece, Color tint, int scale = IconAtlas.Scale)
        {
            var glyph = new SpriteRuntime
            {
                TextureAddress = TextureAddress.Custom,
                WidthUnits = DimensionUnitType.Absolute,
                HeightUnits = DimensionUnitType.Absolute,
                Color = tint,
            };
            SetGlyph(glyph, piece, scale);
            return glyph;
        }

        /// <summary>Points <paramref name="glyph"/> at the kit's <paramref name="piece"/>, sized to it.</summary>
        public static void SetGlyph(SpriteRuntime glyph, string piece, int scale = IconAtlas.Scale)
        {
            var kit = UiStyle.Kit;
            glyph.Visible = kit != null;
            if (kit == null) return;
            var source = kit.Kit(piece, scale);
            if (!ReferenceEquals(glyph.Texture, kit.Texture)) glyph.Texture = kit.Texture;
            if (glyph.SourceRectangle != source) glyph.SourceRectangle = source;
            if (glyph.Width != source.Width) glyph.Width = source.Width;
            if (glyph.Height != source.Height) glyph.Height = source.Height;
        }

        /// <summary>
        /// A stone button with a glyph: at its left before <paramref name="text"/>, or centred when there are no words.
        /// </summary>
        public static Button GlyphButton(string text, string piece, float width, out SpriteRuntime glyph, float height = 28f)
        {
            var button = Button(text, width, height, UiText.StatusBold);
            glyph = Glyph(piece, UiStyle.TextSecondary);
            CentreVertically(glyph);
            if (string.IsNullOrEmpty(text))
            {
                glyph.XUnits = GeneralUnitType.PixelsFromMiddle;
                glyph.XOrigin = HorizontalAlignment.Center;
            }
            else
            {
                glyph.X = 9f;
                if (button.Visual is Gum.Forms.DefaultVisuals.V3.ButtonVisual visual) visual.TextInstance.X += (glyph.Width + 4f) / 2f; // the centred words clear the glyph
            }
            button.Visual.Children.Add(glyph);
            return button;
        }

        /// <summary>A slider: a stone thumb in the kit's groove (Gum's colours where there is no kit).</summary>
        public static Slider Slider(double minimum, double maximum, double step)
        {
            var slider = new Slider { Minimum = minimum, Maximum = maximum, IsSnapToTickEnabled = true, TicksFrequency = step, SmallChange = step, LargeChange = step * 5 };
            if (slider.Visual is Gum.Forms.DefaultVisuals.V3.SliderVisual visual)
            {
                visual.ThumbInstance.BackgroundColor = UiStyle.Accent;
                visual.TrackBackgroundColor = UiStyle.SliderTrack;
            }
            StoneSkin.Apply(slider);
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
