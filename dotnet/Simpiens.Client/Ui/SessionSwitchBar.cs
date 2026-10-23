using Gum.Converters;
using Microsoft.Xna.Framework;
using MonoGameGum.GueDeriving;
using RenderingLibrary.Graphics;
using Simpiens.Presentation.Dashboard;

namespace Simpiens.Client.Ui
{
    /// <summary>
    /// The session switch (Unity's session-switch overlay: under the header, at the right): a banner on a test
    /// session, and the button to the next session. Shown only when a test session is registered (development builds).
    /// </summary>
    public sealed class SessionSwitchBar
    {
        private readonly SessionSwitchModel _model;
        private readonly ContainerRuntime _banner;
        private readonly TextRuntime _bannerText;

        public SessionSwitchBar(SessionSwitchModel model)
        {
            _model = model;
            Root = UiParts.Row(8f);
            Root.Name = "session-switch";
            Root.YUnits = GeneralUnitType.PixelsFromSmall;
            Root.YOrigin = VerticalAlignment.Top;
            Root.Y = HeaderBar.Height + 8f;
            Root.XUnits = GeneralUnitType.PixelsFromLarge;
            Root.XOrigin = HorizontalAlignment.Right;
            Root.X = -16f;
            _banner = UiParts.Pill(out _bannerText, string.Empty, UiStyle.TestBanner, UiStyle.TestBannerBorder, UiStyle.TestBannerText, UiText.BodyBold);
            UiParts.CentreVertically(_banner);
            Root.Children.Add(_banner);
            Button = UiParts.Button(string.Empty, 200f, 30f, UiText.BodyBold);
            Root.Children.Add(Button.Visual);
            Button.Click += (_, _) => _model.Switch();
            Bind();
        }

        public ContainerRuntime Root { get; }
        public Gum.Forms.Controls.Button Button { get; }
        public bool BannerShown => _banner.Visible && Root.Visible;
        public string BannerTextShown => _bannerText.Text;

        public void Bind()
        {
            Root.Visible = _model.Visible;
            if (!_model.Visible) return;
            Button.Text = _model.ButtonText;
            _banner.Visible = _model.BannerVisible;
            UiParts.SetText(_bannerText, _model.BannerText);
        }
    }
}
