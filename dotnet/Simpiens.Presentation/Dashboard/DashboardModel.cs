using Simpiens.Simulation;

namespace Simpiens.Presentation.Dashboard
{
    /// <summary>
    /// The dashboard as data (Epic CLIENT M2.1): everything Unity's <c>FacilityDashboardPresenter</c> showed and decided,
    /// with no UI types, so it is tested without a GPU. The client's Gum views draw it and forward the player's input
    /// to it; the player's actions leave as typed commands through <see cref="DashboardContext.Intervention"/>.
    /// <list type="bullet">
    /// <item><description>The header and an open window show over the map; the three panels show while <see cref="PanelsVisible"/>.</description></item>
    /// <item><description>One window at a time: opening one closes the others (<see cref="IDashboardWindow"/>).</description></item>
    /// <item><description>The selection follows the map's <see cref="WorldSelection"/> both ways, as Unity's did.</description></item>
    /// </list>
    /// </summary>
    public sealed class DashboardModel
    {
        private readonly DashboardContext _context;
        private readonly WorldSelection _selection;
        private EntityId _selectedCrawler;
        private readonly IDashboardWindow[] _windows;

        public DashboardModel(DashboardContext context, WorldSelection selection = null)
        {
            _context = context ?? new DashboardContext();
            _selection = selection;
            Header = new HeaderModel(_context);
            Merchant = new MerchantModel(_context);
            Bounties = new BountyBoardModel(_context);
            Guild = new GuildModel(_context, Moods);
            Stations = new StationModel(_context, Moods);
            _windows = new IDashboardWindow[] { Merchant, Bounties, Guild, Stations };
            Sectors = new SectorPanelModel(_context, Moods);
            Dossier = new DossierModel(_context, Guild, Moods);
            Inspector = new InspectorModel(_context, Moods);
            Saves = new SaveSlotModel(_context.SaveGame, _context.HostCommands);
            Sessions = new SessionSwitchModel(_context.Sessions, _context.HostCommands);
            if (_selection != null) _selection.Changed += OnSelectionChanged;
            Sectors.EnsureSelection();
        }

        public DashboardContext Context => _context;

        /// <summary>The map inspector (Epic UI-Beta B4): the selected crawler's card while the panels are hidden.</summary>
        public InspectorModel Inspector { get; }

        /// <summary>
        /// A click on a crawler's face anywhere (Epic UI-Beta B4, D4): the windows and panels close and the map shows it,
        /// selected, with the camera following, so the inspector shows it.
        /// </summary>
        public void FindOnMap(EntityId crawlerId)
        {
            if (crawlerId == default || _context.Registry?.GetEntity(crawlerId)?.Agent == null) return;
            CloseAllBut(null);
            PanelsVisible = false;
            _selectedCrawler = crawlerId;
            _selection?.SelectCrawler(crawlerId, follow: true);
            Refresh();
        }

        /// <summary>The inspector's Open dossier: the panels, showing the selected crawler.</summary>
        public void OpenDossier()
        {
            if (SelectedCrawler == default) return;
            PanelsVisible = true;
            Refresh();
        }

        /// <summary>The inspector's Follow: the camera follows the selected crawler, or stops.</summary>
        public void ToggleFollow()
        {
            if (_selection == null || _selection.Selected == default) return;
            if (_selection.Following) _selection.StopFollowing();
            else if (_selection.SelectedCreature != default) _selection.SelectCreature(_selection.SelectedCreature, follow: true); // K2.5
            else _selection.SelectCrawler(_selection.SelectedCrawler, follow: true);
            Refresh();
        }

        /// <summary>Each crawler's face over time (Epic UI-Beta B3): the dossier's portrait now, the inspector's and the lists' with B4.</summary>
        public Simpiens.Presentation.Art.People.MoodTracker Moods { get; } = new Simpiens.Presentation.Art.People.MoodTracker();
        public HeaderModel Header { get; }
        public MerchantModel Merchant { get; }
        public BountyBoardModel Bounties { get; }

        /// <summary>The guild's roster window and what the dossier says about recruiting (Epic 10 G4).</summary>
        public GuildModel Guild { get; }

        /// <summary>The station window (Epic 10 Part 2, W1.6a): building, the artisan, its shifts and the order.</summary>
        public StationModel Stations { get; }
        public SectorPanelModel Sectors { get; }
        public DossierModel Dossier { get; }
        public SaveSlotModel Saves { get; }
        public SessionSwitchModel Sessions { get; }

        /// <summary>The three panels over the map (Tab). Unity started with them shown.</summary>
        public bool PanelsVisible { get; set; } = true;

        /// <summary>Counts the sessions this model has seen replaced (respawn, switch, load): a view that sees it move shows the new one at once.</summary>
        public int SessionVersion { get; private set; }

        /// <summary>The crawler the dossier shows.</summary>
        public EntityId SelectedCrawler => _selection != null ? _selection.SelectedCrawler : _selectedCrawler;

        // The dossier of the dead (Epic 10 Part 3, K5.8c): the map's selection drops a crawler that leaves the world, but the
        // dossier keeps one that died until something else is selected
        private EntityId _lastCrawler, _shownDead;

