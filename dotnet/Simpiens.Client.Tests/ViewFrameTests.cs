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
    }
}
