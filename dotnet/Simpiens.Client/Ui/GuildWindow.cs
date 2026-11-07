using System;
using System.Collections.Generic;
using Gum.Converters;
using Gum.DataTypes;
using Gum.Forms.Controls;
using Gum.GueDeriving;
using Gum.Managers;
using RenderingLibrary.Graphics;
using Simpiens.Presentation.Dashboard;
using static System.FormattableString;

namespace Simpiens.Client.Ui
{
    /// <summary>
    /// The guild roster (Epic 10 G4, D6), a window like the merchant terminal, in two tabs (Epic 10 Part 2, W5.1) beside
    /// the guild's status. Members: a card per member (its name and what it is doing, unpaid visits, health, standing with
    /// the outpost and gear, a wage slider, whether it goes with the next party, and Dismiss), and Apply, which sends the
    /// wages that moved. The cards are built once, as many as the guild can hold, and shown as members fill them.
    /// Expeditions: the party builder (Epic 10 G5, D7): the target, the goal, the retreat rule, outfitting from stock,
    /// draughts, Dispatch and Recall, and while a party is out, where it is.
    /// </summary>
    public sealed class GuildWindow
    {
        public const float Width = 960f;

        /// <summary>Two lines (Epic 10 Part 2, W5.1), so the guild's eight cards fit a 720-row window.</summary>
        private const float CardHeight = 48f;

        private readonly GuildModel _model;
        private readonly IconAtlas _icons;
        private readonly TextRuntime _status, _empty, _feedback, _goal, _ruleText, _dispatchText, _out;
        private readonly List<MemberCard> _cards = new List<MemberCard>(8);
        private readonly ContainerRuntime _membersPage, _expeditionsPage;
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
            Window = new Window("guild-roster", "The Guild", "Your members, their wages and their standing with the outpost.", Width, Simpiens.Presentation.Art.UiKit.FrameSteel, Simpiens.Presentation.Art.UiKit.Shield);
            var body = Window.Body;

            // The tabs and the guild's status (Epic 10 Part 2, W5.1)
            var tabBar = new ContainerRuntime { WidthUnits = DimensionUnitType.RelativeToParent, Width = 0f, Height = 36f };
            UiParts.AddInset(tabBar);
            var tabs = UiParts.Row(6f);
            tabs.X = 4f;
            MembersTab = UiParts.Button("Members", 120f, 28f, UiText.BodyBold);
            ExpeditionsTab = UiParts.Button("Expeditions", 120f, 28f, UiText.BodyBold);
            tabs.Children.Add(MembersTab.Visual);
            tabs.Children.Add(ExpeditionsTab.Visual);
            tabBar.Children.Add(tabs);
            _status = UiParts.Text(string.Empty, UiStyle.Text, UiText.StatusBold);
            _status.X = 270f;
            UiParts.CentreVertically(_status);
            tabBar.Children.Add(_status);
            body.Children.Add(tabBar);
            body.Children.Add(Window.Gap(10f));
            MembersTab.Click += (_, _) => { _model.ShowMembers(); Bind(); };
            ExpeditionsTab.Click += (_, _) => { _model.ShowExpeditions(); Bind(); };

            // Members: the cards, then the wages' Apply
            _membersPage = Page();
            _empty = UiParts.Text(GuildModel.EmptyText, UiStyle.TextMuted, UiText.Body);
            _membersPage.Children.Add(_empty);
            for (int i = 0; i < capacity; i++) _cards.Add(AddCard(_membersPage, i));
            _membersPage.Children.Add(Window.Gap(2f));
            _membersPage.Children.Add(Window.Rule());
            var footer = new ContainerRuntime { WidthUnits = DimensionUnitType.RelativeToParent, Width = 0f, Height = 48f };
            _feedback = UiParts.Text(string.Empty, UiStyle.Success, UiText.Small);
            UiParts.CentreVertically(_feedback);
            footer.Children.Add(_feedback);
            ApplyButton = UiParts.PrimaryButton("Set wages", 150f, 32f, UiText.BodyBold);
            ApplyButton.Visual.XUnits = GeneralUnitType.PixelsFromLarge;
            ApplyButton.Visual.XOrigin = HorizontalAlignment.Right;
            UiParts.CentreVertically(ApplyButton.Visual);
            footer.Children.Add(ApplyButton.Visual);
            _membersPage.Children.Add(footer);
            body.Children.Add(_membersPage);
            ApplyButton.Click += (_, _) => { _model.ApplyWages(); Bind(); };

