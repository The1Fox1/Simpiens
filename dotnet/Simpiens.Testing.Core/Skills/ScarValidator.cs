using System;
using System.Collections.Generic;
using Simpiens.Narrative;
using Simpiens.Simulation;
using Simpiens.Simulation.Creatures;
using Simpiens.Simulation.Economy;
using Simpiens.Simulation.Skills;
using Simpiens.Simulation.World;
using Simpiens.Testing.Environment;

namespace Simpiens.Testing.Skills
{
    /// <summary>
    /// Scars (Epic 11, S6.2, A2, D28): the table (ids, words in Latin-1, a row for every creature kind and every hazard but the
    /// flood, one per cause); the record (the places filled in order, four at most, news with each); and the live path, a
    /// downing survived (its bleeding clock stopped with the crawler alive) leaving the scar of what downed it, told once by the
    /// runtime. Runs as a test (ValidatorTests).
    /// </summary>
    public class ScarValidator
    {
        public void RunAllValidationTests()
        {
            TestLog.Log("[ScarValidator] Beginning scar validation...");
            ValidateTheTable();
            ValidateThePlaces();
            ValidateADowningSurvivedLeavesOne();
            TestLog.Log("<color=green>[ScarValidator] ALL 3 TESTS PASSED SUCCESSFULLY!</color>");
        }

        private static void ValidateTheTable()
        {
            var causes = new HashSet<(DeathCause, byte)>();
            foreach (var row in ScarTable.All)
            {
                Assert(row.Id != Scar.None && ScarTable.Get(row.Id) == row, $"{row.Name}: found by its id.");
                Assert(!string.IsNullOrEmpty(row.Words) && causes.Add((row.Cause, row.Detail)), $"{row.Name}: words, and a cause of its own.");
                foreach (char c in row.Words) Assert(c < 256, $"{row.Name}: Latin-1, as the fonts are.");
                Assert(ScarTable.For(row.Cause, row.Detail) == row.Id, $"{row.Name}: found by its cause.");
            }
            foreach (Scar id in Enum.GetValues(typeof(Scar))) Assert(id == Scar.None || ScarTable.Get(id) != null, $"Scar {id} has a row.");
            foreach (var kind in CreatureCatalog.All) Assert(ScarTable.For(DeathCause.Creature, (byte)kind.Id) != Scar.None, $"{kind.Name} leaves a scar.");
            Assert(ScarTable.For(DeathCause.Hazard, (byte)HazardKind.FoulAir) != Scar.None && ScarTable.For(DeathCause.Hazard, (byte)HazardKind.FallingRock) != Scar.None, "Foul air and falling rock leave scars.");
            Assert(ScarTable.For(DeathCause.Hazard, (byte)HazardKind.Flooding) == Scar.None, "The flood leaves none (D28).");
            TestLog.Log($"[PASS] Test 1: {ScarTable.All.Count} scars, one for every creature kind and every hazard but the flood.");
        }

        private static void ValidateThePlaces()
        {
            var traits = default(CrawlerTraits);
            Assert(!traits.AddScar(Scar.None) && traits.ScarCount == 0 && traits.News == LegendNews.None, "No scar, nothing taken.");
            var scars = new[] { Scar.BeetleClaw, Scar.RatBite, Scar.Rockfall, Scar.SpinnerAcid };
            for (int i = 0; i < scars.Length; i++)
            {
                Assert(traits.AddScar(scars[i]) && traits.ScarCount == i + 1 && traits.ScarAt(i) == scars[i], $"Scar {i + 1} in place {(ScarPlace)i}.");
                Assert((traits.News & LegendNews.Scar) != 0, "With news to tell.");
            }
            Assert(!traits.AddScar(Scar.GateBlow) && traits.ScarCount == CrawlerTraits.ScarSlots, "Four places: a fifth isn't taken.");
            Assert(ScarTable.Describe(Scar.BeetleClaw, 0) == "a beetle's claw across the left cheek" && ScarTable.Describe(Scar.None, 0) == string.Empty, "Its words where it sits.");
            TestLog.Log("[PASS] Test 2: the places fill in order (left cheek, brow, right cheek, chin), four at most.");
        }

        private static void ValidateADowningSurvivedLeavesOne()
        {
            var world = new WorldRegistry();
            var clock = new SimulationClock();
            var chronicle = new NarrativeChronicleService();
            var sim = new SimulationManager(world, null, clock: clock, chronicleService: chronicle) { Seed = 7 };
            sim.SetEconomy(EconomyTuning.Default);
            clock.AdvanceTicks(100);
            var crawler = HeadlessEntities.Crawler(world, sim, clock, Vec2.Zero, id: EntityId.Generate());
            var agent = crawler.Agent;
            // Downed by a beetle, its clock running; then on its feet again (out of danger: nothing here is dangerous)
            agent.Health = 0f;
            agent.IsDowned = true;
            agent.DownedBy = DeathCause.Creature;
            agent.DownedDetail = (byte)CreatureKind.StoneBeetle;
            agent.BleedsOutAt = (uint)clock.CurrentTick + 1000u;
            agent.IsDowned = false;
            agent.Health = 10f;
            sim.TickCreatures((uint)clock.CurrentTick);
            Assert(agent.BleedsOutAt == 0 && agent.Traits.Count(Deed.DowningSurvived) == 1, "Its clock stopped: a downing survived.");
            Assert(agent.Traits.ScarCount == 1 && agent.Traits.ScarAt(0) == Scar.BeetleClaw, "It bears a beetle's claw.");
            sim.TickCreatures((uint)clock.CurrentTick + 1);
            Assert(agent.Traits.ScarCount == 1, "Once: the clock is stopped.");
            int Told()
            {
                var all = new List<ChronicleEvent>();
                chronicle.GetAllRecentEvents(all, 64);
                return all.FindAll(e => e.EventType == ChronicleEventType.ScarTaken && e.ActorAId == crawler.Id).Count;
            }
            crawler.Tick(clock.CurrentTick);
            crawler.Tick(clock.CurrentTick + 1);
            Assert(Told() == 1 && agent.Traits.News == LegendNews.None, "Told once by its runtime.");
            var events = new List<ChronicleEvent>();
            chronicle.GetAllRecentEvents(events, 64);
            string line = new TemplateNarrativeFormatter().FormatEvent(events.Find(e => e.EventType == ChronicleEventType.ScarTaken));
            Assert(line.Contains("a beetle's claw across the left cheek"), $"The chronicle says it: \"{line}\".");
            TestLog.Log($"[PASS] Test 3: a downing survived leaves what downed it, told once: \"{line}\"");
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("[ScarValidator] FAILED: " + message);
        }
    }
}
