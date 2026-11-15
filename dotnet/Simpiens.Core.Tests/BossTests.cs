using System.Linq;
using Simpiens.Cognition;
using Simpiens.Core.Session;
using Simpiens.Core.Session.Layouts;
using Simpiens.Hosting;
using Simpiens.Simulation;
using Simpiens.Simulation.Creatures;
using Xunit;

namespace Simpiens.Core.Tests
{
    /// <summary>
    /// The bosses' mechanics (Epic 10 Part 3, K10, Q13), on the real outpost: mechanism tests, with crawlers put and held by
    /// hand as the creature rule's tests do (<see cref="CreatureRuleTests"/>). What the live game makes of them is K10.6's
    /// <c>TheGuildBreaksTheGatekeeper</c>.
    /// </summary>
    public class BossTests
    {
        /// <summary>
        /// K10.2b: the Matriarch calls her brood at half health. Chasing a stray her resting brood can't see, she fights alone
        /// while above the line; under it, every rat of the warren turns on her quarry.
        /// </summary>
        [Fact]
        public void TheMatriarchCallsHerBroodAtHalfHealth()
        {
            using var sim = Outpost();
            var service = sim.Simulation.Creatures;
            var warren = service.Lairs.First(l => l.Definition.Key == Floor1Layout.RatWarren);
            Assert.True(service.TryGet(warren.MemberIn(0), out var her) && her.Kind == CreatureKind.Matriarch);
            var brood = service.Creatures.Where(c => c.LairIndex == warren.Index && c != her).ToList();
            Assert.NotEmpty(brood);
            Assert.All(brood, c => Assert.Equal(CreatureMode.Rest, c.Mode)); // fresh: everyone rests at its place

            // A stray inside the leash on the warren's west side; its brood, by hand, resting on the far side, past their sight
            var stray = sim.Host.CrawlerStates[0];
            var heart = warren.Definition.Position;
            var at = heart + new Vec2(-(her.Definition.Leash - 0.4f), 0f);
            for (int i = 0; i < brood.Count; i++)
            {
                brood[i].Position = heart + new Vec2(1.6f, -0.5f * i);
                brood[i].RestUntil = sim.Clock.CurrentTick + 10000; // resting, not roaming, for the test
                Assert.True(Vec2.Distance(brood[i].Position, at) > brood[i].Definition.Sight + 1f, $"{brood[i]} can't see the stray.");
            }

            // Above half her health she chases it alone
            Chase(her, stray.AgentId);
            stray.Position = at;
            sim.Step(1);
            Assert.Equal(CreatureMode.Chase, her.Mode);
            Assert.All(brood, c => Assert.NotEqual(CreatureMode.Chase, c.Mode));

            // Under half, she calls them: every rat of the warren that rests turns on her quarry
            her.Health = 0.4f * her.Definition.MaxHealth;
            stray.Position = at;
            sim.Step(1);
            stray.Position = at;
            sim.Step(1); // the brood before her in id order answer the tick after
            Assert.All(brood, c => Assert.True(c.Mode == CreatureMode.Chase && c.Target == stray.AgentId, $"{c} answered her call."));
        }

        private static void Chase(CreatureState creature, EntityId quarry)
        {
            creature.Mode = CreatureMode.Chase;
            creature.Target = quarry;
        }

        private static LocalSimulation Outpost()
        {
            var sim = LocalSimulation.Create(SimulationSettings.DefaultSeed);
            sim.Respawn(OutpostSession.SessionId);
            sim.Step(20);
            return sim;
        }
    }
}
