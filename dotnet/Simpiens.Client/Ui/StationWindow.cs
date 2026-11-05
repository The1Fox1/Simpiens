using System.Collections.Generic;
using Gum.Converters;
using Gum.DataTypes;
using Gum.Forms.Controls;
using Gum.GueDeriving;
using RenderingLibrary.Graphics;
using Simpiens.Presentation.Dashboard;
using Simpiens.Simulation.Workshops;
using static System.FormattableString;

namespace Simpiens.Client.Ui
{
    /// <summary>
    /// The station window (Epic 10 Part 2, W1.6a), a window like the guild's: a tab per station; the station's state and,
    /// before it stands, Build with what it costs or why not; once built, its artisan (face, what it is doing, its energy,
    /// a wage stepper, the day's six blocks as toggles, Dismiss) or those who would take the work, each with Hire; then
    /// what it makes, with a count and Order, and the order under way with why it waits. Built once for the most stations,
    /// candidates and recipes there can be, and shown as the model fills them.
    /// </summary>
    public sealed class StationWindow
    {
        public const float Width = 760f;
        private const float CardHeight = 64f;
        private const float RowHeight = 40f;

        private readonly StationModel _model;
        private readonly IconAtlas _icons;
        private readonly PortraitAtlas _portraits;
        private readonly TextRuntime _status, _description, _buildNote, _artisanName, _artisanDoing, _artisanState, _wage, _shiftsNote,
            _candidatesNote, _orderText, _orderNote, _feedback;
        private readonly ContainerRuntime _unbuilt, _built, _artisanCard, _shifts, _candidates, _artisanFace;
        private readonly SpriteRuntime _artisanFaceSprite;
        private readonly List<Button> _tabs = new List<Button>(2);
        private readonly List<Button> _blocks = new List<Button>(ShiftBlocks.Count);
        private readonly List<CandidateCard> _candidateCards = new List<CandidateCard>(StationModel.MaxCandidates);
        private readonly List<RecipeRow> _recipeRows = new List<RecipeRow>(4);

        private sealed class CandidateCard
        {
            public ContainerRuntime Root, Gap, FaceBox;
            public SpriteRuntime Face;
            public TextRuntime Name, Standing, Ask;
            public Button Hire;
            public int Index;
        }

        private sealed class RecipeRow
        {
            public ContainerRuntime Root;
            public SpriteRuntime Icon;
            public TextRuntime Name, Recipe, Count;
            public Button Less, More, Order;
        }

        /// <summary>A click on a face (Epic UI-Beta B4, D4): the dashboard view finds it on the map.</summary>
        public System.Action<EntityId> FindOnMap { get; set; }

