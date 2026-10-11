using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using VContainer;

namespace Simpiens.Core.Editor
{
    [InitializeOnLoad]
    public static class SetupFacilityUIAsset
    {
        static SetupFacilityUIAsset()
        {
            EditorApplication.delayCall += VerifyAndSetupPanelSettings;
            EditorApplication.delayCall += RunOutpostMapBuilderValidator;
        }

        [MenuItem("Simpiens/Map/Run Outpost Map Builder Validator")]
        public static void RunOutpostMapBuilderValidator()
        {
            var validator = new Simpiens.Testing.Environment.OutpostMapBuilderValidator();
            validator.RunAllValidationTests();

            var zoneValidator = new Simpiens.Testing.UI.FacilityZoneServiceValidator();
            zoneValidator.RunAllValidationTests();

            var bountyValidator = new Simpiens.Testing.UI.BountyTerminalValidator();
            bountyValidator.RunAllValidationTests();

            var tradeValidator = new Simpiens.Testing.Economy.TradeTransactionValidator();
            tradeValidator.Start();
        }

        [MenuItem("Simpiens/Narrative/Test Container Resolution")]
        public static void TestContainerResolution()
        {
            var builder = new VContainer.ContainerBuilder();
            builder.Register<Simpiens.Simulation.WorldRegistry>(VContainer.Lifetime.Singleton).AsImplementedInterfaces();
            builder.Register<Simpiens.Simulation.Spatial.SpatialHashGrid>(VContainer.Lifetime.Singleton).AsImplementedInterfaces();
            builder.Register<Simpiens.Simulation.World.SectorRegistry>(VContainer.Lifetime.Singleton).AsImplementedInterfaces();
            builder.Register<Simpiens.Simulation.World.WorldMap>(VContainer.Lifetime.Singleton).AsImplementedInterfaces();
            builder.Register<Simpiens.Cognition.Pathfinding.AsyncPathfinder>(VContainer.Lifetime.Singleton).AsImplementedInterfaces().AsSelf();
            builder.Register<Simpiens.Cognition.Planning.GoapPlanner>(VContainer.Lifetime.Singleton).AsImplementedInterfaces();
            builder.Register<Simpiens.Simulation.SimulationClock>(VContainer.Lifetime.Singleton).AsImplementedInterfaces();
            builder.Register<Simpiens.UI.Zones.FacilityZoneService>(VContainer.Lifetime.Singleton).AsImplementedInterfaces().AsSelf();
            builder.Register<Simpiens.Narrative.AgentIdentityService>(VContainer.Lifetime.Singleton).AsImplementedInterfaces().AsSelf();
            builder.Register<Simpiens.Narrative.NarrativeChronicleService>(VContainer.Lifetime.Singleton).AsImplementedInterfaces().AsSelf();
            builder.Register<Simpiens.Narrative.TemplateNarrativeFormatter>(VContainer.Lifetime.Singleton).AsImplementedInterfaces().AsSelf();
            builder.Register<Simpiens.Narrative.PassthroughNarrativeEnricher>(VContainer.Lifetime.Singleton).AsImplementedInterfaces().AsSelf();
            builder.Register<Simpiens.Simulation.GroupRegistry>(VContainer.Lifetime.Singleton).AsImplementedInterfaces().AsSelf();
            builder.Register<Simpiens.Simulation.SimulationManager>(VContainer.Lifetime.Singleton).AsImplementedInterfaces();
            builder.Register<Simpiens.Core.Session.SwarmSpawner>(VContainer.Lifetime.Singleton).AsImplementedInterfaces().AsSelf();

            var container = builder.Build();
            var spawner = container.Resolve<Simpiens.Core.Session.SwarmSpawner>();
            var simMgr = container.Resolve<Simpiens.Simulation.ISimulationManager>();
            var chronicle = container.Resolve<Simpiens.Narrative.INarrativeChronicleService>();
            Debug.Log($"<color=#10b981>[TestContainerResolution] SUCCESS! Spawner: {spawner != null}, SimMgr: {simMgr != null}, Chronicle: {chronicle != null}</color>");
        }

        [MenuItem("Simpiens/Narrative/Run Narrative Chronicle Validator")]
        public static void RunNarrativeValidator()
        {
            var validator = new Simpiens.Testing.UI.NarrativeChronicleValidator();
            validator.RunAllValidationTests();
        }

        [MenuItem("Simpiens/UI Toolkit/Run Bounty Terminal Validator")]
        public static void RunBountyTerminalValidator()
        {
            var validator = new Simpiens.Testing.UI.BountyTerminalValidator();
            validator.RunAllValidationTests();
        }

        [MenuItem("Simpiens/UI Toolkit/Verify UI Toolkit Setup")]
        public static void VerifyAndSetupPanelSettings()
        {
            const string themePath = "Assets/Resources/UI/UnityDefaultRuntimeTheme.tss";
            const string panelSettingsPath = "Assets/Resources/UI/DefaultPanelSettings.asset";

            // 1. Ensure theme file exists
            if (!File.Exists(themePath))
            {
                var dir = Path.GetDirectoryName(themePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
                File.WriteAllText(themePath, "@import url(\"unity-theme://default\");\n");
                AssetDatabase.ImportAsset(themePath, ImportAssetOptions.ForceSynchronousImport);
            }

            var theme = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(themePath);

            // 2. Ensure PanelSettings asset exists and is configured
            var panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>(panelSettingsPath);
            bool isNew = false;
            if (panelSettings == null)
            {
                panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
                isNew = true;
            }

            panelSettings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            panelSettings.referenceResolution = new Vector2Int(1920, 1080);
            panelSettings.match = 0.5f;
            panelSettings.sortingOrder = 100;

            if (theme != null)
            {
                panelSettings.themeStyleSheet = theme;
            }

            if (isNew)
            {
                AssetDatabase.CreateAsset(panelSettings, panelSettingsPath);
                Debug.Log($"<color=#38bdf8>[SetupFacilityUIAsset] Created new PanelSettings at {panelSettingsPath} with theme {theme?.name}</color>");
            }
            else
            {
                EditorUtility.SetDirty(panelSettings);
                AssetDatabase.SaveAssets();
                Debug.Log($"<color=#38bdf8>[SetupFacilityUIAsset] Verified PanelSettings at {panelSettingsPath}. Theme: {panelSettings.themeStyleSheet?.name}, ScaleMode: {panelSettings.scaleMode}</color>");
            }
        }
    }
}
