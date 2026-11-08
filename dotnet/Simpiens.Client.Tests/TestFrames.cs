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
        public static WorldViewFrames Of(IReadOnlyList<WorldEntity> entities, ISectorRegistry sectors, long tick, IReadOnlyList<Simpiens.Simulation.Economy.TradeStallData> stalls = null, Simpiens.Simulation.Workshops.WorkshopService workshops = null, uint caravanInterval = 0)
        {
            var frames = new WorldViewFrames();
            frames.Restart(entities, sectors, tick, stalls, workshops, caravanInterval);
            return frames;
        }

        public static WorldViewFrames Of(LocalSimulation sim, long tick) => Of(sim.Registry.ActiveEntities, sim.Sectors, tick, sim.Simulation.ActiveStalls, sim.Simulation.Workshops, sim.Simulation.Economy.Supply.RestockIntervalTicks);

        public static WorldViewFrames Of(LocalSimulation sim) => Of(sim, sim.Clock.CurrentTick);
    }

    /// <summary>The outpost's stall in a scene (graphics V5): the counter and the goods it shows.</summary>
    internal static class SceneStalls
    {
        /// <summary>How many items the stall adds to a scene: its counter, and its goods in stock up to its display slots.</summary>
        public static int ShownPieces(LocalSimulation sim, int slots = 4)
        {
            int pieces = 0;
            foreach (var stall in sim.Simulation.ActiveStalls)
            {
                int stocked = 0;
                for (int g = 0; g < Simpiens.Simulation.Economy.TradeStallData.GoodsCapacity; g++)
                {
                    var good = stall.GoodAt(g);
                    if (good.Item != Simpiens.Simulation.Economy.ItemType.None && good.Stock > 0) stocked++;
                }
                pieces += 1 + System.Math.Min(stocked, slots);
            }
            return pieces;
        }
    }

    /// <summary>The map's stations in a scene (Epic 10 Part 2, W1.6a): each drawn once, a plot or the station, unless the fog hides it.</summary>
    internal static class SceneStations
    {
        public static int Shown(LocalSimulation sim, Simpiens.Presentation.WorldScene scene)
        {
            int shown = 0;
            var workshops = sim.Simulation.Workshops;
            for (int i = 0; i < workshops.Count; i++) if (scene.Fog.Visibility(workshops.DefinitionAt(i).Position, 0f) > 0f) shown++;
            return shown;
        }
    }
}
