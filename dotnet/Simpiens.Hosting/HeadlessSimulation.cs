using System;
using Microsoft.Extensions.DependencyInjection;
using Simpiens.Core.Session;
using Simpiens.Simulation;
using Simpiens.Simulation.World;

namespace Simpiens.Hosting
{
    /// <summary>
    /// The simulation with no engine and no frames (Epic CORE C8): the core composed as the game composes it, stepped
    /// tick by tick as fast as the machine allows. Cognition decisions land on a fixed tick (CognitionScheduler), so a
    /// run here is the same run as in Unity at any speed. One instance per world; dispose it to stop.
    /// </summary>
    public sealed class HeadlessSimulation : IDisposable
    {
        private readonly ServiceProvider _services;

        private HeadlessSimulation(ServiceProvider services)
        {
            _services = services;
            Clock = services.GetRequiredService<ISimulationClock>();
            Simulation = services.GetRequiredService<ISimulationManager>();
            // The simulation subscribes to the clock before the session host does: the host depends on it, so it is
            // built first, and crawlers tick after the simulation's tick, as in Unity
            Host = services.GetRequiredService<SessionHost>();
            Sectors = services.GetRequiredService<ISectorRegistry>();
            Registry = services.GetRequiredService<IWorldRegistry>();
        }

        /// <param name="configure">Extra registrations (more sessions, test doubles).</param>
        public static HeadlessSimulation Create(ulong seed, Action<IServiceCollection> configure = null)
        {
            var services = new ServiceCollection().AddSimpiensCore(new SimulationSettings(seed));
            configure?.Invoke(services);
            return new HeadlessSimulation(services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true }));
        }

        public IServiceProvider Services => _services;
        public ISimulationClock Clock { get; }
        public ISimulationManager Simulation { get; }
        public SessionHost Host { get; }
        public ISectorRegistry Sectors { get; }
        public IWorldRegistry Registry { get; }

        /// <summary>Respawns a session at tick 0 (the outpost by default).</summary>
        public void Respawn(string sessionId = OutpostSession.SessionId)
        {
            var session = Host.Find(sessionId) ?? throw new ArgumentException($"No session '{sessionId}' is registered.", nameof(sessionId));
            Host.Respawn(session);
        }

        /// <summary>Runs <paramref name="ticks"/> logical ticks.</summary>
        public void Step(long ticks) => Clock.AdvanceTicks(ticks);

        /// <summary>Runs ticks until the clock reaches <paramref name="tick"/>.</summary>
        public void RunTo(long tick)
        {
            if (tick > Clock.CurrentTick) Clock.AdvanceTicks(tick - Clock.CurrentTick);
        }

        public void Dispose() => _services.Dispose();
    }
}