        public StationWindow(StationModel model, IconAtlas icons, int stations, int recipes, PortraitAtlas portraits = null)
        {
            _model = model;
            _icons = icons;
            _portraits = portraits;
            Window = new Window("stations", "The Workshop", "Build the stations, hire their artisans, set their shifts and what they make.", Width,
                Simpiens.Presentation.Art.UiKit.FrameIron, Simpiens.Presentation.Art.UiKit.Anvil);
            var body = Window.Body;

            // A tab per station
            body.Children.Add(Window.Gap(6f));
            var tabs = UiParts.Row(6f);
            for (int i = 0; i < stations; i++)
            {
                int index = i;
                var tab = UiParts.Button(string.Empty, 110f, 28f, UiText.BodyBold);
                tab.Click += (_, _) => { _model.Show(index); Bind(); };
                _tabs.Add(tab);
                tabs.Children.Add(tab.Visual);
            }
            body.Children.Add(tabs);
            body.Children.Add(Window.Gap(10f));

            var statusBar = new ContainerRuntime { WidthUnits = DimensionUnitType.RelativeToParent, Width = 0f, Height = 34f };
            UiParts.AddInset(statusBar);
            _status = UiParts.Text(string.Empty, UiStyle.Text, UiText.StatusBold);
            _status.X = 12f;
            UiParts.CentreVertically(_status);
            statusBar.Children.Add(_status);
            body.Children.Add(statusBar);
            body.Children.Add(Window.Gap(6f));
            _description = UiParts.Text(string.Empty, UiStyle.TextMuted, UiText.Small);
            body.Children.Add(_description);
            body.Children.Add(Window.Gap(10f));

            // Before it stands: what it costs, or why not, and Build
            _unbuilt = new ContainerRuntime { WidthUnits = DimensionUnitType.RelativeToParent, Width = 0f, Height = 40f };
            _buildNote = UiParts.Text(string.Empty, UiStyle.TextLight, UiText.Small);
            UiParts.CentreVertically(_buildNote);
            _unbuilt.Children.Add(_buildNote);
            BuildButton = UiParts.PrimaryButton(string.Empty, 170f, 32f, UiText.BodyBold);
            RightAligned(BuildButton, 0f);
            _unbuilt.Children.Add(BuildButton.Visual);
            body.Children.Add(_unbuilt);
            BuildButton.Click += (_, _) => { _model.Build(); Bind(); };

            // Once built: the artisan or those who would take the work, then the order
            _built = UiParts.Column(0f);
            _built.WidthUnits = DimensionUnitType.RelativeToParent;
            _built.Width = 0f;
            body.Children.Add(_built);
            _built.Children.Add(Window.SubHeader("ARTISAN"));
            _built.Children.Add(Window.Gap(6f));

            _artisanCard = new ContainerRuntime { WidthUnits = DimensionUnitType.RelativeToParent, Width = 0f, Height = CardHeight };
            UiParts.AddFill(_artisanCard, UiStyle.Card, UiStyle.CardBorder);
            _artisanFace = UiParts.FaceBox(_portraits, out _artisanFaceSprite);
            _artisanFace.X = 12f;
            UiParts.CentreVertically(_artisanFace);
            _artisanFace.Click += (_, _) => { if (_model.HasArtisan) FindOnMap?.Invoke(_model.ArtisanId); };
            _artisanCard.Children.Add(_artisanFace);
            var who = UiParts.Column(2f);
            who.X = _portraits != null ? 12f + PortraitAtlas.FaceSize + 8f : 12f;
            who.Y = 8f;
            _artisanName = UiParts.Text(string.Empty, UiStyle.TextBright, UiText.BodyBold);
            _artisanDoing = UiParts.Text(string.Empty, UiStyle.TextMuted, UiText.Small);
            _artisanState = UiParts.Text(string.Empty, UiStyle.TextLight, UiText.Small);
            who.Children.Add(_artisanName);
            who.Children.Add(_artisanDoing);
            who.Children.Add(_artisanState);
            _artisanCard.Children.Add(who);
            LessWageButton = UiParts.Button("-", 30f, 28f, UiText.BodyBold);
            LessWageButton.X = 380f;
            UiParts.CentreVertically(LessWageButton.Visual);
            _artisanCard.Children.Add(LessWageButton.Visual);
            _wage = UiParts.Text(string.Empty, UiStyle.Accent, UiText.BodyBold);
            _wage.X = 420f;
            UiParts.CentreVertically(_wage);
            _artisanCard.Children.Add(_wage);
            MoreWageButton = UiParts.Button("+", 30f, 28f, UiText.BodyBold);
            MoreWageButton.X = 530f;
            UiParts.CentreVertically(MoreWageButton.Visual);
            _artisanCard.Children.Add(MoreWageButton.Visual);
            DismissButton = UiParts.Button("Dismiss", 78f, 28f);
            RightAligned(DismissButton, -12f);
            UiParts.CentreVertically(DismissButton.Visual);
            _artisanCard.Children.Add(DismissButton.Visual);
            _built.Children.Add(_artisanCard);
            LessWageButton.Click += (_, _) => { _model.LessWage(); Bind(); };
            MoreWageButton.Click += (_, _) => { _model.MoreWage(); Bind(); };
            DismissButton.Click += (_, _) => { _model.Dismiss(); Bind(); };

            // Its shifts: the day's six blocks, then Set
            _shifts = UiParts.Column(4f);
            _shifts.WidthUnits = DimensionUnitType.RelativeToParent;
            _shifts.Width = 0f;
            _shifts.Children.Add(Window.Gap(4f));
            var blocks = new ContainerRuntime { WidthUnits = DimensionUnitType.RelativeToParent, Width = 0f, Height = 30f };
            for (int b = 0; b < ShiftBlocks.Count; b++)
            {
                int block = b;
                var toggle = UiParts.Button(StationModel.BlockLabel(b), 76f, 28f);
                toggle.X = b * 82f;
                toggle.Click += (_, _) => { _model.ToggleBlock(block); Bind(); };
                _blocks.Add(toggle);
                blocks.Children.Add(toggle.Visual);
            }
            ApplyButton = UiParts.PrimaryButton("Set wage and shifts", 170f, 28f, UiText.BodyBold);
            RightAligned(ApplyButton, 0f);
            blocks.Children.Add(ApplyButton.Visual);
            ApplyButton.Click += (_, _) => { _model.ApplyArtisan(); Bind(); };
            _shifts.Children.Add(blocks);
            _shiftsNote = UiParts.Text(string.Empty, UiStyle.TextSecondary, UiText.Small);
            _shifts.Children.Add(_shiftsNote);
            _built.Children.Add(_shifts);

            // Or: who would take the work
            _candidates = UiParts.Column(0f);
            _candidates.WidthUnits = DimensionUnitType.RelativeToParent;
            _candidates.Width = 0f;
            _candidatesNote = UiParts.Text(string.Empty, UiStyle.TextMuted, UiText.Body);
            _candidates.Children.Add(_candidatesNote);
            _candidates.Children.Add(Window.Gap(6f));
            for (int i = 0; i < StationModel.MaxCandidates; i++) _candidateCards.Add(AddCandidate(_candidates, i));
            _built.Children.Add(_candidates);

            // What it makes
            _built.Children.Add(Window.Gap(10f));
            _built.Children.Add(Window.SubHeader("ORDER"));
            _built.Children.Add(Window.Gap(6f));
            for (int i = 0; i < recipes; i++) _recipeRows.Add(AddRecipe(_built, i));
            var order = new ContainerRuntime { WidthUnits = DimensionUnitType.RelativeToParent, Width = 0f, Height = 40f };
            var orderText = UiParts.Column(2f);
            UiParts.CentreVertically(orderText);
            _orderText = UiParts.Text(string.Empty, UiStyle.Accent, UiText.BodyBold);
            _orderNote = UiParts.Text(string.Empty, UiStyle.Warning, UiText.Small);
            orderText.Children.Add(_orderText);
            orderText.Children.Add(_orderNote);
            order.Children.Add(orderText);
            CancelButton = UiParts.Button("Cancel", 90f, 28f);
            RightAligned(CancelButton, 0f);
            UiParts.CentreVertically(CancelButton.Visual);
            order.Children.Add(CancelButton.Visual);
            CancelButton.Click += (_, _) => { _model.CancelOrder(); Bind(); };
            _built.Children.Add(order);

            body.Children.Add(Window.Gap(6f));
            body.Children.Add(Window.Rule());
            var footer = new ContainerRuntime { WidthUnits = DimensionUnitType.RelativeToParent, Width = 0f, Height = 36f };
            _feedback = UiParts.Text(string.Empty, UiStyle.Success, UiText.Small);
            UiParts.CentreVertically(_feedback);
            footer.Children.Add(_feedback);
            body.Children.Add(footer);
        }

