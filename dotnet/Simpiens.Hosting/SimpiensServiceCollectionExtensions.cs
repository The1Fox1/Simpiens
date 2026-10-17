using System;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Simpiens.Cognition;
using Simpiens.Cognition.Contracts;
using Simpiens.Cognition.Pathfinding;
using Simpiens.Cognition.Planning;
using Simpiens.Core.Session;
using Simpiens.Intervention;
using Simpiens.Narrative;
using Simpiens.Simulation;
using Simpiens.Simulation.Economy;
using Simpiens.Simulation.Spatial;
using Simpiens.Simulation.World;

namespace Simpiens.Hosting
{
    /// <summary>
    /// The engine-free core's services for Microsoft.Extensions.DependencyInjection (Epic CORE C8). The same set, built
    /// the same way, as Unity's composition root (<c>AppBootstrapper</c>) minus the views and the dashboard, so a
    /// headless run is the game's simulation, not a test double of it.
    /// </summary>
    public static class SimpiensServiceCollectionExtensions
    {
        /// <param name="settings">The world seed. Unity reads it from SIMPIENS_SEED / -simseed; a host passes it.</param>
        public static IServiceCollection AddSimpiensCore(this IServiceCollection services, SimulationSettings settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));

            services.AddSingleton(settings);
            services.AddSingleton<IEntityIdAllocator, EntityIdAllocator>();
            services.AddSingleton<WorldRegistry>();
            services.AddSingleton<IWorldRegistry>(sp => sp.GetRequiredService<WorldRegistry>());
            services.AddSingleton<SpatialHashGrid>();
            services.AddSingleton<ISpatialPartition>(sp => sp.GetRequiredService<SpatialHashGrid>());
            services.AddSingleton<ISectorRegistry, SectorRegistry>();
            services.AddSingleton<IWorldMap, WorldMap>();
            services.AddSingleton(sp =>
            {
                var pathfinder = new AsyncPathfinder();
                pathfinder.Start(); // fills its pools up front
                return pathfinder;
            });
            services.AddSingleton<IAsyncPathfinder>(sp => sp.GetRequiredService<AsyncPathfinder>());
            services.AddSingleton<ISimulationClock, SimulationClock>();
            services.AddSingleton<IAgentIdentityService, AgentIdentityService>();
            services.AddSingleton<INarrativeChronicleService>(sp => new NarrativeChronicleService());
            services.AddSingleton<INarrativeFormatter>(sp => new TemplateNarrativeFormatter(sp.GetRequiredService<IAgentIdentityService>(), sp.GetRequiredService<ISectorRegistry>()));
            services.AddSingleton<ILlmNarrativeEnricher>(sp => new PassthroughNarrativeEnricher(sp.GetRequiredService<INarrativeFormatter>()));
            services.AddSingleton<IGroupRegistry, GroupRegistry>();
            services.AddSingleton<IAgentRegistry, AgentRegistry>();
            services.AddSingleton<IAgentConsequenceService, AgentConsequenceService>();
            services.AddSingleton<IAgentCognitionService, AgentCognitionService>();
            services.AddSingleton<SimulationManager>();
            services.AddSingleton<ISimulationManager>(sp => sp.GetRequiredService<SimulationManager>());
            services.AddSingleton<IIntentExecutionContext>(sp => sp.GetRequiredService<SimulationManager>());
            services.AddSingleton<IGoapPlanner>(sp => new GoapPlanner());
            services.AddSingleton<IPlayerInterventionService, PlayerInterventionService>();
            services.AddSingleton<IEconomyLedger, EconomyLedger>();
            services.AddSingleton<ISupplyService>(sp => new SupplyService());
            services.AddSingleton<IIntentHandlerRegistry>(sp => IntentHandlerRegistry.CreateDefault());
            services.AddSingleton<IToolExecutionRegistry, ToolExecutionRegistry>();

            // Sessions: the outpost always; hosts and tests may add more (the default is chosen by id)
            services.AddSingleton<ISessionDefinition>(sp => new OutpostSession());
            services.AddSingleton(sp => new SessionHost(
                sp.GetRequiredService<ISpatialPartition>(),
                sp.GetRequiredService<IAsyncPathfinder>(),
                sp.GetRequiredService<IWorldRegistry>(),
                sp.GetRequiredService<ISimulationManager>(),
                sp.GetRequiredService<ISimulationClock>(),
                sp.GetRequiredService<IGoapPlanner>(),
                sp.GetRequiredService<SimulationSettings>(),
                sp.GetRequiredService<IEntityIdAllocator>(),
                sp.GetRequiredService<IWorldMap>(),
                sp.GetServices<ISessionDefinition>().ToList()));
            services.AddSingleton<ISessionSwitcher>(sp => sp.GetRequiredService<SessionHost>());
            services.AddSingleton<ISessionPersistence>(sp => sp.GetRequiredService<SessionHost>());
            return services;
        }
    }
}
