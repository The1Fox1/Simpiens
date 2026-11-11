using System.Linq;
using Simpiens.Cognition;
using Simpiens.Cognition.Contracts;
using Simpiens.Core.Session;
using Simpiens.Core.Session.Layouts;
using Simpiens.Entities;
using Simpiens.Hosting;
using Simpiens.Simulation;
using Simpiens.Simulation.Creatures;
using Xunit;

namespace Simpiens.Core.Tests
{
    /// <summary>
    /// Flight (Epic 10 Part 3, K2.3c; the epic's D19), on the real outpost: a crawler held by the
    /// warren until the first blow, then left to itself, drops what it was doing and flees, shown panicking, until it stands clear of
    /// the warren's reach and nothing has struck it for two seconds; then it goes about its day. A downed crawler limps out
    /// as from a hazard, and nobody strikes it.
    /// </summary>
    public class FlightTests
    {
        private static readonly Vec2 NearTheWarren = new Vec2(20f, 14f); // overgrowth.3, 2.2 from the warren's heart

        [Fact]
        public void AStruckCrawlerFleesClearOfTheReachAndGoesAboutItsDay()
        {
            using var sim = Outpost();
            var stray = sim.Host.CrawlerStates.First(s => !s.IsDrifter);
            var map = sim.Host.Current.Map;
            var warren = map.Lairs.First(l => l.Key == Floor1Layout.RatWarren);
            sim.Step(20);
            // By hand: it stands by the warren until the first blow, then it is left to itself
            long struckAt = -1;
            for (int t = 0; t < 100 && stray.StruckAtTick == 0; t++)
            {
                stray.Position = NearTheWarren;
                sim.Step(1);
            }
            if (stray.StruckAtTick != 0) struckAt = stray.StruckAtTick;

            long fleeingAt = -1, clearAt = -1, safeAt = -1;
            float health = stray.Health, lowest = stray.Health;
            for (int t = 0; t < 600 && safeAt < 0 && struckAt > 0; t++)
            {
                sim.Step(1);
                long tick = sim.Clock.CurrentTick;
                lowest = System.Math.Min(lowest, stray.Health);
                if (fleeingAt < 0 && stray.CurrentIntent is FleeIntent)
                {
                    fleeingAt = tick;
                    Assert.False(stray.IsDowned);
                }
                if (stray.CurrentIntent is FleeIntent && fleeingAt >= 0 && tick > fleeingAt + 2) Assert.Equal(AgentVisualState.Panicking, stray.VisualState);
                if (fleeingAt >= 0 && clearAt < 0 && Vitality.ClearOfLairs(map, stray.Position)) clearAt = tick;
                if (fleeingAt >= 0 && !(stray.CurrentIntent is FleeIntent) && stray.HasActiveIntent) safeAt = tick;
            }
            TestContext.Current.TestOutputHelper?.WriteLine($"struck t{struckAt}, fleeing t{fleeingAt}, clear t{clearAt}, safe t{safeAt}; health {health:0.#} -> lowest {lowest:0.#}");
            Assert.True(struckAt > 0, "The rats struck the stray.");
            Assert.True(fleeingAt > 0 && fleeingAt - struckAt <= 4, "It dropped what it did and fled within a few ticks.");
            Assert.True(clearAt > 0 && clearAt - fleeingAt < 100, "It ran clear of the warren's reach within ten seconds.");
            Assert.True(safeAt >= clearAt, "Its flight ended once it was clear.");
            Assert.False(stray.IsDowned, "It got away on its feet.");
            Assert.True(sim.Simulation.Creatures.IsCharted(map.Lairs.ToList().IndexOf(warren)), "It knows of the warren now.");

            // About its day: nobody strikes it again, and it keeps out of the charted warren
            uint lastStruck = stray.StruckAtTick;
            for (int t = 0; t < 1500; t++)
            {
                sim.Step(1);
                Assert.False(LairReach.Holds(warren, stray.Position) && t > 50, $"t{sim.Clock.CurrentTick}: it went back into the warren's reach.");
            }
            Assert.Equal(lastStruck, stray.StruckAtTick);
        }

        [Fact]
        public void ADownedCrawlerCollapsesAndIsLeftAlone()
        {
            // Downed by the warren, in its reach, it collapses where it lies (Epic 10 Part 3, K5.1, Q3: it limped out before)
            using var sim = Outpost();
            var stray = sim.Host.CrawlerStates.First(s => !s.IsDrifter);
            sim.Step(20);
            for (int t = 0; t < 900 && !stray.IsDowned; t++)
            {
                stray.Position = NearTheWarren; // held by hand, whatever it decides, until it is down
                sim.Step(1);
            }
            Assert.True(stray.IsDowned);
            sim.Step(1);
            Assert.True(stray.IsCollapsed, "In the warren's reach, it collapses.");
            uint downedStruck = stray.StruckAtTick;
            var at = stray.Position;
            bool fled = false;
            for (int t = 0; t < 300; t++)
            {
                sim.Step(1);
                fled |= stray.CurrentIntent is FleeIntent;
                Assert.True(Vec2.Distance(at, stray.Position) < 0.01f, "Collapsed, it doesn't move.");
            }
            Assert.False(fled, "Downed, it doesn't flee.");
            Assert.Equal(downedStruck, stray.StruckAtTick); // nobody strikes a downed crawler
        }

        private static LocalSimulation Outpost()
        {
            var sim = LocalSimulation.Create(SimulationSettings.DefaultSeed);
            sim.Respawn(OutpostSession.SessionId);
            return sim;
        }
    }
}
