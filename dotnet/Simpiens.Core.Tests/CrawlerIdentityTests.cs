using System.Collections.Generic;
using System.Linq;
using Simpiens.Core.Session;
using Simpiens.Hosting;
using Simpiens.Narrative;
using Simpiens.Simulation;
using Simpiens.Simulation.People;
using Simpiens.Simulation.Persistence;
using Xunit;

namespace Simpiens.Core.Tests
{
    /// <summary>
    /// Epic UI-Beta B1's Definition of Done, live and headless: on the real outpost with nothing injected, every crawler
    /// that spawns or arrives has a look and a name of its own (OutpostWatch judges that every tick of the 23 gate seeds'
    /// days, in OutpostScenarioTests), the catalogue's options all turn up across the seeds, a name is never given twice
    /// in a day, a departed drifter keeps its name, and a save and load keeps every look and name.
    /// </summary>
    public class CrawlerIdentityTests
    {
        private static readonly LookGene[] Spread = { LookGene.Lineage, LookGene.Frame, LookGene.Build, LookGene.SkinTone, LookGene.HairStyle };

        /// <summary>Across the gate seeds' outposts, every lineage, frame, build, skin tone and hair style some lineage offers turns up.</summary>
        [Fact]
        public void EveryOptionTurnsUpAcrossTheGateSeeds()
        {
            var seen = Spread.ToDictionary(g => g, g => new HashSet<byte>());
            int crawlers = 0;
            foreach (ulong seed in OutpostWatch.GateSeeds)
            {
                using var sim = LocalSimulation.Create(seed);
                sim.Respawn(OutpostSession.SessionId);
                foreach (var state in sim.Host.CrawlerStates)
                {
                    Assert.True(LookCatalog.IsValid(state.Look), $"Seed {seed}: {state.GivenName} has an invalid look [{state.Look}].");
                    foreach (var gene in Spread) seen[gene].Add(state.Look[gene]);
                    crawlers++;
                }
            }

            foreach (var gene in Spread)
            {
                var missing = LookCatalog.Options(gene).Where(o => LookCatalog.Lineages.Any(l => l.Offers(gene, o.Id)) && !seen[gene].Contains(o.Id)).Select(o => o.Key).ToList();
                Assert.True(missing.Count == 0, $"Across {crawlers} crawlers on {OutpostWatch.GateSeeds.Length} seeds, no crawler had {gene} {string.Join(", ", missing)}.");
            }
        }

        /// <summary>A whole day: nobody is faceless, no name is given twice (plan D5), and the chronicle still names drifters who left.</summary>
        [Fact]
        public void NamesAreNeverGivenTwiceInADay()
        {
            using var sim = LocalSimulation.Create(SimulationSettings.DefaultSeed);
            sim.Respawn(OutpostSession.SessionId);
            var watch = new OutpostWatch(sim);
            watch.Run(9000);

            // Everyone the day saw: who is still here, and who left (CrawlerSpawned fires before the host names a crawler)
            var given = sim.Host.CrawlerStates.Select(s => s.GivenName).Concat(sim.Host.Departed.Select(d => d.GivenName)).ToList();

            Assert.True(watch.Faceless + watch.SharedNames == 0, $"Who's who failed: {watch.FirstIdentityFault}");
            Assert.True(watch.DriftersArrived >= 2 && sim.Host.Departed.Count >= 1, $"The day needs drifters who come and go ({watch.DriftersArrived} came, {sim.Host.Departed.Count} left).");
            Assert.Equal(OutpostSession.DefaultCrawlerCount + watch.DriftersArrived, given.Count);
            var twice = given.GroupBy(n => n).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
            Assert.True(twice.Count == 0, $"Given twice in one day: {string.Join(", ", twice)}.");

            var formatter = new TemplateNarrativeFormatter(sim.Identity);
            foreach (var left in sim.Host.Departed)
            {
                Assert.Equal(left.GivenName, sim.Identity.GetFriendlyName(left.Id));
                string line = formatter.FormatEvent(new ChronicleEvent(ChronicleEventType.DrifterLeft, left.Id, default, 0, 0));
                Assert.Contains(left.GivenName, line);
                Assert.True(LookCatalog.IsValid(left.Look), $"{left.GivenName} left without a valid look.");
            }
        }

        /// <summary>A save after a drifter has left, loaded into a fresh game: every look, name and departed crawler comes back.</summary>
        [Fact]
        public void ASaveAndLoadKeepsEveryLookAndName()
        {
            ulong seed = SimulationSettings.DefaultSeed;
            using var sim = LocalSimulation.Create(seed);
            sim.Respawn(OutpostSession.SessionId);
            var watch = new OutpostWatch(sim);
            while (sim.Host.Departed.Count == 0 && sim.Clock.CurrentTick < 9000) watch.Run(100);
            Assert.True(sim.Host.Departed.Count > 0, "No drifter left within the day.");
            var save = SaveSerializer.Deserialize<SessionSave>(SaveSerializer.Serialize(sim.Host.Capture()));

            using var loaded = LocalSimulation.Create(seed);
            loaded.Respawn(OutpostSession.SessionId);
            Assert.True(loaded.Host.TryRestore(save, out string refusal), refusal);

            var before = sim.Host.CrawlerStates;
            var after = loaded.Host.CrawlerStates;
            Assert.Equal(before.Count, after.Count);
            for (int i = 0; i < before.Count; i++)
            {
                Assert.Equal(before[i].AgentId, after[i].AgentId);
                Assert.Equal(before[i].Look, after[i].Look);
                Assert.Equal(before[i].GivenName, after[i].GivenName);
                Assert.Equal(before[i].GivenName, loaded.Identity.GetFriendlyName(after[i].AgentId));
            }
            Assert.Equal(sim.Host.Departed.Select(d => (d.Id, d.GivenName, d.Look)).OrderBy(d => d.Id),
                loaded.Host.Departed.Select(d => (d.Id, d.GivenName, d.Look)));
            foreach (var left in loaded.Host.Departed) Assert.Equal(left.GivenName, loaded.Identity.GetFriendlyName(left.Id));

            // The next drifter after the load gets the id, name and look it would have without the load. Crawlers decide afresh
            // after a load, so the two games may part ways later; who the next arrival is doesn't depend on that
            var next = NextArrival(sim);
            var nextLoaded = NextArrival(loaded);
            Assert.True(next != null && nextLoaded != null, "A drifter arrives after the save in both games.");
            Assert.Equal((next.AgentId, next.GivenName, next.Look), (nextLoaded.AgentId, nextLoaded.GivenName, nextLoaded.Look));
        }

        // Plays on until the next crawler arrives (at most one sim-day), and returns it once the host has named it
        private static Simpiens.Cognition.AgentState NextArrival(LocalSimulation sim)
        {
            Simpiens.Cognition.AgentState arrived = null;
            void OnSpawned(Simpiens.Cognition.AgentRuntime runtime, Simpiens.Simulation.WorldEntity entity, string name) => arrived ??= runtime.State;
            sim.Host.CrawlerSpawned += OnSpawned;
            for (long end = sim.Clock.CurrentTick + 9000; arrived == null && sim.Clock.CurrentTick < end;) sim.Step(1);
            sim.Host.CrawlerSpawned -= OnSpawned;
            return arrived;
        }
    }
}