            // Expeditions (Epic 10 G5): where, how many, when to turn back, gear from stock, and draughts each (Epic 10
            // Part 2, W2); then send or call home, and where the party is
            _expeditionsPage = Page();
            var controls = new ContainerRuntime { WidthUnits = DimensionUnitType.RelativeToParent, Width = 0f, Height = 30f };
            // Which party (Epic 10 Part 2, W5): its members are chosen on the Members tab
            PartySelectButton = UiParts.Button(string.Empty, 100f, 30f, UiText.BodyBold);
            controls.Children.Add(PartySelectButton.Visual);
            TargetButton = UiParts.Button(string.Empty, 200f, 30f);
            TargetButton.X = 110f;
            controls.Children.Add(TargetButton.Visual);
            LessButton = UiParts.Button("-", 30f, 30f, UiText.BodyBold);
            LessButton.X = 320f;
            controls.Children.Add(LessButton.Visual);
            _goal = UiParts.Text(string.Empty, UiStyle.Accent, UiText.BodyBold);
            _goal.X = 358f;
            UiParts.CentreVertically(_goal);
            controls.Children.Add(_goal);
            MoreButton = UiParts.Button("+", 30f, 30f, UiText.BodyBold);
            MoreButton.X = 424f;
            controls.Children.Add(MoreButton.Visual);
            RuleButton = UiParts.Button(string.Empty, 100f, 30f);
            RuleButton.X = 464f;
            controls.Children.Add(RuleButton.Visual);
            OutfitButton = UiParts.Button(string.Empty, 150f, 30f);
            OutfitButton.X = 574f;
            controls.Children.Add(OutfitButton.Visual);
            DraughtsButton = UiParts.Button(string.Empty, 164f, 30f);
            DraughtsButton.X = 734f;
            controls.Children.Add(DraughtsButton.Visual);
            _expeditionsPage.Children.Add(controls);
            _expeditionsPage.Children.Add(Window.Gap(6f));
            _ruleText = UiParts.Text(string.Empty, UiStyle.TextSecondary, UiText.Small);
            _expeditionsPage.Children.Add(_ruleText);
            _out = UiParts.Text(string.Empty, UiStyle.Accent, UiText.Small);
            _expeditionsPage.Children.Add(_out);
            var send = new ContainerRuntime { WidthUnits = DimensionUnitType.RelativeToParent, Width = 0f, Height = 40f };
            _dispatchText = UiParts.Text(string.Empty, UiStyle.TextLight, UiText.Small);
            UiParts.CentreVertically(_dispatchText);
            send.Children.Add(_dispatchText);
            DispatchButton = UiParts.Button("Dispatch", 110f, 30f, UiText.BodyBold);
            DispatchButton.Visual.XUnits = GeneralUnitType.PixelsFromLarge;
            DispatchButton.Visual.XOrigin = HorizontalAlignment.Right;
            DispatchButton.X = -120f;
            UiParts.CentreVertically(DispatchButton.Visual);
            send.Children.Add(DispatchButton.Visual);
            RecallButton = UiParts.Button("Recall", 110f, 30f, UiText.BodyBold);
            RecallButton.Visual.XUnits = GeneralUnitType.PixelsFromLarge;
            RecallButton.Visual.XOrigin = HorizontalAlignment.Right;
            UiParts.CentreVertically(RecallButton.Visual);
            send.Children.Add(RecallButton.Visual);
            _expeditionsPage.Children.Add(send);
            _expeditionsPage.Children.Add(Window.Gap(6f));
            body.Children.Add(_expeditionsPage);
            var expedition = _model.Expedition;
            PartySelectButton.Click += (_, _) => { expedition.NextParty(); Bind(); };
            TargetButton.Click += (_, _) => { expedition.NextTarget(); Bind(); };
            LessButton.Click += (_, _) => { expedition.LessGoal(); Bind(); };
            MoreButton.Click += (_, _) => { expedition.MoreGoal(); Bind(); };
            RuleButton.Click += (_, _) => { expedition.NextRule(); Bind(); };
            OutfitButton.Click += (_, _) => { expedition.ToggleOutfit(); Bind(); };
            DraughtsButton.Click += (_, _) => { expedition.NextDraughts(); Bind(); };
            DispatchButton.Click += (_, _) => { expedition.Dispatch(); Bind(); };
            RecallButton.Click += (_, _) => { expedition.Recall(); Bind(); };
        }

