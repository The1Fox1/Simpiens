using Simpiens.Entities;
using Simpiens.Simulation;
using Simpiens.Simulation.Spatial;
using Simpiens.Testing;
using Simpiens.Testing.Environment;
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
        private readonly NodeFactory _nodeFactory;
        private readonly ISpatialPartition _spatialPartition;
        private readonly ISimulationManager _simulationManager;
        private readonly IObjectResolver _resolver;
        private readonly IWorldRegistry _registry;

        [Inject]
        public SimulationStarter(NodeFactory nodeFactory, ISpatialPartition spatialPartition, IObjectResolver resolver, IWorldRegistry registry, ISimulationManager simulationManager)
        {
            _nodeFactory = nodeFactory;
            _spatialPartition = spatialPartition;
            _resolver = resolver;
            _registry = registry;
            _simulationManager = simulationManager;
        }

        public void Start()
        {
            SimLog.Bootstrap.ZLogInformation($"Simulation starting (code-first bootstrap)");

            // Initialize Spatial Partition
            Camera cam = Camera.main;
            float worldHeight = 36f; // Generous fallback to contain 36x28 outpost arena
            float worldWidth = 44f;  // Generous fallback to contain 36x28 outpost arena
            if (cam != null && cam.orthographic)
            {
                // Ensure spatial partition comfortably covers the arena even with camera offsets
                worldHeight = Mathf.Max(36f, (Mathf.Abs(cam.transform.position.y) + cam.orthographicSize) * 2f + 4f);
                worldWidth = Mathf.Max(44f, (Mathf.Abs(cam.transform.position.x) + cam.orthographicSize * cam.aspect) * 2f + 4f);
            }

            // Initialize with the calculated bounds and a reasonable cell size
            _spatialPartition.Initialize(worldWidth, worldHeight, 1.0f);

            // Attach GridVisualizer for Validation Testing
            var visualizerGo = new GameObject("[GridVisualizer]");
            var visualizer = visualizerGo.AddComponent<GridVisualizer>();
            _resolver.Inject(visualizer);

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
            _resolver.Inject(presenter);
            var toggleController = dashboardGo.AddComponent<Simpiens.UI.ViewToggleController>();
            toggleController.Initialize(presenter);

            dashboardGo.SetActive(true); // Activate now with all dependencies ready
            SimLog.Bootstrap.ZLogInformation($"Facility Dashboard initialized (PanelSettings: {uiDoc.panelSettings?.name}, VisualTree: {uiDoc.visualTreeAsset?.name})");

            bool isRunningTests = false;
            string[] args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i].Equals("-runTests", System.StringComparison.OrdinalIgnoreCase))
                {
                    isRunningTests = true;
                    break;
                }
            }

            if (Application.isBatchMode && !isRunningTests)
            {
                var batchGo = new GameObject("[BatchPlaymodeController]");
                batchGo.AddComponent<BatchPlaymodeController>();
            }
        }
    }
}
