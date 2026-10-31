using System;
using System.Collections.Generic;
using Gum.Converters;
using Gum.DataTypes;
using Gum.Forms.Controls;
using Gum.GueDeriving;
using RenderingLibrary.Graphics;
using Simpiens.Presentation.Dashboard;
using static System.FormattableString;

namespace Simpiens.Client.Ui
{
    /// <summary>
    /// The guild roster (Epic 10 G4, D6), a window like the merchant terminal: a card per member (its name and what it is
    /// doing, health, gear, standing with the outpost, unpaid visits, a wage slider and Dismiss), and Apply, which sends
    /// the wages that moved. The cards are built once, as many as the guild can hold, and shown as members fill them.
    /// </summary>
    public sealed class GuildWindow
    {
        public const float Width = 740f;
        private const float CardHeight = 64f;

        private readonly GuildModel _model;
        private readonly IconAtlas _icons;
        private readonly TextRuntime _status, _empty, _feedback;
        private readonly List<MemberCard> _cards = new List<MemberCard>(4);
        private bool _binding;

        private sealed class MemberCard
        {
            public ContainerRuntime Root, Gap;
            public TextRuntime Name, Doing, Health, Gear, Standing, Unpaid, Wage;
            public SpriteRuntime GearIcon;
            public Slider WageSlider;
            public Button Dismiss;
            public int Index;
        }

        public GuildWindow(GuildModel model, IconAtlas icons, int capacity)
        {
            _model = model;
            _icons = icons;
            Window = new Window("guild-roster", "GUILD ROSTER", "Your members, their wages and their standing with the outpost.", Width, UiStyle.Success);
            var body = Window.Body;

            var statusBar = new ContainerRuntime { WidthUnits = DimensionUnitType.RelativeToParent, Width = 0f, Height = 34f };
            UiParts.AddFill(statusBar, UiStyle.Inset, UiStyle.Border);
            _status = UiParts.Text(string.Empty, UiStyle.Text, UiText.StatusBold);
            _status.X = 12f;
            UiParts.CentreVertically(_status);
            statusBar.Children.Add(_status);
            body.Children.Add(statusBar);
            body.Children.Add(Window.Gap(14f));
            body.Children.Add(Window.SubHeader("MEMBERS"));
            body.Children.Add(Window.Gap(6f));
            _empty = UiParts.Text(GuildModel.EmptyText, UiStyle.TextMuted, UiText.Body);
            body.Children.Add(_empty);
            for (int i = 0; i < capacity; i++) _cards.Add(AddCard(body, i));

            body.Children.Add(Window.Gap(6f));
            body.Children.Add(Window.Rule());
            var footer = new ContainerRuntime { WidthUnits = DimensionUnitType.RelativeToParent, Width = 0f, Height = 48f };
            _feedback = UiParts.Text(string.Empty, UiStyle.Success, UiText.Small);
            UiParts.CentreVertically(_feedback);
            footer.Children.Add(_feedback);
            ApplyButton = UiParts.Button("APPLY WAGES", 150f, 32f, UiText.BodyBold);
            ApplyButton.Visual.XUnits = GeneralUnitType.PixelsFromLarge;
            ApplyButton.Visual.XOrigin = HorizontalAlignment.Right;
            UiParts.CentreVertically(ApplyButton.Visual);
            footer.Children.Add(ApplyButton.Visual);
            body.Children.Add(footer);
            ApplyButton.Click += (_, _) => { _model.ApplyWages(); Bind(); };
        }

        public Window Window { get; }
        public Button ApplyButton { get; }

        public string StatusText => _status.Text;
        public int CardsShown { get; private set; }
        public string CardName(int index) => _cards[index].Name.Text;
        public string CardStanding(int index) => _cards[index].Standing.Text;
        public Slider WageSlider(int index) => _cards[index].WageSlider;
        public Button DismissButton(int index) => _cards[index].Dismiss;

