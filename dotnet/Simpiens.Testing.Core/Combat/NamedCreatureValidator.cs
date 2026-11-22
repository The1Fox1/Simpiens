using System;
using System.Collections.Generic;
using Simpiens.Core.Session.Layouts;
using Simpiens.Narrative;
using Simpiens.Simulation;
using Simpiens.Simulation.Combat;
using Simpiens.Simulation.Creatures;
using Simpiens.Simulation.World;
using Simpiens.Testing.Environment;

namespace Simpiens.Testing.Combat
{
    /// <summary>
    /// Named creatures (Epic 11, S7.1, A8, D31), on Floor 1's lairs through the simulation's own blow: the titles' table; a
    /// creature named at the downing its blow causes, told; its rank rising with each crawler it downs, to the most; a creature
    /// named at a death it caused; a boss never named; a named creature slain not returning, its place refilled by a plain one.
    /// The save's round trip is <see cref="Simpiens.Testing.Persistence.SessionRestoreValidator"/>'s. Runs as a test
    /// (ValidatorTests).
    /// </summary>
    public class NamedCreatureValidator
    {
        private const ulong Seed = 424242;

        public void RunAllValidationTests()
        {
            TestLog.Log("[NamedCreatureValidator] Beginning named creature validation...");
            ValidateTheTitles();
            ValidateNamedAtADowning();
            ValidateTheRankRises();
            ValidateNamedAtADeath();
            ValidateABossIsNeverNamed();
            ValidateANamedCreatureDoesNotReturn();
            TestLog.Log("<color=green>[NamedCreatureValidator] ALL 6 TESTS PASSED SUCCESSFULLY!</color>");
        }

        private static void ValidateTheTitles()
        {
            var words = new HashSet<string>();
            for (int i = 0; i < CreatureTitleTable.All.Count; i++)
            {
                var row = CreatureTitleTable.All[i];
                Assert(row.Rank == i + 1 && CreatureTitleTable.Get(row.Rank) == row, $"{row.Word}: rank {i + 1}, found by it.");
                Assert(!string.IsNullOrEmpty(row.Word) && words.Add(row.Word), $"{row.Word}: a word of its own.");
                foreach (char c in row.Word) Assert(c < 256, $"{row.Word}: Latin-1, as the fonts are.");
            }
            Assert(CreatureTitleTable.All.Count == CreatureTitleTable.MaxRank, "A title for each rank to the most.");
            Assert(CreatureTitleTable.Get(0) == null && CreatureTitleTable.Get(CreatureTitleTable.MaxRank + 1) == null && CreatureTitleTable.NameOf("Ama", 0).Length == 0, "None unnamed or past the table.");
            Assert(CreatureTitleTable.NameOf("Ama", 1) == "Ama's Bane", $"Named for its victim: \"{CreatureTitleTable.NameOf("Ama", 1)}\".");
            TestLog.Log($"[PASS] Test 1: {CreatureTitleTable.All.Count} titles, a rank each: {string.Join(", ", words)}.");
        }

        private static void ValidateNamedAtADowning()
        {
            var w = World();
            var beetle = First(w.Sim, CreatureKind.StoneBeetle);
            var ama = w.Crawler(beetle.Position);
            w.Down(ama, beetle.Id);
            Assert(ama.Agent.IsDowned, "The beetle's blow downed Ama.");
            Assert(beetle.IsNamed && beetle.Rank == 1 && beetle.Downed == 1 && beetle.Victim == ama.Node.Id, "The beetle took its name from Ama, rank 1.");
            var told = w.Told(ChronicleEventType.CreatureNamed);
            Assert(told.Count == 1 && told[0].ActorAId == beetle.Id && told[0].ActorBId == ama.Node.Id, "Told once: the beetle, named for Ama.");
            string line = w.Line(told[0]);
            Assert(line.Contains("stone beetle that downed Ama") && line.Contains("Ama's Bane"), $"The chronicle says it: \"{line}\".");
            TestLog.Log($"[PASS] Test 2: named at the downing its blow caused: \"{line}\"");
        }

        private static void ValidateTheRankRises()
        {
            var w = World();
            var beetle = First(w.Sim, CreatureKind.StoneBeetle);
            w.Down(w.Crawler(beetle.Position, "Ama"), beetle.Id);
            var bram = w.Crawler(beetle.Position, "Bram");
            w.Down(bram, beetle.Id);
            Assert(beetle.Rank == 2 && beetle.Downed == 2, "A second crawler downed: rank 2.");
            var rose = w.Told(ChronicleEventType.CreatureRankRose);
            Assert(rose.Count == 1 && rose[0].ActorAId == beetle.Id && rose[0].ActorBId == bram.Node.Id, "Its rank's rise told, with the crawler it downed.");
            string line = w.Line(rose[0]);
            Assert(line.Contains("Bram fell to") && line.Contains("second") && line.Contains("Scourge"), $"The chronicle says it: \"{line}\".");
            w.Down(w.Crawler(beetle.Position, "Cora"), beetle.Id);
            Assert(beetle.Rank == CreatureTitleTable.MaxRank && beetle.Downed == 3, "A third: its highest rank.");
            w.Down(w.Crawler(beetle.Position, "Dov"), beetle.Id);
            Assert(beetle.Rank == CreatureTitleTable.MaxRank && beetle.Downed == 4 && beetle.Victim != default, "A fourth is counted; its rank holds at the most, its name its first victim's.");
            Assert(w.Told(ChronicleEventType.CreatureRankRose).Count == 2 && w.Told(ChronicleEventType.CreatureNamed).Count == 1, "Named once, risen twice: nothing told at the highest rank.");
            TestLog.Log($"[PASS] Test 3: its rank rises with each crawler it downs, to {CreatureTitleTable.MaxRank}: \"{line}\"");
        }

