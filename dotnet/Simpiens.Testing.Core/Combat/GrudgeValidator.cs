using System;
using System.Collections.Generic;
using Simpiens.Cognition.Social;
using Simpiens.Core.Session.Layouts;
using Simpiens.Narrative;
using Simpiens.Simulation;
using Simpiens.Simulation.Combat;
using Simpiens.Simulation.Creatures;
using Simpiens.Simulation.Skills;
using Simpiens.Simulation.Spatial;
using Simpiens.Simulation.World;
using Simpiens.Testing.Environment;

namespace Simpiens.Testing.Combat
{
    /// <summary>
    /// Grudges both ways (Epic 11, S7.3, D33), on Floor 1's lairs through the simulation's own blows: a named creature remembers
    /// the last crawler that struck it and lived (an unnamed one doesn't) and, choosing its quarry, sees that foe nearer; a friend
    /// of the crawler a named creature downs takes a grudge with the trait its act earns, told once, by warmth (not a boss's
    /// downing, not with every trait slot taken); the grudge's lair braved with a quarter less to spare, only that lair while its
    /// foe leads it, and only by the bold and armed; the grudge settled when its creature is slain. Runs as a test
    /// (ValidatorTests).
    /// </summary>
    public class GrudgeValidator
    {
        private const ulong Seed = 424242;

        public void RunAllValidationTests()
        {
            TestLog.Log("[GrudgeValidator] Beginning grudge validation...");
            ValidateTheFoe();
            ValidateTheFoesDraw();
            ValidateAFriendTakesAGrudge();
            ValidateTheEasedBar();
            ValidateTheGrudgeSettled();
            TestLog.Log("<color=green>[GrudgeValidator] ALL 5 TESTS PASSED SUCCESSFULLY!</color>");
        }

        private static void ValidateTheFoe()
        {
            EntityId StruckBy(bool named)
            {
                var w = World();
                var beetle = First(w.Sim, CreatureKind.StoneBeetle);
                if (named) w.Down(w.Crawler(beetle.Position, "Ama"), beetle.Id);
                var bram = w.Crawler(beetle.Position + new Vec2(0.6f, 0f), "Bram");
                var skills = default(SkillSet);
                skills.Set(Skill.Melee, 8);
                bram.Agent.Skills = skills;
                bram.Agent.Inventory.TryEquip(Simpiens.Simulation.Items.Outfitting.NewPiece(Simpiens.Simulation.Economy.ItemType.IronBlade, Simpiens.Simulation.Items.Grades.Standard));
                float whole = beetle.Health;
                for (int t = 0; t < 600 && beetle.Health >= whole && beetle.IsAlive; t++)
                {
                    bram.Agent.Health = 100f; // it stands through the beetle's answer
                    w.Clock.AdvanceTicks(1);
                    w.Sim.SwingAtCreature(bram.Node, beetle.Id);
                    w.Sim.TickCreatures((uint)w.Clock.CurrentTick);
                }
                Assert(beetle.IsAlive && beetle.Health < whole, "Bram's blow landed and the beetle lived.");
                return beetle.Foe == bram.Node.Id ? bram.Node.Id : beetle.Foe;
            }
            Assert(StruckBy(named: true) != default, "A named beetle remembers Bram, who struck it and lived.");
            Assert(StruckBy(named: false) == default, "An unnamed one keeps no foe.");
            TestLog.Log("[PASS] Test 1: a named creature remembers the last crawler that struck it; an unnamed one doesn't.");
        }

        private static void ValidateTheFoesDraw()
        {
            EntityId Chases(bool foe)
            {
                var w = World();
                var beetle = First(w.Sim, CreatureKind.StoneBeetle);
                w.Down(w.Crawler(beetle.Position, "Ama"), beetle.Id);
                var near = w.Crawler(beetle.Position + new Vec2(2f, 0f), "Near");
                var far = w.Crawler(beetle.Position + new Vec2(0f, 3.5f), "Far"); // within its sight, nearer drawn by 3 as its foe
                beetle.Mode = CreatureMode.Rest;
                beetle.RestUntil = long.MaxValue;
                if (foe) beetle.Foe = far.Node.Id;
                w.Sim.TickCreatures((uint)w.Clock.CurrentTick + 1u);
                return beetle.Mode == CreatureMode.Chase ? beetle.Target : default;
            }
            var w0 = Chases(foe: false);
            var w1 = Chases(foe: true);
            Assert(w0 != default && w1 != default && w0 != w1, "Without a foe it chases the nearer; with one, its foe, farther off.");
            TestLog.Log($"[PASS] Test 2: noticing quarry, it sees its foe {CreatureService.FoeDraw:F0} units nearer.");
        }

