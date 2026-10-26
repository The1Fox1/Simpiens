using Gum.DataTypes;
using Gum.Wireframe;
using Microsoft.Xna.Framework;
using Gum.GueDeriving;
using Simpiens.Presentation;

namespace Simpiens.Client.Ui
{
    /// <summary>
    /// A cutscene's text box (M3 design D9), Fire Emblem style: a dark panel with the speaker's name on top and the line
    /// below it, appearing letter by letter. It sits wherever its screen puts it each frame (the letterbox's bottom for a
    /// hard cutscene, the window's bottom for a dynamic one), in baked Inter 16, drawn 1:1.
    /// </summary>
    public sealed class CutsceneTextBox
    {
        public CutsceneTextBox(string name)
        {
            Root = new ContainerRuntime { Name = name, Visible = false };
            UiParts.AddFill(Root, UiParts.Alpha(UiStyle.Panel, 0.94f), UiStyle.Border);
            var column = UiParts.Column(6f);
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

        public ContainerRuntime Root { get; }
        public TextRuntime Speaker { get; }
        public TextRuntime Line { get; }

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
