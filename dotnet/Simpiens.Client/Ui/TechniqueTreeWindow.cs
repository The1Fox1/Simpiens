using System.Collections.Generic;
using Gum.Converters;
using Gum.DataTypes;
using Gum.Forms.Controls;
using Gum.GueDeriving;
using Microsoft.Xna.Framework;
using RenderingLibrary.Graphics;
using Simpiens.Presentation.Dashboard;
using Simpiens.Simulation.Skills;

namespace Simpiens.Client.Ui
{
    /// <summary>
    /// A crawler's technique tree (Epic 11, S4.11, D25), a window like the guild's, in blued steel:
    /// <list type="bullet">
    /// <item><b>The strip:</b> the crawler's face, its name and title, where it stands with the patron, and the treasury.</item>
    /// <item><b>The skills</b> that offer techniques, down the left, the one shown lit.</item>
    /// <item><b>The tree:</b> the skill and its level at the root; a trunk down through its bands (Apprentice, Journeyman,
    /// Master), lit as far as its level has climbed; at each band a branch to either side to its two techniques. A node is
    /// lit stone when held; it shows its price when its band is reached, and the band's level when not; a ring marks the
    /// one picked.</item>
    /// <item><b>The detail</b> of the technique picked: its band, the crawler's vow, what it does, and the offer.</item>
    /// </list>
    /// Built once: the skills' buttons for every skill that offers techniques, three tiers of two nodes; the model's words
    /// fill them. Nothing here decides: a click picks, Offer asks the model.
    /// </summary>
    public sealed class TechniqueTreeWindow
    {
        public const float Width = 980f;

        // The columns, in the body: the skills, the tree, the detail
        private const float SkillsWidth = 150f, ColumnGap = 16f, TreeWidth = 520f;
        private const float TreeX = SkillsWidth + ColumnGap, DetailX = TreeX + TreeWidth + ColumnGap;
        private const float SkillHeight = 34f, SkillGap = 6f, StripHeight = 44f;

        // The tree's geometry
        /// <summary>A technique's node: its name and corner word, then up to three lines of what it does.</summary>
        public const float NodeWidth = 200f, NodeHeight = 74f;
        private const float RootWidth = 160f, RootHeight = 30f, RootGap = 24f, TierGap = 26f, PillWidth = 92f, PillHeight = 36f, Trunk = 2f;
        private const float NodePadding = 10f, EffectsY = 26f;

        /// <summary>The tree's height: the root, its gap, and three tiers.</summary>
        public const float TreeHeight = RootHeight + RootGap + TechniqueTreeModel.Tiers * NodeHeight + (TechniqueTreeModel.Tiers - 1) * TierGap;

        private readonly TechniqueTreeModel _model;
        private readonly PortraitAtlas _portraits;
        private readonly TextRuntime _name, _standing, _treasury, _root;
        private readonly ContainerRuntime _faceBox;
        private readonly SpriteRuntime _face;
        private readonly List<SkillEntry> _skills = new List<SkillEntry>(SkillTable.Capacity);
        private readonly Tier[] _tiers = new Tier[TechniqueTreeModel.Tiers];
        private readonly RectangleRuntime _trunkLit, _ring, _ringInner;
        private readonly TextRuntime _title, _subtitle, _vow, _sponsorLine, _feedback;
        private readonly TextRuntime[] _effects = new TextRuntime[MostEffects];

        /// <summary>The most effects a technique has (the table's rows have one to three).</summary>
        public const int MostEffects = 3;

        private sealed class SkillEntry
        {
            public Button Button;
            public TextRuntime Text, Band;
            public Skill Skill;
        }

        private sealed class Tier
        {
            public TextRuntime Label, Gate;
            public RectangleRuntime Outline;
            public readonly Node[] Nodes = new Node[TechniqueTreeModel.NodesPerTier];
            public readonly RectangleRuntime[] Branches = new RectangleRuntime[TechniqueTreeModel.NodesPerTier];
        }

