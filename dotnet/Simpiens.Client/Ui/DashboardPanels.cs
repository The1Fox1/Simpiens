using System;
using System.Collections.Generic;
using Gum.Converters;
using Gum.DataTypes;
using Gum.Forms.Controls;
using Gum.Managers;
using Gum.Wireframe;
using Microsoft.Xna.Framework;
using Gum.GueDeriving;
using RenderingLibrary.Graphics;
using Simpiens.Entities;
using Simpiens.Presentation.Dashboard;

namespace Simpiens.Client.Ui
{
    /// <summary>
    /// The Tab dashboard (Unity's dashboard-root): three panels over the whole map under the header, opaque, so the map
    /// takes no clicks while they show.
    /// <list type="bullet">
    /// <item><description>The facility's sectors, each with its crawler count or SHROUDED (260 px).</description></item>
    /// <item><description>The selected room: name, description, residents and recent events (the rest of the width).</description></item>
    /// <item><description>The selected crawler's dossier (380 px).</description></item>
    /// </list>
    /// </summary>
    public sealed class DashboardPanels
    {
        public const float SectorsWidth = 260f, DossierWidth = 380f, Outer = 12f, Margin = 4f, Inner = 14f;


        private readonly DashboardModel _model;
        private readonly ContainerRuntime _sectorsList, _dossierDetails;
        private readonly GraphicalUiElement _residentsList, _eventsList, _affinitiesList;
        private readonly TextRuntime _sectorTitle, _sectorDescription, _residentsEmpty, _eventsEmpty, _dossierEmpty, _dossierName, _dossierBadge, _goal, _action, _affinitiesEmpty, _gear;
        private readonly TextRuntime _groups, _recruitText; // Epic 10 G4
        private readonly RectangleRuntime _dossierBadgeFill;
        private readonly List<Card> _sectorCards = new List<Card>(16), _residentCards = new List<Card>(16);
        private readonly List<TextRuntime> _eventRows = new List<TextRuntime>(SectorPanelModel.MaxEvents), _affinityRows = new List<TextRuntime>(16);
        private readonly DriveBar _health, _hunger, _energy, _social, _frustration;