        /// <summary>A tab's page: a column the body's width, shown while its tab is.</summary>
        private static ContainerRuntime Page()
        {
            var page = UiParts.Column(0f);
            page.WidthUnits = DimensionUnitType.RelativeToParent;
            page.Width = 0f;
            return page;
        }

        public Window Window { get; }
        public Button MembersTab { get; }
        public Button ExpeditionsTab { get; }
        public Button ApplyButton { get; }
        public Button PartySelectButton { get; }
        public Button TargetButton { get; }
        public Button LessButton { get; }
        public Button MoreButton { get; }
        public Button RuleButton { get; }
        public Button OutfitButton { get; }
        public Button DraughtsButton { get; }
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
        public ContainerRuntime Card(int index) => _cards[index].Root;

        private MemberCard AddCard(ContainerRuntime body, int index)
        {
            var card = new MemberCard { Index = index };
            card.Root = new ContainerRuntime { WidthUnits = DimensionUnitType.RelativeToParent, Width = 0f, Height = CardHeight };
            UiParts.AddFill(card.Root, UiStyle.Card, UiStyle.CardBorder);

            // Left: its face (Epic UI-Beta B4); who, with any unpaid visits, and what it is doing
            card.FaceBox = UiParts.FaceBox(_portraits, out card.Face);
            card.FaceBox.Click += (_, _) =>
            {
                var rows = _model.Rows;
                if (card.Index < rows.Count) FindOnMap?.Invoke(rows[card.Index].CrawlerId);
            };
            card.FaceBox.X = 12f;
            UiParts.CentreVertically(card.FaceBox);
            card.Root.Children.Add(card.FaceBox);
            var who = UiParts.Column(1f);
            who.X = _portraits != null ? 12f + PortraitAtlas.FaceSize + 8f : 12f;
            who.Y = 6f;
            var nameLine = Line(8f);
            card.Name = UiParts.Text(string.Empty, UiStyle.TextBright, UiText.BodyBold);
            card.Unpaid = UiParts.Text(string.Empty, UiStyle.Warning, UiText.Small);
            UiParts.CentreVertically(card.Unpaid);
            nameLine.Children.Add(card.Name);
            nameLine.Children.Add(card.Unpaid);
            who.Children.Add(nameLine);
            card.Doing = UiParts.Text(string.Empty, UiStyle.TextMuted, UiText.Small);
            who.Children.Add(card.Doing);
            card.Root.Children.Add(who);

            // Middle: health and standing; then gear
            var standing = UiParts.Column(3f);
            standing.X = 270f;
            standing.Y = 7f;
            var standingLine = Line(10f);
            card.Health = UiParts.Text(string.Empty, UiStyle.TextLight, UiText.Small);
            card.Standing = UiParts.Text(string.Empty, UiStyle.TextLight, UiText.Small);
            standingLine.Children.Add(card.Health);
            standingLine.Children.Add(card.Standing);
            standing.Children.Add(standingLine);
            var gearLine = new ContainerRuntime { Width = 230f, Height = IconAtlas.SmallSize };
            card.GearIcon = UiParts.Icon(_icons, null, scale: 1);
            gearLine.Children.Add(card.GearIcon);
            card.Gear = UiParts.Text(string.Empty, UiStyle.TextSecondary, UiText.Small);
            UiParts.CentreVertically(card.Gear);
            gearLine.Children.Add(card.Gear);
            standing.Children.Add(gearLine);
            card.Root.Children.Add(standing);

            // Right: the wage slider and its value, then Dismiss
            card.WageSlider = UiParts.Slider(1, 50, 1);
            card.WageSlider.Visual.WidthUnits = DimensionUnitType.Absolute;
            card.WageSlider.Width = 120f;
            card.WageSlider.X = 520f;
            UiParts.CentreVertically(card.WageSlider.Visual);
            card.Root.Children.Add(card.WageSlider.Visual);
            card.Wage = UiParts.Text(string.Empty, UiStyle.Accent, UiText.BodyBold);
            card.Wage.X = 650f;
            UiParts.CentreVertically(card.Wage);
            card.Root.Children.Add(card.Wage);
            card.Dismiss = UiParts.Button("Dismiss", 78f, 28f);
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
                if (card.Index < _model.Rows.Count) _model.CycleParty(_model.Rows[card.Index].CrawlerId);
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
            card.Gap = Window.Gap(4f);
            body.Children.Add(card.Gap);
            return card;
        }