        private sealed class Node
        {
            public Button Button;
            public TextRuntime Name, Tag;
            public readonly TextRuntime[] Effects = new TextRuntime[MostEffects];
            public Technique Technique;
        }

        public TechniqueTreeWindow(TechniqueTreeModel model, PortraitAtlas portraits = null)
        {
            _model = model;
            _portraits = portraits;
            Window = new Window("technique-tree", "Techniques", "What a crawler can become at each band of its skills, and your gold to steer it.", Width,
                Simpiens.Presentation.Art.UiKit.FrameSteel, Simpiens.Presentation.Art.UiKit.Shield);
            var body = Window.Body;

            // The strip: face, name and standing, treasury
            var strip = new ContainerRuntime { WidthUnits = DimensionUnitType.RelativeToParent, Width = 0f, Height = StripHeight };
            UiParts.AddInset(strip);
            _faceBox = UiParts.FaceBox(portraits, out _face);
            _faceBox.X = 8f;
            UiParts.CentreVertically(_faceBox);
            strip.Children.Add(_faceBox);
            var who = UiParts.Column(2f);
            who.X = 8f + PortraitAtlas.FaceSize + 10f;
            UiParts.CentreVertically(who);
            _name = UiParts.Text(string.Empty, UiStyle.TextTitle, UiText.BodyBold);
            _standing = UiParts.Text(string.Empty, UiStyle.TextSecondary, UiText.Small);
            who.Children.Add(_name);
            who.Children.Add(_standing);
            strip.Children.Add(who);
            _treasury = UiParts.Text(string.Empty, UiStyle.GoldText, UiText.StatusBold);
            _treasury.XUnits = GeneralUnitType.PixelsFromLarge;
            _treasury.XOrigin = HorizontalAlignment.Right;
            _treasury.X = -12f;
            UiParts.CentreVertically(_treasury);
            strip.Children.Add(_treasury);
            body.Children.Add(strip);
            body.Children.Add(Window.Gap(12f));

            var main = new ContainerRuntime { WidthUnits = DimensionUnitType.RelativeToParent, Width = 0f, Height = TreeHeight };
            body.Children.Add(main);
            body.Children.Add(Window.Gap(4f));

            // The skills, down the left
            var skills = UiParts.Column(SkillGap);
            main.Children.Add(skills);
            foreach (var skill in TechniqueTreeModel.TreeSkills)
            {
                var entry = new SkillEntry { Skill = skill, Button = UiParts.Button(string.Empty, SkillsWidth, SkillHeight) };
                entry.Text = UiParts.Text(string.Empty, UiStyle.Text, UiText.BodyBold);
                entry.Text.X = 10f;
                UiParts.CentreVertically(entry.Text);
                entry.Band = UiParts.Text(string.Empty, UiStyle.TextMuted, UiText.Small);
                entry.Band.XUnits = GeneralUnitType.PixelsFromLarge;
                entry.Band.XOrigin = HorizontalAlignment.Right;
                entry.Band.X = -10f;
                UiParts.CentreVertically(entry.Band);
                entry.Button.Visual.Children.Add(entry.Text);
                entry.Button.Visual.Children.Add(entry.Band);
                var picked = entry.Skill;
                entry.Button.Click += (_, _) => { _model.SelectSkill(picked); Bind(); };
                skills.Children.Add(entry.Button.Visual);
                _skills.Add(entry);
            }

            // The tree: the trunk under everything, lit as far as the level climbs, then the branches, the pills and the nodes
            var tree = new ContainerRuntime { X = TreeX, Width = TreeWidth, Height = TreeHeight };
            main.Children.Add(tree);
            float trunkX = TreeWidth / 2f - Trunk / 2f;
            tree.Children.Add(Line(trunkX, RootHeight, Trunk, TierCentre(TechniqueTreeModel.Tiers - 1) - RootHeight, UiStyle.Border));
            _trunkLit = Line(trunkX, RootHeight, Trunk, 0f, UiStyle.Accent);
            tree.Children.Add(_trunkLit);
            var root = new ContainerRuntime { X = (TreeWidth - RootWidth) / 2f, Width = RootWidth, Height = RootHeight };
            UiParts.AddInset(root);
            root.Children.Add(Outline(UiStyle.Accent));
            _root = UiParts.Text(string.Empty, UiStyle.Accent, UiText.BodyBold);
            Centre(_root);
            root.Children.Add(_root);
            tree.Children.Add(root);
            for (int t = 0; t < _tiers.Length; t++) _tiers[t] = AddTier(tree, t);
            // The ring round the technique picked: two lines, two pixels outside the node
            _ring = new RectangleRuntime { IsFilled = false, StrokeWidth = 1, StrokeColor = UiStyle.TextTitle, Width = NodeWidth + 6f, Height = NodeHeight + 6f, Visible = false };
            _ringInner = new RectangleRuntime { IsFilled = false, StrokeWidth = 1, StrokeColor = UiStyle.TextTitle, Width = NodeWidth + 4f, Height = NodeHeight + 4f, Visible = false };
            tree.Children.Add(_ring);
            tree.Children.Add(_ringInner);

            // The detail, on the right
            var detail = new ContainerRuntime { X = DetailX, WidthUnits = DimensionUnitType.RelativeToParent, Width = -DetailX, Height = TreeHeight };
            UiParts.AddInset(detail);
            main.Children.Add(detail);
            var column = UiParts.Column(4f);
            column.X = 12f;
            column.Y = 10f;
            column.WidthUnits = DimensionUnitType.RelativeToParent;
            column.Width = -24f;
            detail.Children.Add(column);
            _title = Wrapped(UiStyle.TextTitle, UiText.Title);
            _subtitle = Wrapped(UiStyle.TextSecondary, UiText.Small);
            _vow = Wrapped(UiStyle.TextLight, UiText.Body);
            column.Children.Add(_title);
            column.Children.Add(_subtitle);
            column.Children.Add(Window.Gap(2f));
            column.Children.Add(_vow);
            column.Children.Add(Window.Gap(4f));
            column.Children.Add(Window.SubHeader("WHAT IT DOES"));
            for (int i = 0; i < _effects.Length; i++)
            {
                _effects[i] = Wrapped(UiStyle.TextLight, UiText.Small);
                column.Children.Add(_effects[i]);
            }
            column.Children.Add(Window.Gap(4f));
            column.Children.Add(Window.SubHeader("SPONSOR"));
            _sponsorLine = Wrapped(UiStyle.TextLight, UiText.Small);
            column.Children.Add(_sponsorLine);
            column.Children.Add(Window.Gap(2f));
            OfferButton = UiParts.PrimaryButton(string.Empty, DetailWidth - 24f, 32f, UiText.BodyBold);
            OfferButton.Visual.YUnits = GeneralUnitType.PixelsFromSmall;
            OfferButton.Visual.YOrigin = VerticalAlignment.Top;
            OfferButton.Click += (_, _) => { _model.Offer(); Bind(); };
            column.Children.Add(OfferButton.Visual);
            _feedback = Wrapped(UiStyle.Success, UiText.Small);
            column.Children.Add(_feedback);
        }

