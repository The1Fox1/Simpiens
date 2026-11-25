using System.Linq;
using Simpiens.Core.Session;
using Simpiens.Hosting;
using Simpiens.Simulation.Life;
using Xunit;

namespace Simpiens.Core.Tests
{
    /// <summary>
    /// The routine on the real outpost (Epic LIFE-1 plan 3, R.1, D28, D29): every resident's chronotype drawn at spawn on its
    /// own salt, about a fifth larks and a fifth owls across the gate seeds, and its routine's block what the context computes.
    /// </summary>
    public sealed class RoutineTests
    {
        [Fact]
        public void ChronotypesAreDrawnAtSpawnOnTheGateSeeds()
        {
            int lark = 0, owl = 0, all = 0;
            foreach (ulong seed in OutpostWatch.GateSeeds)
            {
                using var sim = LocalSimulation.Create(seed);
                sim.Respawn(OutpostSession.SessionId);
                foreach (var crawler in sim.Host.CrawlerStates.Where(c => !c.IsDrifter))
                {
                    Assert.Equal(Routine.Draw(seed, crawler.AgentId), crawler.Life.Chronotype);
                    all++;
                    if (crawler.Life.Chronotype == Chronotype.Lark) lark++;
                    if (crawler.Life.Chronotype == Chronotype.Owl) owl++;
                }
            }
            Assert.InRange(lark / (float)all, 0.12f, 0.28f);
            Assert.InRange(owl / (float)all, 0.12f, 0.28f);
        }
    }
}
