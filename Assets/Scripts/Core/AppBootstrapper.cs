using UnityEngine;
using VContainer;
using VContainer.Unity;
using Simpiens.Entities;
using Simpiens.Simulation;
using Simpiens.Simulation.Spatial;
using Simpiens.Cognition;
using Simpiens.Cognition.Pathfinding;
using Simpiens.Cognition.Testing;
using Simpiens.Intervention;

namespace Simpiens.Core
{
    /// <summary>
    /// The absolute entry point for the simulation.
    /// This removes the need for an "App" or "GameManager" GameObject in the scene.
    /// </summary>
    public class AppBootstrapper : LifetimeScope
    {
        // This runs before any scene is loaded, injecting our entire game structure programmatically.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void AutoBoot()
        {
            GameObject bootGo = new GameObject("[AppBootstrapper]");
            DontDestroyOnLoad(bootGo);
            
            // VContainer's LifetimeScope builds automatically in Awake()
            bootGo.AddComponent<AppBootstrapper>();
        }

        protected override void Configure(IContainerBuilder builder)
        {
            // 1. Core Services
            builder.RegisterEntryPoint<ScreenBoundsGenerator>();
            
            // 2. Factories
            builder.Register<NodeFactory>(Lifetime.Singleton);

            // 3. Simulation & Cognition Systems
            builder.Register<WorldRegistry>(Lifetime.Singleton).AsImplementedInterfaces();
            builder.Register<SpatialHashGrid>(Lifetime.Singleton).AsImplementedInterfaces();
            builder.Register<AsyncPathfinder>(Lifetime.Singleton).AsImplementedInterfaces().AsSelf();
            builder.Register<SimulationClock>(Lifetime.Singleton).AsImplementedInterfaces();
            builder.Register<SimulationManager>(Lifetime.Singleton).AsImplementedInterfaces();
            builder.Register<ContextValidator>(Lifetime.Singleton);
            builder.Register<ICognitiveEngine, CognitiveEngine>(Lifetime.Singleton);
            builder.Register<IPlayerInterventionService, PlayerInterventionService>(Lifetime.Singleton);

            // 4. Testing & Validation Harness
            builder.Register<SwarmSpawner>(Lifetime.Singleton).AsImplementedInterfaces();
            builder.Register<EdgeCaseInjector>(Lifetime.Singleton).AsImplementedInterfaces();

            // 5. Entry Points (App Logic)
            builder.RegisterEntryPoint<SimulationStarter>();
        }
    }
}
