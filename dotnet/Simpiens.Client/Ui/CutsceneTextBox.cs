using Gum.DataTypes;
using Gum.Managers;
using Gum.Wireframe;
using Microsoft.Xna.Framework;
using Gum.GueDeriving;
using Simpiens.Presentation;

namespace Simpiens.Client.Ui
{
    /// <summary>
    /// A cutscene's text box (M3 design D9), Fire Emblem style: a dark panel with the speaker's name on top and the line
    /// below it, appearing letter by letter. It sits wherever its screen puts it each frame (the letterbox's bottom for a
    /// hard cutscene, the window's bottom for a dynamic one), in baked Inter 16, drawn 1:1. A speaker with a face shows
    /// its portrait on the left (Epic UI-Beta B3, D9), at the size of the atlas's cell it is given.
    /// </summary>
    public sealed class CutsceneTextBox
    {
        public CutsceneTextBox(string name)
        {
            Root = new ContainerRuntime { Name = name, Visible = false };
            UiParts.AddFrame(Root);
            Face = new SpriteRuntime
            {
                TextureAddress = TextureAddress.Custom,
                WidthUnits = DimensionUnitType.Absolute,
                HeightUnits = DimensionUnitType.Absolute,
                X = FaceMargin,
                Y = FaceMargin,
                Visible = false,
            };
            Root.Children.Add(Face);
            var column = UiParts.Column(6f);
            _column = column;
            column.HasEvents = false; // a click lands on the box itself
            column.X = 14f;
            column.Y = 10f;
            column.WidthUnits = DimensionUnitType.RelativeToParent;
            column.Width = -28f;
            Speaker = UiParts.Text(string.Empty, UiStyle.Accent, UiText.Speaker);
            column.Children.Add(Speaker);
            Line = UiParts.Text(string.Empty, UiStyle.Text, UiText.Dialogue);
            Line.WidthUnits = DimensionUnitType.RelativeToParent;
            Line.Width = 0f;
            column.Children.Add(Line);
            Root.Children.Add(column);
        }

        private const float FaceMargin = 11f;
        private readonly ContainerRuntime _column;
        private Rectangle _faceShown;

        public ContainerRuntime Root { get; }
        public TextRuntime Speaker { get; }
        public TextRuntime Line { get; }

        /// <summary>The speaker's portrait, drawn 1:1 from its cell; hidden for a speaker with no face.</summary>
        public SpriteRuntime Face { get; }

        /// <summary>Shows <paramref name="source"/> of <paramref name="texture"/> as the speaker's face, 1:1, the text beside it; null hides it.</summary>
        public void ShowFace(Microsoft.Xna.Framework.Graphics.Texture2D texture, Rectangle source)
        {
            bool shown = texture != null;
            float columnX = shown ? FaceMargin + source.Width + 10f : 14f;
            if (Face.Visible != shown) Face.Visible = shown;
            if (_column.X != columnX)
            {
                _column.X = columnX;
                _column.Width = -(columnX + 14f);
            }
            if (!shown) return;
            if (Face.Width != source.Width)
            {
                Face.Width = source.Width;
                Face.Height = source.Height;
            }
            if (!ReferenceEquals(Face.Texture, texture)) Face.Texture = texture;
            if (source != _faceShown) Face.SourceRectangle = _faceShown = source;
        }

        /// <summary>Places the box (screen pixels) and shows <paramref name="lettersShown"/> letters of <paramref name="line"/> under <paramref name="speaker"/>.</summary>
        public void Show(float x, float y, float width, float height, string speaker, string line, int lettersShown)
        {
            Root.Visible = true;
            Root.X = x;
            Root.Y = y;
            Root.Width = width;
            Root.Height = height;
            UiParts.SetText(Speaker, speaker ?? string.Empty);
            UiParts.SetText(Line, line ?? string.Empty);
            Line.MaxLettersToShow = lettersShown;
        }

        public void Hide() => Root.Visible = false;

        public bool Shown => Root.Visible;
    }
}
