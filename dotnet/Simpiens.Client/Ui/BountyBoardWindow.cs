using System;
using System.Collections.Generic;
using Gum.Converters;
using Gum.DataTypes;
using Gum.Forms.Controls;
using Gum.Managers;
using Microsoft.Xna.Framework;
using MonoGameGum.GueDeriving;
using RenderingLibrary.Graphics;
using Simpiens.Presentation.Dashboard;
using Simpiens.Simulation.Economy;

namespace Simpiens.Client.Ui
{
    /// <summary>
    /// The public bounty board (Unity's bounty window, 820 px): the active contracts on the left, each with Cancel while
    /// it can be cancelled, and the form that posts one on the right. The model holds every rule; this draws it and
    /// forwards the player's choices.
    /// </summary>
    public sealed class BountyBoardWindow
    {
        public const float Width = 820f;
        private const float ListWidth = 400f, FormWidth = 360f, ColumnGap = 28f;

        private readonly BountyBoardModel _model;
        private readonly ContainerRuntime _cards;
        private readonly List<BountyCard> _pool = new List<BountyCard>(16);
        private readonly TextRuntime _count, _empty, _sectorState, _quantityValue, _rewardValue, _durationValue, _feedback;
        private readonly ContainerRuntime _itemGroup, _quantityGroup;
        private int _choicesShown = -1;
        private bool _binding;

        public BountyBoardWindow(BountyBoardModel model)
        {
            _model = model;
            Window = new Window("bounty-board", "PUBLIC BOUNTY BOARD", "Contract posting, automated escrow, and public agent directives.", Width, UiStyle.Gold);

            var columns = new ContainerRuntime { WidthUnits = DimensionUnitType.RelativeToParent, Width = 0f, HeightUnits = DimensionUnitType.RelativeToChildren, Height = 0f, ChildrenLayout = ChildrenLayout.LeftToRightStack, StackSpacing = ColumnGap };
            Window.Body.Children.Add(columns);

            // Left: the active contracts
            var list = UiParts.Column(6f);
            list.WidthUnits = DimensionUnitType.Absolute;
            list.Width = ListWidth;
            var listHeader = new ContainerRuntime { WidthUnits = DimensionUnitType.RelativeToParent, Width = 0f, Height = 22f };
            listHeader.Children.Add(Window.SubHeader("ACTIVE CONTRACTS"));
            var countPill = UiParts.Pill(out _count, "0 ACTIVE", UiParts.Alpha(UiStyle.Gold, 0.15f), UiStyle.Gold, UiStyle.GoldText);
            countPill.XUnits = GeneralUnitType.PixelsFromLarge;
            countPill.XOrigin = HorizontalAlignment.Right;
            listHeader.Children.Add(countPill);
            list.Children.Add(listHeader);
            _empty = UiParts.Text("No active contracts. Post one to put the crew to work.", UiStyle.TextMuted, UiText.Small);
            list.Children.Add(_empty);
            _cards = UiParts.Column(6f);
            _cards.WidthUnits = DimensionUnitType.RelativeToParent;
            _cards.Width = 0f;
            list.Children.Add(_cards);
            columns.Children.Add(list);

            // Right: the form
            var form = UiParts.Column(4f);
            form.WidthUnits = DimensionUnitType.Absolute;
            form.Width = FormWidth;
            form.Children.Add(Window.SubHeader("POST NEW CONTRACT"));
            form.Children.Add(Window.Gap(4f));

            form.Children.Add(Label("Contract Directive Type:"));
            TypeBox = Dropdown(new[] { nameof(BountyType.GatherResource), nameof(BountyType.ScoutSector) });
            form.Children.Add(TypeBox.Visual);
            TypeBox.SelectionChanged += (_, _) =>
            {
                if (_binding) return;
                _model.SetType(TypeBox.SelectedIndex == 1 ? BountyType.ScoutSector : BountyType.GatherResource);
                Bind();
            };

            var sectorLabelRow = new ContainerRuntime { WidthUnits = DimensionUnitType.RelativeToParent, Width = 0f, Height = 18f };
            sectorLabelRow.Children.Add(Label("Target Sector:"));
            _sectorState = UiParts.Text(string.Empty, UiStyle.Accent, UiText.SmallBold);
            _sectorState.XUnits = GeneralUnitType.PixelsFromLarge;
            _sectorState.XOrigin = HorizontalAlignment.Right;
            sectorLabelRow.Children.Add(_sectorState);
            form.Children.Add(sectorLabelRow);
            SectorBox = Dropdown(Array.Empty<string>());
            form.Children.Add(SectorBox.Visual);
            SectorBox.SelectionChanged += (_, _) =>
            {
                if (_binding) return;
                _model.SelectedSectorIndex = SectorBox.SelectedIndex;
                Bind();
            };

            _itemGroup = FormGroup();
            _itemGroup.Children.Add(Label("Requested Salvage Item:"));
            ItemBox = Dropdown(new[] { nameof(ItemType.IronOre), nameof(ItemType.MedicinalHerb) });
            _itemGroup.Children.Add(ItemBox.Visual);
            ItemBox.SelectionChanged += (_, _) => { if (!_binding) _model.Item = ItemBox.SelectedIndex == 1 ? ItemType.MedicinalHerb : ItemType.IronOre; };
            form.Children.Add(_itemGroup);

            _quantityGroup = FormGroup();
            QuantitySlider = SliderRow(_quantityGroup, "Required Quantity:", BountyBoardModel.MinQuantity, BountyBoardModel.MaxQuantity, 1, v => _model.Quantity = v, BountyBoardModel.QuantityText, out _quantityValue);
            form.Children.Add(_quantityGroup);
            RewardSlider = SliderRow(form, "Reward Escrow (Gold):", BountyBoardModel.MinReward, BountyBoardModel.MaxReward, 1, v => _model.Reward = v, BountyBoardModel.RewardText, out _rewardValue);
            DurationSlider = SliderRow(form, "Contract Duration:", BountyBoardModel.MinDuration, BountyBoardModel.MaxDuration, 50, v => _model.Duration = v, BountyBoardModel.DurationText, out _durationValue);

            form.Children.Add(Window.Gap(6f));
            form.Children.Add(Window.Rule());
            _feedback = UiParts.Text(string.Empty, UiStyle.Success, UiText.Small);
            _feedback.WidthUnits = DimensionUnitType.RelativeToParent;
            _feedback.Width = 0f;
            form.Children.Add(Window.Gap(6f));
            form.Children.Add(_feedback);
            PostButton = UiParts.Button("POST BOUNTY (ESCROW GOLD)", FormWidth, 34f, UiText.BodyBold);
            form.Children.Add(PostButton.Visual);
            PostButton.Click += (_, _) => { _model.Post(); Bind(); };
            columns.Children.Add(form);

            LoadForm();
        }

