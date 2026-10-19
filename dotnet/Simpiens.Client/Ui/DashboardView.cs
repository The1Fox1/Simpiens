using Simpiens.Presentation.Dashboard;

namespace Simpiens.Client.Ui
{
    /// <summary>
    /// The dashboard in Gum (Epic CLIENT M2): the views over <see cref="DashboardModel"/>. They draw what the model says
    /// and forward the player's clicks to it; nothing here decides anything. Refreshed at 10 Hz, as Unity's presenter
    /// was (tests refresh every frame).
    /// </summary>
    public sealed class DashboardView
    {
        private readonly float _refreshSeconds;
        private float _sinceRefresh;

        public DashboardView(UiLayer ui, DashboardModel model, float refreshSeconds)
        {
            Model = model;
            _refreshSeconds = refreshSeconds;
            Header = new HeaderBar();
            Header.MerchantButton.Click += (_, _) => { Model.ToggleMerchant(); Refresh(); };
            Header.BountiesButton.Click += (_, _) => { Model.ToggleBounties(); Refresh(); };
            Header.TabButton.Click += (_, _) => TogglePanels();
            ui.Root.Children.Add(Header.Root);
            Refresh();
        }

        public DashboardModel Model { get; }
        public HeaderBar Header { get; }

        public void TogglePanels()
        {
            Model.TogglePanels();
            Refresh();
        }

        /// <summary>Esc: closes the open window; false when there was none (then Esc stops the camera following).</summary>
        public bool CloseWindow()
        {
            if (!Model.CloseWindow()) return false;
            Refresh();
            return true;
        }

        public void Update(float realSeconds)
        {
            if (realSeconds > 0f) _sinceRefresh += realSeconds;
            if (_refreshSeconds > 0f && _sinceRefresh < _refreshSeconds) return;
            _sinceRefresh = 0f;
            Refresh();
        }

        public void Refresh()
        {
            Model.Refresh();
            Header.Bind(Model);
        }
    }
}
