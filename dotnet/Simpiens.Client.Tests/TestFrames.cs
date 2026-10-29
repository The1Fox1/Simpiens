using System.Collections.Generic;
using Simpiens.Hosting;
using Simpiens.Presentation;
using Simpiens.Simulation;
using Simpiens.Simulation.World;

namespace Simpiens.Client.Tests
{
    /// <summary>Frames captured by hand, for tests that build a scene without a game: the world as it stands, at a tick.</summary>
    internal static class TestFrames
    {
        public static WorldViewFrames Of(IReadOnlyList<WorldEntity> entities, ISectorRegistry sectors, long tick, IReadOnlyList<Simpiens.Simulation.Economy.TradeStallData> stalls = null)
        {
            var frames = new WorldViewFrames();
            frames.Restart(entities, sectors, tick, stalls);
            return frames;
        }

        public static WorldViewFrames Of(LocalSimulation sim, long tick) => Of(sim.Registry.ActiveEntities, sim.Sectors, tick, sim.Simulation.ActiveStalls);

        public static WorldViewFrames Of(LocalSimulation sim) => Of(sim, sim.Clock.CurrentTick);
    }
}