        /// <summary>A left-to-right line of texts inside a card's column.</summary>
        private static ContainerRuntime Line(float spacing) => new ContainerRuntime
        {
            ChildrenLayout = ChildrenLayout.LeftToRightStack,
            StackSpacing = spacing,
            WidthUnits = DimensionUnitType.RelativeToChildren,
            HeightUnits = DimensionUnitType.RelativeToChildren,
            Width = 0f,
            Height = 0f,
        };

        public static string WageText(int gold) => Invariant($"{gold}g a visit");

        /// <summary>Beside the name, in warning colour: "Unpaid 2 visits"; nothing while every visit is paid.</summary>
        public static string UnpaidText(int visits) => visits == 0 ? string.Empty : visits == 1 ? "Unpaid 1 visit" : Invariant($"Unpaid {visits} visits");

        /// <summary>The card's party button (Epic 10 Part 2, W5): its party, or none.</summary>
        public static string PartyText(byte party) => party == 0 ? "No party" : Invariant($"Party {party}");

        public void Bind()
        {
            Window.IsShown = _model.IsOpen;
            if (!_model.IsOpen) return;
            Window.SnapToPixels();
            UiParts.SetText(_status, _model.Status);
            bool expeditions = _model.ShowingExpeditions;
            _membersPage.Visible = !expeditions;
            _expeditionsPage.Visible = expeditions;
            StoneSkin.SetActive(MembersTab, !expeditions);
            StoneSkin.SetActive(ExpeditionsTab, expeditions);
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
                UiParts.SetText(card.Gear, row.Bunk.Length > 0 ? "Gear: " + row.Gear + " · " + row.Bunk : "Gear: " + row.Gear);
                UiParts.SetIcon(card.GearIcon, _icons, row.GearIcon);
                float gearX = row.GearIcon != null ? IconAtlas.SmallSize + 6f : 0f;
                if (card.Gear.X != gearX) card.Gear.X = gearX;
                UiParts.SetText(card.Standing, row.Standing);
                UiParts.SetText(card.Unpaid, UnpaidText(row.UnpaidVisits));
                if ((int)Math.Round(card.WageSlider.Value) != row.PendingWage) card.WageSlider.Value = row.PendingWage;
                UiParts.SetText(card.Wage, WageText(row.PendingWage));
                string party = PartyText(row.Party);
                if (card.Party.Text != party) card.Party.Text = party;
            }
            _binding = false;

            var expedition = _model.Expedition;
            if (PartySelectButton.Text != expedition.PartyText) PartySelectButton.Text = expedition.PartyText;
            if (TargetButton.Text != expedition.TargetName) TargetButton.Text = expedition.TargetName;
            UiParts.SetText(_goal, Invariant($"Goal {expedition.Goal}"));
            string rule = expedition.Rule.ToString().ToUpperInvariant();
            if (RuleButton.Text != rule) RuleButton.Text = rule;
            string outfit = expedition.OutfitText;
            if (OutfitButton.Text != outfit) OutfitButton.Text = outfit;
            if (DraughtsButton.Text != expedition.DraughtsText) DraughtsButton.Text = expedition.DraughtsText;
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