        public Window Window { get; }
        public ComboBox TypeBox { get; }
        public ComboBox SectorBox { get; }
        public ComboBox ItemBox { get; }
        public Slider QuantitySlider { get; }
        public Slider RewardSlider { get; }
        public Slider DurationSlider { get; }
        public Button PostButton { get; }

        /// <summary>The cards shown, top to bottom (pooled: only the first <see cref="ShownCards"/> are in use).</summary>
        public IReadOnlyList<BountyCard> Cards => _pool;
        public int ShownCards { get; private set; }
        public string FeedbackText => _feedback.Text;
        public string CountText => _count.Text;
        public bool ItemFieldsShown => _itemGroup.Visible;

        /// <summary>A part of the form that can hide (Scout has no item or quantity): as wide as the form, so its rows and controls are.</summary>
        private static ContainerRuntime FormGroup()
        {
            var group = UiParts.Column(4f);
            group.WidthUnits = DimensionUnitType.RelativeToParent;
            group.Width = 0f;
            return group;
        }

        private static TextRuntime Label(string text) => UiParts.Text(text, UiStyle.TextLight, UiText.SmallBold);

        private static ComboBox Dropdown(string[] choices)
        {
            var box = new ComboBox();
            foreach (var choice in choices) box.Items.Add(choice);
            box.Visual.WidthUnits = DimensionUnitType.RelativeToParent;
            box.Width = 0f;
            UiParts.SetTextStyle(box.Visual, UiText.Body);
            return box;
        }

        private Slider SliderRow(ContainerRuntime parent, string label, int min, int max, int step, Action<int> set, Func<int, string> format, out TextRuntime value)
        {
            var row = new ContainerRuntime { WidthUnits = DimensionUnitType.RelativeToParent, Width = 0f, Height = 18f };
            row.Children.Add(Label(label));
            var shown = UiParts.Text(format(min), UiStyle.Accent, UiText.SmallBold);
            shown.XUnits = GeneralUnitType.PixelsFromLarge;
            shown.XOrigin = HorizontalAlignment.Right;
            row.Children.Add(shown);
            parent.Children.Add(row);
            var slider = UiParts.Slider(min, max, step);
            slider.Visual.WidthUnits = DimensionUnitType.RelativeToParent;
            slider.Width = 0f;
            parent.Children.Add(slider.Visual);
            slider.ValueChanged += (_, _) =>
            {
                int v = (int)Math.Round(slider.Value);
                shown.Text = format(v);
                if (!_binding) set(v);
            };
            value = shown;
            return slider;
        }

