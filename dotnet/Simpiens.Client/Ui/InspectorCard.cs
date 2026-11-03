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
    /// Its portrait at 2x; its name, state badge and how it feels; health and its needs; what it is doing; Open dossier
    /// and Follow. A view over <see cref="InspectorModel"/>; it decides nothing.
    /// </summary>
    public sealed class InspectorCard
    {
        public const float Width = 420f, Margin = 16f;

        private readonly DashboardModel _model;
        private readonly PortraitAtlas _portraits;
        private readonly TextRuntime _name, _badge, _mood, _action;
        private readonly RectangleRuntime _badgeFill;
        private readonly DashboardPanels.DriveBar _health, _hunger, _energy, _social, _frustration;
        private Rectangle _portraitShown;

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
            UiParts.AddFill(Root, UiStyle.Panel, UiStyle.Border);
            Portrait = new SpriteRuntime
            {
                Texture = portraits?.Texture,
                TextureAddress = TextureAddress.Custom,
                WidthUnits = DimensionUnitType.Absolute,
                HeightUnits = DimensionUnitType.Absolute,
                Width = PortraitAtlas.Size,
                Height = PortraitAtlas.Size,
                X = 12f,
                Y = 12f,
                Visible = false,
            };
            Root.Children.Add(Portrait);

            var column = UiParts.Column(3f);
            column.X = 12f + PortraitAtlas.Size + 12f;
            column.Y = 10f;
            column.WidthUnits = DimensionUnitType.RelativeToParent;
            column.Width = -(column.X + 12f);
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
            var buttons = new ContainerRuntime { ChildrenLayout = ChildrenLayout.LeftToRightStack, StackSpacing = 8f, WidthUnits = DimensionUnitType.RelativeToParent, Width = 0f, Height = 34f };
            float half = (Width - column.X - 12f - 8f) / 2f;
            OpenDossierButton = UiParts.Button("Open dossier", half, 28f, UiText.SmallBold);
            OpenDossierButton.Click += (_, _) => _model.OpenDossier();
            FollowButton = UiParts.Button("Follow", half, 28f, UiText.SmallBold);
            FollowButton.Click += (_, _) => { _model.ToggleFollow(); Bind(); };
            buttons.Children.Add(OpenDossierButton.Visual);
            buttons.Children.Add(FollowButton.Visual);
            column.Children.Add(buttons);
            column.Children.Add(Window.Gap(4f));
            Root.Children.Add(column);
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
            var source = _portraits.Show(inspector.Portrait, PortraitAtlas.Scale);
            if (source != _portraitShown) Portrait.SourceRectangle = _portraitShown = source;
        }
    }
}
