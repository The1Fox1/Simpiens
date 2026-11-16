using System;
using System.Collections.Generic;
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
    /// The creature's rule (Epic 10 Part 3, K2.2; the epic's D9–D15), on the real outpost: mechanism tests, with crawlers put
    /// and held near a lair by hand (the live session hunts since K2.3). Creatures rest and roam near their lairs and in their
    /// sectors; a rat chases a stray in its sight and within its leash and strikes it down, then goes home; the leash ends a
    /// chase; a hurt rat flees home, heeding nothing, and heals there; every swing of a tick lands together and the same seed
    /// plays the same; and the rule allocates nothing once warm.
    /// </summary>
    public class CreatureRuleTests
    {
        private static readonly Vec2 NearTheWarren = new Vec2(20f, 14f); // overgrowth.3, 2.2 from the warren's heart

        [Fact]
        public void CreaturesRestAndRoamNearTheirLairs()
        {
            using var sim = Outpost();
            var service = sim.Simulation.Creatures;
            var map = sim.Host.Current.Map;
            var roamed = new HashSet<EntityId>();
            float farthest = 0f;
            for (int t = 0; t < 3000; t++)
            {
                sim.Step(1);
                foreach (var c in service.Creatures)
                {
                    var lair = service.Lairs[c.LairIndex].Definition;
                    Assert.Equal(map.FindSector(lair.Position).Id, map.FindSector(c.Position).Id);
                    float d = Vec2.Distance(c.Position, lair.Position);
                    if (c.Mode != CreatureMode.Rest && c.Mode != CreatureMode.Roam) continue; // after a crawler, or home from one
                    farthest = Math.Max(farthest, d / c.Definition.Leash);
                    Assert.True(d <= c.Definition.Leash * CreatureService.RoamShareOfLeash + 1f, $"{c} roamed past half its leash.");
                    if (c.Mode == CreatureMode.Roam) roamed.Add(c.Id);
                }
            }
            TestContext.Current.TestOutputHelper?.WriteLine($"{roamed.Count} of {service.Creatures.Count} roamed; the farthest went {farthest:P0} of its leash");
            Assert.Equal(service.Creatures.Count, roamed.Count);
        }

        [Fact]
        public void ARatChasesAStrayStrikesItDownAndGoesHome()
        {
            using var sim = Outpost();
            var stray = sim.Host.CrawlerStates[0];
            // The warren's pack: its rats and, since K10.2a, its Matriarch, whose bite (6) is two of a rat's
            var rats = sim.Simulation.Creatures.Creatures.Where(c => c.Kind == CreatureKind.BurrowRat || c.Kind == CreatureKind.Matriarch).Select(c => c.Id).ToHashSet();
            long struckAt = -1, downedAt = -1;
            float health = stray.Health;
            int blows = 0;
            for (int t = 0; t < 600 && downedAt < 0; t++)
            {
                Hold(stray, NearTheWarren);
                sim.Step(1);
                if (stray.StruckAtTick != 0 && struckAt < 0) struckAt = sim.Clock.CurrentTick;
                if (stray.HurtThisTick > 0f)
                {
                    Assert.Contains(stray.StruckBy, rats);
                    float hurt = stray.HurtThisTick; // the red coat shows it
                    // A rat's blow takes 3 (attack 3, no defense), the last one what was left
                    Assert.True(stray.Health == 0f || Math.Abs(hurt / 3f - MathF.Round(hurt / 3f)) < 1e-4f, $"A rat's blow takes 3, {hurt} this tick.");
                    Assert.True(stray.Health < health, "The blows took its health.");
                    blows += (int)MathF.Round(hurt / 3f);
                    health = stray.Health;
                }
                if (stray.IsDowned) downedAt = sim.Clock.CurrentTick;
            }
            TestContext.Current.TestOutputHelper?.WriteLine($"first swing t{struckAt}, downed t{downedAt} after {blows} blows");
            Assert.True(struckAt > 0 && struckAt < 100, "Rats noticed the stray and swung within ten seconds.");
            Assert.True(downedAt > 0 && stray.Health == 0f, "Held there, it was struck down.");

            // Downed, it is nobody's quarry (until K5 it limps out): every rat goes home
            uint downedTick = stray.StruckAtTick;
            for (int t = 0; t < 300; t++)
            {
                Hold(stray, NearTheWarren);
                sim.Step(1);
            }
            Assert.Equal(downedTick, stray.StruckAtTick);
            Assert.All(sim.Simulation.Creatures.Creatures.Where(c => rats.Contains(c.Id)), c => Assert.NotEqual(CreatureMode.Chase, c.Mode));
        }

        [Fact]
        public void TheLeashEndsAChase()
        {
            using var sim = Outpost();
            var stray = sim.Host.CrawlerStates[0];
            var service = sim.Simulation.Creatures;
            var heart = service.Lairs[0].Definition.Position;
            var rat = service.Creatures.First(c => c.Kind == CreatureKind.BurrowRat);
            for (int t = 0; t < 100 && rat.Mode != CreatureMode.Chase; t++)
            {
                Hold(stray, NearTheWarren);
                sim.Step(1);
            }
            Assert.Equal(CreatureMode.Chase, rat.Mode);

            // Past its leash of the heart, the quarry is let go, and nobody follows it there
            var pastTheLeash = new Vec2(13f, 12f);
            Assert.True(Vec2.Distance(pastTheLeash, heart) > rat.Definition.Leash);
            float farthest = 0f;
            for (int t = 0; t < 400; t++)
            {
                Hold(stray, pastTheLeash);
                sim.Step(1);
                foreach (var c in service.Creatures.Where(c => c.Kind == CreatureKind.BurrowRat)) farthest = Math.Max(farthest, Vec2.Distance(c.Position, heart));
            }
            Assert.NotEqual(CreatureMode.Chase, rat.Mode);
            Assert.True(farthest <= rat.Definition.Leash + 1f, $"A rat went {farthest:0.0} from the heart, past its leash of {rat.Definition.Leash}.");
        }

        [Fact]
        public void AHurtRatFleesHomeHeedingNothingAndHeals()
        {
            using var sim = Outpost();
            var stray = sim.Host.CrawlerStates[0];
            var service = sim.Simulation.Creatures;
            var rat = service.Creatures.First(c => c.Kind == CreatureKind.BurrowRat);
            for (int t = 0; t < 100 && rat.Mode != CreatureMode.Chase; t++)
            {
                Hold(stray, NearTheWarren);
                sim.Step(1);
            }
            Assert.Equal(CreatureMode.Chase, rat.Mode);

            rat.Health = rat.Definition.MaxHealth * 0.3f; // by hand: hurt past its line (crawlers fight back from K3)
            long home = -1;
            for (int t = 0; t < 200 && home < 0; t++)
            {
                Hold(stray, NearTheWarren);
                sim.Step(1);
                Assert.True(rat.Mode == CreatureMode.Flee || rat.Mode == CreatureMode.Rest, $"Hurt, the rat only flees and rests ({rat.Mode}).");
                if (rat.Mode == CreatureMode.Rest) home = sim.Clock.CurrentTick;
            }
            Assert.True(home > 0 && Vec2.Distance(rat.Position, rat.Home) <= CreatureService.Arrived, "It ran to its place.");

            float hurt = rat.Health;
            for (int t = 0; t < DayClock.TicksPerHour; t++)
            {
                Hold(stray, NearTheWarren);
                sim.Step(1);
                Assert.Equal(CreatureMode.Rest, rat.Mode); // the stray in its sight, still under its line: it rests
            }
            Assert.Equal(hurt + rat.Definition.MaxHealth / rat.Definition.HealHours, rat.Health, 1); // an hour's healing of its twelve
        }

        [Fact]
        public void TheSameSeedPlaysTheSameAndSwingsLandTogether()
        {
            var first = Trace();
            var second = Trace();
            Assert.Equal(first.Health, second.Health);
            Assert.Equal(first.Positions, second.Positions);
            Assert.True(first.Together > 0, "Two rats struck the stray on one tick, and both blows landed (D12).");
        }

        [Fact]
        public void TheRuleAllocatesNothingOnceWarm()
        {
            using var sim = Outpost();
            var stray = sim.Host.CrawlerStates[0];
            var world = (ICreatureWorld)sim.Simulation;
            var service = sim.Simulation.Creatures;
            for (int t = 0; t < 30; t++)
            {
                Hold(stray, NearTheWarren);
                sim.Step(1);
            }
            Assert.Contains(service.Creatures, c => c.Mode == CreatureMode.Chase);
            uint tick = (uint)sim.Clock.CurrentTick;
            stray.Health = 1e6f; // by hand: it stays up, so no downing is told in the measured loop
            stray.IsDowned = false;
            for (int i = 0; i < 50; i++) service.Tick(world, tick + (uint)i); // warm
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 50; i < 250; i++) service.Tick(world, tick + (uint)i);
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.Equal(0, allocated);
        }


        private static (List<float> Health, List<Vec2> Positions, int Together) Trace()
        {
            using var sim = Outpost();
            var stray = sim.Host.CrawlerStates[0];
            var health = new List<float>();
            var positions = new List<Vec2>();
            int together = 0;
            for (int t = 0; t < 400; t++)
            {
                Hold(stray, NearTheWarren);
                // By hand, once: every rat at the stray swings on the same tick (they keep the offsets of their first blows)
                if (t == 150)
                    foreach (var c in sim.Simulation.Creatures.Creatures.Where(c => c.Mode == CreatureMode.Chase)) c.NextSwingAt = sim.Clock.CurrentTick + 1;
                float before = stray.Health;
                sim.Step(1);
                if (stray.HurtThisTick > 6.5f && stray.Health > 0f) // more than any one bite (a rat's 3, the Matriarch's 6, K10.2a)
                {
                    together++; // several blows on one tick, all landed together
                    Assert.Equal(before - stray.Health, stray.HurtThisTick, 1);
                }
                health.Add(stray.Health);
                foreach (var c in sim.Simulation.Creatures.Creatures) positions.Add(c.Position);
                if (stray.IsDowned)
                {
                    stray.IsDowned = false;
                    stray.Health = 100f; // stood up again by hand, to keep the rats at it
                }
            }
            return (health, positions, together);
        }

        private static LocalSimulation Outpost()
        {
            var sim = LocalSimulation.Create(SimulationSettings.DefaultSeed);
            sim.Respawn(OutpostSession.SessionId);
            return sim;
        }

        // By hand: the stray stands there, whatever it decides
        private static void Hold(AgentState crawler, Vec2 at) => crawler.Position = at;
    }
}
