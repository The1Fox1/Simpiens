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
    /// doing, health, gear, standing with the outpost, unpaid visits, a wage slider, whether it goes with the next party,
    /// and Dismiss), and Apply, which sends the wages that moved. The cards are built once, as many as the guild can hold,
    /// and shown as members fill them. Under them, the party builder (Epic 10 G5, D7): the target, the goal, the retreat
    /// rule, outfitting from stock, Dispatch and Recall, and while a party is out, where it is.
    /// </summary>
    public sealed class GuildWindow
    {
        public const float Width = 820f;
        private const float CardHeight = 64f;

        private readonly GuildModel _model;
        private readonly IconAtlas _icons;
        private readonly TextRuntime _status, _empty, _feedback, _goal, _ruleText, _dispatchText, _out;
        private readonly List<MemberCard> _cards = new List<MemberCard>(4);
        private bool _binding;

        private sealed class MemberCard
        {
            public ContainerRuntime Root, Gap;
            public TextRuntime Name, Doing, Health, Gear, Standing, Unpaid, Wage;
            public SpriteRuntime GearIcon, Face;
            public ContainerRuntime FaceBox;
            public Slider WageSlider;
            public Button Dismiss, Party;
            public int Index;
        }

        private readonly PortraitAtlas _portraits;

        /// <summary>A click on a member's face (Epic UI-Beta B4, D4): the dashboard view finds it on the map.</summary>
        public Action<EntityId> FindOnMap { get; set; }

        /// <summary>A member's face (tests click it).</summary>
        public ContainerRuntime MemberFace(int index) => _cards[index].FaceBox;

        public GuildWindow(GuildModel model, IconAtlas icons, int capacity, PortraitAtlas portraits = null)
        {
            _model = model;
            _icons = icons;
            _portraits = portraits;
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

            // The party builder (Epic 10 G5): where, how many, when to turn back, gear from stock; then send or call home
            body.Children.Add(Window.Gap(6f));
            body.Children.Add(Window.SubHeader("EXPEDITION"));
            body.Children.Add(Window.Gap(6f));
            var controls = new ContainerRuntime { WidthUnits = DimensionUnitType.RelativeToParent, Width = 0f, Height = 30f };
            TargetButton = UiParts.Button(string.Empty, 240f, 30f);
            controls.Children.Add(TargetButton.Visual);
            LessButton = UiParts.Button("-", 30f, 30f, UiText.BodyBold);
            LessButton.X = 254f;
            controls.Children.Add(LessButton.Visual);
            _goal = UiParts.Text(string.Empty, UiStyle.Accent, UiText.BodyBold);
            _goal.X = 294f;
            UiParts.CentreVertically(_goal);
            controls.Children.Add(_goal);
            MoreButton = UiParts.Button("+", 30f, 30f, UiText.BodyBold);
            MoreButton.X = 364f;
            controls.Children.Add(MoreButton.Visual);
            RuleButton = UiParts.Button(string.Empty, 110f, 30f);
            RuleButton.X = 410f;
            controls.Children.Add(RuleButton.Visual);
            OutfitButton = UiParts.Button(string.Empty, 190f, 30f);
            OutfitButton.X = 534f;
            controls.Children.Add(OutfitButton.Visual);
            body.Children.Add(controls);
            body.Children.Add(Window.Gap(6f));
            _ruleText = UiParts.Text(string.Empty, UiStyle.TextSecondary, UiText.Small);
            body.Children.Add(_ruleText);
            _out = UiParts.Text(string.Empty, UiStyle.Accent, UiText.Small);
            body.Children.Add(_out);
            var send = new ContainerRuntime { WidthUnits = DimensionUnitType.RelativeToParent, Width = 0f, Height = 40f };
            _dispatchText = UiParts.Text(string.Empty, UiStyle.TextLight, UiText.Small);
            UiParts.CentreVertically(_dispatchText);
            send.Children.Add(_dispatchText);
            DispatchButton = UiParts.Button("DISPATCH", 110f, 30f, UiText.BodyBold);
            DispatchButton.Visual.XUnits = GeneralUnitType.PixelsFromLarge;
            DispatchButton.Visual.XOrigin = HorizontalAlignment.Right;
            DispatchButton.X = -120f;
            UiParts.CentreVertically(DispatchButton.Visual);
            send.Children.Add(DispatchButton.Visual);
            RecallButton = UiParts.Button("RECALL", 110f, 30f, UiText.BodyBold);
            RecallButton.Visual.XUnits = GeneralUnitType.PixelsFromLarge;
            RecallButton.Visual.XOrigin = HorizontalAlignment.Right;
            UiParts.CentreVertically(RecallButton.Visual);
            send.Children.Add(RecallButton.Visual);
            body.Children.Add(send);
            var expedition = _model.Expedition;
            TargetButton.Click += (_, _) => { expedition.NextTarget(); Bind(); };
            LessButton.Click += (_, _) => { expedition.LessGoal(); Bind(); };
            MoreButton.Click += (_, _) => { expedition.MoreGoal(); Bind(); };
            RuleButton.Click += (_, _) => { expedition.NextRule(); Bind(); };
            OutfitButton.Click += (_, _) => { expedition.ToggleOutfit(); Bind(); };
            DispatchButton.Click += (_, _) => { expedition.Dispatch(); Bind(); };
            RecallButton.Click += (_, _) => { expedition.Recall(); Bind(); };

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
        public Button TargetButton { get; }
        public Button LessButton { get; }
        public Button MoreButton { get; }
        public Button RuleButton { get; }
        public Button OutfitButton { get; }
        public Button DispatchButton { get; }
        public Button RecallButton { get; }
        public string DispatchText => _dispatchText.Text;

        public string StatusText => _status.Text;
        public int CardsShown { get; private set; }
        public string CardName(int index) => _cards[index].Name.Text;
        public string CardStanding(int index) => _cards[index].Standing.Text;
        public Slider WageSlider(int index) => _cards[index].WageSlider;
        public Button DismissButton(int index) => _cards[index].Dismiss;
        public Button PartyButton(int index) => _cards[index].Party;

        private MemberCard AddCard(ContainerRuntime body, int index)
        {
            var card = new MemberCard { Index = index };
            card.Root = new ContainerRuntime { WidthUnits = DimensionUnitType.RelativeToParent, Width = 0f, Height = CardHeight };
            UiParts.AddFill(card.Root, UiStyle.Card, UiStyle.CardBorder);

            // Left: its face (Epic UI-Beta B4), who, and what it is doing; then its health and gear
            card.FaceBox = UiParts.FaceBox(_portraits, out card.Face);
            card.FaceBox.Click += (_, _) =>
            {
                var rows = _model.Rows;
                if (card.Index < rows.Count) FindOnMap?.Invoke(rows[card.Index].CrawlerId);
            };
            card.FaceBox.X = 12f;
            UiParts.CentreVertically(card.FaceBox);
            card.Root.Children.Add(card.FaceBox);
            var who = UiParts.Column(2f);
            who.X = _portraits != null ? 12f + PortraitAtlas.FaceSize + 8f : 12f;
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
            card.Party = UiParts.Button(string.Empty, 70f, 28f);
            card.Party.Visual.XUnits = GeneralUnitType.PixelsFromLarge;
            card.Party.Visual.XOrigin = HorizontalAlignment.Right;
            card.Party.X = -98f;
            UiParts.CentreVertically(card.Party.Visual);
            card.Root.Children.Add(card.Party.Visual);
            card.Party.Click += (_, _) =>
            {
                if (card.Index < _model.Rows.Count) _model.Rows[card.Index].InParty = !_model.Rows[card.Index].InParty;
                _model.Expedition.Refresh();
                Bind();
            };

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

        /// <summary>The card's party toggle: it goes with the next party, or stays home.</summary>
        public static string PartyText(bool inParty) => inParty ? "GOES" : "STAYS";

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
                UiParts.SetFace(card.FaceBox, card.Face, _portraits, row.HasPortrait, row.Portrait);
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
                string party = PartyText(row.InParty);
                if (card.Party.Text != party) card.Party.Text = party;
            }
            _binding = false;

            var expedition = _model.Expedition;
            if (TargetButton.Text != expedition.TargetName) TargetButton.Text = expedition.TargetName;
            UiParts.SetText(_goal, Invariant($"Goal {expedition.Goal}"));
            string rule = expedition.Rule.ToString().ToUpperInvariant();
            if (RuleButton.Text != rule) RuleButton.Text = rule;
            string outfit = expedition.Outfit ? "OUTFIT FROM STOCK" : "NO OUTFITTING";
            if (OutfitButton.Text != outfit) OutfitButton.Text = outfit;
            UiParts.SetText(_ruleText, expedition.RuleText);
            UiParts.SetText(_out, expedition.IsOut ? Invariant($"{expedition.StageText} · {expedition.TimeOut} · {expedition.HaulText}") : string.Empty);
            UiParts.SetText(_dispatchText, expedition.Feedback.Text.Length > 0 && !expedition.CanDispatch ? expedition.Feedback.Text : expedition.DispatchText);
            DispatchButton.IsEnabled = expedition.CanDispatch;
            RecallButton.IsEnabled = expedition.CanRecall;
            CardsShown = rows.Count;
            UiParts.SetText(_feedback, _model.Feedback.Text);
            _feedback.Color = UiStyle.ToneColor(_model.Feedback.Tone);
        }
    }
}