        /// <summary>The detail column's width: what the body leaves after the skills and the tree.</summary>
        private const float DetailWidth = Width - 2f * Window.Padding - DetailX;

        public Window Window { get; }

        /// <summary>Offers the patron's gold for the technique picked.</summary>
        public Button OfferButton { get; }

        /// <summary>A skill's button in the list (tests click it); null for a skill that offers no techniques.</summary>
        public Button SkillButton(Skill skill)
        {
            for (int i = 0; i < _skills.Count; i++) if (_skills[i].Skill == skill) return _skills[i].Button;
            return null;
        }

        /// <summary>The node of <paramref name="tier"/>'s technique at <paramref name="index"/> (tests click it and read its words).</summary>
        public Button NodeButton(int tier, int index) => _tiers[tier].Nodes[index].Button;

        /// <summary>A node's words as shown: its name, its corner word, and each effect line.</summary>
        public string NodeName(int tier, int index) => _tiers[tier].Nodes[index].Name.Text;
        public string NodeTag(int tier, int index) => _tiers[tier].Nodes[index].Tag.Text;
        public IEnumerable<TextRuntime> NodeEffects(int tier, int index)
        {
            foreach (var effect in _tiers[tier].Nodes[index].Effects) if (effect.Visible) yield return effect;
        }

