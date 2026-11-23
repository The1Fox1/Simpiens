using System.Linq;
using Simpiens.Core.Session;
using Simpiens.Hosting;
using Simpiens.Intervention;
using Simpiens.Simulation;
using Simpiens.Simulation.Guild;
using Simpiens.Simulation.Skills;
using Xunit;

namespace Simpiens.Core.Tests
{
    /// <summary>
    /// The playtest tools (2026-11-20, the producer's "make simulations a bit easier to control for playtesting"), as typed
    /// commands on the live outpost: Max trust sets a crawler's warmth and trust toward the player at their most, so it signs
    /// on; Down puts it down where it stands, as a beetle's blow would, and out of danger the fallen's rule counts a downing
    /// survived, with a beetle's claw; a crawler already down isn't downed again; a crawler that isn't there is refused.
    /// </summary>
    public class PlaytestCommandsTests
    {
        [Fact]
        public void MaxTrustAndDownApplyOnTheTick()
        {
            using var sim = LocalSimulation.Create(SimulationSettings.DefaultSeed);
            sim.Respawn(OutpostSession.SessionId);
            var commands = (IPlayerInterventionService)sim.Services.GetService(typeof(IPlayerInterventionService));
            var crawler = sim.Host.CrawlerStates.First(c => !c.IsDrifter);
            uint tick = (uint)sim.Clock.CurrentTick;
            Assert.True(crawler.Memory.GetAffinity(WorldRegistry.PlayerEntityId, tick).Trust < 100, "It starts short of full trust.");

            commands.QueueInfluence(InfluenceEvent.CreatePlaytest("test", crawler.AgentId, PlaytestAction.MaxStanding));
            sim.Step(1);
            var affinity = crawler.Memory.GetAffinity(WorldRegistry.PlayerEntityId, (uint)sim.Clock.CurrentTick);
            Assert.Equal((100, 100, 0), ((int)affinity.Warmth, (int)affinity.Trust, (int)affinity.Fear));
            var (trust, warmth) = GuildRules.Standing(crawler, (uint)sim.Clock.CurrentTick);
            Assert.True(trust >= 99 && warmth >= 99, "The guild reads it at its most.");
            Assert.Equal(GuildRules.Check.Ok, sim.Simulation.Guild.Recruit((IGuildHost)sim.Simulation, crawler.AgentId, 30, (uint)sim.Clock.CurrentTick));

            Assert.False(crawler.IsDowned);
            commands.QueueInfluence(InfluenceEvent.CreatePlaytest("test", crawler.AgentId, PlaytestAction.Down));
            sim.Step(2);
            Assert.True(crawler.IsDowned && crawler.Health < Simpiens.Cognition.Vitality.RecoveredAt, "Downed where it stands.");
            Assert.Equal(DeathCause.Creature, crawler.DownedBy);
            Assert.False(sim.Simulation.InDanger(crawler.Position), "At the outpost, out of danger.");
            Assert.Equal(1, crawler.Traits.Count(Deed.DowningSurvived));
            Assert.Equal(Scar.BeetleClaw, crawler.Traits.ScarAt(0));
            Assert.Equal(0u, crawler.BleedsOutAt);

            commands.QueueInfluence(InfluenceEvent.CreatePlaytest("test", crawler.AgentId, PlaytestAction.Down));
            sim.Step(2);
            Assert.Equal(1, crawler.Traits.Count(Deed.DowningSurvived)); // already down: not downed again
            commands.QueueInfluence(InfluenceEvent.CreatePlaytest("test", new EntityId(9, 9, 9, 9), PlaytestAction.Down)); // nobody: refused
            sim.Step(1);
        }
    }
}
