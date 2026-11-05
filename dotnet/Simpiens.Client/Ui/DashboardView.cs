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
        private int _sessionVersion;

        /// <param name="motion">The interface moves (<see cref="ClientOptions.UiMotion"/>): windows arrive, the gold rolls.</param>
        public DashboardView(UiLayer ui, DashboardModel model, float refreshSeconds, IconAtlas icons, PortraitAtlas portraits = null, bool motion = false)
        {
            Model = model;
            _refreshSeconds = refreshSeconds;
            Header = new HeaderBar(icons) { Motion = motion };
            Panels = new DashboardPanels(model, icons, portraits);
            ui.Root.Children.Add(Panels.Root); // under the windows and the header
            Tracker = new ExpeditionTracker(model.Context.Simulation?.Economy?.Guild.MaxMembers ?? Simpiens.Simulation.Economy.GuildTuning.Default.MaxMembers);
            ui.Root.Children.Add(Tracker.Root);
            Inspector = new InspectorCard(model, portraits); // Epic UI-Beta B4: on the map, under the windows
            ui.Root.Children.Add(Inspector.Root);
            Panels.FindOnMap = FindOnMap;
            Merchant = new MerchantWindow(model.Merchant, icons);
            Merchant.Window.CloseButton.Click += (_, _) => { Model.Merchant.Close(); Refresh(); };
            ui.Root.Children.Add(Merchant.Window.Veil);
            Bounties = new BountyBoardWindow(model.Bounties, icons);
            Bounties.Window.CloseButton.Click += (_, _) => { Model.Bounties.Close(); Refresh(); };
            ui.Root.Children.Add(Bounties.Window.Veil);
            Guild = new GuildWindow(model.Guild, icons, model.Context.Simulation?.Economy?.Guild.MaxMembers ?? Simpiens.Simulation.Economy.GuildTuning.Default.MaxMembers, portraits);
            Guild.FindOnMap = FindOnMap;
            Guild.Window.CloseButton.Click += (_, _) => { Model.Guild.Close(); Refresh(); };
            ui.Root.Children.Add(Guild.Window.Veil);
            Header.MerchantButton.Click += (_, _) => { Model.ToggleMerchant(); Refresh(); };
            Header.BountiesButton.Click += (_, _) => { Model.ToggleBounties(); Refresh(); };
            Header.GuildButton.Click += (_, _) => { Model.ToggleGuild(); Refresh(); };
            Header.TabButton.Click += (_, _) => TogglePanels();
            Header.MenuButton.Click += (_, _) => Header.ToggleMenu();
            Header.SaveButton.Click += (_, _) => { Header.CloseMenu(); Model.Saves.Save(); Refresh(); };
            Header.LoadButton.Click += (_, _) => Header.AskToLoad(); // Load asks first (the UI polish plan, U4)
            Header.ConfirmLoadButton.Click += (_, _) => { Header.CloseMenu(); Model.Saves.Load(); Refresh(); };
            Header.CancelLoadButton.Click += (_, _) => Header.CancelLoad();
            Header.KeyHelpButton.Click += (_, _) => Header.CloseMenu();
            Header.PauseButton.Click += (_, _) => Model.Header.TogglePause();
            for (int i = 0; i < Header.SpeedButtons.Count; i++)
            {
                int step = i;
                Header.SpeedButtons[i].Click += (_, _) => { Model.Header.SetSpeedStep(step); Refresh(); };
            }
            ui.Root.Children.Add(Header.Root);
            ui.Root.Children.Add(Header.Status);
            Merchant.Window.Motion = motion;
            Bounties.Window.Motion = motion;
            Guild.Window.Motion = motion;
            Sessions = new SessionSwitchBar(model.Sessions);
            ui.Root.Children.Add(Sessions.Root);
            ui.Root.Children.Add(Header.Menu); // over everything but the tips: the session switch sits where it drops
            Refresh();
        }

        public DashboardModel Model { get; }
        public HeaderBar Header { get; }
        public DashboardPanels Panels { get; }
        public MerchantWindow Merchant { get; }
        public BountyBoardWindow Bounties { get; }
        public GuildWindow Guild { get; }

        /// <summary>The expedition tracker on the map view while a party is out (Epic 10 G5).</summary>
        public ExpeditionTracker Tracker { get; }

        /// <summary>The map inspector (Epic UI-Beta B4): the selected crawler's card on the map.</summary>
        public InspectorCard Inspector { get; }
        public SessionSwitchBar Sessions { get; }

        /// <summary>True unless a hard cutscene hid it.</summary>
        public bool Shown { get; private set; } = true;

        /// <summary>Hides the whole dashboard (a hard cutscene covers the game).</summary>
        public void Hide()
        {
            Shown = false;
            Header.Root.Visible = false;
            Header.CloseMenu();
            Header.Status.Visible = false;
            Panels.IsShown = false;
            Merchant.Window.IsShown = false;
            Bounties.Window.IsShown = false;
            Guild.Window.IsShown = false;
            Tracker.Root.Visible = false;
            Inspector.Root.Visible = false;
            Sessions.Root.Visible = false;
        }

        /// <summary>Shows it again as the model has it.</summary>
        public void Show()
        {
            Shown = true;
            Header.Root.Visible = true;
            Refresh();
        }

        /// <summary>A click on a face (Epic UI-Beta B4, D4): the map, with that crawler selected and followed, and its card.</summary>
        public void FindOnMap(EntityId crawler)
        {
            Model.FindOnMap(crawler);
            Refresh();
        }

        public void TogglePanels()
        {
            Model.TogglePanels();
            Refresh();
        }

        /// <summary>Esc: closes the menu or the open window; false when there was none (then Esc stops the camera following).</summary>
        public bool CloseWindow()
        {
            if (Header.CloseMenu()) return true;
            if (!Model.CloseWindow()) return false;
            Refresh();
            return true;
        }

        public void Update(float realSeconds)
        {
            Model.Saves.Poll();
            if (realSeconds > 0f)
            {
                // Motion runs every frame on real time; the binds below stay at the refresh's rate
                Header.Animate(realSeconds);
                Merchant.Window.Animate(realSeconds);
                Bounties.Window.Animate(realSeconds);
                Guild.Window.Animate(realSeconds);
            }
            if (realSeconds > 0f) _sinceRefresh += realSeconds;
            bool newSession = Model.SessionVersion != _sessionVersion; // a switch or a load shows at once, not at the next refresh
            if (!newSession && _refreshSeconds > 0f && _sinceRefresh < _refreshSeconds) return;
            _sinceRefresh = 0f;
            Refresh();
        }

        public void Refresh()
        {
            _sessionVersion = Model.SessionVersion;
            Model.Refresh();
            Header.Bind(Model);
            Panels.Bind();
            if (Model.Merchant.IsOpen && !_merchantWasOpen) Merchant.LoadPrices(); // opening loads the stall's prices into the sliders
            _merchantWasOpen = Model.Merchant.IsOpen;
            Merchant.Bind();
            if (Model.Bounties.IsOpen && !_bountiesWereOpen) Bounties.LoadForm();
            _bountiesWereOpen = Model.Bounties.IsOpen;
            Bounties.Bind();
            Guild.Bind();
            Tracker.Bind(Model.Guild.Expedition, mapView: !Model.PanelsVisible);
            Inspector.Bind();
            Sessions?.Bind();
        }
    }
}