        private static void ValidateNamedAtADeath()
        {
            var w = World();
            var rat = First(w.Sim, CreatureKind.BurrowRat);
            var ama = w.Crawler(rat.Position);
            ama.Agent.IsDowned = true; // downed by a hazard, then struck last by the rat: it bleeds out
            w.Sim.Die(ama.Node, DeathCause.Creature, (byte)rat.Kind, rat.Id);
            Assert(ama.Agent.HasLeft && rat.IsNamed && rat.Victim == ama.Node.Id && rat.Rank == 1, "The rat that struck Ama last took its name from Ama's death.");
            string line = w.Line(w.Told(ChronicleEventType.CreatureNamed)[0]);
            Assert(line.Contains("burrow rat that killed Ama"), $"The chronicle says it: \"{line}\".");

            // A death after the downing that named it adds nothing: its downing counted
            var beetle = First(w.Sim, CreatureKind.StoneBeetle);
            var bram = w.Crawler(beetle.Position, "Bram");
            w.Down(bram, beetle.Id);
            w.Sim.Die(bram.Node, DeathCause.Creature, (byte)beetle.Kind, beetle.Id);
            Assert(beetle.Rank == 1 && beetle.Downed == 1 && w.Told(ChronicleEventType.CreatureRankRose).Count == 0, "Bram downed, then dead: one crawler, rank 1.");
            TestLog.Log($"[PASS] Test 4: named at a death it caused; a death after its downing adds nothing: \"{line}\"");
        }

        private static void ValidateABossIsNeverNamed()
        {
            var w = World();
            var matriarch = First(w.Sim, CreatureKind.Matriarch);
            var ama = w.Crawler(matriarch.Position);
            w.Down(ama, matriarch.Id);
            Assert(ama.Agent.IsDowned && !matriarch.IsNamed && matriarch.Victim == default, "The Matriarch downed Ama and took no name: she has one.");
            Assert(w.Told(ChronicleEventType.CreatureNamed).Count == 0, "Nothing told.");
            TestLog.Log("[PASS] Test 5: a boss is never named.");
        }

        private static void ValidateANamedCreatureDoesNotReturn()
        {
            var w = World();
            var beetle = First(w.Sim, CreatureKind.StoneBeetle);
            w.Down(w.Crawler(beetle.Position), beetle.Id);
            int lair = beetle.LairIndex, slot = beetle.Slot;
            uint tick = (uint)w.Clock.CurrentTick;
            Assert(w.Sim.Creatures.Slay(w.Sim, beetle.Id, tick), "The named beetle slain.");
            Assert(!w.Sim.Creatures.TryGet(beetle.Id, out _), "It is gone.");
            uint returns = w.Sim.Creatures.Lairs[lair].ReturnAt(slot);
            Assert(returns > tick, "Its place waits for a creature in its kind's hours.");
            w.Sim.TickCreatures(returns);
            var member = w.Sim.Creatures.Lairs[lair].MemberIn(slot);
            Assert(member != default && member != beetle.Id && w.Sim.Creatures.TryGet(member, out var plain) && !plain.IsNamed && plain.Victim == default,
                "Its place refilled with a plain beetle, not itself.");
            TestLog.Log("[PASS] Test 6: a named creature slain doesn't return; its place refills with a plain one.");
        }

        private static CreatureState First(SimulationManager sim, CreatureKind kind)
        {
            foreach (var creature in sim.Creatures.Creatures)
                if (creature.Kind == kind) return creature;
            throw new InvalidOperationException($"[NamedCreatureValidator] FAILED: Floor 1 holds no {kind} on seed {Seed}.");
        }

        private sealed class Fixture
        {
            public SimulationManager Sim;
            public SimulationClock Clock;
            public WorldRegistry Registry;
            public NarrativeChronicleService Chronicle;
            public AgentIdentityService Identity;

            public TestCrawler Crawler(Vec2 at, string name = "Ama")
            {
                var crawler = HeadlessEntities.Crawler(Registry, Sim, Clock, at, id: EntityId.Generate(), name: name);
                crawler.Agent.Health = 100f;
                Identity.RegisterCustomName(crawler.Node.Id, name);
                return crawler;
            }

            // The creature's blow, through the simulation's own landing: enough to take the crawler to nothing
            public void Down(TestCrawler crawler, EntityId creature)
                => ((ICreatureWorld)Sim).Strike(crawler.Node, creature, new SwingOutcome(true, 1f, 1000f), (uint)Clock.CurrentTick);

            public List<ChronicleEvent> Told(ChronicleEventType type)
            {
                var events = new List<ChronicleEvent>();
                Chronicle.GetAllRecentEvents(events, 256);
                return events.FindAll(e => e.EventType == type);
            }

            public string Line(in ChronicleEvent e) => new TemplateNarrativeFormatter(Identity).FormatEvent(e);
        }

        private static Fixture World()
        {
            var sectors = new SectorRegistry();
            sectors.Load(Floor1Layout.Definition);
            var registry = new WorldRegistry();
            var clock = new SimulationClock();
            var chronicle = new NarrativeChronicleService();
            var sim = new SimulationManager(registry, null, clock: clock, chronicleService: chronicle, sectors: sectors);
            sim.SetEconomy(Simpiens.Simulation.Economy.EconomyTuning.Default);
            sim.PopulateLairs(Seed);
            clock.AdvanceTicks(100);
            return new Fixture { Sim = sim, Clock = clock, Registry = registry, Chronicle = chronicle, Identity = new AgentIdentityService() };
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("[NamedCreatureValidator] FAILED: " + message);
        }
    }
}