        public DashboardPanels(DashboardModel model, IconAtlas icons)
        {
            _icons = icons;
            _model = model;
            Root = new ContainerRuntime
            {
                Name = "dashboard-root",
                WidthUnits = DimensionUnitType.RelativeToParent,
                HeightUnits = DimensionUnitType.RelativeToParent,
                Width = 0f,
                Height = -HeaderBar.Height,
                Y = HeaderBar.Height,
                HasEvents = true,
            };
            UiParts.AddFill(Root, UiStyle.Backdrop);

            // Column 1: the facility's sectors
            var sectors = Panel(Outer + Margin, SectorsWidth, "FACILITY SECTORS", out var sectorsBody);
            sectors.Children.Add(sectorsBody);
            _sectorsList = Stack(sectorsBody, 6f);
            Root.Children.Add(sectors);

            // Column 2: the selected sector
            float detailX = Outer + Margin + SectorsWidth + 2f * Margin;
            var detail = Panel(detailX, 0f, null, out var detailBody);
            detail.WidthUnits = DimensionUnitType.RelativeToParent;
            detail.Width = -(2f * Outer + 6f * Margin + SectorsWidth + DossierWidth);
            detailBody.HeightUnits = DimensionUnitType.RelativeToParent; // the events fill what is left (Unity's flex-grow)
            detailBody.Height = -2f * Inner;
            _sectorTitle = UiParts.Text(string.Empty, UiStyle.TextBright, UiText.Title);
            detailBody.Children.Add(_sectorTitle);
            _sectorDescription = Wrapped(string.Empty, UiStyle.TextSecondary, UiText.Body);
            var description = new ContainerRuntime { WidthUnits = DimensionUnitType.RelativeToParent, Width = 0f, Height = 36f }; // two lines
            description.Children.Add(_sectorDescription);
            detailBody.Children.Add(description);
            detailBody.Children.Add(Window.Gap(8f));
            detailBody.Children.Add(Window.SubHeader("RESIDENTS IN SECTOR"));
            _residentsEmpty = UiParts.Text("No residents in this sector.", UiStyle.TextMuted, UiText.Small);
            detailBody.Children.Add(_residentsEmpty);
            _residentsList = Scroll(detailBody, 240f, 6f); // Unity's max-height
            detailBody.Children.Add(Window.Gap(8f));
            detailBody.Children.Add(Window.SubHeader("RECENT SECTOR EVENTS"));
            _eventsEmpty = UiParts.Text("Nothing has happened here yet.", UiStyle.TextMuted, UiText.Small);
            detailBody.Children.Add(_eventsEmpty);
            _eventsList = Scroll(detailBody, 0f, 4f, fill: true);
            detail.Children.Add(detailBody);
            Root.Children.Add(detail);

            // Column 3: the dossier
            var dossier = Panel(0f, DossierWidth, "AGENT DOSSIER", out var dossierBody);
            dossier.XUnits = GeneralUnitType.PixelsFromLarge;
            dossier.XOrigin = HorizontalAlignment.Right;
            dossier.X = -(Outer + Margin);
            _dossierEmpty = Wrapped(DossierModel.EmptyText, UiStyle.TextMuted, UiText.Body);
            dossierBody.Children.Add(Window.Gap(20f));
            dossierBody.Children.Add(_dossierEmpty);
            _dossierDetails = UiParts.Column(4f);
            _dossierDetails.WidthUnits = DimensionUnitType.RelativeToParent;
            _dossierDetails.Width = 0f;
            var titleRow = new ContainerRuntime { WidthUnits = DimensionUnitType.RelativeToParent, Width = 0f, Height = 26f };
            _dossierName = UiParts.Text(string.Empty, UiStyle.TextTitle, UiText.Title);
            UiParts.CentreVertically(_dossierName);
            titleRow.Children.Add(_dossierName);
            var badge = Badge(out _dossierBadge, out _dossierBadgeFill);
            badge.XUnits = GeneralUnitType.PixelsFromLarge;
            badge.XOrigin = HorizontalAlignment.Right;
            titleRow.Children.Add(badge);
            _dossierDetails.Children.Add(titleRow);
            _dossierDetails.Children.Add(Window.Gap(6f));
            _dossierDetails.Children.Add(Window.SubHeader("BIOLOGICAL DRIVES"));
            _health = new DriveBar(_dossierDetails, "Health:", UiStyle.DriveHealth);
            _hunger = new DriveBar(_dossierDetails, "Hunger:", UiStyle.DriveHunger);
            _energy = new DriveBar(_dossierDetails, "Energy:", UiStyle.DriveEnergy);
            _social = new DriveBar(_dossierDetails, "Social:", UiStyle.DriveSocial);
            _frustration = new DriveBar(_dossierDetails, "Frustration:", UiStyle.Danger);
            // Worn gear (Epic 10 G3): its icon at 1x (graphics V4), then its words
            var gearLine = new ContainerRuntime { WidthUnits = DimensionUnitType.RelativeToParent, Width = 0f, Height = IconAtlas.SmallSize };
            GearIcon = UiParts.Icon(icons, null, scale: 1);
            gearLine.Children.Add(GearIcon);
            _gear = UiParts.Text(string.Empty, UiStyle.TextSecondary, UiText.Small);
            _gear.X = IconAtlas.SmallSize + 6f;
            UiParts.CentreVertically(_gear);
            gearLine.Children.Add(_gear);
            _dossierDetails.Children.Add(gearLine);
            // Its groups (Epic 10 G4): "Settlement: led by Mara · Guild: member, 5g a visit"
            _groups = UiParts.Text(string.Empty, UiStyle.TextSecondary, UiText.Small);
            _dossierDetails.Children.Add(_groups);
            _dossierDetails.Children.Add(Window.Gap(8f));
            _dossierDetails.Children.Add(Window.SubHeader("COGNITIVE INTENT (GOAP)"));
            var intent = new ContainerRuntime { WidthUnits = DimensionUnitType.RelativeToParent, Width = 0f, Height = 44f };
            UiParts.AddFill(intent, UiStyle.Card);
            _goal = IntentRow(intent, "Goal:", 6f);
            _action = IntentRow(intent, "Action:", 24f);
            _dossierDetails.Children.Add(intent);
            _dossierDetails.Children.Add(Window.Gap(8f));
            _dossierDetails.Children.Add(Window.SubHeader("SOCIAL AFFINITIES (WARMTH / TRUST / FEAR / RESPECT)"));
            _affinitiesEmpty = UiParts.Text("No relationships yet.", UiStyle.TextMuted, UiText.Small);
            _dossierDetails.Children.Add(_affinitiesEmpty);
            _affinitiesList = Scroll(_dossierDetails, 92f, 3f); // Unity's max-height was 120: the guild's lines took the rest (Epic 10 G4)
            _dossierDetails.Children.Add(Window.Gap(8f));
            _dossierDetails.Children.Add(Window.SubHeader("PLAYER INTERVENTION"));
            // Whether it would sign on with the guild, then Calm and Recruit side by side (Epic 10 G4, D6)
            _recruitText = UiParts.Text(string.Empty, UiStyle.TextLight, UiText.Small);
            _dossierDetails.Children.Add(_recruitText);
            // In the dossier's stack, so not UiParts.Row, which centres itself in its parent
            var actions = new ContainerRuntime
            {
                ChildrenLayout = ChildrenLayout.LeftToRightStack,
                StackSpacing = 8f,
                WidthUnits = DimensionUnitType.RelativeToParent,
                Width = 0f,
                Height = 32f,
            };
            float half = (DossierWidth - 2f * Inner - 8f) / 2f;
            CalmButton = UiParts.Button("Calm Frustration", half, 32f, UiText.BodyBold);
            CalmButton.Click += (_, _) => { _model.Dossier.Calm(); };
            RecruitButton = UiParts.Button("Recruit", half, 32f, UiText.BodyBold);
            RecruitButton.Click += (_, _) => { _model.Dossier.Recruit(); };
            actions.Children.Add(CalmButton.Visual);
            actions.Children.Add(RecruitButton.Visual);
            _dossierDetails.Children.Add(actions);
            dossierBody.Children.Add(_dossierDetails);
            dossier.Children.Add(dossierBody);
            Root.Children.Add(dossier);
        }

