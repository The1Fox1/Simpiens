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

        /// <summary>
        /// K10.3a: the Gatekeeper's slam strikes every crawler on its feet in its reach with one swing, and nobody past it.
        /// Three held in its reach and one just past it; the web's spinners slain by hand, so every blow is the keeper's.
        /// </summary>
        [Fact]
        public void TheGatekeepersSlamStrikesEveryoneInItsReach()
        {
            using var sim = Outpost();
            SlayTheWeb(sim);
            var keeper = TheKeeper(sim);
            var crawlers = sim.Host.CrawlerStates.Where(c => !c.IsDrifter).Take(4).ToList();
            var k = keeper.Home;
            var at = new[] { k + new Vec2(1.5f, 0f), k + new Vec2(0f, 1.5f), k + new Vec2(-1.5f, 0f), k + new Vec2(3.6f, 0f) };
            long swung = -1;
            for (int t = 0; t < 200 && swung < 0; t++)
            {
                for (int i = 0; i < crawlers.Count; i++) crawlers[i].Position = at[i];
                sim.Step(1);
                if (keeper.SwungAtTick != 0) swung = keeper.SwungAtTick;
            }
            Assert.True(swung > 0, "The keeper noticed them and swung.");
            for (int i = 0; i < 3; i++)
                Assert.True(crawlers[i].StruckAtTick == swung && crawlers[i].StruckBy == keeper.Id, $"Crawler {i} in its reach was struck by the slam.");
            Assert.NotEqual((uint)swung, crawlers[3].StruckAtTick); // just past its reach
        }

        /// <summary>
        /// K10.3c: the Gatekeeper heals nothing resting, and stands whole once its lair has been empty of crawlers for a game
        /// hour, not a tick before; the clock (its last visitor's tick) is saved.
        /// </summary>
        [Fact]
        public void TheGatekeeperStandsWholeAnHourAfterItsLairIsLeft()
        {
            using var sim = Outpost();
            SlayTheWeb(sim);
            var keeper = TheKeeper(sim);
            var id = keeper.Id;
            var visitor = sim.Host.CrawlerStates.First(c => !c.IsDrifter);
            keeper.Health = 100f; // hurt by hand, as an attempt would leave it
            var inLair = keeper.Home + new Vec2(3f, 0f);
            for (int t = 0; t < 5; t++)
            {
                visitor.Position = inLair;
                sim.Step(1);
            }
            Assert.True(keeper.LastVisitAt > 0, "Its lair noted its visitor.");

            // Away: the visitor back at the hub, held there
            var away = new Vec2(0f, 0f);
            visitor.Position = away;
            sim.Step(1);
            uint left = keeper.LastVisitAt;
            uint whole = left + keeper.Definition.WholeAfterTicks;

            // A save while it waits keeps the clock
            var save = sim.Host.Capture();
            Assert.True(sim.Host.TryRestore(save, out var refusal), refusal);
            Assert.True(sim.Simulation.Creatures.TryGet(id, out keeper) && keeper.LastVisitAt == left && keeper.Health == 100f, "The save kept its last visitor's tick.");
            visitor = sim.Host.CrawlerStates.First(c => c.AgentId == visitor.AgentId);

            while (sim.Clock.CurrentTick < whole - 1)
            {
                visitor.Position = away;
                sim.Step(1);
            }
            Assert.Equal(100f, keeper.Health); // no healing at rest, and not a tick before its hour
            visitor.Position = away;
            sim.Step(1);
            Assert.Equal(keeper.Definition.MaxHealth, keeper.Health);
        }

        /// <summary>K10.3d: slain, the Gatekeeper never returns, across days and a save.</summary>
        [Fact]
        public void TheGatekeeperBeatenOnceStaysBeaten()
        {
            using var sim = Outpost();
            var keeper = TheKeeper(sim);
            var gate = sim.Simulation.Creatures.Lairs[keeper.LairIndex];
            Assert.True(sim.Simulation.Creatures.Slay((ICreatureWorld)sim.Simulation, keeper.Id, (uint)sim.Clock.CurrentTick));
            Assert.Equal(0u, gate.ReturnAt(0));
            sim.Step(8 * (int)DayClock.TicksPerDay);
            Assert.Equal(0, gate.Alive);
            Assert.DoesNotContain(sim.Simulation.Creatures.Creatures, c => c.Kind == CreatureKind.Gatekeeper);
            var save = sim.Host.Capture();
            Assert.True(sim.Host.TryRestore(save, out var refusal), refusal);
            sim.Step(100);
            Assert.DoesNotContain(sim.Simulation.Creatures.Creatures, c => c.Kind == CreatureKind.Gatekeeper);
        }

        private static CreatureState TheKeeper(LocalSimulation sim)
        {
            var service = sim.Simulation.Creatures;
            var gate = service.Lairs.First(l => l.Definition.GuardsGate);
            Assert.True(service.TryGet(gate.MemberIn(0), out var keeper) && keeper.Kind == CreatureKind.Gatekeeper);
            return keeper;
        }

        // The web's spinners slain by hand (they return in two days), so a test at the gate meets only its keeper
        private static void SlayTheWeb(LocalSimulation sim)
        {
            var service = sim.Simulation.Creatures;
            var web = service.Lairs.First(l => l.Definition.Key == Floor1Layout.SpinnerWeb);
            for (int slot = 0; slot < web.Pack; slot++)
                if (web.MemberIn(slot) != default) Assert.True(service.Slay((ICreatureWorld)sim.Simulation, web.MemberIn(slot), (uint)sim.Clock.CurrentTick));
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