        /// <summary>Whether a node is drawn as held (the lit stone).</summary>
        public bool NodeLit(int tier, int index) => StoneSkin.PieceOf(_tiers[tier].Nodes[index].Button) == Simpiens.Presentation.Art.UiKit.ButtonActive;

        /// <summary>The ring round the technique picked, in the tree.</summary>
        public RectangleRuntime Ring => _ring;

        /// <summary>The lit trunk: how far the level has climbed.</summary>
        public RectangleRuntime TrunkLit => _trunkLit;

        public string NameShown => _name.Text;
        public string StandingShown => _standing.Text;
        public string RootShown => _root.Text;
        public string DetailTitleShown => _title.Text;
        public string SponsorLineShown => _sponsorLine.Text;
        public string FeedbackShown => _feedback.Text;

        /// <summary>The detail's words, for the layout's checks: every text in the column.</summary>
        public IEnumerable<TextRuntime> DetailTexts
        {
            get
            {
                yield return _title;
                yield return _subtitle;
                yield return _vow;
                foreach (var effect in _effects) yield return effect;
                yield return _sponsorLine;
                yield return _feedback;
            }
        }

        public void Bind()
        {
            Window.IsShown = _model.IsOpen;
            if (!_model.IsOpen) return;
            Window.SnapToPixels();
            UiParts.SetText(_name, _model.Name);
            UiParts.SetText(_standing, _model.Standing);
            UiParts.SetText(_treasury, _model.Treasury);
            UiParts.SetFace(_faceBox, _face, _portraits, _model.HasPortrait, _model.Portrait);

            var rows = _model.Skills;
            for (int i = 0; i < _skills.Count && i < rows.Count; i++)
            {
                var entry = _skills[i];
                UiParts.SetText(entry.Text, rows[i].Text);
                UiParts.SetText(entry.Band, rows[i].Band);
                StoneSkin.SetActive(entry.Button, rows[i].Selected);
            }

            UiParts.SetText(_root, _model.RootText);
            float lit = TrunkY(_model.Progress) - RootHeight;
            if (_trunkLit.Height != lit) _trunkLit.Height = lit;
            bool ringed = false;
            for (int t = 0; t < _tiers.Length; t++)
            {
                var tier = _tiers[t];
                var row = _model.TierRows[t];
                tier.Label.Color = row.Reached ? UiStyle.Accent : UiStyle.TextMuted;
                tier.Gate.Color = row.Reached ? UiStyle.TextLight : UiStyle.TextMuted;
                tier.Outline.StrokeColor = row.Reached ? UiStyle.Accent : UiStyle.CardBorder;
                for (int n = 0; n < tier.Nodes.Length; n++)
                {
                    var node = tier.Nodes[n];
                    var shown = row.Nodes[n];
                    node.Technique = shown.Technique;
                    node.Button.Visual.Visible = shown.Technique != Technique.None;
                    ShowNode(node, shown);
                    tier.Branches[n].FillColor = shown.State == TechniqueNodeState.Held ? UiStyle.Accent : row.Reached ? UiStyle.ControlBorder : UiStyle.Border;
                    if (!shown.Selected) continue;
                    ringed = true;
                    var visual = node.Button.Visual;
                    _ring.X = visual.X - 3f;
                    _ring.Y = visual.Y - 3f;
                    _ringInner.X = visual.X - 2f;
                    _ringInner.Y = visual.Y - 2f;
                }
            }
            _ring.Visible = _ringInner.Visible = ringed;

            UiParts.SetText(_title, _model.DetailTitle);
            UiParts.SetText(_subtitle, _model.DetailSubtitle);
            UiParts.SetText(_vow, _model.Vow);
            var effects = _model.DetailEffects;
            for (int i = 0; i < _effects.Length; i++)
            {
                bool has = i < effects.Count;
                if (_effects[i].Visible != has) _effects[i].Visible = has;
                if (!has) continue;
                UiParts.SetText(_effects[i], effects[i].Text);
                _effects[i].Color = effects[i].IsUpside ? UiStyle.Success : UiStyle.ToneColor(FeedbackTone.Error);
            }
            UiParts.SetText(_sponsorLine, _model.SponsorLine);
            if (!string.Equals(OfferButton.Text, _model.OfferText, System.StringComparison.Ordinal)) OfferButton.Text = _model.OfferText;
            OfferButton.IsEnabled = _model.CanOffer;
            UiParts.SetText(_feedback, _model.Feedback.Text);
            _feedback.Color = UiStyle.ToneColor(_model.Feedback.Tone);
        }