        public ContainerRuntime Root { get; }
        public Button CalmButton { get; }
        public Button RecruitButton { get; }
        public string GroupsShown => _groups.Text;
        public string RecruitShown => _recruitText.Text;

        public bool IsShown
        {
            get => Root.Visible;
            set => Root.Visible = value;
        }

        /// <summary>The sector cards and resident cards in use, top to bottom.</summary>
        public IReadOnlyList<Card> SectorCards => _sectorCards;
        public IReadOnlyList<Card> ResidentCards => _residentCards;
        public int ResidentsShown { get; private set; }
        public int EventsShown { get; private set; }
        public string SectorTitle => _sectorTitle.Text;
        public string DossierName => _dossierName.Text;
        public bool DossierShown => _dossierDetails.Visible;
        public float HungerShown => _hunger.Value;
        public float HealthShown => _health.Value;
        public string GearShown => _gear.Text;

        /// <summary>The worn gear's icon; hidden with none.</summary>
        public SpriteRuntime GearIcon { get; }

        private readonly IconAtlas _icons;

        public void Bind()
        {
            IsShown = _model.PanelsVisible;
            if (!IsShown) return;
            var panel = _model.Sectors;

            // Sectors
            var rows = panel.Sectors;
            while (_sectorCards.Count < rows.Count)
            {
                var card = new Card(38f);
                card.Root.Click += (_, _) => { if (card.SectorId >= 0) _model.SelectSector(card.SectorId); Bind(); };
                _sectorCards.Add(card);
                _sectorsList.Children.Add(card.Root);
            }
            for (int i = 0; i < _sectorCards.Count; i++)
            {
                var card = _sectorCards[i];
                card.Root.Visible = i < rows.Count;
                if (i >= rows.Count) continue;
                var row = rows[i];
                card.SectorId = row.SectorId;
                UiParts.SetText(card.Name, row.Name);
                card.Name.Color = row.Shrouded ? UiStyle.TextSecondary : UiStyle.TextBright;
                UiParts.SetText(card.Badge, row.Badge);
                card.Badge.Color = row.Shrouded ? UiStyle.TextSecondary : UiStyle.Accent;
                card.BadgeFill.FillColor = row.Shrouded ? UiParts.Alpha(UiStyle.TextMuted, 0.25f) : UiStyle.Inset;
                card.Style(row.Selected ? UiStyle.Border : row.Shrouded ? UiStyle.Shrouded : UiStyle.Card, row.Selected ? UiStyle.Accent : row.Shrouded ? UiStyle.ShroudedBorder : UiStyle.CardBorder, row.Selected);
            }

            // The selected sector
            UiParts.SetText(_sectorTitle, panel.Title);
            UiParts.SetText(_sectorDescription, panel.Description);
            var residents = panel.Residents;
            while (_residentCards.Count < residents.Count)
            {
                var card = new Card(46f, withDetail: true);
                card.Root.Click += (_, _) => { if (card.CrawlerId != default) _model.SelectCrawler(card.CrawlerId); Bind(); };
                _residentCards.Add(card);
                _residentsList.Children.Add(card.Root);
            }
            for (int i = 0; i < _residentCards.Count; i++)
            {
                var card = _residentCards[i];
                card.Root.Visible = i < residents.Count;
                if (i >= residents.Count) continue;
                var row = residents[i];
                card.CrawlerId = row.CrawlerId;
                UiParts.SetText(card.Name, row.Name);
                UiParts.SetText(card.Detail, row.Activity);
                UiParts.SetText(card.Badge, row.Badge);
                card.Badge.Color = UiStyle.BadgeText;
                card.BadgeFill.FillColor = UiStyle.StateColor(row.State);
                card.Style(row.Selected ? UiStyle.Border : UiStyle.Card, row.Selected ? UiStyle.Success : UiStyle.CardBorder, row.Selected, UiStyle.Success);
            }
            ResidentsShown = residents.Count;
            _residentsEmpty.Visible = residents.Count == 0;

            var events = panel.Events;
            for (int i = 0; i < events.Count; i++)
            {
                if (i == _eventRows.Count) _eventRows.Add(EventRow());
                UiParts.SetText(_eventRows[i], events[i]);
                ((GraphicalUiElement)_eventRows[i].Parent).Visible = true;
            }
            for (int i = events.Count; i < _eventRows.Count; i++) ((GraphicalUiElement)_eventRows[i].Parent).Visible = false;
            EventsShown = events.Count;
            _eventsEmpty.Visible = events.Count == 0;

            // The dossier
            var dossier = _model.Dossier;
            _dossierEmpty.Visible = !dossier.HasCrawler;
            _dossierDetails.Visible = dossier.HasCrawler;
            if (!dossier.HasCrawler) return;
            UiParts.SetText(_dossierName, dossier.Name);
            UiParts.SetText(_dossierBadge, dossier.Badge);
            _dossierBadgeFill.FillColor = UiStyle.StateColor(dossier.State);
            _health.Set(dossier.Health);
            UiParts.SetText(_gear, "Gear: " + dossier.Gear);
            UiParts.SetIcon(GearIcon, _icons, dossier.GearIcon);
            float gearTextX = dossier.GearIcon != null ? IconAtlas.SmallSize + 6f : 0f; // no gap where no icon shows
            if (_gear.X != gearTextX) _gear.X = gearTextX;
            _hunger.Set(dossier.Hunger);
            _energy.Set(dossier.Energy);
            _social.Set(dossier.Social);
            _frustration.Set(dossier.Frustration);
            UiParts.SetText(_goal, dossier.Goal);
            UiParts.SetText(_action, dossier.Action);
            var affinities = dossier.Affinities;
            for (int i = 0; i < affinities.Count; i++)
            {
                if (i == _affinityRows.Count)
                {
                    var text = UiParts.Text(string.Empty, UiStyle.TextLight, UiText.Small);
                    _affinityRows.Add(text);
                    _affinitiesList.Children.Add(text);
                }
                UiParts.SetText(_affinityRows[i], affinities[i]);
                _affinityRows[i].Visible = true;
            }
            for (int i = affinities.Count; i < _affinityRows.Count; i++) _affinityRows[i].Visible = false;
            _affinitiesEmpty.Visible = affinities.Count == 0;
            CalmButton.IsEnabled = dossier.CanCalm;
            UiParts.SetText(_groups, dossier.Groups);
            UiParts.SetText(_recruitText, dossier.RecruitText);
            RecruitButton.IsEnabled = dossier.CanRecruit;
        }