        private MemberCard AddCard(ContainerRuntime body, int index)
        {
            var card = new MemberCard { Index = index };
            card.Root = new ContainerRuntime { WidthUnits = DimensionUnitType.RelativeToParent, Width = 0f, Height = CardHeight };
            UiParts.AddFill(card.Root, UiStyle.Card, UiStyle.CardBorder);

            // Left: who, and what it is doing; then its health and gear
            var who = UiParts.Column(2f);
            who.X = 12f;
            who.Y = 8f;
            card.Name = UiParts.Text(string.Empty, UiStyle.TextBright, UiText.BodyBold);
            card.Doing = UiParts.Text(string.Empty, UiStyle.TextMuted, UiText.Small);
            who.Children.Add(card.Name);
            who.Children.Add(card.Doing);
            var gearLine = new ContainerRuntime { Width = 220f, Height = IconAtlas.SmallSize };
            card.GearIcon = UiParts.Icon(_icons, null, scale: 1);
            gearLine.Children.Add(card.GearIcon);
            card.Gear = UiParts.Text(string.Empty, UiStyle.TextSecondary, UiText.Small);
            UiParts.CentreVertically(card.Gear);
            gearLine.Children.Add(card.Gear);
            who.Children.Add(gearLine);
            card.Root.Children.Add(who);

            // Middle: health, standing, unpaid visits
            var standing = UiParts.Column(2f);
            standing.X = 250f;
            standing.Y = 8f;
            card.Health = UiParts.Text(string.Empty, UiStyle.TextLight, UiText.Small);
            card.Standing = UiParts.Text(string.Empty, UiStyle.TextLight, UiText.Small);
            card.Unpaid = UiParts.Text(string.Empty, UiStyle.TextSecondary, UiText.Small);
            standing.Children.Add(card.Health);
            standing.Children.Add(card.Standing);
            standing.Children.Add(card.Unpaid);
            card.Root.Children.Add(standing);

            // Right: the wage slider and its value, then Dismiss
            card.WageSlider = UiParts.Slider(1, 50, 1);
            card.WageSlider.Visual.WidthUnits = DimensionUnitType.Absolute;
            card.WageSlider.Width = 120f;
            card.WageSlider.X = 410f;
            UiParts.CentreVertically(card.WageSlider.Visual);
            card.Root.Children.Add(card.WageSlider.Visual);
            card.Wage = UiParts.Text(string.Empty, UiStyle.Accent, UiText.BodyBold);
            card.Wage.X = 540f;
            UiParts.CentreVertically(card.Wage);
            card.Root.Children.Add(card.Wage);
            card.Dismiss = UiParts.Button("DISMISS", 78f, 28f);
            card.Dismiss.Visual.XUnits = GeneralUnitType.PixelsFromLarge;
            card.Dismiss.Visual.XOrigin = HorizontalAlignment.Right;
            card.Dismiss.X = -12f;
            UiParts.CentreVertically(card.Dismiss.Visual);
            card.Root.Children.Add(card.Dismiss.Visual);

            card.WageSlider.ValueChanged += (_, _) =>
            {
                int gold = (int)Math.Round(card.WageSlider.Value);
                card.Wage.Text = WageText(gold);
                if (!_binding && card.Index < _model.Rows.Count) _model.Rows[card.Index].PendingWage = gold;
            };
            card.Dismiss.Click += (_, _) =>
            {
                if (card.Index < _model.Rows.Count) _model.Dismiss(_model.Rows[card.Index].CrawlerId);
                Bind();
            };

            body.Children.Add(card.Root);
            card.Gap = Window.Gap(8f);
            body.Children.Add(card.Gap);
            return card;
        }

        public static string WageText(int gold) => Invariant($"{gold}g a visit");

        public void Bind()
        {
            Window.IsShown = _model.IsOpen;
            if (!_model.IsOpen) return;
            Window.SnapToPixels();
            UiParts.SetText(_status, _model.Status);
            var rows = _model.Rows;
            _empty.Visible = rows.Count == 0;
            _binding = true;
            for (int i = 0; i < _cards.Count; i++)
            {
                var card = _cards[i];
                bool shown = i < rows.Count;
                card.Root.Visible = shown;
                card.Gap.Visible = shown;
                if (!shown) continue;
                var row = rows[i];
                UiParts.SetText(card.Name, row.Name);
                UiParts.SetText(card.Doing, row.Doing);
                UiParts.SetText(card.Health, row.Downed ? Invariant($"Health {row.Health:F0}% · {DossierModel.DownedBadge}") : Invariant($"Health {row.Health:F0}%"));
                card.Health.Color = row.Downed ? UiStyle.Danger : UiStyle.TextLight;
                UiParts.SetText(card.Gear, "Gear: " + row.Gear);
                UiParts.SetIcon(card.GearIcon, _icons, row.GearIcon);
                float gearX = row.GearIcon != null ? IconAtlas.SmallSize + 6f : 0f;
                if (card.Gear.X != gearX) card.Gear.X = gearX;
                UiParts.SetText(card.Standing, row.Standing);
                UiParts.SetText(card.Unpaid, row.UnpaidVisits == 0 ? "Paid every visit" : Invariant($"Unpaid {row.UnpaidVisits} visit(s) in a row"));
                card.Unpaid.Color = row.UnpaidVisits == 0 ? UiStyle.TextSecondary : UiStyle.Warning;
                if ((int)Math.Round(card.WageSlider.Value) != row.PendingWage) card.WageSlider.Value = row.PendingWage;
                UiParts.SetText(card.Wage, WageText(row.PendingWage));
            }
            _binding = false;
            CardsShown = rows.Count;
            UiParts.SetText(_feedback, _model.Feedback.Text);
            _feedback.Color = UiStyle.ToneColor(_model.Feedback.Tone);
        }
    }
}
