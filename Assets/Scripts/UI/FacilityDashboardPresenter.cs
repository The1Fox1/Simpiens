using System;
using System.Collections.Generic;
using Simpiens.Cognition;
using Simpiens.Core.Session;
using Simpiens.Entities;
using Simpiens.Intervention;
using Simpiens.Narrative;
using Simpiens.Simulation;
using Simpiens.Simulation.Economy;
using Simpiens.Simulation.World;
using Simpiens.UI.Pooling;
using Simpiens.UI.Zones;
using UnityEngine;
using UnityEngine.UIElements;
using VContainer;
using Simpiens.Diagnostics;
using ZLogger;

namespace Simpiens.UI
{
    /// <summary>
    /// Dumb view / presenter component bridging pure C# simulation state to Unity UI Toolkit.
    /// Strictly manages DOM element updates and player click dispatches without executing simulation logic.
    /// Employs pre-allocated element pools and throttled updates to guarantee zero GC allocations in hot paths.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class FacilityDashboardPresenter : MonoBehaviour
    {
        private IFacilityZoneService _zoneService;
        private IWorldRegistry _worldRegistry;
        private ISimulationClock _simulationClock;
        private ISimulationManager _simulationManager;
        private IPlayerInterventionService _interventionService;
        private INarrativeChronicleService _chronicleService;
        private IAgentIdentityService _identityService;
        private INarrativeFormatter _narrativeFormatter;

        private UIDocument _uiDocument;
        private VisualElement _root;
        private VisualElement _dashboardRoot;

        // UI Header Elements
        private Label _clockTickLabel;
        private Label _clockMultiplierLabel;
        private Button _btnViewToggle;

        // Session switch (Dev Map toggle): only shown when a test session is registered
        private ISessionSwitcher _sessionSwitcher;
        private VisualElement _sessionSwitchOverlay;
        private Label _sessionBanner;
        private Button _btnSessionSwitch;

        // Quick save and load (Milestone 1, D2 = A): one slot, two header buttons and a status line. Like the session
        // switch, these act on the session, not through simulation commands; a save is captured between ticks.
        private ISaveGameService _saveGame;
        private Button _btnQuickSave;
        private Button _btnQuickLoad;
        private Label _saveStatusLabel;

        // Header Economic HUD Metrics
        private Label _treasuryGoldLabel;
        private Label _provisionsStockLabel;
        private Label _salvageStockLabel;
        private Label _bountiesActiveLabel;
        private Button _btnMerchantTerminal;
        private Button _btnBountyBoard;

        // Merchant Terminal Overlay
        private VisualElement _merchantTerminalOverlay;
        private Button _btnCloseMerchant;
        private Label _merchantStallStatus;
        private Label _merchantStallTreasury;
        private Label _merchantStallInventory;
        private SliderInt _sliderRationPrice;
        private Label _labelRationPrice;
        private SliderInt _sliderOrePrice;
        private Label _labelOrePrice;
        private SliderInt _sliderHerbPrice;
        private Label _labelHerbPrice;
        private Label _merchantFeedbackLabel;
        private Button _btnApplyPrices;

        // Bounty Board Overlay
        private VisualElement _bountyBoardOverlay;
        private Button _btnCloseBounties;
        private Label _bountyCountBadge;
        private ScrollView _bountiesScrollList;
        private DropdownField _dropdownBountyType;
        private DropdownField _dropdownBountySector;
        private Label _labelBountySector;

        // The sector picker's choices, rebuilt when the map, the type or the charted sectors change
        private readonly List<int> _bountySectorIds = new List<int>(16);
        private readonly List<string> _bountySectorChoices = new List<string>(16);
        private BountyType _bountySectorType = BountyType.None;
        private ulong _bountySectorMask;
        private int _bountySectorZoneCount = -1;
        private ISectorRegistry _sectorRegistry;

        public const string AllChartedText = "Every sector is charted: there is nothing left to scout.";
        public const string AwaitsChartSuffix = " (claimable once charted)";
        private VisualElement _groupBountyItem;
        private DropdownField _dropdownBountyItem;
        private VisualElement _groupBountyQty;
        private SliderInt _sliderBountyQty;
        private Label _labelBountyQty;
        private SliderInt _sliderBountyReward;
        private Label _labelBountyReward;
        private SliderInt _sliderBountyDuration;
        private Label _labelBountyDuration;
        private Label _bountyFeedbackLabel;
        private Button _btnPostBounty;
        private Label _bountiesEmptyLabel;
        private VisualElementPool<BountyContract, VisualElement> _bountyPool;

        // Column 1: Sectors
        private ScrollView _sectorsScrollList;

        // Column 2: Sector Detail
        private Label _activeSectorName;
        private Label _activeSectorDesc;
        private ScrollView _agentsScrollList;
        private ScrollView _eventsScrollList;

        // Column 3: Dossier
        private VisualElement _dossierEmptyState;
        private VisualElement _dossierDetailsContainer;
        private Label _dossierAgentName;
        private Label _dossierVisualBadge;
        private ProgressBar _barHunger;
        private ProgressBar _barEnergy;
        private ProgressBar _barSocial;
        private ProgressBar _barFrustration;
        private Label _valHunger;
        private Label _valEnergy;
        private Label _valSocial;
        private Label _valFrustration;
        private Label _dossierActiveGoal;
        private Label _dossierActiveAction;
        private ScrollView _dossierAffinitiesList;
        private Button _btnCalmTantrum;

        private class AgentCardItem
        {
            public VisualElement Element;
            public Label NameLabel;
            public Label DescLabel;
            public Label BadgeLabel;
            public EntityId AgentId;
            public string CurrentBadgeClass;
        }

        // State & Pre-allocated Buffers
        private int _selectedZoneId = -1; // Chosen from the loaded map by BuildSectorList
        private EntityId _selectedAgentId = default;
        private Simpiens.Entities.WorldSelection _worldSelection;
        private float _refreshTimer = 0f;
        private const float RefreshInterval = 0.1f; // 10 Hz refresh for zero-allocation performance

        private readonly List<NodeController> _agentsInZoneBuffer = new List<NodeController>(32);
        private readonly List<VisualElement> _sectorCardsPool = new List<VisualElement>(16);
        private readonly List<AgentCardItem> _agentCardItems = new List<AgentCardItem>(32);
        private readonly List<Label> _eventItemsPool = new List<Label>(128);
        private readonly List<Label> _affinityItemsPool = new List<Label>(32);
        private readonly List<ChronicleEvent> _chronicleQueryBuffer = new List<ChronicleEvent>(128);
        private const int MaxSectorEventsToDisplay = 100;

        private Label _agentsEmptyLabel;
        private Label _eventsEmptyLabel;
        private Label _affinitiesEmptyLabel;

        public bool IsVisible
        {
            get => IsShown(_dashboardRoot);
            set
            {
                if (_dashboardRoot != null)
                {
                    _dashboardRoot.style.display = value ? DisplayStyle.Flex : DisplayStyle.None;
                }
                UpdateViewToggleLabel();
            }
        }

        [Inject]
        public void Construct(
            IFacilityZoneService zoneService = null,
            IWorldRegistry worldRegistry = null,
            ISimulationClock simulationClock = null,
            ISimulationManager simulationManager = null,
            IPlayerInterventionService interventionService = null,
            INarrativeChronicleService chronicleService = null,
            IAgentIdentityService identityService = null,
            INarrativeFormatter narrativeFormatter = null,
            ISessionSwitcher sessionSwitcher = null,
            Simpiens.Entities.WorldSelection worldSelection = null,
            ISectorRegistry sectorRegistry = null,
            ISaveGameService saveGame = null)
        {
            _sectorRegistry = sectorRegistry;
            _saveGame = saveGame;
            _zoneService = zoneService;
            _worldRegistry = worldRegistry;
            _simulationClock = simulationClock;
            _simulationManager = simulationManager;
            _interventionService = interventionService;
            _chronicleService = chronicleService ?? simulationManager?.Chronicle;
            _identityService = identityService ?? new AgentIdentityService();
            _narrativeFormatter = narrativeFormatter ?? new TemplateNarrativeFormatter(_identityService, _zoneService);

            if (_sessionSwitcher != null) _sessionSwitcher.SessionChanged -= HandleSessionChanged;
            _sessionSwitcher = sessionSwitcher;
            if (_sessionSwitcher != null) _sessionSwitcher.SessionChanged += HandleSessionChanged;

            if (_worldSelection != null) _worldSelection.Changed -= HandleWorldSelectionChanged;
            _worldSelection = worldSelection;
            if (_worldSelection != null) _worldSelection.Changed += HandleWorldSelectionChanged;
        }

        private void OnDestroy()
        {
            if (_sessionSwitcher != null) _sessionSwitcher.SessionChanged -= HandleSessionChanged;
            if (_worldSelection != null) _worldSelection.Changed -= HandleWorldSelectionChanged;
        }

        /// <summary>A click on the map (or a session reset) changed the selection: show the same crawler and room here.</summary>
        private void HandleWorldSelectionChanged()
        {
            if (_selectedAgentId != _worldSelection.SelectedCrawler)
            {
                _selectedAgentId = _worldSelection.SelectedCrawler;
                RefreshDossierView();
                UpdateAgentCardsSelection();
            }
            int sector = _worldSelection.SelectedSector;
            if (sector > 0 && sector != _selectedZoneId && _zoneService != null && _zoneService.TryGetZone(sector, out _))
            {
                _selectedZoneId = sector;
                if (IsVisible) RefreshAllViews();
            }
        }

        private void Awake()
        {
            _uiDocument = GetComponent<UIDocument>();
            EnsureVisualTreeLoaded();
            BindVisualElements();
        }

        private void Start()
        {
            BuildSectorList();
            RefreshAllViews();
            RefreshSessionSwitch();
            RefreshSaveButtons();
        }

        /// <summary>The session the switch button leads to: the next registered one after the current session.</summary>
        private ISessionDefinition NextSession()
        {
            var sessions = _sessionSwitcher.Sessions;
            int current = 0;
            for (int i = 0; i < sessions.Count; i++)
            {
                if (sessions[i] == _sessionSwitcher.Current) current = i;
            }
            return sessions[(current + 1) % sessions.Count];
        }

        private void HandleQuickSaveClicked()
        {
            if (_saveGame == null) return;
            QuickSaveAsync().Forget();
        }

        private async Cysharp.Threading.Tasks.UniTaskVoid QuickSaveAsync()
        {
            ShowSaveStatus("Saving...");
            var outcome = await _saveGame.QuickSaveAsync();
            ShowSaveStatus(outcome.Message);
        }

        /// <summary>A successful load raises SessionChanged first, so the dashboard has already rebuilt for the loaded game.</summary>
        private void HandleQuickLoadClicked()
        {
            if (_saveGame == null) return;
            ShowSaveStatus(_saveGame.QuickLoad().Message);
        }

        private void ShowSaveStatus(string message)
        {
            if (_saveStatusLabel != null) _saveStatusLabel.text = message ?? string.Empty;
        }

        private void RefreshSaveButtons()
        {
            var display = _saveGame != null ? DisplayStyle.Flex : DisplayStyle.None;
            if (_btnQuickSave != null) _btnQuickSave.style.display = display;
            if (_btnQuickLoad != null) _btnQuickLoad.style.display = display;
            if (_saveStatusLabel != null) _saveStatusLabel.style.display = display;
        }

        private void HandleSessionSwitchClicked()
        {
            if (_sessionSwitcher == null || _sessionSwitcher.Sessions.Count < 2) return;
            var next = NextSession();
            SimLog.Bootstrap.ZLogInformation($"Session switch: {_sessionSwitcher.Current.DisplayName} -> {next.DisplayName}");
            _sessionSwitcher.SwitchTo(next);
        }

        /// <summary>A respawn replaced every entity (ids are reissued): drop selections and rebuild from the new map.</summary>
        private void HandleSessionChanged(ISessionDefinition session)
        {
            _selectedAgentId = default;
            CloseMerchantTerminal();
            CloseBountyBoard();
            BuildSectorList();
            RebuildBountySectorChoices(force: true);
            RefreshAllViews();
            RefreshSessionSwitch();
        }

        private void RefreshSessionSwitch()
        {
            if (_sessionSwitchOverlay == null) return;

            bool switchable = _sessionSwitcher != null && _sessionSwitcher.Sessions.Count > 1;
            _sessionSwitchOverlay.style.display = switchable ? DisplayStyle.Flex : DisplayStyle.None;
            if (!switchable) return;

            if (_btnSessionSwitch != null) _btnSessionSwitch.text = $"SWITCH TO {NextSession().DisplayName.ToUpperInvariant()}";

            var current = _sessionSwitcher.Current;
            bool testSession = current != _sessionSwitcher.Default;
            if (_sessionBanner != null)
            {
                _sessionBanner.style.display = testSession ? DisplayStyle.Flex : DisplayStyle.None;
                _sessionBanner.text = $"{current.DisplayName.ToUpperInvariant()}: TEST SESSION (NOT THE GAME)";
            }
        }

        private void EnsureVisualTreeLoaded()
        {
            if (_uiDocument == null) return;

            if (_uiDocument.panelSettings == null)
            {
                var panelSettings = Resources.Load<PanelSettings>("UI/DefaultPanelSettings");
                if (panelSettings == null)
                {
                    panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
                    panelSettings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
                    panelSettings.referenceResolution = new Vector2Int(1920, 1080);
                    panelSettings.match = 0.5f;
                }
                _uiDocument.panelSettings = panelSettings;
            }

            if (_uiDocument.visualTreeAsset == null)
            {
                var visualTree = Resources.Load<VisualTreeAsset>("UI/FacilityDashboard");
                if (visualTree != null)
                {
                    _uiDocument.visualTreeAsset = visualTree;
                }
            }

            _root = _uiDocument.rootVisualElement;
            if (_root != null)
            {
                // Explicitly clone visual tree into rootVisualElement if not already populated
                if (_root.childCount == 0 && _uiDocument.visualTreeAsset != null)
                {
                    _uiDocument.visualTreeAsset.CloneTree(_root);
                }

                // Ensure root container stretches across the full screen
                _root.style.position = Position.Absolute;
                _root.style.left = 0;
                _root.style.top = 0;
                _root.style.right = 0;
                _root.style.bottom = 0;
                _root.style.width = Length.Percent(100);
                _root.style.height = Length.Percent(100);
                _root.style.flexGrow = 1;

                var styleSheet = Resources.Load<StyleSheet>("UI/FacilityDashboard");
                if (styleSheet != null && !_root.styleSheets.Contains(styleSheet))
                {
                    _root.styleSheets.Add(styleSheet);
                }
            }
        }

        private void BindVisualElements()
        {
            if (_root == null) return;

            _dashboardRoot = _root.Q<VisualElement>("dashboard-root");
            if (_dashboardRoot != null)
            {
                _dashboardRoot.style.display = DisplayStyle.Flex;
            }

            _clockTickLabel = _root.Q<Label>("clock-tick-label");
            _clockMultiplierLabel = _root.Q<Label>("clock-multiplier-label");
            _btnViewToggle = _root.Q<Button>("btn-view-toggle");
            if (_btnViewToggle != null)
            {
                _btnViewToggle.clicked += ToggleVisibility;
                UpdateViewToggleLabel();
            }

            _btnQuickSave = _root.Q<Button>("btn-quick-save");
            if (_btnQuickSave != null) _btnQuickSave.clicked += HandleQuickSaveClicked;
            _btnQuickLoad = _root.Q<Button>("btn-quick-load");
            if (_btnQuickLoad != null) _btnQuickLoad.clicked += HandleQuickLoadClicked;
            _saveStatusLabel = _root.Q<Label>("save-status-label");

            _sessionSwitchOverlay = _root.Q<VisualElement>("session-switch-overlay");
            _sessionBanner = _root.Q<Label>("session-banner");
            _btnSessionSwitch = _root.Q<Button>("btn-session-switch");
            if (_btnSessionSwitch != null)
            {
                _btnSessionSwitch.clicked += HandleSessionSwitchClicked;
            }

            // Economic HUD
            _treasuryGoldLabel = _root.Q<Label>("treasury-gold-label");
            _provisionsStockLabel = _root.Q<Label>("provisions-stock-label");
            _salvageStockLabel = _root.Q<Label>("salvage-stock-label");
            _bountiesActiveLabel = _root.Q<Label>("bounties-active-label");
            _btnMerchantTerminal = _root.Q<Button>("btn-merchant-terminal");
            if (_btnMerchantTerminal != null)
            {
                _btnMerchantTerminal.clicked += ToggleMerchantTerminal;
            }
            _btnBountyBoard = _root.Q<Button>("btn-bounty-board");
            if (_btnBountyBoard != null)
            {
                _btnBountyBoard.clicked += ToggleBountyBoard;
            }

            // Merchant Terminal Overlay
            _merchantTerminalOverlay = _root.Q<VisualElement>("merchant-terminal-overlay");
            _btnCloseMerchant = _root.Q<Button>("btn-close-merchant");
            if (_btnCloseMerchant != null)
            {
                _btnCloseMerchant.clicked += CloseMerchantTerminal;
            }
            _merchantStallStatus = _root.Q<Label>("merchant-stall-status");
            _merchantStallTreasury = _root.Q<Label>("merchant-stall-treasury");
            _merchantStallInventory = _root.Q<Label>("merchant-stall-inventory");
            _sliderRationPrice = _root.Q<SliderInt>("slider-ration-price");
            _labelRationPrice = _root.Q<Label>("label-ration-price");
            if (_sliderRationPrice != null)
            {
                _sliderRationPrice.RegisterValueChangedCallback(evt =>
                {
                    if (_labelRationPrice != null) _labelRationPrice.text = $"{evt.newValue} Gold";
                });
            }
            _sliderOrePrice = _root.Q<SliderInt>("slider-ore-price");
            _labelOrePrice = _root.Q<Label>("label-ore-price");
            if (_sliderOrePrice != null)
            {
                _sliderOrePrice.RegisterValueChangedCallback(evt =>
                {
                    if (_labelOrePrice != null) _labelOrePrice.text = $"{evt.newValue} Gold";
                });
            }
            _sliderHerbPrice = _root.Q<SliderInt>("slider-herb-price");
            _labelHerbPrice = _root.Q<Label>("label-herb-price");
            if (_sliderHerbPrice != null)
            {
                _sliderHerbPrice.RegisterValueChangedCallback(evt =>
                {
                    if (_labelHerbPrice != null) _labelHerbPrice.text = $"{evt.newValue} Gold";
                });
            }
            _merchantFeedbackLabel = _root.Q<Label>("merchant-feedback-label");
            _btnApplyPrices = _root.Q<Button>("btn-apply-prices");
            if (_btnApplyPrices != null)
            {
                _btnApplyPrices.clicked += HandleApplyPrices;
            }

            // Bounty Board Overlay
            _bountyBoardOverlay = _root.Q<VisualElement>("bounty-board-overlay");
            _btnCloseBounties = _root.Q<Button>("btn-close-bounties");
            if (_btnCloseBounties != null)
            {
                _btnCloseBounties.clicked += CloseBountyBoard;
            }
            _bountyCountBadge = _root.Q<Label>("bounty-count-badge");
            _bountiesScrollList = _root.Q<ScrollView>("bounties-scroll-list");
            _dropdownBountyType = _root.Q<DropdownField>("dropdown-bounty-type");
            _dropdownBountySector = _root.Q<DropdownField>("dropdown-bounty-sector");
            _labelBountySector = _root.Q<Label>("label-bounty-sector");
            if (_dropdownBountySector != null)
            {
                _dropdownBountySector.RegisterValueChangedCallback(_ => UpdateBountySectorLabel());
            }
            _groupBountyItem = _root.Q<VisualElement>("group-bounty-item");
            _dropdownBountyItem = _root.Q<DropdownField>("dropdown-bounty-item");
            _groupBountyQty = _root.Q<VisualElement>("group-bounty-qty");
            _sliderBountyQty = _root.Q<SliderInt>("slider-bounty-qty");
            _labelBountyQty = _root.Q<Label>("label-bounty-qty");
            if (_sliderBountyQty != null)
            {
                _sliderBountyQty.RegisterValueChangedCallback(evt =>
                {
                    if (_labelBountyQty != null) _labelBountyQty.text = $"{evt.newValue} Unit{(evt.newValue > 1 ? "s" : "")}";
                });
            }
            _sliderBountyReward = _root.Q<SliderInt>("slider-bounty-reward");
            _labelBountyReward = _root.Q<Label>("label-bounty-reward");
            if (_sliderBountyReward != null)
            {
                _sliderBountyReward.RegisterValueChangedCallback(evt =>
                {
                    if (_labelBountyReward != null) _labelBountyReward.text = $"{evt.newValue} Gold";
                });
            }
            _sliderBountyDuration = _root.Q<SliderInt>("slider-bounty-duration");
            _labelBountyDuration = _root.Q<Label>("label-bounty-duration");
            if (_sliderBountyDuration != null)
            {
                _sliderBountyDuration.RegisterValueChangedCallback(evt =>
                {
                    if (_labelBountyDuration != null) _labelBountyDuration.text = $"{evt.newValue} Ticks";
                });
            }
            if (_dropdownBountyType != null)
            {
                _dropdownBountyType.RegisterValueChangedCallback(evt =>
                {
                    bool isGather = evt.newValue == "GatherResource";
                    if (_groupBountyItem != null) _groupBountyItem.style.display = isGather ? DisplayStyle.Flex : DisplayStyle.None;
                    if (_groupBountyQty != null) _groupBountyQty.style.display = isGather ? DisplayStyle.Flex : DisplayStyle.None;
                    RebuildBountySectorChoices();
                });
            }
            _bountyFeedbackLabel = _root.Q<Label>("bounty-feedback-label");
            _btnPostBounty = _root.Q<Button>("btn-post-bounty");
            if (_btnPostBounty != null)
            {
                _btnPostBounty.clicked += HandlePostBounty;
            }

            if (_bountiesScrollList != null)
            {
                _bountiesEmptyLabel = new Label("(No active bounty contracts on the outpost board)");
                _bountiesEmptyLabel.AddToClassList("empty-state-text");
                _bountiesEmptyLabel.style.display = DisplayStyle.None;
                _bountiesScrollList.Add(_bountiesEmptyLabel);

                _bountyPool = new VisualElementPool<BountyContract, VisualElement>(
                    _bountiesScrollList,
                    CreateBountyCardItem,
                    BindBountyCardItem,
                    16);
            }

            _sectorsScrollList = _root.Q<ScrollView>("sectors-scroll-list");

            _activeSectorName = _root.Q<Label>("active-sector-name");
            _activeSectorDesc = _root.Q<Label>("active-sector-desc");
            _agentsScrollList = _root.Q<ScrollView>("agents-scroll-list");
            _eventsScrollList = _root.Q<ScrollView>("events-scroll-list");

            _dossierEmptyState = _root.Q<VisualElement>("dossier-empty-state");
            _dossierDetailsContainer = _root.Q<VisualElement>("dossier-details-container");
            _dossierAgentName = _root.Q<Label>("dossier-agent-name");
            _dossierVisualBadge = _root.Q<Label>("dossier-visual-badge");

            _barHunger = _root.Q<ProgressBar>("bar-hunger");
            _barEnergy = _root.Q<ProgressBar>("bar-energy");
            _barSocial = _root.Q<ProgressBar>("bar-social");
            _barFrustration = _root.Q<ProgressBar>("bar-frustration");

            _valHunger = _root.Q<Label>("val-hunger");
            _valEnergy = _root.Q<Label>("val-energy");
            _valSocial = _root.Q<Label>("val-social");
            _valFrustration = _root.Q<Label>("val-frustration");

            _dossierActiveGoal = _root.Q<Label>("dossier-active-goal");
            _dossierActiveAction = _root.Q<Label>("dossier-active-action");
            _dossierAffinitiesList = _root.Q<ScrollView>("dossier-affinities-list");

            _btnCalmTantrum = _root.Q<Button>("btn-calm-tantrum");
            if (_btnCalmTantrum != null)
            {
                _btnCalmTantrum.clicked += HandleCalmTantrum;
            }

            if (_agentsScrollList != null)
            {
                _agentsEmptyLabel = new Label("(No agents currently stationed in this sector)");
                _agentsEmptyLabel.AddToClassList("empty-state-text");
                _agentsEmptyLabel.style.display = DisplayStyle.None;
                _agentsScrollList.Add(_agentsEmptyLabel);
            }

            if (_eventsScrollList != null)
            {
                _eventsEmptyLabel = new Label("(No recent incidents recorded in this sector)");
                _eventsEmptyLabel.AddToClassList("empty-state-text");
                _eventsEmptyLabel.style.display = DisplayStyle.None;
                _eventsScrollList.Add(_eventsEmptyLabel);
            }

            if (_dossierAffinitiesList != null)
            {
                _affinitiesEmptyLabel = new Label("(No relational contacts formed yet)");
                _affinitiesEmptyLabel.AddToClassList("empty-state-text");
                _affinitiesEmptyLabel.style.display = DisplayStyle.None;
                _dossierAffinitiesList.Add(_affinitiesEmptyLabel);
            }

            SimLog.Bootstrap.ZLogDebug($"FacilityDashboardPresenter initialized (root {_root != null}, dashboard {_dashboardRoot != null}, children {_root?.childCount}, panel {_root?.panel != null}, sectors {_sectorsScrollList != null})");
        }

        private AgentCardItem CreateAgentCardItem()
        {
            var card = new VisualElement();
            card.AddToClassList("agent-card");

            var leftBox = new VisualElement();
            var nameLabel = new Label();
            nameLabel.AddToClassList("agent-card-name");
            leftBox.Add(nameLabel);

            var descLabel = new Label();
            descLabel.AddToClassList("agent-card-desc");
            leftBox.Add(descLabel);
            card.Add(leftBox);

            var badge = new Label();
            badge.AddToClassList("state-badge");
            card.Add(badge);

            var item = new AgentCardItem
            {
                Element = card,
                NameLabel = nameLabel,
                DescLabel = descLabel,
                BadgeLabel = badge,
                AgentId = default,
                CurrentBadgeClass = null
            };

            card.RegisterCallback<ClickEvent>(_ =>
            {
                if (item.AgentId != default)
                {
                    SelectAgent(item.AgentId);
                }
            });

            return item;
        }

        /// <summary>
        /// Whether an element is displayed. A "display: none" written in the UXML is not an inline style here: until
        /// code sets <c>style.display</c>, it reads as the default (Flex, keyword Null), so the resolved style decides.
        /// </summary>
        private static bool IsShown(VisualElement element)
        {
            if (element == null) return false;
            var display = element.style.display;
            return display.keyword == StyleKeyword.Null ? element.resolvedStyle.display != DisplayStyle.None : display.value != DisplayStyle.None;
        }

        public void ToggleVisibility()
        {
            IsVisible = !IsVisible;
        }

        /// <summary>The Tab button names where it leads.</summary>
        private void UpdateViewToggleLabel()
        {
            if (_btnViewToggle != null) _btnViewToggle.text = IsVisible ? "[TAB] MAP VIEW" : "[TAB] DASHBOARD";
        }

        private void Update()
        {
            if (_root == null) return;

            _refreshTimer += Time.deltaTime;
            if (_refreshTimer >= RefreshInterval)
            {
                _refreshTimer = 0f;
                // The header and the windows live in the HUD layer and show over the map too (Epic MAP Phase 4, D2)
                if (IsVisible) RefreshAllViews();
                else RefreshHud();
            }
        }

        public void SelectZone(int zoneId)
        {
            _selectedZoneId = zoneId;
            _worldSelection?.SelectSector(zoneId);
            RefreshAllViews();
        }

        public void SelectAgent(EntityId agentId)
        {
            _selectedAgentId = agentId;
            _worldSelection?.SelectCrawler(agentId, follow: true); // back on the map, the camera follows the dossier's crawler
            RefreshDossierView();
            UpdateAgentCardsSelection();
        }

        private void UpdateAgentCardsSelection()
        {
            int count = _agentCardItems.Count;
            for (int i = 0; i < count; i++)
            {
                var item = _agentCardItems[i];
                if (item.AgentId == _selectedAgentId)
                {
                    item.Element.AddToClassList("agent-card-selected");
                }
                else
                {
                    item.Element.RemoveFromClassList("agent-card-selected");
                }
            }
        }

        private void BuildSectorList()
        {
            if (_sectorsScrollList == null || _zoneService == null) return;
            _sectorsScrollList.Clear();
            _sectorCardsPool.Clear();

            var zones = _zoneService.GetAllZones();
            int count = zones.Count;
            for (int i = 0; i < count; i++)
            {
                var zone = zones[i];
                var card = new VisualElement();
                card.AddToClassList("sector-card");

                var nameLabel = new Label(zone.Name);
                nameLabel.AddToClassList("sector-name-label");
                card.Add(nameLabel);

                var badge = new Label("0");
                badge.name = $"sector-badge-{zone.ZoneId}";
                badge.AddToClassList("occupancy-badge");
                card.Add(badge);

                int capturedZoneId = zone.ZoneId;
                card.RegisterCallback<ClickEvent>(_ => SelectZone(capturedZoneId));

                _sectorsScrollList.Add(card);
                _sectorCardsPool.Add(card);
            }

            // Keep the selection while it exists on this map; otherwise select the map's first real sector
            if (!_zoneService.TryGetZone(_selectedZoneId, out _))
            {
                _selectedZoneId = 0;
                for (int i = 0; i < count; i++)
                {
                    if (zones[i].ZoneId != 0)
                    {
                        _selectedZoneId = zones[i].ZoneId;
                        break;
                    }
                }
            }
        }

        /// <summary>What shows over the map: the header and any open window.</summary>
        private void RefreshHud()
        {
            UpdateClockMetrics();
            UpdateEconomicHUDMetrics();
            RefreshMerchantTerminalView();
            RefreshBountyBoardView();
        }

        private void RefreshAllViews()
        {
            UpdateClockMetrics();
            UpdateEconomicHUDMetrics();
            UpdateSectorListOccupancy();
            RefreshSectorDetailView();
            RefreshDossierView();
            RefreshMerchantTerminalView();
            RefreshBountyBoardView();
        }

        private void UpdateClockMetrics()
        {
            if (_simulationClock != null)
            {
                if (_clockTickLabel != null)
                {
                    _clockTickLabel.text = $"TICK: {_simulationClock.CurrentTick:D5}";
                }
                if (_clockMultiplierLabel != null)
                {
                    _clockMultiplierLabel.text = $"SPEED: {_simulationClock.TimeMultiplier:F1}x";
                }
            }
        }

        private void UpdateSectorListOccupancy()
        {
            if (_zoneService == null || _worldRegistry == null) return;

            var zones = _zoneService.GetAllZones();
            int count = zones.Count;
            for (int i = 0; i < count && i < _sectorCardsPool.Count; i++)
            {
                var zone = zones[i];
                var card = _sectorCardsPool[i];
                var badge = card.Q<Label>($"sector-badge-{zone.ZoneId}");
                bool isExplored = _zoneService.IsSectorExplored(zone.ZoneId);

                if (isExplored)
                {
                    card.RemoveFromClassList("sector-card-shrouded");
                    if (badge != null)
                    {
                        badge.RemoveFromClassList("shroud-badge");
                        badge.AddToClassList("occupancy-badge");
                        int occ = _zoneService.GetOccupancyCount(zone.ZoneId, _worldRegistry);
                        badge.text = occ.ToString();
                    }
                }
                else
                {
                    card.AddToClassList("sector-card-shrouded");
                    if (badge != null)
                    {
                        badge.RemoveFromClassList("occupancy-badge");
                        badge.AddToClassList("shroud-badge");
                        badge.text = "SHROUDED";
                    }
                }

                if (zone.ZoneId == _selectedZoneId)
                {
                    card.AddToClassList("sector-card-selected");
                }
                else
                {
                    card.RemoveFromClassList("sector-card-selected");
                }
            }
        }

        private void RefreshSectorDetailView()
        {
            if (_zoneService == null) return;

            var zone = _zoneService.GetZoneById(_selectedZoneId);
            if (_activeSectorName != null) _activeSectorName.text = zone.Name.ToUpper();
            if (_activeSectorDesc != null)
            {
                bool isExplored = _zoneService.IsSectorExplored(_selectedZoneId);
                _activeSectorDesc.text = isExplored
                    ? zone.Description
                    : $"{zone.Description} [UNCHARTED: post a Scout bounty to have it mapped]";
            }

            // 1. Populate resident agents in this sector with stable pooled elements
            if (_agentsScrollList != null && _worldRegistry != null)
            {
                _agentsInZoneBuffer.Clear();
                _zoneService.GetAgentsInZone(_selectedZoneId, _worldRegistry, _agentsInZoneBuffer);

                int agentCount = _agentsInZoneBuffer.Count;
                for (int i = 0; i < agentCount; i++)
                {
                    var node = _agentsInZoneBuffer[i];
                    if (node == null) continue;
                    var agent = node.Agent;

                    AgentCardItem item;
                    if (i < _agentCardItems.Count)
                    {
                        item = _agentCardItems[i];
                    }
                    else
                    {
                        item = CreateAgentCardItem();
                        _agentCardItems.Add(item);
                        _agentsScrollList.Add(item.Element);
                    }

                    item.AgentId = node.Id;
                    string friendlyName = _identityService != null ? _identityService.GetFriendlyName(node.Id) : node.name;
                    item.NameLabel.text = $"{friendlyName} [{node.name}]";
                    item.DescLabel.text = GetAgentActivityDescription(agent);

                    var state = agent != null ? agent.VisualState : AgentVisualState.Idle;
                    item.BadgeLabel.text = state.ToString().ToUpper();
                    string newBadgeClass = GetBadgeClassForState(state);
                    if (item.CurrentBadgeClass != newBadgeClass)
                    {
                        if (!string.IsNullOrEmpty(item.CurrentBadgeClass))
                        {
                            item.BadgeLabel.RemoveFromClassList(item.CurrentBadgeClass);
                        }
                        item.BadgeLabel.AddToClassList(newBadgeClass);
                        item.CurrentBadgeClass = newBadgeClass;
                    }

                    if (node.Id == _selectedAgentId)
                    {
                        item.Element.AddToClassList("agent-card-selected");
                    }
                    else
                    {
                        item.Element.RemoveFromClassList("agent-card-selected");
                    }

                    item.Element.style.display = DisplayStyle.Flex;
                }

                for (int i = agentCount; i < _agentCardItems.Count; i++)
                {
                    _agentCardItems[i].Element.style.display = DisplayStyle.None;
                }

                if (_agentsEmptyLabel != null)
                {
                    _agentsEmptyLabel.style.display = agentCount == 0 ? DisplayStyle.Flex : DisplayStyle.None;
                }
            }

            // 2. Populate live sector events from the narrative chronicle service
            if (_eventsScrollList != null)
            {
                PopulateSectorEvents();
            }
        }

        private void PopulateSectorEvents()
        {
            if (_eventsScrollList == null) return;

            _chronicleQueryBuffer.Clear();
            if (_chronicleService != null)
            {
                _chronicleService.GetSectorEvents(_selectedZoneId, _chronicleQueryBuffer, maxCount: MaxSectorEventsToDisplay);
            }

            uint currentTick = _simulationClock != null ? (uint)_simulationClock.CurrentTick : 0;
            int eventCount = _chronicleQueryBuffer.Count;

            for (int i = 0; i < eventCount; i++)
            {
                var evt = _chronicleQueryBuffer[i];
                Label item;
                if (i < _eventItemsPool.Count)
                {
                    item = _eventItemsPool[i];
                }
                else
                {
                    item = new Label();
                    item.AddToClassList("event-item");
                    _eventItemsPool.Add(item);
                    _eventsScrollList.Add(item);
                }

                string timeBadge = RelativeTimeFormatter.Format(evt.SimulationTick, currentTick);
                string narrative = _narrativeFormatter != null
                    ? _narrativeFormatter.FormatEvent(evt)
                    : $"{evt.EventType} (Sector {evt.SectorId})";

                item.text = $"{timeBadge} {narrative}";
                item.style.display = DisplayStyle.Flex;
            }

            for (int i = eventCount; i < _eventItemsPool.Count; i++)
            {
                _eventItemsPool[i].style.display = DisplayStyle.None;
            }

            if (_eventsEmptyLabel != null)
            {
                _eventsEmptyLabel.style.display = eventCount == 0 ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        private void RefreshDossierView()
        {
            if (_worldRegistry == null) return;

            NodeController node = null;
            if (_selectedAgentId != default)
            {
                node = _worldRegistry.GetNode(_selectedAgentId);
            }

            if (node == null || node.Agent == null)
            {
                if (_dossierEmptyState != null) _dossierEmptyState.style.display = DisplayStyle.Flex;
                if (_dossierDetailsContainer != null) _dossierDetailsContainer.style.display = DisplayStyle.None;
                return;
            }

            if (_dossierEmptyState != null) _dossierEmptyState.style.display = DisplayStyle.None;
            if (_dossierDetailsContainer != null) _dossierDetailsContainer.style.display = DisplayStyle.Flex;

            var agent = node.Agent;

            if (_dossierAgentName != null)
            {
                string friendlyName = _identityService != null ? _identityService.GetFriendlyName(node.Id) : node.name;
                string title = _identityService != null ? _identityService.GetFriendlyTitle(node.Id) : "";
                _dossierAgentName.text = string.IsNullOrEmpty(title)
                    ? $"{friendlyName} [{node.name}]"
                    : $"{friendlyName} the {title} [{node.name}]";
            }

            var state = agent.VisualState;
            if (_dossierVisualBadge != null)
            {
                _dossierVisualBadge.text = state.ToString().ToUpper();
                _dossierVisualBadge.ClearClassList();
                _dossierVisualBadge.AddToClassList("state-badge");
                _dossierVisualBadge.AddToClassList(GetBadgeClassForState(state));
            }

            // Biological drives
            if (_barHunger != null) _barHunger.value = agent.Hunger;
            if (_valHunger != null) _valHunger.text = $"{agent.Hunger:F0}%";

            if (_barEnergy != null) _barEnergy.value = agent.Energy;
            if (_valEnergy != null) _valEnergy.text = $"{agent.Energy:F0}%";

            if (_barSocial != null) _barSocial.value = agent.Social;
            if (_valSocial != null) _valSocial.text = $"{agent.Social:F0}%";

            if (_barFrustration != null) _barFrustration.value = agent.Frustration;
            if (_valFrustration != null) _valFrustration.text = $"{agent.Frustration:F0}%";

            // Cognitive intent
            if (_dossierActiveGoal != null)
            {
                _dossierActiveGoal.text = agent.ActiveGoal != null ? agent.ActiveGoal.GetType().Name : "(None)";
            }
            if (_dossierActiveAction != null)
            {
                _dossierActiveAction.text = (agent.HasActivePlan && agent.ActivePlan.CurrentAction != null)
                    ? agent.ActivePlan.CurrentAction.GetType().Name
                    : "(Idle)";
            }

            // Social Affinities with stable pooled elements
            if (_dossierAffinitiesList != null)
            {
                int affinityCount = 0;
                if (agent.Memory?.AffinityMap != null && agent.Memory.AffinityMap.Count > 0)
                {
                    foreach (var kvp in agent.Memory.AffinityMap)
                    {
                        var peerNode = _worldRegistry.GetNode(kvp.Key);
                        string peerName = peerNode != null ? peerNode.name : kvp.Key.ToString().Substring(0, 6);
                        var rel = kvp.Value;

                        Label row;
                        if (affinityCount < _affinityItemsPool.Count)
                        {
                            row = _affinityItemsPool[affinityCount];
                        }
                        else
                        {
                            row = new Label();
                            row.AddToClassList("affinity-row");
                            _affinityItemsPool.Add(row);
                            _dossierAffinitiesList.Add(row);
                        }

                        row.text = $"{peerName}: W:{rel.Warmth:+0;-#;0} | T:{rel.Trust:+0;-#;0} | F:{rel.Fear:+0;-#;0} | R:{rel.Respect:+0;-#;0} ({rel.InteractionCount}x)";
                        row.style.display = DisplayStyle.Flex;
                        affinityCount++;
                    }
                }

                for (int i = affinityCount; i < _affinityItemsPool.Count; i++)
                {
                    _affinityItemsPool[i].style.display = DisplayStyle.None;
                }

                if (_affinitiesEmptyLabel != null)
                {
                    _affinitiesEmptyLabel.style.display = affinityCount == 0 ? DisplayStyle.Flex : DisplayStyle.None;
                }
            }
        }

        private string GetAgentActivityDescription(AutonomousAgent agent)
        {
            if (agent == null) return "Unknown";
            if (agent.IsInTantrum) return "Throwing tantrum in anger";
            if (agent.IsStarvationPanicking) return "Panicking from severe hunger!";
            if (agent.IsExhaustionCollapsed) return "Collapsed from exhaustion";
            if (agent.IsYielding) return "Yielding to dominant peer";
            if (agent.IsSharing) return "Sharing food rations";
            if (agent.IsGossiping) return "Gossiping with neighbor";
            if (agent.HasActivePlan && agent.ActivePlan.CurrentAction != null)
            {
                return agent.ActivePlan.CurrentAction.GetType().Name.Replace("Action", "");
            }
            return "Loitering / Idle";
        }

        private string GetBadgeClassForState(AgentVisualState state)
        {
            return state switch
            {
                AgentVisualState.Walking => "badge-walking",
                AgentVisualState.Harvesting => "badge-harvesting",
                AgentVisualState.Resting => "badge-resting",
                AgentVisualState.Sleeping => "badge-resting",
                AgentVisualState.Gossiping => "badge-gossiping",
                AgentVisualState.Sharing => "badge-sharing",
                AgentVisualState.Panicking => "badge-panicking",
                AgentVisualState.Yielding => "badge-yielding",
                _ => "badge-idle"
            };
        }

        private void HandleCalmTantrum()
        {
            if (_selectedAgentId == default || _worldRegistry == null) return;
            var node = _worldRegistry.GetNode(_selectedAgentId);
            if (node?.Agent != null)
            {
                node.Agent.Frustration = 0f;
                node.Agent.RelieveFrustration(100f);
                node.Agent.TriggerReliefVisual();
                RefreshDossierView();
                SimLog.Intervention.ZLogInformation($"Player calmed {SimLog.Agent(node.Id)} (frustration -> 0)");
            }
        }

        #region Economic HUD, Merchant Terminal & Bounty Board

        private void UpdateEconomicHUDMetrics()
        {
            if (_simulationManager != null && _simulationManager.ActiveStalls != null && _simulationManager.ActiveStalls.Count > 0)
            {
                var stall = _simulationManager.ActiveStalls[0];
                if (_treasuryGoldLabel != null) _treasuryGoldLabel.text = $"💰 {stall.TreasuryGold} GOLD";
                if (_provisionsStockLabel != null) _provisionsStockLabel.text = $"🍞 {stall.RationStock} RATIONS";
                if (_salvageStockLabel != null) _salvageStockLabel.text = $"⛏️ {stall.OreStock} ORE | 🌿 {stall.HerbStock} HERB";
            }
            else
            {
                if (_treasuryGoldLabel != null) _treasuryGoldLabel.text = "💰 0 GOLD";
                if (_provisionsStockLabel != null) _provisionsStockLabel.text = "🍞 0 RATIONS";
                if (_salvageStockLabel != null) _salvageStockLabel.text = "⛏️ 0 ORE | 🌿 0 HERB";
            }

            int activeBounties = (_simulationManager != null && _simulationManager.ActiveBounties != null) ? _simulationManager.ActiveBounties.Count : 0;
            if (_bountiesActiveLabel != null)
            {
                _bountiesActiveLabel.text = $"📜 {activeBounties} BOUNTIES";
            }
            if (_bountyCountBadge != null)
            {
                _bountyCountBadge.text = $"{activeBounties} ACTIVE";
            }
        }

        public void ToggleMerchantTerminal()
        {
            if (_merchantTerminalOverlay == null) return;
            bool isOpen = IsShown(_merchantTerminalOverlay);
            if (isOpen) CloseMerchantTerminal();
            else OpenMerchantTerminal();
        }

        public void OpenMerchantTerminal()
        {
            if (_merchantTerminalOverlay != null)
            {
                _merchantTerminalOverlay.style.display = DisplayStyle.Flex;
            }
            if (_bountyBoardOverlay != null)
            {
                _bountyBoardOverlay.style.display = DisplayStyle.None;
            }

            if (_simulationManager != null && _simulationManager.ActiveStalls != null && _simulationManager.ActiveStalls.Count > 0)
            {
                var stall = _simulationManager.ActiveStalls[0];
                if (_sliderRationPrice != null) _sliderRationPrice.value = stall.RationSellPrice;
                if (_sliderOrePrice != null) _sliderOrePrice.value = stall.OreBuyPrice;
                if (_sliderHerbPrice != null) _sliderHerbPrice.value = stall.HerbBuyPrice;
                if (_labelRationPrice != null) _labelRationPrice.text = $"{stall.RationSellPrice} Gold";
                if (_labelOrePrice != null) _labelOrePrice.text = $"{stall.OreBuyPrice} Gold";
                if (_labelHerbPrice != null) _labelHerbPrice.text = $"{stall.HerbBuyPrice} Gold";
            }

            RefreshMerchantTerminalView();
        }

        public void CloseMerchantTerminal()
        {
            if (_merchantTerminalOverlay != null)
            {
                _merchantTerminalOverlay.style.display = DisplayStyle.None;
            }
        }

        public void ToggleBountyBoard()
        {
            if (_bountyBoardOverlay == null) return;
            bool isOpen = IsShown(_bountyBoardOverlay);
            if (isOpen) CloseBountyBoard();
            else OpenBountyBoard();
        }

        public void OpenBountyBoard()
        {
            if (_bountyBoardOverlay != null)
            {
                _bountyBoardOverlay.style.display = DisplayStyle.Flex;
            }
            if (_merchantTerminalOverlay != null)
            {
                _merchantTerminalOverlay.style.display = DisplayStyle.None;
            }
            RebuildBountySectorChoices(force: true);
            RefreshBountyBoardView();
        }

        public void CloseBountyBoard()
        {
            if (_bountyBoardOverlay != null)
            {
                _bountyBoardOverlay.style.display = DisplayStyle.None;
            }
        }

        private void RefreshMerchantTerminalView()
        {
            if (!IsShown(_merchantTerminalOverlay))
                return;

            if (_simulationManager != null && _simulationManager.ActiveStalls != null && _simulationManager.ActiveStalls.Count > 0)
            {
                var stall = _simulationManager.ActiveStalls[0];
                if (_merchantStallStatus != null) _merchantStallStatus.text = "Outpost Stall @ Central Corridor";
                if (_merchantStallTreasury != null) _merchantStallTreasury.text = stall.InArrears
                    ? $"Treasury: {stall.TreasuryGold} Gold (in arrears: not buying salvage)"
                    : $"Treasury: {stall.TreasuryGold} Gold";
                if (_merchantStallInventory != null) _merchantStallInventory.text = $"Stock: {stall.RationStock} Rations | {stall.OreStock} Ore | {stall.HerbStock} Herb";
            }
            else
            {
                if (_merchantStallStatus != null) _merchantStallStatus.text = "No active stall established";
                if (_merchantStallTreasury != null) _merchantStallTreasury.text = "Treasury: 0 Gold";
                if (_merchantStallInventory != null) _merchantStallInventory.text = "Stock: 0 Rations | 0 Ore | 0 Herb";
            }
        }

        private void RefreshBountyBoardView()
        {
            if (!IsShown(_bountyBoardOverlay))
                return;

            RebuildBountySectorChoices(); // a scout's report may have charted a sector while the board is open

            var activeBounties = _simulationManager != null ? _simulationManager.ActiveBounties : null;
            int count = activeBounties != null ? activeBounties.Count : 0;

            if (_bountiesEmptyLabel != null)
            {
                _bountiesEmptyLabel.style.display = count == 0 ? DisplayStyle.Flex : DisplayStyle.None;
            }

            _bountyPool?.Sync(activeBounties);
        }

        private VisualElement CreateBountyCardItem()
        {
            var card = new VisualElement();
            card.AddToClassList("bounty-card");

            var header = new VisualElement();
            header.AddToClassList("bounty-card-header");
            var idLabel = new Label();
            idLabel.name = "bounty-card-id";
            idLabel.AddToClassList("bounty-card-id");
            var badge = new Label();
            badge.name = "bounty-card-badge";
            badge.AddToClassList("bounty-badge");
            header.Add(idLabel);
            header.Add(badge);
            card.Add(header);

            var details = new Label();
            details.name = "bounty-card-details";
            details.AddToClassList("bounty-card-details");
            card.Add(details);

            var footer = new VisualElement();
            footer.AddToClassList("bounty-card-footer");
            var reward = new Label();
            reward.name = "bounty-card-reward";
            reward.AddToClassList("bounty-card-reward");
            var cancelBtn = new Button();
            cancelBtn.name = "bounty-btn-cancel";
            cancelBtn.text = "✕ Cancel Contract";
            cancelBtn.AddToClassList("bounty-btn-cancel");
            footer.Add(reward);
            footer.Add(cancelBtn);
            card.Add(footer);

            cancelBtn.clicked += () =>
            {
                if (cancelBtn.userData is ushort capturedId)
                {
                    HandleCancelBounty(capturedId);
                }
            };

            return card;
        }

        private void BindBountyCardItem(VisualElement card, BountyContract bounty, int index)
        {
            var idLabel = card.Q<Label>("bounty-card-id");
            var badge = card.Q<Label>("bounty-card-badge");
            var details = card.Q<Label>("bounty-card-details");
            var reward = card.Q<Label>("bounty-card-reward");
            var cancelBtn = card.Q<Button>("bounty-btn-cancel");

            if (idLabel != null)
            {
                idLabel.text = $"#{bounty.BountyId} - {FormatBountyType(bounty.Type)}";
            }

            if (badge != null)
            {
                badge.text = bounty.Status.ToString().ToUpperInvariant();
                badge.RemoveFromClassList("bounty-badge-open");
                badge.RemoveFromClassList("bounty-badge-claimed");
                badge.RemoveFromClassList("bounty-badge-cancelled");
                badge.AddToClassList(bounty.Status switch
                {
                    BountyStatus.Open => "bounty-badge-open",
                    BountyStatus.Claimed => "bounty-badge-claimed",
                    _ => "bounty-badge-cancelled"
                });
            }

            if (details != null)
            {
                string sectorName = (_zoneService != null && _zoneService.TryGetZone(bounty.TargetSectorId, out var zone))
                    ? zone.Name
                    : $"Sector {bounty.TargetSectorId}";

                string taskDesc = bounty.Type == BountyType.GatherResource
                    ? $"{bounty.TargetQuantity}x {bounty.TargetItemType} from {sectorName}"
                    : $"Explore & Scout {sectorName}";

                details.text = $"{taskDesc} | Expires: {bounty.ExpirationTick} ticks";
            }

            if (reward != null)
            {
                reward.text = $"💰 {bounty.RewardGold} Gold";
            }

            if (cancelBtn != null)
            {
                cancelBtn.userData = bounty.BountyId;
                bool canCancel = bounty.Status == BountyStatus.Open || bounty.Status == BountyStatus.Claimed;
                cancelBtn.SetEnabled(canCancel);
                cancelBtn.style.display = canCancel ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        private static string FormatBountyType(BountyType type)
        {
            return type switch
            {
                BountyType.GatherResource => "Gather Salvage",
                BountyType.ScoutSector => "Scout Sector",
                _ => "Directive"
            };
        }

        public void HandleApplyPrices()
        {
            ushort rationPrice = (ushort)(_sliderRationPrice != null ? _sliderRationPrice.value : 15);
            ushort orePrice = (ushort)(_sliderOrePrice != null ? _sliderOrePrice.value : 20);
            ushort herbPrice = (ushort)(_sliderHerbPrice != null ? _sliderHerbPrice.value : 25);

            string stallId = "";
            if (_simulationManager != null && _simulationManager.ActiveStalls != null && _simulationManager.ActiveStalls.Count > 0)
            {
                stallId = _simulationManager.ActiveStalls[0].StallEntityId.ToString();
            }

            EntityId parsedStallId = default;
            if (!string.IsNullOrEmpty(stallId))
            {
                EntityId.TryParse(stallId, out parsedStallId);
            }
            else if (_simulationManager != null && _simulationManager.ActiveStalls != null && _simulationManager.ActiveStalls.Count > 0)
            {
                parsedStallId = _simulationManager.ActiveStalls[0].StallEntityId;
            }

            if (_interventionService != null)
            {
                var priceCmd = new UpdateStallPricesCommand(parsedStallId, (ushort)rationPrice, (ushort)orePrice, (ushort)herbPrice);
                _interventionService.QueueInfluence(InfluenceEvent.CreateUpdateStallPrices("Player", priceCmd));
                if (_merchantFeedbackLabel != null)
                {
                    _merchantFeedbackLabel.text = $"Rates updated: Ration {rationPrice}g | Ore {orePrice}g | Herb {herbPrice}g";
                    _merchantFeedbackLabel.style.color = new StyleColor(new Color(0.1f, 0.8f, 0.4f));
                }
                SimLog.Intervention.ZLogInformation($"Player queued UpdateStallPrices: ration {rationPrice}g, ore {orePrice}g, herb {herbPrice}g");
            }
            else
            {
                if (_merchantFeedbackLabel != null)
                {
                    _merchantFeedbackLabel.text = "No intervention queue available.";
                    _merchantFeedbackLabel.style.color = new StyleColor(new Color(0.95f, 0.4f, 0.4f));
                }
            }
        }

        private void ShowBountyFeedback(string text, bool ok)
        {
            if (_bountyFeedbackLabel == null) return;
            _bountyFeedbackLabel.text = text;
            _bountyFeedbackLabel.style.color = new StyleColor(ok ? new Color(0.1f, 0.8f, 0.4f) : new Color(0.95f, 0.3f, 0.3f));
        }

        private BountyType SelectedBountyType()
        {
            return _dropdownBountyType != null && System.Enum.TryParse<BountyType>(_dropdownBountyType.value, out var parsed) && parsed != BountyType.None
                ? parsed
                : BountyType.GatherResource;
        }

        /// <summary>The ids behind the sector picker's choices (in order), for the current bounty type.</summary>
        public IReadOnlyList<int> BountySectorChoiceIds
        {
            get
            {
                RebuildBountySectorChoices();
                return _bountySectorIds;
            }
        }

        /// <summary>Selects a sector in the picker; false when the current type doesn't offer it.</summary>
        public bool SelectBountySector(int sectorId)
        {
            RebuildBountySectorChoices();
            int index = _bountySectorIds.IndexOf(sectorId);
            if (index < 0 || _dropdownBountySector == null) return false;
            _dropdownBountySector.index = index;
            return true;
        }

        /// <summary>
        /// Fills the sector picker from the loaded map. Scout offers only uncharted sectors; Gather offers every sector,
        /// marking uncharted ones as claimable once charted. Rebuilt only when the map, the bounty type or the charted
        /// sectors change, keeping the current choice when it is still offered.
        /// </summary>
        private void RebuildBountySectorChoices(bool force = false)
        {
            if (_dropdownBountySector == null || _zoneService == null) return;

            var type = SelectedBountyType();
            var zones = _zoneService.GetAllZones();
            ulong mask = 0UL;
            for (int i = 0; i < zones.Count; i++)
            {
                if (zones[i].IsExplored && zones[i].ZoneId < 64) mask |= 1UL << zones[i].ZoneId;
            }
            if (!force && type == _bountySectorType && mask == _bountySectorMask && zones.Count == _bountySectorZoneCount) return;
            _bountySectorType = type;
            _bountySectorMask = mask;
            _bountySectorZoneCount = zones.Count;

            int previous = _dropdownBountySector.index >= 0 && _dropdownBountySector.index < _bountySectorIds.Count ? _bountySectorIds[_dropdownBountySector.index] : -1;
            _bountySectorIds.Clear();
            _bountySectorChoices.Clear();
            for (int i = 0; i < zones.Count; i++)
            {
                var zone = zones[i];
                if (type == BountyType.ScoutSector && zone.IsExplored) continue;
                _bountySectorIds.Add(zone.ZoneId);
                _bountySectorChoices.Add(zone.IsExplored || type == BountyType.ScoutSector ? zone.Name : zone.Name + AwaitsChartSuffix);
            }

            _dropdownBountySector.choices = new List<string>(_bountySectorChoices);
            int keep = previous >= 0 ? _bountySectorIds.IndexOf(previous) : -1;
            if (_bountySectorIds.Count == 0) _dropdownBountySector.SetValueWithoutNotify(string.Empty);
            else _dropdownBountySector.index = keep >= 0 ? keep : 0;

            bool canPost = _bountySectorIds.Count > 0;
            _btnPostBounty?.SetEnabled(canPost);
            if (!canPost && type == BountyType.ScoutSector) ShowBountyFeedback(AllChartedText, false);
            UpdateBountySectorLabel();
        }

        private void UpdateBountySectorLabel()
        {
            if (_labelBountySector == null || _dropdownBountySector == null) return;
            int i = _dropdownBountySector.index;
            if (i < 0 || i >= _bountySectorIds.Count || _zoneService == null || !_zoneService.TryGetZone(_bountySectorIds[i], out var zone))
            {
                _labelBountySector.text = string.Empty;
                return;
            }
            _labelBountySector.text = zone.IsExplored ? "Charted" : "Uncharted";
        }

        public void HandlePostBounty()
        {
            var type = BountyType.GatherResource;
            if (_dropdownBountyType != null && System.Enum.TryParse<BountyType>(_dropdownBountyType.value, out var parsedType))
            {
                type = parsedType;
            }

            RebuildBountySectorChoices();
            int choice = _dropdownBountySector != null ? _dropdownBountySector.index : -1;
            if (choice < 0 || choice >= _bountySectorIds.Count)
            {
                ShowBountyFeedback(type == BountyType.ScoutSector && _bountySectorIds.Count == 0 ? AllChartedText : "Pick a target sector.", false);
                return;
            }
            byte sectorId = (byte)_bountySectorIds[choice];

            // The simulation enforces the same rule; checking here gives the player the reason
            var target = BountyRules.CheckTarget(type, sectorId, _sectorRegistry);
            if (target != BountyRules.TargetCheck.Ok)
            {
                ShowBountyFeedback(BountyRules.Describe(target, sectorId, _sectorRegistry), false);
                return;
            }

            var itemType = ItemType.IronOre;
            if (_dropdownBountyItem != null && System.Enum.TryParse<ItemType>(_dropdownBountyItem.value, out var parsedItem))
            {
                itemType = parsedItem;
            }
            if (type == BountyType.ScoutSector)
            {
                itemType = ItemType.None;
            }

            byte qty = (byte)(_sliderBountyQty != null ? _sliderBountyQty.value : 1);
            ushort rewardGold = (ushort)(_sliderBountyReward != null ? _sliderBountyReward.value : 25);
            uint durationTicks = (uint)(_sliderBountyDuration != null ? _sliderBountyDuration.value : 1000);

            // Validate player / stall treasury gold
            EntityId creatorStallId = default;
            if (_simulationManager != null && _simulationManager.ActiveStalls != null && _simulationManager.ActiveStalls.Count > 0)
            {
                var stall = _simulationManager.ActiveStalls[0];
                creatorStallId = stall.StallEntityId;
                if (stall.TreasuryGold < rewardGold)
                {
                    if (_bountyFeedbackLabel != null)
                    {
                        _bountyFeedbackLabel.text = $"Insufficient treasury gold ({stall.TreasuryGold}g available, need {rewardGold}g).";
                        _bountyFeedbackLabel.style.color = new StyleColor(new Color(0.95f, 0.3f, 0.3f));
                    }
                    return;
                }
            }

            if (_interventionService != null)
            {
                var bountyCmd = new PostBountyCommand(0, type, sectorId, itemType, qty, rewardGold, durationTicks, Vector2.zero, creatorStallId);
                _interventionService.QueueInfluence(InfluenceEvent.CreatePostBounty("Player", bountyCmd));
                if (_bountyFeedbackLabel != null)
                {
                    _bountyFeedbackLabel.text = $"Posted {FormatBountyType(type)} contract for {rewardGold}g reward.";
                    _bountyFeedbackLabel.style.color = new StyleColor(new Color(0.1f, 0.8f, 0.4f));
                }
                SimLog.Intervention.ZLogInformation($"Player queued PostBounty: {type} ({itemType} x{qty}, {rewardGold}g)");
            }
            else
            {
                if (_bountyFeedbackLabel != null)
                {
                    _bountyFeedbackLabel.text = "No intervention queue available.";
                    _bountyFeedbackLabel.style.color = new StyleColor(new Color(0.95f, 0.4f, 0.4f));
                }
            }
        }

        public void HandleCancelBounty(ushort bountyId)
        {
            if (_interventionService != null)
            {
                _interventionService.QueueInfluence(InfluenceEvent.CreateCancelBounty("Player", bountyId));
                if (_bountyFeedbackLabel != null)
                {
                    _bountyFeedbackLabel.text = $"Dispatched cancellation for contract #{bountyId}.";
                    _bountyFeedbackLabel.style.color = new StyleColor(new Color(0.95f, 0.8f, 0.2f));
                }
                SimLog.Intervention.ZLogInformation($"Player queued CancelBounty #{bountyId}");
            }
        }

        #endregion
    }
}