        private static ContainerRuntime Panel(float x, float width, string header, out ContainerRuntime body)
        {
            var panel = new ContainerRuntime
            {
                X = x,
                Y = Outer + Margin,
                Width = width,
                HeightUnits = DimensionUnitType.RelativeToParent,
                Height = -2f * (Outer + Margin),
                ClipsChildren = true,
            };
            UiParts.AddFill(panel, UiStyle.Panel, UiStyle.Border);
            body = UiParts.Column(4f);
            body.X = Inner;
            body.Y = Inner;
            body.WidthUnits = DimensionUnitType.RelativeToParent;
            body.Width = -2f * Inner;
            if (header != null)
            {
                body.Children.Add(UiParts.Text(header, UiStyle.TextBright, UiText.Heading));
                body.Children.Add(Window.Gap(2f));
                body.Children.Add(Window.Rule());
                body.Children.Add(Window.Gap(6f));
            }
            return panel;
        }

        /// <summary>A scrolling list: a fixed height, or (<paramref name="fill"/>) the rest of its stack. Rows go into the returned panel.</summary>
        private static GraphicalUiElement Scroll(ContainerRuntime parent, float height, float spacing, bool fill = false)
        {
            var scroll = new ScrollViewer();
            scroll.Visual.WidthUnits = DimensionUnitType.RelativeToParent;
            scroll.Width = 0f;
            scroll.Visual.HeightUnits = fill ? DimensionUnitType.Ratio : DimensionUnitType.Absolute;
            scroll.Height = fill ? 1f : height;
            var inner = scroll.InnerPanel;
            inner.ChildrenLayout = ChildrenLayout.TopToBottomStack;
            inner.StackSpacing = spacing;
            parent.Children.Add(scroll.Visual);
            return inner;
        }

