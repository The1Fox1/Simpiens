using Simpiens.Core.Session;
using UnityEngine;
using VContainer;
using VContainer.Unity;
using Simpiens.Diagnostics;
using Simpiens.Entities;
using Simpiens.Narrative;
using Simpiens.Simulation;
using Simpiens.Simulation.Spatial;
using Simpiens.Cognition;
using Simpiens.Cognition.Pathfinding;
using Simpiens.Cognition.Planning;
using Simpiens.Intervention;
using Simpiens.UI.Zones;

namespace Simpiens.Core.Bootstrapping
{
    /// <summary>
    /// The absolute entry point for the simulation.
    /// This removes the need for an "App" or "GameManager" GameObject in the scene.
    /// </summary>
    public class AppBootstrapper : LifetimeScope
    {
        public static AppBootstrapper Instance { get; private set; }

        // This runs before any scene is loaded, injecting our entire game structure programmatically.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void AutoBoot()
        {
            GameObject bootGo = new GameObject("[AppBootstrapper]");
            DontDestroyOnLoad(bootGo);
            
            // VContainer's LifetimeScope builds automatically in Awake()
            bootGo.AddComponent<AppBootstrapper>();
        }

        protected override void Awake()
        {
            Instance = this;

            // Logging first, so container construction and entry points can already log.
            // Levels come from SIMPIENS_LOG* env vars / -simlog* command-line args (see SimLogSettings).
            if (!SimLog.IsConfigured)
            {
                SimLog.Configure(SimLogSettings.FromEnvironment());
            }

            base.Awake();
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            if (Instance == this)
            {
                Instance = null;
                SimLog.Shutdown();
            }
        }

        protected override void Configure(IContainerBuilder builder)
        {
            // 1. Core Services
            builder.RegisterEntryPoint<ScreenBoundsGenerator>();
            
            // 2. Factories
            builder.Register<NodeFactory>(Lifetime.Singleton);

            // 3. Simulation, Cognition & Narrative Systems
            builder.RegisterInstance(SimulationSettings.FromEnvironment());
            builder.Register<EntityIdAllocator>(Lifetime.Singleton).AsImplementedInterfaces();
            builder.Register<WorldRegistry>(Lifetime.Singleton).AsImplementedInterfaces();
            builder.Register<SpatialHashGrid>(Lifetime.Singleton).AsImplementedInterfaces();
            builder.Register<AsyncPathfinder>(Lifetime.Singleton).AsImplementedInterfaces().AsSelf();
            builder.Register<SimulationClock>(Lifetime.Singleton).AsImplementedInterfaces();
            builder.Register<FacilityZoneService>(Lifetime.Singleton).AsImplementedInterfaces().AsSelf();
            builder.Register<AgentIdentityService>(Lifetime.Singleton).AsImplementedInterfaces().AsSelf();
            builder.Register<NarrativeChronicleService>(Lifetime.Singleton).AsImplementedInterfaces().AsSelf();
            builder.Register<TemplateNarrativeFormatter>(Lifetime.Singleton).AsImplementedInterfaces().AsSelf();
            builder.Register<PassthroughNarrativeEnricher>(Lifetime.Singleton).AsImplementedInterfaces().AsSelf();
            builder.Register<GroupRegistry>(Lifetime.Singleton).AsImplementedInterfaces().AsSelf();
            builder.Register<AgentRegistry>(Lifetime.Singleton).AsImplementedInterfaces().AsSelf();
            builder.Register<AgentConsequenceService>(Lifetime.Singleton).AsImplementedInterfaces().AsSelf();
            builder.Register<AgentCognitionService>(Lifetime.Singleton).AsImplementedInterfaces().AsSelf();
            builder.Register<SimulationManager>(Lifetime.Singleton).AsImplementedInterfaces();
            builder.Register<Simpiens.Environment.OutpostMapBuilder>(Lifetime.Singleton).AsImplementedInterfaces().AsSelf();
            builder.Register<IGoapPlanner, GoapPlanner>(Lifetime.Singleton);
            builder.Register<IPlayerInterventionService, PlayerInterventionService>(Lifetime.Singleton);
            builder.Register<Simpiens.Simulation.Economy.EconomyLedger>(Lifetime.Singleton).AsImplementedInterfaces().AsSelf();
            builder.Register<Simpiens.Simulation.Economy.ISupplyService>(resolver => new Simpiens.Simulation.Economy.SupplyService(
                Simpiens.Simulation.Economy.SupplyTuning.For(resolver.Resolve<SimulationSettings>())), Lifetime.Singleton);
            builder.Register(resolver => Simpiens.Cognition.Contracts.IntentHandlerRegistry.CreateDefault(), Lifetime.Singleton).AsImplementedInterfaces().AsSelf();
            builder.Register<ToolExecutionRegistry>(Lifetime.Singleton).AsImplementedInterfaces().AsSelf();

            // 4. Session population (pens + Outpost Commons)
            builder.Register<SwarmSpawner>(Lifetime.Singleton).AsImplementedInterfaces().AsSelf();

            // Optional assemblies (the Simpiens.Testing harness in editor/test builds) add their own services.
            // Domain validators no longer run at boot; they run as PlayMode tests (SwarmPlayModeTests).
            BootstrapExtensions.InstallAll(builder, gameObject);

            // 5. Entry Points (App Logic)
            builder.RegisterEntryPoint<SimulationStarter>();
        }
    }
}
