using System.Collections.Generic;
using System.Linq;
using Simpiens.Core.Session;
using Simpiens.Hosting;
using Simpiens.Presentation;
using Simpiens.Presentation.Art.People;
using Simpiens.Simulation;
using Simpiens.Simulation.Economy;
using Xunit;

namespace Simpiens.Client.Tests.People
{
    /// <summary>
    /// Bodies in the live outpost (Epic UI-Beta B2, the Definition of Done's live evidence): the real session, nothing
    /// injected, the guild's scripted player acting only through commands, and a body table kept as the client keeps it
    /// (moved on each tick, its bakes taken). Every crawler on every tick is drawn as its own body; the day's first drifter
    /// arrives with one; the first guild member wears the tabard. Headless: the table and the kit need no GPU.
    /// </summary>
    public class LiveBodiesTests
    {
        private const long Day = 9000;

        [Fact]
        public void EveryCrawlerOfTheDayIsDrawnAsItsOwnBody()
        {
            using var sim = LocalSimulation.Create(SimulationSettings.DefaultSeed);
            sim.Respawn(OutpostSession.SessionId);
            var player = new GuildPlayer(sim, targetSize: 1);
            using var frames = new WorldViewFrames();
            frames.Follow(sim.Host, sim.Registry, sim.Sectors, sim.Clock, null, sim.Simulation);
            var bodies = new CrawlerBodies();
            var bake = new Rgba32[BodyKit.PixelsPerBody];
            long drifterAt = -1, memberAt = -1;
            int bakes = 0, mostBodies = 0;

            while (sim.Clock.CurrentTick < Day && (drifterAt < 0 || memberAt < 0))
            {
                sim.Step(1);
                var frame = frames.Current;
                bodies.Update(frame);
                while (bodies.TryTakePending(out int pending))
                {
                    bodies.BakeSlot(pending, bake);
                    bakes++;
                }
                Assert.Equal(0, bodies.Overflow);
                mostBodies = System.Math.Max(mostBodies, bodies.InUse);
                for (int i = 0; i < frame.Count; i++)
                {
                    ref readonly var entity = ref frame[i];
                    if (!entity.IsCrawler) continue;
                    Assert.True(entity.Look.IsComplete, $"t{frame.Tick}: {entity.Id} has no look.");
                    Assert.True(bodies.TrySlotOf(entity, out int slot), $"t{frame.Tick}: {entity.Id} has no body.");
                    Assert.True(bodies.TryKeyIn(slot, out var key));
                    Assert.Equal(entity.Look, key.Look);
                    Assert.Equal(entity.Standing == CrawlerStanding.GuildMember, key.Tabard);
                    if (entity.Standing == CrawlerStanding.Drifter && drifterAt < 0) drifterAt = frame.Tick;
                    if (entity.Standing == CrawlerStanding.GuildMember && memberAt < 0)
                    {
                        memberAt = frame.Tick;
                        bodies.BakeSlot(slot, bake);
                        Assert.Contains(BodyKit.TabardColour, bake); // its body wears the guild's colours
                    }
                }
            }
            TestContext.Current.TestOutputHelper?.WriteLine($"First drifter at t{drifterAt}, first guild member at t{memberAt}; {bakes} bakes, at most {mostBodies} bodies at once.");
            Assert.True(drifterAt > 0, "No drifter arrived within the day.");
            Assert.True(memberAt > 0, $"Nobody joined the guild within the day (asked at t{player.FirstRecruitTick}).");
        }

        /// <summary>No two residents of a gate seed bake to the same body (the epic's §7: two residents alike on the map).</summary>
        [Fact]
        public void NoTwoResidentsOfAGateSeedLookAlikeOnTheMap()
        {
            var report = new List<string>();
            foreach (ulong seed in OutpostWatch.GateSeeds)
            {
                using var sim = LocalSimulation.Create(seed);
                sim.Respawn(OutpostSession.SessionId);
                var seen = new Dictionary<string, int>();
                for (int i = 0; i < sim.Host.CrawlerStates.Count; i++)
                {
                    var pixels = new Rgba32[BodyKit.PixelsPerFrame];
                    BodyKit.BakeFrame(sim.Host.CrawlerStates[i].Look, ItemType.None, false, Simpiens.Entities.AgentVisualState.Idle, 0, pixels);
                    string body = string.Join(",", pixels);
                    Assert.False(seen.TryGetValue(body, out int twin), $"Seed {seed}: residents {twin} and {i} look alike on the map.");
                    seen[body] = i;
                }
                report.Add($"{seed}: {seen.Count} residents, {seen.Count} bodies");
            }
            TestContext.Current.TestOutputHelper?.WriteLine(string.Join("\n", report));
        }
    }
}
