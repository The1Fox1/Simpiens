using Gum.Converters;
using Gum.DataTypes;
using Gum.Forms.Controls;
using Gum.GueDeriving;
using Gum.Managers;
using Gum.Wireframe;
using Microsoft.Xna.Framework;
using RenderingLibrary.Graphics;
using Simpiens.Presentation.Dashboard;

namespace Simpiens.Client.Ui
{
    /// <summary>
    /// The map inspector (Epic UI-Beta B4, Q10): a card at the bottom left of the map view while a crawler is selected.
    /// Its portrait at the atlas's scale (2x in a 720-row window, larger in taller ones: the card widens with it); its name,
    /// state badge and how it feels; health and its needs; what it is doing; Open dossier and Follow. A view over
    /// <see cref="InspectorModel"/>; it decides nothing.
    /// </summary>
    public sealed class InspectorCard
    {
        /// <summary>The card's width with a 2x portrait; a larger portrait widens it by as much.</summary>
        public const float Width = 420f, Margin = 16f;

        private const float PortraitX = 12f, Gap = 12f, ButtonGap = 8f;

        private readonly DashboardModel _model;
        private readonly PortraitAtlas _portraits;
        private readonly TextRuntime _name, _badge, _mood, _action;
        private readonly RectangleRuntime _badgeFill;
        private readonly DashboardPanels.DriveBar _health, _hunger, _energy, _social, _frustration;
        private readonly ContainerRuntime _column;
        private Rectangle _portraitShown;
        private int _portraitSize;

        public InspectorCard(DashboardModel model, PortraitAtlas portraits)
        {
            _model = model;
            _portraits = portraits;
            Root = new ContainerRuntime
            {
                Width = Width,
                HeightUnits = DimensionUnitType.RelativeToChildren,
                Height = 12f,
                X = Margin,
                YUnits = GeneralUnitType.PixelsFromLarge,
                YOrigin = VerticalAlignment.Bottom,
                Y = -Margin,
                Visible = false,
                HasEvents = true, // clicks on the card stay off the map
            };
            UiParts.AddFrame(Root);
            Portrait = new SpriteRuntime
            {
                Texture = portraits?.Texture,
                TextureAddress = TextureAddress.Custom,
                WidthUnits = DimensionUnitType.Absolute,
                HeightUnits = DimensionUnitType.Absolute,
                X = PortraitX,
                Y = 12f,
                Visible = false,
            };
            Root.Children.Add(Portrait);

            var column = UiParts.Column(3f);
            _column = column;
            column.Y = 10f;
            column.WidthUnits = DimensionUnitType.RelativeToParent;
            var top = new ContainerRuntime { WidthUnits = DimensionUnitType.RelativeToParent, Width = 0f, Height = 24f };
            _name = UiParts.Text(string.Empty, UiStyle.TextTitle, UiText.Title);
            UiParts.CentreVertically(_name);
            top.Children.Add(_name);
            var badge = DashboardPanels.Badge(out _badge, out _badgeFill);
            badge.XUnits = GeneralUnitType.PixelsFromLarge;
            badge.XOrigin = HorizontalAlignment.Right;
            top.Children.Add(badge);
            column.Children.Add(top);
            _mood = UiParts.Text(string.Empty, UiStyle.TextLight, UiText.Small);
            column.Children.Add(_mood);
            _health = new DashboardPanels.DriveBar(column, "Health:", UiStyle.DriveHealth);
            _hunger = new DashboardPanels.DriveBar(column, "Hunger:", UiStyle.DriveHunger);
            _energy = new DashboardPanels.DriveBar(column, "Energy:", UiStyle.DriveEnergy);
            _social = new DashboardPanels.DriveBar(column, "Social:", UiStyle.DriveSocial);
            _frustration = new DashboardPanels.DriveBar(column, "Frustration:", UiStyle.Danger);
            _action = UiParts.Text(string.Empty, UiStyle.Accent, UiText.Small);
            column.Children.Add(_action);
            var buttons = new ContainerRuntime { ChildrenLayout = ChildrenLayout.LeftToRightStack, StackSpacing = ButtonGap, WidthUnits = DimensionUnitType.RelativeToParent, Width = 0f, Height = 34f };
            OpenDossierButton = UiParts.Button("Open dossier", 0f, 28f, UiText.SmallBold);
            OpenDossierButton.Click += (_, _) => _model.OpenDossier();
            FollowButton = UiParts.Button("Follow", 0f, 28f, UiText.SmallBold);
            FollowButton.Click += (_, _) => { _model.ToggleFollow(); Bind(); };
            buttons.Children.Add(OpenDossierButton.Visual);
            buttons.Children.Add(FollowButton.Visual);
            column.Children.Add(buttons);
            column.Children.Add(Window.Gap(4f));
            Root.Children.Add(column);
            Fit(portraits?.Size ?? PortraitAtlas.FramedSize * PortraitAtlas.BaseScale);
        }

        /// <summary>Lays the card out for a portrait <paramref name="size"/> pixels square: the words move over and the card widens.</summary>
        public void Fit(int size)
        {
            if (size == _portraitSize) return;
            _portraitSize = size;
            Portrait.Width = size;
            Portrait.Height = size;
            float card = Width + size - PortraitAtlas.FramedSize * PortraitAtlas.BaseScale;
            Root.Width = card;
            _column.X = PortraitX + size + Gap;
            _column.Width = -(_column.X + Gap);
            float half = (card - _column.X - Gap - ButtonGap) / 2f;
            OpenDossierButton.Width = half;
            FollowButton.Width = half;
        }

        public ContainerRuntime Root { get; }
        public SpriteRuntime Portrait { get; }
        public Button OpenDossierButton { get; }
        public Button FollowButton { get; }
        public string NameShown => _name.Text;
        public string MoodShown => _mood.Text;

        public void Bind()
        {
            var inspector = _model.Inspector;
            bool shown = inspector.IsShown && !_model.PanelsVisible;
            if (Root.Visible != shown) Root.Visible = shown;
            if (!shown) return;
            UiParts.SetText(_name, inspector.Name);
            UiParts.SetText(_badge, inspector.Badge);
            _badgeFill.FillColor = UiStyle.StateColor(inspector.State);
            UiParts.SetText(_mood, inspector.MoodLine);
            _health.Set(inspector.Health);
            _hunger.Set(inspector.Hunger);
            _energy.Set(inspector.Energy);
            _social.Set(inspector.Social);
            _frustration.Set(inspector.Frustration);
            UiParts.SetText(_action, inspector.Action);
            if (FollowButton.Text != inspector.FollowText) FollowButton.Text = inspector.FollowText;
            bool portrait = inspector.HasPortrait && _portraits != null;
            if (Portrait.Visible != portrait) Portrait.Visible = portrait;
            if (!portrait) return;
            var source = _portraits.Show(inspector.Portrait);
            if (source != _portraitShown) Portrait.SourceRectangle = _portraitShown = source;
        }
    }
}