        /// <summary>Whom the dossier shows: the selected crawler, or the one selected when it died.</summary>
        public EntityId DossierCrawler => SelectedCrawler != default ? SelectedCrawler : _shownDead;

        /// <summary>The creature the inspector shows (Epic 10 Part 3, K2.5); the dossier is a crawler's alone.</summary>
        public EntityId SelectedCreature => _selection != null ? _selection.SelectedCreature : default;

        public void TogglePanels()
        {
            PanelsVisible = !PanelsVisible;
            Refresh();
        }

        public void OpenMerchant()
        {
            CloseAllBut(Merchant);
            Merchant.Open();
        }

        public void OpenBounties()
        {
            CloseAllBut(Bounties);
            Bounties.Open();
        }

        public void OpenGuild()
        {
            CloseAllBut(Guild);
            Guild.Open();
        }

        /// <summary>The station window on the station at <paramref name="index"/> (a click on it on the map), or the one shown last.</summary>
        public void OpenStation(int index = -1)
        {
            CloseAllBut(Stations);
            Stations.Open(index);
        }

        public void ToggleStations()
        {
            if (Stations.IsOpen) Stations.Close();
            else OpenStation();
        }

        public void ToggleGuild()
        {
            if (Guild.IsOpen) Guild.Close();
            else OpenGuild();
        }

        public void ToggleMerchant()
        {
            if (Merchant.IsOpen) Merchant.Close();
            else OpenMerchant();
        }

        public void ToggleBounties()
        {
            if (Bounties.IsOpen) Bounties.Close();
            else OpenBounties();
        }

        /// <summary>Closes the open window, if any (Esc); false when none was open.</summary>
        public bool CloseWindow()
        {
            bool open = false;
            for (int i = 0; i < _windows.Length; i++) open |= _windows[i].IsOpen;
            CloseAllBut(null);
            return open;
        }

        /// <summary>One window at a time: closes every window but <paramref name="keep"/> (none for null).</summary>
        private void CloseAllBut(IDashboardWindow keep)
        {
            for (int i = 0; i < _windows.Length; i++) if (!ReferenceEquals(_windows[i], keep)) _windows[i].Close();
        }

        /// <summary>A sector picked in the list: shown here, and selected on the map.</summary>
        public void SelectSector(int sectorId)
        {
            Sectors.SelectedSectorId = sectorId;
            _selection?.SelectSector(sectorId);
            Refresh();
        }

        /// <summary>A resident picked in the list: the dossier shows it, and back on the map the camera follows it.</summary>
        public void SelectCrawler(EntityId crawlerId)
        {
            _selectedCrawler = crawlerId;
            _selection?.SelectCrawler(crawlerId, follow: true);
            Refresh();
        }

        /// <summary>Re-reads what is shown (Unity did this at 10 Hz): the header and open windows always, the panels while visible.</summary>
        public void Refresh()
        {
            Header.Refresh();
            Merchant.Refresh();
            Bounties.Refresh();
            Guild.Refresh();
            Stations.Refresh();
            Inspector.Refresh(PanelsVisible ? default : SelectedCrawler, _selection != null && _selection.Following, PanelsVisible ? default : SelectedCreature);
            if (!PanelsVisible) return;
            Sectors.Refresh(SelectedCrawler);
            Dossier.Refresh(DossierCrawler);
        }

        /// <summary>A respawn or a load replaced every entity (ids are reissued): drop the selection, close the windows, rebuild from the new map.</summary>
        public void SessionChanged()
        {
            _selectedCrawler = default;
            _lastCrawler = _shownDead = default;
            CloseAllBut(null);
            Bounties.Reset();
            Sectors.SelectedSectorId = -1;
            Sectors.EnsureSelection();
            SessionVersion++;
            Refresh();
        }

        /// <summary>A click on the map (or a reset) changed the selection: show the same crawler and room here.</summary>
        private void OnSelectionChanged()
        {
            // Dropped because it died: its dossier stays; anything else selected, or a crawler that walked out, ends that
            var now = _selection.SelectedCrawler;
            _shownDead = now == default && _selection.SelectedCreature == default && _lastCrawler != default && Died(_lastCrawler) ? _lastCrawler
                : now == default && _selection.SelectedCreature == default ? _shownDead : default;
            _lastCrawler = now;
            int sector = _selection.SelectedSector;
            if (sector > 0 && sector != Sectors.SelectedSectorId && _context.Zones.TryGetZone(sector, out _)) Sectors.SelectedSectorId = sector;
            if (PanelsVisible)
            {
                Sectors.Refresh(SelectedCrawler);
                Dossier.Refresh(DossierCrawler);
            }
            else Inspector.Refresh(SelectedCrawler, _selection.Following, SelectedCreature);
        }

        private bool Died(EntityId id)
        {
            var departed = _context.Departed?.Departed;
            if (departed == null) return false;
            for (int i = departed.Count - 1; i >= 0; i--) if (departed[i].Id == id) return departed[i].Died;
            return false;
        }
    }
}