        public Window Window { get; }
        public Button BuildButton { get; }
        public Button LessWageButton { get; }
        public Button MoreWageButton { get; }
        public Button DismissButton { get; }
        public Button ApplyButton { get; }
        public Button CancelButton { get; }
        public Button Tab(int index) => _tabs[index];
        public Button Block(int block) => _blocks[block];
        public Button HireButton(int index) => _candidateCards[index].Hire;
        public Button OrderButton(int recipe) => _recipeRows[recipe].Order;
        public Button MoreCountButton(int recipe) => _recipeRows[recipe].More;

        public string StatusText => _status.Text;
        public string BuildNoteText => _buildNote.Text;
        public string ArtisanNameText => _artisanName.Text;
        public string ShiftsNoteText => _shiftsNote.Text;
        public string CandidatesNoteText => _candidatesNote.Text;
        public string OrderText => _orderText.Text;
        public string OrderNoteText => _orderNote.Text;
        public int CandidatesShown { get; private set; }
        public string CandidateName(int index) => _candidateCards[index].Name.Text;
        public bool ShowsArtisan => _artisanCard.Visible && _built.Visible;
        public bool ShowsBuild => _unbuilt.Visible;

        private static void RightAligned(Button button, float x)
        {
            button.Visual.XUnits = GeneralUnitType.PixelsFromLarge;
            button.Visual.XOrigin = HorizontalAlignment.Right;
            button.X = x;
            UiParts.CentreVertically(button.Visual);
        }

