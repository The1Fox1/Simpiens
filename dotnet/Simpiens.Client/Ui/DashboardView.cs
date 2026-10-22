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
        private bool _merchantWasOpen, _bountiesWereOpen;

        public DashboardView(UiLayer ui, DashboardModel model, float refreshSeconds)
        {
            Model = model;
            _refreshSeconds = refreshSeconds;
            Header = new HeaderBar();
            Panels = new DashboardPanels(model);
            ui.Root.Children.Add(Panels.Root); // under the windows and the header
            Merchant = new MerchantWindow(model.Merchant);
            Merchant.Window.CloseButton.Click += (_, _) => { Model.Merchant.Close(); Refresh(); };
            ui.Root.Children.Add(Merchant.Window.Veil);
            Bounties = new BountyBoardWindow(model.Bounties);
            Bounties.Window.CloseButton.Click += (_, _) => { Model.Bounties.Close(); Refresh(); };
            ui.Root.Children.Add(Bounties.Window.Veil);
            Header.MerchantButton.Click += (_, _) => { Model.ToggleMerchant(); Refresh(); };
            Header.BountiesButton.Click += (_, _) => { Model.ToggleBounties(); Refresh(); };
            Header.TabButton.Click += (_, _) => TogglePanels();
            Header.SaveButton.Click += (_, _) => { Model.Saves.Save(); Refresh(); };
            Header.LoadButton.Click += (_, _) => { Model.Saves.Load(); Refresh(); };
            ui.Root.Children.Add(Header.Root);
            ui.Root.Children.Add(Header.Status);
            Sessions = new SessionSwitchBar(model.Sessions);
            Sessions.Button.Click += (_, _) => Refresh();
            ui.Root.Children.Add(Sessions.Root);
            Refresh();
        }

        public DashboardModel Model { get; }
        public HeaderBar Header { get; }
        public DashboardPanels Panels { get; }
        public MerchantWindow Merchant { get; }
        public BountyBoardWindow Bounties { get; }
        public SessionSwitchBar Sessions { get; }

        /// <summary>True unless a hard cutscene hid it.</summary>
        public bool Shown { get; private set; } = true;

        /// <summary>Hides the whole dashboard (a hard cutscene covers the game).</summary>
        public void Hide()
        {
            Shown = false;
            Header.Root.Visible = false;
            Header.Status.Visible = false;
            Panels.IsShown = false;
            Merchant.Window.IsShown = false;
            Bounties.Window.IsShown = false;
            Sessions.Root.Visible = false;
        }

        /// <summary>Shows it again as the model has it.</summary>
        public void Show()
        {
            Shown = true;
            Header.Root.Visible = true;
            Refresh();
        }

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
            Model.Saves.Poll();
            if (realSeconds > 0f) _sinceRefresh += realSeconds;
            if (_refreshSeconds > 0f && _sinceRefresh < _refreshSeconds) return;
            _sinceRefresh = 0f;
            Refresh();
        }

        public void Refresh()
        {
            Model.Refresh();
            Header.Bind(Model);
            Panels.Bind();
            if (Model.Merchant.IsOpen && !_merchantWasOpen) Merchant.LoadPrices(); // opening loads the stall's prices into the sliders
            _merchantWasOpen = Model.Merchant.IsOpen;
            Merchant.Bind();
            if (Model.Bounties.IsOpen && !_bountiesWereOpen) Bounties.LoadForm();
            _bountiesWereOpen = Model.Bounties.IsOpen;
            Bounties.Bind();
            Sessions?.Bind();
        }
    }
}
