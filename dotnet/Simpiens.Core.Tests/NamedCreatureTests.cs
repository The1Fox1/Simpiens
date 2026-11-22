using System.Collections.Generic;
using System.Linq;
using Simpiens.Core.Session;
using Simpiens.Hosting;
using Simpiens.Narrative;
using Simpiens.Simulation;
using Simpiens.Simulation.Combat;
using Simpiens.Simulation.Creatures;
using Simpiens.Simulation.Persistence;
using Xunit;

namespace Simpiens.Core.Tests
{
    /// <summary>
    /// A named creature's name on the live outpost (Epic 11, S7.1, D31): the session host registers "{its victim}'s {title}" with
    /// the identity service, as it does a crawler's given name, so the chronicle names it; its rank rising renames it; a save and
    /// a load keep it, byte for byte, and name it again; its victim's death keeps the name, from the departed list. The downings
    /// are the creatures' own blows, through the simulation's landing, on residents placed by hand.
    /// </summary>
    public class NamedCreatureTests
    {
        [Fact]
        public void TheChronicleAndALoadNameIt()
        {
            using var sim = LocalSimulation.Create(424242);
            sim.Respawn(OutpostSession.SessionId);
            sim.Step(10);
            var beetle = sim.Simulation.Creatures.Creatures.First(c => c.Kind == CreatureKind.StoneBeetle);
            var residents = sim.Host.CrawlerStates.Where(c => !c.IsDrifter).Take(2).ToList();
            var (ama, bram) = (residents[0], residents[1]);

            Down(sim, ama, beetle);
            sim.Step(1);
            Assert.Equal($"{ama.GivenName}'s Bane", sim.Identity.GetFriendlyName(beetle.Id));
            Down(sim, bram, beetle);
            sim.Step(1);
            Assert.Equal($"{ama.GivenName}'s Scourge", sim.Identity.GetFriendlyName(beetle.Id));
            var formatter = new TemplateNarrativeFormatter(sim.Identity, sim.Sectors);
            var told = new List<ChronicleEvent>();
            sim.Chronicle.GetAllRecentEvents(told, 256);
            var rose = told.Single(e => e.EventType == ChronicleEventType.CreatureRankRose);
            Assert.Contains($"{bram.GivenName} fell to {ama.GivenName}'s Scourge", formatter.FormatEvent(rose));

            // A save and a load: the same bytes, and the name again
            byte[] saved = SaveSerializer.Serialize(sim.Host.Capture());
            Assert.True(sim.Host.TryRestore(SaveSerializer.Deserialize<SessionSave>(saved), out string refusal), refusal);
            Assert.Equal(saved, SaveSerializer.Serialize(sim.Host.Capture()));
            Assert.True(sim.Simulation.Creatures.TryGet(beetle.Id, out var loaded) && loaded.Rank == 2 && loaded.Victim == ama.AgentId);
            Assert.Equal($"{ama.GivenName}'s Scourge", sim.Identity.GetFriendlyName(beetle.Id));

            // Its victim dies; after another load the departed list's name names it
            var amaNow = sim.Host.CrawlerStates.First(c => c.AgentId == ama.AgentId);
            ((SimulationManager)sim.Simulation).Die(sim.Registry.GetEntity(amaNow.AgentId), DeathCause.Creature, (byte)CreatureKind.StoneBeetle, beetle.Id);
            sim.Step(1);
            Assert.DoesNotContain(sim.Host.CrawlerStates, c => c.AgentId == ama.AgentId);
            saved = SaveSerializer.Serialize(sim.Host.Capture());
            Assert.True(sim.Host.TryRestore(SaveSerializer.Deserialize<SessionSave>(saved), out refusal), refusal);
            Assert.Equal($"{ama.GivenName}'s Scourge", sim.Identity.GetFriendlyName(beetle.Id));
        }

        private static void Down(LocalSimulation sim, Simpiens.Cognition.AgentState crawler, CreatureState creature)
        {
            var entity = sim.Registry.GetEntity(crawler.AgentId);
            ((ICreatureWorld)sim.Simulation).Strike(entity, creature.Id, new SwingOutcome(true, 1f, 1000f), (uint)sim.Clock.CurrentTick);
            Assert.True(crawler.IsDowned);
        }
    }
}