        /// <summary>The model's form values into the controls.</summary>
        public void LoadForm()
        {
            _binding = true;
            TypeBox.SelectedIndex = _model.Type == BountyType.ScoutSector ? 1 : 0;
            ItemBox.SelectedIndex = _model.Item == ItemType.MedicinalHerb ? 1 : 0;
            QuantitySlider.Value = _model.Quantity;
            RewardSlider.Value = _model.Reward;
            DurationSlider.Value = _model.Duration;
            UiParts.SetText(_quantityValue, BountyBoardModel.QuantityText(_model.Quantity));
            UiParts.SetText(_rewardValue, BountyBoardModel.RewardText(_model.Reward));
            UiParts.SetText(_durationValue, BountyBoardModel.DurationText(_model.Duration));
            _binding = false;
        }

        public void Bind()
        {
            Window.IsShown = _model.IsOpen;
            UiParts.SetText(_count, _model.CountBadge);
            if (!_model.IsOpen) return;

            _binding = true;
            // The sector choices change with the type and the chart: rebuild only when they did
            int signature = _model.SectorChoiceIds.Count * 1000 + (int)_model.Type;
            for (int i = 0; i < _model.SectorChoiceIds.Count; i++) signature = signature * 31 + _model.SectorChoiceIds[i];
            if (signature != _choicesShown)
            {
                SectorBox.Items.Clear();
                foreach (var label in _model.SectorChoiceLabels) SectorBox.Items.Add(label);
                _choicesShown = signature;
            }
            if (SectorBox.SelectedIndex != _model.SelectedSectorIndex) SectorBox.SelectedIndex = _model.SelectedSectorIndex;
            _binding = false;

            UiParts.SetText(_sectorState, _model.SectorState);
            _itemGroup.Visible = _model.ShowsItem;
            _quantityGroup.Visible = _model.ShowsItem;
            PostButton.IsEnabled = _model.CanPost;
            UiParts.SetText(_feedback, _model.Feedback.Text);
            _feedback.Color = UiStyle.ToneColor(_model.Feedback.Tone);

            var rows = _model.Contracts;
            _empty.Visible = rows.Count == 0;
            for (int i = 0; i < rows.Count; i++)
            {
                if (i == _pool.Count)
                {
                    var card = new BountyCard();
                    card.CancelButton.Click += (_, _) => { if (card.BountyId != 0) { _model.Cancel(card.BountyId); Bind(); } };
                    _pool.Add(card);
                    _cards.Children.Add(card.Root);
                }
                _pool[i].Bind(rows[i]);
                _pool[i].Root.Visible = true;
            }
            for (int i = rows.Count; i < _pool.Count; i++) _pool[i].Root.Visible = false;
            ShownCards = rows.Count;
        }
    }

    /// <summary>One contract on the board (Unity's bounty card).</summary>
    public sealed class BountyCard
    {
        private readonly TextRuntime _title, _status, _details, _reward;

        public BountyCard()
        {
            Root = new ContainerRuntime { WidthUnits = DimensionUnitType.RelativeToParent, Width = 0f, Height = 64f };
            UiParts.AddFill(Root, UiStyle.Card, UiStyle.CardBorder);
            _title = UiParts.Text(string.Empty, UiStyle.TextBright, UiText.BodyBold);
            _title.X = 10f;
            _title.Y = 6f;
            Root.Children.Add(_title);
            _status = UiParts.Text(string.Empty, UiStyle.Success, UiText.SmallBold);
            _status.XUnits = GeneralUnitType.PixelsFromLarge;
            _status.XOrigin = HorizontalAlignment.Right;
            _status.X = -10f;
            _status.Y = 6f;
            Root.Children.Add(_status);
            _details = UiParts.Text(string.Empty, UiStyle.TextMuted, UiText.Small);
            _details.X = 10f;
            _details.Y = 24f;
            Root.Children.Add(_details);
            _reward = UiParts.Text(string.Empty, UiStyle.GoldText, UiText.SmallBold);
            _reward.X = 10f;
            _reward.Y = 42f;
            Root.Children.Add(_reward);
            CancelButton = UiParts.Button("CANCEL", 70f, 22f);
            CancelButton.Visual.XUnits = GeneralUnitType.PixelsFromLarge;
            CancelButton.Visual.XOrigin = HorizontalAlignment.Right;
            CancelButton.X = -8f;
            CancelButton.Y = 36f;
            Root.Children.Add(CancelButton.Visual);
        }

        public ContainerRuntime Root { get; }
        public Button CancelButton { get; }
        public ushort BountyId { get; private set; }
        public string Title => _title.Text;
        public string Status => _status.Text;

        public void Bind(in BountyRow row)
        {
            BountyId = row.BountyId;
            UiParts.SetText(_title, row.Title);
            UiParts.SetText(_status, row.StatusText);
            _status.Color = UiStyle.BountyStatusColor(row.Status);
            UiParts.SetText(_details, row.Details);
            UiParts.SetText(_reward, row.Reward);
            CancelButton.IsVisible = row.CanCancel;
        }
    }
}