        private static void ValidateAFriendTakesAGrudge()
        {
            var w = World();
            var beetle = First(w.Sim, CreatureKind.StoneBeetle);
            var ama = w.Crawler(beetle.Position, "Ama");
            var bram = w.Crawler(Vec2.Zero, "Bram");
            var cora = w.Crawler(Vec2.Zero, "Cora");
            var dov = w.Crawler(Vec2.Zero, "Dov");
            bram.Agent.Memory.SetAffinity(ama.Node.Id, new AffinityRecord(60, 50, 0, 0, 5, 0));
            cora.Agent.Memory.SetAffinity(ama.Node.Id, new AffinityRecord(20, 50, 0, 0, 5, 0));
            dov.Agent.Memory.SetAffinity(ama.Node.Id, new AffinityRecord(80, 50, 0, 0, 5, 0));
            var full = dov.Agent.Traits; // three traits held: no room to hate
            full.AddTrait(EarnedTrait.Lifesaver);
            full.AddTrait(EarnedTrait.PatientTeacher);
            full.AddTrait(EarnedTrait.MonsterHunter);
            full.ClearNews(LegendNews.Trait);
            dov.Agent.Traits = full;

            w.Down(ama, beetle.Id); // named by Ama's downing, and Ama's friends hate it
            int hollow = beetle.LairIndex;
            Assert(EarnedTraitRules.HoldsAct(bram.Agent.Traits, TraitAct.FriendFelled) && bram.Agent.Traits.Grudge == beetle.Id && bram.Agent.Traits.GrudgeLair == hollow,
                "Bram, Ama's friend (warmth 60), hates the beetle that downed Ama, in its lair.");
            Assert(cora.Agent.Traits.Grudge == default && !EarnedTraitRules.HoldsAct(cora.Agent.Traits, TraitAct.FriendFelled), "Cora, warmth 20, doesn't.");
            Assert(dov.Agent.Traits.Grudge == default && dov.Agent.Traits.TraitCount == 3, "Dov, every slot taken, has no room to hate.");
            Assert(ama.Agent.Traits.Grudge == default, "Ama, downed, holds none.");

            bram.Runtime.Tick(w.Clock.CurrentTick);
            var told = w.Told(ChronicleEventType.TraitEarned).FindAll(e => e.ActorAId == bram.Node.Id);
            Assert(told.Count == 1 && told[0].ActorBId == beetle.Id && bram.Agent.Traits.News == LegendNews.None, "Told once by its runtime, naming its foe.");
            w.Identity.RegisterCustomName(beetle.Id, "Ama's Bane");
            string line = w.Line(told[0]);
            Assert(line.Contains("Bram is Vengeful now") && line.Contains("Ama's Bane felled a friend"), $"The chronicle says it: \"{line}\".");

            // A boss's downing kindles nothing: she has no name to hate
            var w2 = World();
            var matriarch = First(w2.Sim, CreatureKind.Matriarch);
            var eli = w2.Crawler(matriarch.Position, "Eli");
            var fen = w2.Crawler(Vec2.Zero, "Fen");
            fen.Agent.Memory.SetAffinity(eli.Node.Id, new AffinityRecord(90, 50, 0, 0, 5, 0));
            w2.Down(eli, matriarch.Id);
            Assert(fen.Agent.Traits.Grudge == default, "Downed by the Matriarch: no grudge.");
            TestLog.Log($"[PASS] Test 3: a friend at warmth {Simpiens.Cognition.Vitality.GrudgeWarmth} takes a grudge, told: \"{line}\"");
        }

