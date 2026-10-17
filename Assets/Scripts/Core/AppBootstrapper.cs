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
            UnitySimLogSinks.Install(); // the engine-free logger's console is Unity's
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
            // 1. Core Services (camera framing is done by MapView when a map loads)

            // 2. Factories

            // 3. Simulation, Cognition & Narrative Systems
            builder.RegisterInstance(SimulationSettings.FromEnvironment());
            builder.Register<EntityIdAllocator>(Lifetime.Singleton).AsImplementedInterfaces();
            builder.Register<WorldRegistry>(Lifetime.Singleton).AsImplementedInterfaces();
            builder.Register<SpatialHashGrid>(Lifetime.Singleton).AsImplementedInterfaces();
            builder.Register<Simpiens.Simulation.World.SectorRegistry>(Lifetime.Singleton).AsImplementedInterfaces();
            builder.Register<Simpiens.Simulation.World.WorldMap>(Lifetime.Singleton).AsImplementedInterfaces();
            // Engine-free services carry no container attributes: classes with several constructors are built here
            builder.Register(resolver =>
            {
                var pathfinder = new AsyncPathfinder();
                pathfinder.Start(); // fills its pools up front
                return pathfinder;
            }, Lifetime.Singleton).AsImplementedInterfaces().AsSelf();
            builder.Register<SimulationClock>(Lifetime.Singleton).AsImplementedInterfaces();
            builder.RegisterEntryPoint<SimulationClockDriver>(); // advances the clock with Unity's frame time
            builder.Register<FacilityZoneService>(Lifetime.Singleton).AsImplementedInterfaces().AsSelf();
            builder.Register<Simpiens.Entities.WorldSelection>(Lifetime.Singleton);
            builder.Register<AgentIdentityService>(Lifetime.Singleton).AsImplementedInterfaces().AsSelf();
            builder.Register(resolver => new NarrativeChronicleService(), Lifetime.Singleton).AsImplementedInterfaces().AsSelf();
            builder.Register(resolver => new TemplateNarrativeFormatter(resolver.Resolve<IAgentIdentityService>(), resolver.Resolve<Simpiens.UI.Zones.IFacilityZoneService>()), Lifetime.Singleton).AsImplementedInterfaces().AsSelf();
            builder.Register(resolver => new PassthroughNarrativeEnricher(resolver.Resolve<INarrativeFormatter>()), Lifetime.Singleton).AsImplementedInterfaces().AsSelf();
            builder.Register<GroupRegistry>(Lifetime.Singleton).AsImplementedInterfaces().AsSelf();
            builder.Register<AgentRegistry>(Lifetime.Singleton).AsImplementedInterfaces().AsSelf();
            builder.Register<AgentConsequenceService>(Lifetime.Singleton).AsImplementedInterfaces().AsSelf();
            builder.Register<AgentCognitionService>(Lifetime.Singleton).AsImplementedInterfaces().AsSelf();
            builder.Register<SimulationManager>(Lifetime.Singleton).AsImplementedInterfaces();
            builder.Register<IGoapPlanner>(resolver => new GoapPlanner(), Lifetime.Singleton);
            builder.Register<IPlayerInterventionService, PlayerInterventionService>(Lifetime.Singleton);
            builder.Register<Simpiens.Simulation.Economy.EconomyLedger>(Lifetime.Singleton).AsImplementedInterfaces().AsSelf();
            // The supply service's tuning comes from the session (EconomyTuning), applied by SwarmSpawner on every respawn
            builder.Register<Simpiens.Simulation.Economy.ISupplyService>(resolver => new Simpiens.Simulation.Economy.SupplyService(), Lifetime.Singleton);
            builder.Register(resolver => Simpiens.Cognition.Contracts.IntentHandlerRegistry.CreateDefault(), Lifetime.Singleton).AsImplementedInterfaces().AsSelf();
            builder.Register<ToolExecutionRegistry>(Lifetime.Singleton).AsImplementedInterfaces().AsSelf();

            // 4. Sessions and the spawner that builds them. Optional assemblies may register further sessions
            // (the test harness adds the Dev Map); the default is chosen by id, never by registration order.
            builder.Register<ISessionDefinition>(resolver => new OutpostSession(), Lifetime.Singleton);
            // The session itself is engine-free (SessionHost); SwarmSpawner draws it and keeps the session API for the UI
            builder.Register<SessionHost>(Lifetime.Singleton).AsSelf();
            builder.Register<SwarmSpawner>(Lifetime.Singleton).AsImplementedInterfaces().AsSelf();

            // The player's quick-save slot (Milestone 1): Saves/quicksave.simsave under the persistent data path
            builder.Register<ISaveGameService>(resolver => new SaveGameService(resolver.Resolve<ISessionPersistence>(), System.IO.Path.Combine(Application.persistentDataPath, "Saves")), Lifetime.Singleton);

            // Optional assemblies (the Simpiens.Testing harness in editor/test builds) add their own services.
            // Domain validators no longer run at boot; they run as PlayMode tests (SwarmPlayModeTests).
            BootstrapExtensions.InstallAll(builder, gameObject);

            // 5. Entry Points (App Logic)
            builder.RegisterEntryPoint<SimulationStarter>();
        }
    }
}