        private static ContainerRuntime Stack(ContainerRuntime parent, float spacing)
        {
            var stack = UiParts.Column(spacing);
            stack.WidthUnits = DimensionUnitType.RelativeToParent;
            stack.Width = 0f;
            parent.Children.Add(stack);
            return stack;
        }

        private static TextRuntime Wrapped(string text, Color color, UiText style)
        {
            var label = UiParts.Text(text, color, style);
            label.WidthUnits = DimensionUnitType.RelativeToParent;
            label.Width = 0f;
            return label;
        }

        private TextRuntime EventRow()
        {
            var box = new ContainerRuntime { WidthUnits = DimensionUnitType.RelativeToParent, Width = 0f, HeightUnits = DimensionUnitType.RelativeToChildren, Height = 6f };
            UiParts.AddFill(box, UiStyle.Inset);
            box.Children.Add(new RectangleRuntime { IsFilled = true, StrokeWidth = 0, FillColor = UiStyle.Stock, Width = 2f, HeightUnits = DimensionUnitType.RelativeToParent, Height = 0f });
            var text = UiParts.Text(string.Empty, UiStyle.TextLight, UiText.Small);
            text.X = 8f;
            text.Y = 3f;
            text.WidthUnits = DimensionUnitType.RelativeToParent;
            text.Width = -16f;
            box.Children.Add(text);
            _eventsList.Children.Add(box);
            return text;
        }

        private static TextRuntime IntentRow(ContainerRuntime box, string key, float y)
        {
            var keyText = UiParts.Text(key, UiStyle.TextMuted, UiText.Small);
            keyText.X = 10f;
            keyText.Y = y;
            box.Children.Add(keyText);
            var value = UiParts.Text(string.Empty, UiStyle.Accent, UiText.BodyBold);
            value.X = 65f;
            value.Y = y - 1f;
            box.Children.Add(value);
            return value;
        }

        private static ContainerRuntime Badge(out TextRuntime text, out RectangleRuntime fill)
        {
            var badge = new ContainerRuntime { WidthUnits = DimensionUnitType.RelativeToChildren, HeightUnits = DimensionUnitType.RelativeToChildren, Width = 6f, Height = 2f };
            UiParts.CentreVertically(badge);
            fill = new RectangleRuntime { IsFilled = true, StrokeWidth = 0, WidthUnits = DimensionUnitType.RelativeToParent, HeightUnits = DimensionUnitType.RelativeToParent, Width = 0f, Height = 0f };
            badge.Children.Add(fill);
            text = UiParts.Text(string.Empty, UiStyle.BadgeText, UiText.Badge);
            text.X = 6f;
            text.Y = 2f;
            badge.Children.Add(text);
            return badge;
        }

        /// <summary>A row in a list that can be clicked: a sector or a resident, with a badge on the right.</summary>
        public sealed class Card
        {
            private readonly RectangleRuntime _fill, _bar;
            private readonly RectangleRuntime _outline;

