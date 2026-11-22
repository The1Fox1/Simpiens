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
    /// named at a death it caused; a boss never named; a named creature slain not returning, its place refilled by a plain one;
    /// growing with its rank (S7.2, D32: its health and attack); its lair judged more dangerous, so a lone crawler keeps away;
    /// and leading its pack, the mates in its lair's reach taking its quarry. The save's round trip is <see cref="Simpiens.Testing.Persistence.SessionRestoreValidator"/>'s. Runs as a test
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
            ValidateItGrows();
            ValidateTheLairsDanger();
            ValidateItLeadsItsPack();
            TestLog.Log("<color=green>[NamedCreatureValidator] ALL 9 TESTS PASSED SUCCESSFULLY!</color>");
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

        private static void ValidateItGrows()
        {
            int lastHealth = 0, lastAttack = 0;
            foreach (var row in CreatureTitleTable.All)
            {
                Assert(row.HealthPercent > lastHealth && row.AttackPercent > lastAttack, $"{row.Word}: grows more than the rank below.");
                Assert(row.HealthPercent <= CreatureTitleTable.MaxHealthPercent && row.AttackPercent <= CreatureTitleTable.MaxAttackPercent, $"{row.Word}: within the bounds.");
                lastHealth = row.HealthPercent;
                lastAttack = row.AttackPercent;
            }
            Assert(CreatureTitleTable.HealthFactor(0) == 1f && CreatureTitleTable.AttackFactor(0) == 1f, "Unnamed: as its kind.");

            var w = World();
            var beetle = First(w.Sim, CreatureKind.StoneBeetle);
            var kind = beetle.Definition;
            Assert(beetle.MaxHealth == kind.MaxHealth && beetle.Health == kind.MaxHealth, "Unnamed, whole: its kind's health.");
            float plainAttack = Combatant.ForCreature(kind, beetle.Id, beetle.Position, beetle.Health, beetle.Rank).Attack;
            w.Down(w.Crawler(beetle.Position), beetle.Id);
            Assert(Near(beetle.MaxHealth, kind.MaxHealth * 1.2f) && Near(beetle.Health, beetle.MaxHealth), $"A Bane: a fifth more health, grown at once ({beetle.Health:F1} of {beetle.MaxHealth:F1}).");
            float baneAttack = Combatant.ForCreature(kind, beetle.Id, beetle.Position, beetle.Health, beetle.Rank).Attack;
            Assert(Near(baneAttack, plainAttack * 1.1f), $"A tenth more attack: {plainAttack:F2} to {baneAttack:F2}.");
            beetle.Health = 30f; // hurt, then its rank rises: its health rises by as much as its whole health
            w.Down(w.Crawler(beetle.Position, "Bram"), beetle.Id);
            Assert(Near(beetle.Health, 30f + kind.MaxHealth * 0.2f) && Near(beetle.MaxHealth, kind.MaxHealth * 1.4f), $"A Scourge, hurt: {beetle.Health:F1} of {beetle.MaxHealth:F1}.");
            w.Down(w.Crawler(beetle.Position, "Cora"), beetle.Id);
            Assert(Near(beetle.MaxHealth, kind.MaxHealth * 1.6f) && Near(Combatant.ForCreature(kind, beetle.Id, beetle.Position, beetle.Health, beetle.Rank).Attack, plainAttack * 1.3f), "A Terror: three fifths more health, three tenths more attack.");
            TestLog.Log($"[PASS] Test 7: it grows with its rank: a beetle's health {kind.MaxHealth:F0} to {beetle.MaxHealth:F0}, its attack {plainAttack:F1} to {plainAttack * 1.3f:F1}.");
        }

        private static void ValidateTheLairsDanger()
        {
            var map = Floor1Layout.Definition;
            int hollow = LairIndex(map, Floor1Layout.BeetleHollow);
            var lair = map.Lairs[hollow];
            float[] danger = new float[CreatureTitleTable.MaxRank + 1];
            for (int rank = 0; rank <= CreatureTitleTable.MaxRank; rank++) danger[rank] = LairReach.Danger(lair, 0f, false, rank);
            for (int rank = 1; rank <= CreatureTitleTable.MaxRank; rank++) Assert(danger[rank] > danger[rank - 1], $"A rank-{rank} leader makes the hollow more dangerous: {danger[rank - 1]:F3} to {danger[rank]:F3}.");

            // A lone crawler that would brave the hollow as it is keeps away from it under a Terror
            var skills = default(Simpiens.Simulation.Skills.SkillSet);
            skills.Set(Simpiens.Simulation.Skills.Skill.Melee, 5);
            var worn = new Simpiens.Cognition.Inventory.AgentInventory();
            worn.TryEquip(Simpiens.Simulation.Items.Outfitting.NewPiece(Simpiens.Simulation.Economy.ItemType.IronBlade, Simpiens.Simulation.Items.Grades.Standard));
            var traits = default(Simpiens.Simulation.Skills.CrawlerTraits);
            float plain = Simpiens.Cognition.Vitality.HarmToClear(lair, false, skills, worn, traits);
            float led = Simpiens.Cognition.Vitality.HarmToClear(lair, false, skills, worn, traits, CreatureTitleTable.MaxRank);
            Assert(led > plain, $"Clearing it under a Terror costs more: {plain:F1} to {led:F1}.");
            float health = (float)(Simpiens.Cognition.Vitality.RetreatBelow + (plain + led) / 2f);
            Assert(Simpiens.Cognition.Vitality.BravesLair(lair, false, skills, worn, traits, health, 100, false)
                && !Simpiens.Cognition.Vitality.BravesLair(lair, false, skills, worn, traits, health, 100, false, CreatureTitleTable.MaxRank), "Braved as it is; not under a Terror.");

            // The simulation's own: the hollow's leader rank, as the snapshot carries it, until the leader is slain
            var w = World();
            var beetle = First(w.Sim, CreatureKind.StoneBeetle);
            Assert(w.Sim.Creatures.LeaderRank(hollow) == 0, "No leader yet.");
            w.Down(w.Crawler(beetle.Position), beetle.Id);
            w.Sim.Creatures.NamedLairs(out ulong low, out ulong high);
            Assert(w.Sim.Creatures.LeaderRank(hollow) == 1 && LairReach.LeaderRank(low, high, hollow) == 1 && LairReach.LeaderRank(low, high, LairIndex(map, Floor1Layout.RatWarren)) == 0, "The hollow led by a Bane, the warren by nobody.");
            w.Sim.Creatures.Slay(w.Sim, beetle.Id, (uint)w.Clock.CurrentTick);
            Assert(w.Sim.Creatures.LeaderRank(hollow) == 0, "Slain, it leads nothing.");
            TestLog.Log($"[PASS] Test 8: its lair is judged more dangerous by its rank ({danger[0]:F3} to {danger[CreatureTitleTable.MaxRank]:F3} a second), and a lone crawler keeps away.");
        }

        private static void ValidateItLeadsItsPack()
        {
            var map = Floor1Layout.Definition;
            int hollow = LairIndex(map, Floor1Layout.BeetleHollow);
            ulong seed = 1;
            while (CreatureService.PackOf(seed, hollow, map.Lairs[hollow]) < 2) seed++; // a hollow of two beetles
            var heart = map.Lairs[hollow].Position;
            var kind = CreatureCatalog.Get(CreatureKind.StoneBeetle);
            var where = heart + new Vec2(kind.Leash - 0.5f, 0f); // within its reach, past a mate's sight
            Assert(kind.Leash - 0.5f > kind.Sight + 1f, "Past a resting mate's sight.");

            bool Follows(bool named)
            {
                var w = World(seed);
                CreatureState leader = null, mate = null;
                foreach (var c in w.Sim.Creatures.Creatures)
                    if (c.LairIndex == hollow) { if (leader == null) leader = c; else mate = c; }
                if (named) w.Down(w.Crawler(leader.Position), leader.Id);
                var quarry = w.Crawler(where, "Quinn");
                leader.Mode = CreatureMode.Chase;
                leader.Target = quarry.Node.Id;
                mate.Mode = CreatureMode.Rest;
                mate.RestUntil = long.MaxValue;
                w.Sim.TickCreatures((uint)w.Clock.CurrentTick + 1u);
                return mate.Mode == CreatureMode.Chase && mate.Target == quarry.Node.Id;
            }
            Assert(Follows(named: true), "A named leader's mate takes its quarry, out of its own sight.");
            Assert(!Follows(named: false), "An unnamed chaser's mate rests on.");
            TestLog.Log($"[PASS] Test 9: it leads its pack: the mate in its lair's reach takes its quarry (seed {seed}).");
        }

        private static bool Near(float a, float b) => System.Math.Abs(a - b) < 0.01f;

        private static int LairIndex(MapDefinition map, string key)
        {
            for (int i = 0; i < map.Lairs.Count; i++) if (map.Lairs[i].Key == key) return i;
            throw new InvalidOperationException($"[NamedCreatureValidator] FAILED: Floor 1 has no {key}.");
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

        private static Fixture World(ulong seed = Seed)
        {
            var sectors = new SectorRegistry();
            sectors.Load(Floor1Layout.Definition);
            var registry = new WorldRegistry();
            var clock = new SimulationClock();
            var chronicle = new NarrativeChronicleService();
            var sim = new SimulationManager(registry, null, clock: clock, chronicleService: chronicle, sectors: sectors);
            sim.SetEconomy(Simpiens.Simulation.Economy.EconomyTuning.Default);
            sim.PopulateLairs(seed);
            clock.AdvanceTicks(100);
            return new Fixture { Sim = sim, Clock = clock, Registry = registry, Chronicle = chronicle, Identity = new AgentIdentityService() };
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("[NamedCreatureValidator] FAILED: " + message);
        }
    }
}
