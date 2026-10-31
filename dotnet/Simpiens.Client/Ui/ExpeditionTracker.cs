using System.Collections.Generic;
using Gum.Converters;
using Gum.DataTypes;
using Gum.GueDeriving;
using RenderingLibrary.Graphics;
using Simpiens.Presentation.Dashboard;
using static System.FormattableString;

namespace Simpiens.Client.Ui
{
    /// <summary>
    /// The expedition tracker (Epic 10 G5): a compact panel at the top right of the map view while a party is out. Where
    /// it went and under which rule, its stage and time out, the haul so far, and each member's health and part. A view
    /// over <see cref="ExpeditionModel"/>; it decides nothing.
    /// </summary>
    public sealed class ExpeditionTracker
    {
        public const float Width = 320f;

        private readonly TextRuntime _title, _stage, _haul;
        private readonly List<(TextRuntime Name, TextRuntime State)> _members = new List<(TextRuntime, TextRuntime)>(4);

        public ExpeditionTracker(int capacity)
        {
            Root = new ContainerRuntime
            {
                Width = Width,
                HeightUnits = DimensionUnitType.RelativeToChildren,
                Height = 16f,
                XUnits = GeneralUnitType.PixelsFromLarge,
                XOrigin = HorizontalAlignment.Right,
                X = -16f,
                Y = HeaderBar.Height + 12f,
                Visible = false,
            };
            UiParts.AddFill(Root, UiStyle.Panel, UiStyle.Border);
            var column = UiParts.Column(3f);
            column.X = 12f;
            column.Y = 10f;
            column.Children.Add(UiParts.Text("EXPEDITION", UiStyle.Accent, UiText.Small));
            _title = UiParts.Text(string.Empty, UiStyle.TextBright, UiText.BodyBold);
            _stage = UiParts.Text(string.Empty, UiStyle.TextLight, UiText.Small);
            _haul = UiParts.Text(string.Empty, UiStyle.GoldText, UiText.Small);
            column.Children.Add(_title);
            column.Children.Add(_stage);
            column.Children.Add(_haul);
            for (int i = 0; i < capacity; i++)
            {
                var line = new ContainerRuntime { Width = Width - 24f, Height = 18f };
                var name = UiParts.Text(string.Empty, UiStyle.Text, UiText.Small);
                var state = UiParts.Text(string.Empty, UiStyle.TextSecondary, UiText.Small);
                state.X = 110f;
                line.Children.Add(name);
                line.Children.Add(state);
                column.Children.Add(line);
                _members.Add((name, state));
            }
            Root.Children.Add(column);
        }

        public ContainerRuntime Root { get; }
        public bool IsShown => Root.Visible;
        public string StageText => _stage.Text;

        /// <summary>Shown while a party is out, on the map view (the dashboard's panels hidden).</summary>
        public void Bind(ExpeditionModel model, bool mapView)
        {
            Root.Visible = model.IsOut && mapView;
            if (!Root.Visible) return;
            UiParts.SetText(_title, model.Title);
            UiParts.SetText(_stage, Invariant($"{model.StageText} · {model.TimeOut}"));
            UiParts.SetText(_haul, model.HaulText);
            for (int i = 0; i < _members.Count; i++)
            {
                var (name, state) = _members[i];
                bool shown = i < model.Members.Count;
                name.Parent.Visible = shown;
                if (!shown) continue;
                var row = model.Members[i];
                UiParts.SetText(name, row.Name);
                UiParts.SetText(state, row.WithParty ? Invariant($"{row.Health:F0}% · {row.Status}") : row.Status);
                state.Color = row.Downed ? UiStyle.Danger : row.WithParty ? UiStyle.TextSecondary : UiStyle.TextMuted;
            }
        }
    }
}