        private CandidateCard AddCandidate(ContainerRuntime parent, int index)
        {
            var card = new CandidateCard { Index = index };
            card.Root = new ContainerRuntime { WidthUnits = DimensionUnitType.RelativeToParent, Width = 0f, Height = 48f };
            UiParts.AddFill(card.Root, UiStyle.Card, UiStyle.CardBorder);
            card.FaceBox = UiParts.FaceBox(_portraits, out card.Face);
            card.FaceBox.X = 10f;
            UiParts.CentreVertically(card.FaceBox);
            card.FaceBox.Click += (_, _) =>
            {
                if (card.Index < _model.Candidates.Count) FindOnMap?.Invoke(_model.Candidates[card.Index].CrawlerId);
            };
            card.Root.Children.Add(card.FaceBox);
            var who = UiParts.Column(2f);
            who.X = _portraits != null ? 10f + PortraitAtlas.FaceSize + 8f : 10f;
            UiParts.CentreVertically(who);
            card.Name = UiParts.Text(string.Empty, UiStyle.TextBright, UiText.BodyBold);
            card.Standing = UiParts.Text(string.Empty, UiStyle.TextSecondary, UiText.Small);
            who.Children.Add(card.Name);
            who.Children.Add(card.Standing);
            card.Root.Children.Add(who);
            card.Ask = UiParts.Text(string.Empty, UiStyle.Accent, UiText.BodyBold);
            card.Ask.X = 380f;
            UiParts.CentreVertically(card.Ask);
            card.Root.Children.Add(card.Ask);
            card.Hire = UiParts.Button("Hire", 78f, 28f);
            RightAligned(card.Hire, -10f);
            card.Root.Children.Add(card.Hire.Visual);
            card.Hire.Click += (_, _) =>
            {
                if (card.Index < _model.Candidates.Count) _model.Hire(_model.Candidates[card.Index].CrawlerId);
                Bind();
            };
            parent.Children.Add(card.Root);
            card.Gap = Window.Gap(6f);
            parent.Children.Add(card.Gap);
            return card;
        }

        private RecipeRow AddRecipe(ContainerRuntime parent, int index)
        {
            var row = new RecipeRow();
            row.Root = new ContainerRuntime { WidthUnits = DimensionUnitType.RelativeToParent, Width = 0f, Height = RowHeight };
            row.Icon = UiParts.Icon(_icons, null);
            UiParts.CentreVertically(row.Icon);
            row.Root.Children.Add(row.Icon);
            var what = UiParts.Column(2f);
            what.X = IconAtlas.Size + 10f;
            UiParts.CentreVertically(what);
            row.Name = UiParts.Text(string.Empty, UiStyle.TextBright, UiText.BodyBold);
            row.Recipe = UiParts.Text(string.Empty, UiStyle.TextSecondary, UiText.Small);
            what.Children.Add(row.Name);
            what.Children.Add(row.Recipe);
            row.Root.Children.Add(what);
            row.Less = UiParts.Button("-", 30f, 28f, UiText.BodyBold);
            row.Less.X = 470f;
            UiParts.CentreVertically(row.Less.Visual);
            row.Root.Children.Add(row.Less.Visual);
            row.Count = UiParts.Text(string.Empty, UiStyle.Accent, UiText.BodyBold);
            row.Count.X = 512f;
            UiParts.CentreVertically(row.Count);
            row.Root.Children.Add(row.Count);
            row.More = UiParts.Button("+", 30f, 28f, UiText.BodyBold);
            row.More.X = 548f;
            UiParts.CentreVertically(row.More.Visual);
            row.Root.Children.Add(row.More.Visual);
            row.Order = UiParts.PrimaryButton("Order", 90f, 28f, UiText.BodyBold);
            RightAligned(row.Order, 0f);
            row.Root.Children.Add(row.Order.Visual);
            row.Less.Click += (_, _) => { _model.LessCount(index); Bind(); };
            row.More.Click += (_, _) => { _model.MoreCount(index); Bind(); };
            row.Order.Click += (_, _) => { _model.Order(index); Bind(); };
            parent.Children.Add(row.Root);
            return row;
        }