        private static void ValidateTheEasedBar()
        {
            var map = Floor1Layout.Definition;
            int hollow = LairIndex(map, Floor1Layout.BeetleHollow), warren = LairIndex(map, Floor1Layout.RatWarren);
            var lair = map.Lairs[hollow];
            var skills = default(SkillSet);
            skills.Set(Skill.Melee, 5);
            var armed = new Simpiens.Cognition.Inventory.AgentInventory();
            armed.TryEquip(Simpiens.Simulation.Items.Outfitting.NewPiece(Simpiens.Simulation.Economy.ItemType.IronBlade, Simpiens.Simulation.Items.Grades.Standard));
            var unarmed = new Simpiens.Cognition.Inventory.AgentInventory();
            var traits = default(CrawlerTraits);
            float harm = Simpiens.Cognition.Vitality.HarmToClear(lair, false, skills, armed, traits, 1);
            float health = (float)(Simpiens.Cognition.Vitality.RetreatBelow + harm * 0.875f); // between the eased and the plain bar
            Assert(!Simpiens.Cognition.Vitality.BravesLair(lair, false, skills, armed, traits, health, 100, false, 1)
                && Simpiens.Cognition.Vitality.BravesLair(lair, false, skills, armed, traits, health, 100, false, 1, grudge: true), "Braved with a grudge; not without.");
            Assert(!Simpiens.Cognition.Vitality.BravesLair(lair, false, skills, unarmed, traits, health, 100, false, 1, grudge: true), "Not by the unarmed.");
            Assert(!Simpiens.Cognition.Vitality.BravesLair(lair, false, skills, armed, traits, health, 0, false, 1, grudge: true), "Not by the cautious.");

            // Only its grudge's lair, while a named creature leads it, as the snapshot says
            traits.SetGrudge(new EntityId(5u, 5u, 5u, 5u), hollow);
            var snapshot = new SharedWorldSnapshot(8, 8, 8f, 4, null);
            snapshot.SetMap(map);
            snapshot.SetNamedLairs(1UL << hollow, 0UL);
            Assert(Simpiens.Cognition.Vitality.IsGrudgeLair(traits, snapshot, hollow) && !Simpiens.Cognition.Vitality.IsGrudgeLair(traits, snapshot, warren), "Its grudge's lair, not the warren.");
            snapshot.SetNamedLairs(0UL, 0UL);
            Assert(!Simpiens.Cognition.Vitality.IsGrudgeLair(traits, snapshot, hollow), "Not once no named creature leads it.");
            Assert(!Simpiens.Cognition.Vitality.IsGrudgeLair(default, snapshot, hollow), "Not without a grudge.");
            TestLog.Log($"[PASS] Test 4: the grudge's lair braved with a quarter less to spare (harm judged at {Simpiens.Cognition.Vitality.GrudgeEase:P0}), only it, only by the bold and armed.");
        }

        private static void ValidateTheGrudgeSettled()
        {
            var w = World();
            var beetle = First(w.Sim, CreatureKind.StoneBeetle);
            var ama = w.Crawler(beetle.Position, "Ama");
            var bram = w.Crawler(Vec2.Zero, "Bram");
            bram.Agent.Memory.SetAffinity(ama.Node.Id, new AffinityRecord(60, 50, 0, 0, 5, 0));
            w.Down(ama, beetle.Id);
            Assert(bram.Agent.Traits.Grudge == beetle.Id, "Bram hates the beetle.");
            uint tick = (uint)w.Clock.CurrentTick;
            w.Sim.Creatures.Slay(w.Sim, beetle.Id, tick);
            ((ICreatureWorld)w.Sim).CreatureSlain(bram.Node.Id, beetle, ReadOnlySpan<Simpiens.Simulation.Economy.ItemInstance>.Empty, tick);
            Assert(bram.Agent.Traits.Grudge == default && bram.Agent.Traits.GrudgeLair == -1 && EarnedTraitRules.HoldsAct(bram.Agent.Traits, TraitAct.FriendFelled),
                "Slain, the grudge settled; the trait kept.");
            TestLog.Log("[PASS] Test 5: the grudge settled when its creature is slain.");
        }

        private static int LairIndex(MapDefinition map, string key)
        {
            for (int i = 0; i < map.Lairs.Count; i++) if (map.Lairs[i].Key == key) return i;
            throw new InvalidOperationException($"[GrudgeValidator] FAILED: Floor 1 has no {key}.");
        }

        private static CreatureState First(SimulationManager sim, CreatureKind kind)
        {
            foreach (var creature in sim.Creatures.Creatures)
                if (creature.Kind == kind) return creature;
            throw new InvalidOperationException($"[GrudgeValidator] FAILED: Floor 1 holds no {kind} on seed {Seed}.");
        }

        private sealed class Fixture
        {
            public SimulationManager Sim;
            public SimulationClock Clock;
            public WorldRegistry Registry;
            public NarrativeChronicleService Chronicle;
            public AgentIdentityService Identity;

            public TestCrawler Crawler(Vec2 at, string name)
            {
                var crawler = HeadlessEntities.Crawler(Registry, Sim, Clock, at, id: EntityId.Generate(), name: name);
                crawler.Agent.Health = 100f;
                Identity.RegisterCustomName(crawler.Node.Id, name);
                return crawler;
            }

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
            if (!condition) throw new InvalidOperationException("[GrudgeValidator] FAILED: " + message);
        }
    }
}
