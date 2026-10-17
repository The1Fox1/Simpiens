using Simpiens.Entities;
using Simpiens.Simulation;
using Simpiens.Simulation.Spatial;
using UnityEngine;
using VContainer;
using VContainer.Unity;
using Simpiens.Diagnostics;
using ZLogger;

namespace Simpiens.Core.Bootstrapping
{
    /// <summary>
    /// Serves as the main controller for the simulation state.
    /// Implements VContainer's IStartable to run after dependencies are resolved.
    /// </summary>
    public class SimulationStarter : IStartable
    {
        private readonly ISimulationManager _simulationManager;
        private readonly IObjectResolver _resolver;
        private readonly IWorldRegistry _registry;
        private readonly SimulationSettings _settings;

        [Inject]
        public SimulationStarter(IObjectResolver resolver, IWorldRegistry registry, ISimulationManager simulationManager, SimulationSettings settings)
        {
            _settings = settings;
            _resolver = resolver;
            _registry = registry;
            _simulationManager = simulationManager;
        }

        public void Start()
        {
            SimLog.Bootstrap.ZLogInformation($"Simulation starting (code-first bootstrap), seed {(_settings != null ? _settings.Seed : SimulationSettings.DefaultSeed)}");

            // The spatial partition is sized from the loaded map (IWorldMap.Load in SwarmSpawner.Respawn), not the camera

            // Attach GridVisualizer for Validation Testing
            var visualizerGo = new GameObject("[GridVisualizer]");
            var visualizer = visualizerGo.AddComponent<GridVisualizer>();
            _resolver.Inject(visualizer);

            CreateDashboard(_resolver);

            // The headless batch controller is added by the test harness (Simpiens.Testing.BatchPlaymodeBootstrap)
        }

        /// <summary>
        /// Builds the Facility Dashboard (UI Toolkit document, presenter, view toggle) and injects it. Called once at
        /// boot; PlayMode scenarios that exercise the UI call it too, because loading the test scene destroys the
        /// boot-time dashboard.
        /// </summary>
        internal static GameObject CreateDashboard(IObjectResolver resolver)
        {
            // Instantiate Facility Dashboard UI (UI Toolkit)
            var dashboardGo = new GameObject("[FacilityDashboard]");
            dashboardGo.SetActive(false); // Inactive until properties are assigned so OnEnable runs properly

            var uiDoc = dashboardGo.AddComponent<UnityEngine.UIElements.UIDocument>();

            var panelSettings = Resources.Load<UnityEngine.UIElements.PanelSettings>("UI/DefaultPanelSettings");
            if (panelSettings == null)
            {
                panelSettings = ScriptableObject.CreateInstance<UnityEngine.UIElements.PanelSettings>();
                panelSettings.scaleMode = UnityEngine.UIElements.PanelScaleMode.ScaleWithScreenSize;
                panelSettings.referenceResolution = new Vector2Int(1920, 1080);
                panelSettings.match = 0.5f;
            }
            uiDoc.panelSettings = panelSettings;

            var visualTree = Resources.Load<UnityEngine.UIElements.VisualTreeAsset>("UI/FacilityDashboard");
            if (visualTree != null)
            {
                uiDoc.visualTreeAsset = visualTree;
            }

            var presenter = dashboardGo.AddComponent<Simpiens.UI.FacilityDashboardPresenter>();
            resolver.Inject(presenter);
            var toggleController = dashboardGo.AddComponent<Simpiens.UI.ViewToggleController>();
            toggleController.Initialize(presenter);

            dashboardGo.SetActive(true); // Activate now with all dependencies ready
            SimLog.Bootstrap.ZLogInformation($"Facility Dashboard initialized (PanelSettings: {uiDoc.panelSettings?.name}, VisualTree: {uiDoc.visualTreeAsset?.name})");

            return dashboardGo;
        }
    }
}