        // A node as the model has it: lit when held; its words bright when its band is reached, dim when not
        private static void ShowNode(Node node, TechniqueNode shown)
        {
            bool locked = shown.State == TechniqueNodeState.Locked;
            StoneSkin.SetActive(node.Button, shown.State == TechniqueNodeState.Held);
            UiParts.SetText(node.Name, shown.Name);
            node.Name.Color = locked ? UiStyle.TextMuted : UiStyle.TextBright;
            UiParts.SetText(node.Tag, shown.Tag);
            node.Tag.Color = shown.State switch
            {
                TechniqueNodeState.Held => UiStyle.Accent,
                TechniqueNodeState.Open => UiStyle.GoldText,
                _ => UiStyle.TextMuted,
            };
            for (int i = 0; i < node.Effects.Length; i++)
            {
                var text = node.Effects[i];
                bool has = i < shown.Effects.Count;
                if (text.Visible != has) text.Visible = has;
                if (!has) continue;
                UiParts.SetText(text, shown.Effects[i].Text);
                var colour = shown.Effects[i].IsUpside ? UiStyle.Success : UiStyle.ToneColor(FeedbackTone.Error);
                text.Color = locked ? UiParts.Alpha(colour, 0.55f) : colour;
            }
        }

        private Tier AddTier(ContainerRuntime tree, int index)
        {
            var tier = new Tier();
            float top = TierTop(index), centre = TierCentre(index);
            float pillX = (TreeWidth - PillWidth) / 2f;
            // The branches first, under the pill and the nodes
            tier.Branches[0] = Line(NodeWidth, centre - Trunk / 2f, pillX - NodeWidth, Trunk, UiStyle.Border);
            tier.Branches[1] = Line(pillX + PillWidth, centre - Trunk / 2f, TreeWidth - NodeWidth - pillX - PillWidth, Trunk, UiStyle.Border);
            tree.Children.Add(tier.Branches[0]);
            tree.Children.Add(tier.Branches[1]);
            var pill = new ContainerRuntime { X = pillX, Y = centre - PillHeight / 2f, Width = PillWidth, Height = PillHeight };
            UiParts.AddInset(pill);
            tier.Outline = Outline(UiStyle.CardBorder);
            pill.Children.Add(tier.Outline);
            var words = UiParts.Column(1f);
            words.XUnits = GeneralUnitType.PixelsFromMiddle;
            words.XOrigin = HorizontalAlignment.Center;
            UiParts.CentreVertically(words);
            var row = _model.TierRows[index];
            tier.Label = UiParts.Text(row.Label, UiStyle.TextMuted, UiText.Badge);
            tier.Gate = UiParts.Text(row.Gate, UiStyle.TextMuted, UiText.Small);
            tier.Label.XUnits = tier.Gate.XUnits = GeneralUnitType.PixelsFromMiddle;
            tier.Label.XOrigin = tier.Gate.XOrigin = HorizontalAlignment.Center;
            words.Children.Add(tier.Label);
            words.Children.Add(tier.Gate);
            pill.Children.Add(words);
            tree.Children.Add(pill);
            for (int n = 0; n < tier.Nodes.Length; n++)
            {
                var node = new Node { Button = UiParts.Button(string.Empty, NodeWidth, NodeHeight) };
                node.Button.Visual.X = n == 0 ? 0f : TreeWidth - NodeWidth;
                node.Button.Visual.Y = top;
                node.Name = UiParts.Text(string.Empty, UiStyle.TextBright, UiText.BodyBold);
                node.Name.X = NodePadding;
                node.Name.Y = 7f;
                node.Button.Visual.Children.Add(node.Name);
                node.Tag = UiParts.Text(string.Empty, UiStyle.TextMuted, UiText.SmallBold);
                node.Tag.XUnits = GeneralUnitType.PixelsFromLarge;
                node.Tag.XOrigin = HorizontalAlignment.Right;
                node.Tag.X = -NodePadding;
                node.Tag.Y = 9f;
                node.Button.Visual.Children.Add(node.Tag);
                // What it does, a line each, wrapped within the node
                var effects = UiParts.Column(0f);
                effects.HasEvents = false; // a container takes events by default: the click is the node's
                effects.X = NodePadding;
                effects.Y = EffectsY;
                effects.WidthUnits = DimensionUnitType.Absolute;
                effects.Width = NodeWidth - 2f * NodePadding;
                for (int i = 0; i < node.Effects.Length; i++)
                {
                    var effect = Wrapped(UiStyle.TextLight, UiText.Small);
                    effects.Children.Add(effect);
                    node.Effects[i] = effect;
                }
                node.Button.Visual.Children.Add(effects);
                var me = node;
                node.Button.Click += (_, _) => { _model.SelectTechnique(me.Technique); Bind(); };
                tree.Children.Add(node.Button.Visual);
                tier.Nodes[n] = node;
            }
            return tier;
        }

