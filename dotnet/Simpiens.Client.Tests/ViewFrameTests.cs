using System.Collections.Generic;
using Simpiens.Core.Session;
using Simpiens.Hosting;
using Simpiens.Simulation;
using Xunit;

namespace Simpiens.Client.Tests
{
    /// <summary>The views' per-tick frame (review A3): captured when a tick is complete, read instead of the live world.</summary>
    public class ViewFrameTests
    {
        [Fact]
        public void TheHostSignalsTheEndOfEveryTickAfterWhichNothingMoves()
        {
            using var sim = LocalSimulation.Create(SimulationSettings.DefaultSeed);
            sim.Respawn(OutpostSession.SessionId);
            var ticks = new List<long>();
            var seen = new Dictionary<EntityId, Vec2>();
            sim.Host.TickCompleted += tick =>
            {
                ticks.Add(tick);
                seen.Clear();
                foreach (var entity in sim.Registry.ActiveEntities) seen[entity.Id] = entity.Position;
            };

            sim.Step(5);
            Assert.Equal(new long[] { 1, 2, 3, 4, 5 }, ticks);
            Assert.Equal(sim.Registry.ActiveEntities.Count, seen.Count);
            foreach (var entity in sim.Registry.ActiveEntities) Assert.Equal(seen[entity.Id], entity.Position); // the crawlers had ticked
        }

        /// <summary>
        /// Frames carry what items show in the world (graphics V5.1): each crawler's worn gear and the first salvage it
        /// holds, and every stall's goods and stock, as plain copies that follow the simulation tick by tick.
        /// </summary>
        [Fact]
        public void FramesCarryWornGearCarriedSalvageAndTheStalls()
        {
            using var sim = Simpiens.Hosting.LocalSimulation.Create(Simpiens.Simulation.SimulationSettings.DefaultSeed);
            sim.Respawn(Simpiens.Core.Session.OutpostSession.SessionId);
            var wearer = sim.Host.CrawlerStates[0];
            var carrier = sim.Host.CrawlerStates[1];
            wearer.Inventory.TryEquip(new Simpiens.Simulation.Economy.ItemInstance(1, Simpiens.Simulation.Economy.ItemType.HazardGear, 1, 30, durability: 100));
            Assert.True(carrier.Inventory.TryAddItem(new Simpiens.Simulation.Economy.ItemInstance(2, Simpiens.Simulation.Economy.ItemType.IronOre, 2, 8), out _));
            using var frames = new Simpiens.Presentation.WorldViewFrames();
            frames.Follow(sim.Host, sim.Registry, sim.Sectors, sim.Clock, null, sim.Simulation);

            var frame = frames.Current;
            Assert.True(frame.TryGetIndex(wearer.AgentId, out int w));
            Assert.True(frame.TryGetIndex(carrier.AgentId, out int c));
            Assert.Equal(Simpiens.Simulation.Economy.ItemType.HazardGear, frame[w].Worn);
            Assert.Equal(Simpiens.Simulation.Economy.ItemType.IronOre, frame[c].Carried);
            Assert.Equal(Simpiens.Simulation.Economy.ItemType.None, frame[c].Worn);
            for (int i = 0; i < frame.Count; i++)
                if (!frame[i].IsCrawler) Assert.Equal((Simpiens.Simulation.Economy.ItemType.None, Simpiens.Simulation.Economy.ItemType.None), (frame[i].Worn, frame[i].Carried));

            Assert.Equal(sim.Simulation.ActiveStalls.Count, frame.StallCount);
            Assert.True(frame.StallCount > 0);
            Assert.Equal(sim.Simulation.ActiveStalls[0].StallEntityId, frame.Stall(0).StallEntityId);

            // The next tick's frame follows the stall's stock
            sim.Step(1);
            Assert.Equal(sim.Simulation.ActiveStalls[0].StockOf(Simpiens.Simulation.Economy.ItemType.Ration), frames.Current.Stall(0).StockOf(Simpiens.Simulation.Economy.ItemType.Ration));
        }
    }
}
