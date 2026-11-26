using System.Linq;
using Simpiens.Cognition;
using Simpiens.Cognition.Contracts;
using Simpiens.Cognition.Planning;
using Simpiens.Core.Session;
using Simpiens.Core.Session.Layouts;
using Simpiens.Hosting;
using Simpiens.Simulation.Life;
using Simpiens.Simulation.Spatial;
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

        /// <summary>
        /// A rest day keeps to safe zones (Epic LIFE-1 plan 3, R.8, D35), judged on the real outpost: a bold, healthy resident would
        /// work the Sump on a working day and not on its day off, hunts on neither its day off, and still goes for the fallen there.
        /// </summary>
        [Fact]
        public void ARestDayKeepsToSafeZones()
        {
            using var sim = LocalSimulation.Create(Simpiens.Simulation.SimulationSettings.DefaultSeed);
            sim.Respawn(OutpostSession.SessionId);
            var crawler = sim.Host.CrawlerStates.First(c => !c.IsDrifter);
            crawler.Personality = new AgentPersonality(0f, 0.9f, 0f, 0f, 0f);
            crawler.Health = Vitality.MaxHealth;
            var lark = crawler.Life;
            lark.SetChronotype(Chronotype.Lark); // up at 05:00, so its routine day has begun at the session's 06:00
            crawler.Life = lark;
            var sump = sim.Host.Current.Map.Sectors.First(s => s.Id == Floor1Layout.SumpSectorId).Bounds.Center;
            sim.Step(1); // a snapshot with the map
            var snapshot = ((ISpatialPartition)sim.Services.GetService(typeof(ISpatialPartition))).GetActiveSnapshot();
            Assert.True(Vitality.HazardAt(snapshot.Map, sump).IsHazardous, "The Sump is a hazard.");
            uint tick = (uint)sim.Clock.CurrentTick;
            long day = crawler.RoutineAt(tick).Day;
            var drawn = crawler.Life;
            drawn.SetIntention(DayIntention.Work, day); // whatever it drew on waking, a working day first
            crawler.Life = drawn;

            var working = crawler.CreateContext(snapshot, tick);
            Assert.Equal(DayIntention.Work, working.Intention);
            Assert.True(Vitality.WouldVenture(working) && Vitality.WouldEnterAt(working, sump), "On a working day the bold go into the Sump.");

            var life = crawler.Life;
            life.SetIntention(DayIntention.Rest, day);
            crawler.Life = life;
            var resting = crawler.CreateContext(snapshot, tick);
            Assert.Equal(DayIntention.Rest, resting.Intention);
            Assert.False(Vitality.WouldVenture(resting), "On its day off it ventures nowhere.");
            Assert.False(Vitality.WouldEnterAt(resting, sump), "Nor into the Sump.");
            Assert.True(Vitality.WouldEnterAt(resting, sump, restDay: false), "But a friend fallen there is still within reach (rescue judges it as a working day).");
            Assert.False(Hunting.IsHunter(resting), "Nor does it hunt.");
            Assert.True(Vitality.WouldEnterAt(resting, sim.Host.Current.Map.Sectors.First(s => s.Id == Floor1Layout.HubSectorId).Bounds.Center), "The hub is safe.");
        }
    }
}
