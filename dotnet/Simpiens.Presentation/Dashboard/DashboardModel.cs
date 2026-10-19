using Simpiens.Simulation;

namespace Simpiens.Presentation.Dashboard
{
    /// <summary>
    /// The dashboard as data (Epic CLIENT M2.1): everything Unity's <c>FacilityDashboardPresenter</c> showed and decided,
    /// with no UI types, so it is tested without a GPU. The client's Gum views draw it and forward the player's input
    /// to it; the player's actions leave as typed commands through <see cref="DashboardContext.Intervention"/>.
    /// <list type="bullet">
    /// <item><description>The header and an open window show over the map; the three panels show while <see cref="PanelsVisible"/>.</description></item>
    /// <item><description>One window at a time: opening the merchant closes the bounty board, and the other way round.</description></item>
    /// <item><description>The selection follows the map's <see cref="WorldSelection"/> both ways, as Unity's did.</description></item>
    /// </list>
    /// </summary>
    public sealed class DashboardModel
    {
        private readonly DashboardContext _context;
        private readonly WorldSelection _selection;
        private EntityId _selectedCrawler;

        public DashboardModel(DashboardContext context, WorldSelection selection = null)
        {
            _context = context ?? new DashboardContext();
            _selection = selection;
            Header = new HeaderModel(_context);
            Merchant = new MerchantModel(_context);
            Bounties = new BountyBoardModel(_context);
            Sectors = new SectorPanelModel(_context);
            Dossier = new DossierModel(_context);
            Saves = new SaveSlotModel(_context.SaveGame);
            Sessions = new SessionSwitchModel(_context.Sessions);
            if (_selection != null) _selection.Changed += OnSelectionChanged;
            Sectors.EnsureSelection();
        }

        public DashboardContext Context => _context;
        public HeaderModel Header { get; }
        public MerchantModel Merchant { get; }
        public BountyBoardModel Bounties { get; }
        public SectorPanelModel Sectors { get; }
        public DossierModel Dossier { get; }
        public SaveSlotModel Saves { get; }
        public SessionSwitchModel Sessions { get; }

        /// <summary>The three panels over the map (Tab). Unity started with them shown.</summary>
        public bool PanelsVisible { get; set; } = true;

        /// <summary>The crawler the dossier shows.</summary>
        public EntityId SelectedCrawler => _selection != null ? _selection.SelectedCrawler : _selectedCrawler;

        public void TogglePanels()
        {
            PanelsVisible = !PanelsVisible;
            Refresh();
        }

        public void OpenMerchant()
        {
            Bounties.Close();
            Merchant.Open();
        }

        public void OpenBounties()
        {
            Merchant.Close();
            Bounties.Open();
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
            if (!Merchant.IsOpen && !Bounties.IsOpen) return false;
            Merchant.Close();
            Bounties.Close();
            return true;
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
            if (!PanelsVisible) return;
            Sectors.Refresh(SelectedCrawler);
            Dossier.Refresh(SelectedCrawler);
        }

        /// <summary>A respawn or a load replaced every entity (ids are reissued): drop the selection, close the windows, rebuild from the new map.</summary>
        public void SessionChanged()
        {
            _selectedCrawler = default;
            Merchant.Close();
            Bounties.Close();
            Bounties.Reset();
            Sectors.SelectedSectorId = -1;
            Sectors.EnsureSelection();
            Refresh();
        }

        /// <summary>A click on the map (or a reset) changed the selection: show the same crawler and room here.</summary>
        private void OnSelectionChanged()
        {
            int sector = _selection.SelectedSector;
            if (sector > 0 && sector != Sectors.SelectedSectorId && _context.Zones.TryGetZone(sector, out _)) Sectors.SelectedSectorId = sector;
            if (PanelsVisible)
            {
                Sectors.Refresh(SelectedCrawler);
                Dossier.Refresh(SelectedCrawler);
            }
        }
    }
}