            public Card(float height, bool withDetail = false)
            {
                Root = new ContainerRuntime { WidthUnits = DimensionUnitType.RelativeToParent, Width = 0f, Height = height, HasEvents = true };
                _fill = new RectangleRuntime { IsFilled = true, StrokeWidth = 0, WidthUnits = DimensionUnitType.RelativeToParent, HeightUnits = DimensionUnitType.RelativeToParent, Width = 0f, Height = 0f };
                _outline = new RectangleRuntime { IsFilled = false, StrokeWidth = 1, WidthUnits = DimensionUnitType.RelativeToParent, HeightUnits = DimensionUnitType.RelativeToParent, Width = 0f, Height = 0f };
                _bar = new RectangleRuntime { IsFilled = true, StrokeWidth = 0, Width = 4f, HeightUnits = DimensionUnitType.RelativeToParent, Height = 0f, Visible = false };
                Root.Children.Add(_fill);
                Root.Children.Add(_outline);
                Root.Children.Add(_bar);
                Name = UiParts.Text(string.Empty, UiStyle.TextBright, UiText.BodyBold);
                Name.X = 12f;
                if (withDetail)
                {
                    Name.Y = 7f;
                    Detail = UiParts.Text(string.Empty, UiStyle.TextMuted, UiText.Small);
                    Detail.X = 12f;
                    Detail.Y = 26f;
                    Root.Children.Add(Detail);
                }
                else
                {
                    UiParts.CentreVertically(Name);
                }
                Root.Children.Add(Name);
                var badge = Badge(out var badgeText, out var badgeFill);
                badge.XUnits = GeneralUnitType.PixelsFromLarge;
                badge.XOrigin = HorizontalAlignment.Right;
                badge.X = -10f;
                Badge = badgeText;
                BadgeFill = badgeFill;
                Root.Children.Add(badge);
            }

            public ContainerRuntime Root { get; }
            public TextRuntime Name { get; }
            public TextRuntime Detail { get; }
            public TextRuntime Badge { get; }
            public RectangleRuntime BadgeFill { get; }
            public int SectorId { get; set; } = -1;
            public EntityId CrawlerId { get; set; }
            public bool Selected { get; private set; }

            internal void Style(Color fill, Color border, bool selected, Color? bar = null)
            {
                _fill.FillColor = fill;
                _outline.StrokeColor = border;
                _bar.Visible = selected;
                _bar.FillColor = bar ?? UiStyle.Accent;
                Selected = selected;
            }
        }

        /// <summary>A drive's label, bar and percentage (Unity's drive row).</summary>
        private sealed class DriveBar
        {
            private readonly RectangleRuntime _fill;
            private readonly TextRuntime _value;

            public DriveBar(ContainerRuntime parent, string label, Color color)
            {
                var row = new ContainerRuntime { WidthUnits = DimensionUnitType.RelativeToParent, Width = 0f, Height = 20f };
                var name = UiParts.Text(label, UiStyle.TextSecondary, UiText.Body);
                UiParts.CentreVertically(name);
                row.Children.Add(name);
                var track = new RectangleRuntime { IsFilled = true, StrokeWidth = 0, FillColor = UiStyle.Border, X = 80f, WidthUnits = DimensionUnitType.RelativeToParent, Width = -(80f + 46f), Height = 14f };
                UiParts.CentreVertically(track);
                row.Children.Add(track);
                _fill = new RectangleRuntime { IsFilled = true, StrokeWidth = 0, FillColor = color, WidthUnits = DimensionUnitType.PercentageOfParent, Width = 0f, HeightUnits = DimensionUnitType.RelativeToParent, Height = 0f };
                track.Children.Add(_fill);
                _value = UiParts.Text("0%", UiStyle.TextLight, UiText.Small);
                _value.XUnits = GeneralUnitType.PixelsFromLarge;
                _value.XOrigin = HorizontalAlignment.Right;
                UiParts.CentreVertically(_value);
                row.Children.Add(_value);
                parent.Children.Add(row);
            }

            public float Value { get; private set; }

            public void Set(float value)
            {
                Value = value;
                _fill.Width = Math.Clamp(value, 0f, 100f);
                UiParts.SetText(_value, DossierModel.Percent(value));
            }
        }
    }
}