        public static string WageText(int gold) => Invariant($"{gold}g a visit");

        public void Bind()
        {
            Window.IsShown = _model.IsOpen;
            if (!_model.IsOpen) return;
            Window.SnapToPixels();
            for (int i = 0; i < _tabs.Count; i++)
            {
                bool shown = i < _model.StationCount;
                _tabs[i].IsVisible = shown;
                if (!shown) continue;
                string name = _model.TabName(i);
                if (_tabs[i].Text != name) _tabs[i].Text = name;
                StoneSkin.SetActive(_tabs[i], i == _model.Index);
            }
            UiParts.SetText(_status, _model.Title + " · " + _model.Status);
            UiParts.SetText(_description, _model.Description);

            _unbuilt.Visible = !_model.Built;
            UiParts.SetText(_buildNote, _model.BuildNote);
            _buildNote.Color = _model.CanBuild ? UiStyle.TextLight : UiStyle.Warning;
            if (BuildButton.Text != _model.BuildLabel) BuildButton.Text = _model.BuildLabel;
            BuildButton.IsEnabled = _model.CanBuild;

            _built.Visible = _model.Built;
            _artisanCard.Visible = _model.HasArtisan;
            _shifts.Visible = _model.HasArtisan;
            _candidates.Visible = !_model.HasArtisan;
            if (_model.HasArtisan)
            {
                UiParts.SetFace(_artisanFace, _artisanFaceSprite, _portraits, _model.ArtisanHasPortrait, _model.ArtisanPortrait);
                UiParts.SetText(_artisanName, _model.ArtisanName);
                UiParts.SetText(_artisanDoing, _model.ArtisanDoing);
                UiParts.SetText(_artisanState, _model.ArtisanState);
                _artisanState.Color = _model.ArtisanOnBreak ? UiStyle.Warning : UiStyle.TextLight;
                UiParts.SetText(_wage, WageText(_model.PendingWage));
                for (int b = 0; b < _blocks.Count; b++) StoneSkin.SetActive(_blocks[b], _model.IsBlockOn(b));
                UiParts.SetText(_shiftsNote, _model.ShiftsNote);
                _shiftsNote.Color = _model.ShiftsTire ? UiStyle.Warning : UiStyle.TextSecondary;
                ApplyButton.IsEnabled = _model.HasChanges;
            }

            UiParts.SetText(_candidatesNote, _model.CandidatesNote);
            var candidates = _model.Candidates;
            for (int i = 0; i < _candidateCards.Count; i++)
            {
                var card = _candidateCards[i];
                bool shown = i < candidates.Count;
                card.Root.Visible = shown;
                card.Gap.Visible = shown;
                if (!shown) continue;
                var row = candidates[i];
                UiParts.SetFace(card.FaceBox, card.Face, _portraits, row.HasPortrait, row.Portrait);
                UiParts.SetText(card.Name, row.Name);
                UiParts.SetText(card.Standing, row.Standing);
                UiParts.SetText(card.Ask, "Asks " + WageText(row.Ask));
            }
            CandidatesShown = candidates.Count;

            var recipes = _model.Recipes;
            for (int i = 0; i < _recipeRows.Count; i++)
            {
                var row = _recipeRows[i];
                bool shown = i < recipes.Count;
                row.Root.Visible = shown;
                if (!shown) continue;
                UiParts.SetIcon(row.Icon, _icons, recipes[i].Icon);
                UiParts.SetText(row.Name, recipes[i].Name);
                UiParts.SetText(row.Recipe, recipes[i].Recipe);
                UiParts.SetText(row.Count, Invariant($"{recipes[i].PendingCount}"));
                row.Order.IsEnabled = _model.CanOrder;
            }
            UiParts.SetText(_orderText, _model.OrderText);
            _orderText.Color = _model.HasOrder ? UiStyle.Accent : UiStyle.TextMuted;
            UiParts.SetText(_orderNote, _model.OrderNote);
            CancelButton.IsVisible = _model.HasOrder;
            CancelButton.IsEnabled = _model.CanOrder;
            UiParts.SetText(_feedback, _model.Feedback.Text);
            _feedback.Color = UiStyle.ToneColor(_model.Feedback.Tone);
        }
    }
}