        private static float TierTop(int index) => RootHeight + RootGap + index * (NodeHeight + TierGap);

        private static float TierCentre(int index) => TierTop(index) + NodeHeight / 2f;

        /// <summary>Where the trunk's light reaches for a climb of <paramref name="progress"/> tiers: the root's foot at 0, each tier's centre at its number.</summary>
        private static float TrunkY(float progress)
        {
            if (progress <= 0f) return RootHeight;
            if (progress <= 1f) return RootHeight + progress * (TierCentre(0) - RootHeight);
            int below = (int)progress;
            if (below >= TechniqueTreeModel.Tiers) return TierCentre(TechniqueTreeModel.Tiers - 1);
            return TierCentre(below - 1) + (progress - below) * (TierCentre(below) - TierCentre(below - 1));
        }

        private static RectangleRuntime Line(float x, float y, float width, float height, Color colour)
            => new RectangleRuntime { IsFilled = true, StrokeWidth = 0, FillColor = colour, X = x, Y = y, Width = width, Height = height };

        private static RectangleRuntime Outline(Color colour)
            => new RectangleRuntime { IsFilled = false, StrokeWidth = 1, StrokeColor = colour, WidthUnits = DimensionUnitType.RelativeToParent, HeightUnits = DimensionUnitType.RelativeToParent, Width = 0f, Height = 0f };

        private static void Centre(TextRuntime text)
        {
            text.XUnits = GeneralUnitType.PixelsFromMiddle;
            text.XOrigin = HorizontalAlignment.Center;
            UiParts.CentreVertically(text);
        }

        private static TextRuntime Wrapped(Color colour, UiText style)
        {
            var text = UiParts.Text(string.Empty, colour, style);
            text.WidthUnits = DimensionUnitType.RelativeToParent;
            text.Width = 0f;
            return text;
        }
    }
}
