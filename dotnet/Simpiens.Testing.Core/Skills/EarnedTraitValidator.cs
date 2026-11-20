using System;
using System.Collections.Generic;
using Simpiens.Cognition.Contracts;
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
    /// Earned traits (Epic 11, S6.3, A1, A2, D29), a test per kind: the table (ids, words in Latin-1, a deed and a count, effects
    /// within bounds on work they can change); the rule (the count reached, the nature asked, once, three at most); a help
    /// through the techniques' door (Battle-Hardened's defense in a fight and nowhere else, Lifesaver's at tending, Patient
    /// Teacher's anywhere, summed with a technique within bounds); a hurt (Shaken's lair judged a quarter more dangerous, its
    /// blows weaker); and the live path, a cautious crawler's downing survived earning Shaken, told once. Runs as a test
    /// (ValidatorTests).
    /// </summary>
    public class EarnedTraitValidator
    {
        private static readonly AgentPersonality Bold = new AgentPersonality(0f, 0.6f, 0f, 0f, 0f);
        private static readonly AgentPersonality Cautious = new AgentPersonality(0f, -0.6f, 0f, 0f, 0f);
        private static readonly AgentPersonality Even = new AgentPersonality(0f, 0f, 0f, 0f, 0f);

        public void RunAllValidationTests()
        {
            TestLog.Log("[EarnedTraitValidator] Beginning earned trait validation...");
            ValidateTheTable();
            ValidateTheRule();
            ValidateAHelpThroughTheDoor();
            ValidateAHurt();
            ValidateADowningSurvivedEarnsOne();
            TestLog.Log("<color=green>[EarnedTraitValidator] ALL 5 TESTS PASSED SUCCESSFULLY!</color>");
        }

        private static void ValidateTheTable()
        {
            var words = new HashSet<string>();
            foreach (var row in EarnedTraitTable.All)
            {
                Assert(row.Id != EarnedTrait.None && EarnedTraitTable.Get(row.Id) == row && EarnedTraitTable.NameOf(row.Id) == row.Name, $"{row.Name}: found by its id.");
                Assert(!string.IsNullOrEmpty(row.Words) && words.Add(row.Words), $"{row.Name}: words of its own.");
                foreach (char c in row.Words + row.EffectWords) Assert(c < 256, $"{row.Name}: Latin-1, as the fonts are.");
                Assert(DeedTable.Get(row.Deed) != null && row.Count >= 1, $"{row.Name}: a deed and a count.");
                Assert(row.Effects.Count >= 1, $"{row.Name}: an effect.");
                foreach (var effect in row.Effects)
                {
                    var (min, max) = TechniqueRules.Bounds(effect.Stat);
                    Assert(effect.Value >= min && effect.Value <= max, $"{row.Name}: {effect.Stat} within its bounds.");
                    if (row.Uses != null)
                        foreach (var use in row.Uses) Assert(TechniqueRules.AppliesTo(effect.Stat, use), $"{row.Name}: {effect.Stat} can change {use}.");
                }
            }
            foreach (EarnedTrait id in Enum.GetValues(typeof(EarnedTrait))) Assert(id == EarnedTrait.None || EarnedTraitTable.Get(id) != null, $"Trait {id} has a row.");
            Assert(Has(TraitKind.Help) && Has(TraitKind.Hurt), "Traits that help and traits that hurt.");
            TestLog.Log($"[PASS] Test 1: {EarnedTraitTable.All.Count} earned traits, each a deed and a count, its effects within bounds on work they can change.");
        }

        private static bool Has(TraitKind kind)
        {
            foreach (var row in EarnedTraitTable.All) if (row.Kind == kind) return true;
            return false;
        }

        private static void ValidateTheRule()
        {
            // A downing survived: the cautious are Shaken at the first; the bold Battle-Hardened at the second; the even neither
            var cautious = Counted(Deed.DowningSurvived, 1, Cautious);
            Assert(cautious.HasTrait(EarnedTrait.Shaken) && cautious.TraitCount == 1 && (cautious.News & LegendNews.Trait) != 0, "Cautious, one downing survived: Shaken, with news.");
            var bold = Counted(Deed.DowningSurvived, 1, Bold);
            Assert(bold.TraitCount == 0, "Bold, one downing survived: nothing yet.");
            bold = Counted(Deed.DowningSurvived, 2, Bold);
            Assert(bold.HasTrait(EarnedTrait.BattleHardened) && !bold.HasTrait(EarnedTrait.Shaken), "Bold, two: Battle-Hardened.");
            Assert(Counted(Deed.DowningSurvived, 3, Even).TraitCount == 0, "Even-tempered: neither.");
            var hunter = Counted(Deed.CreatureSlain, 9, Even);
            Assert(!hunter.HasTrait(EarnedTrait.MonsterHunter), "Nine kills: not yet.");
            hunter = Counted(Deed.CreatureSlain, 12, Even);
            Assert(hunter.HasTrait(EarnedTrait.MonsterHunter) && hunter.TraitCount == 1, "The tenth: Monster Hunter, once.");

            // Three at most, never twice
            var full = default(CrawlerTraits);
            Assert(full.AddTrait(EarnedTrait.Lifesaver) && !full.AddTrait(EarnedTrait.Lifesaver), "Never twice.");
            Assert(full.AddTrait(EarnedTrait.PatientTeacher) && full.AddTrait(EarnedTrait.MonsterHunter) && full.TraitCount == 3, "Three held.");
            Assert(!full.AddTrait(EarnedTrait.Shaken) && full.TraitCount == CrawlerTraits.EarnedTraitSlots && full.TraitAt(3) == EarnedTrait.None, "A fourth isn't taken.");
            TestLog.Log("[PASS] Test 2: earned at the count, by nature where a row asks it, once, three at most.");
        }

        private static CrawlerTraits Counted(Deed deed, int times, in AgentPersonality personality)
        {
            var traits = default(CrawlerTraits);
            for (int i = 0; i < times; i++)
            {
                traits.Add(deed);
                EarnedTraitRules.Judge(ref traits, deed, personality);
            }
            return traits;
        }

        private static void ValidateAHelpThroughTheDoor()
        {
            var skills = default(SkillSet);
            var hardened = default(CrawlerTraits);
            hardened.AddTrait(EarnedTrait.BattleHardened);
            Assert(CrawlerTechniques.Get(hardened, skills, SkillWork.Fight(ranged: false), TechniqueStat.Defense) == 1
                && CrawlerTechniques.Get(hardened, skills, SkillWork.Fight(ranged: true), TechniqueStat.Defense) == 1, "Battle-Hardened: +1 defense in a fight, in reach or from range.");
            Assert(CrawlerTechniques.Get(hardened, skills, SkillWork.Harvest(ItemType.IronOre), TechniqueStat.Defense) == 0, "Not at a seam.");
            var lifesaver = default(CrawlerTraits);
            lifesaver.AddTrait(EarnedTrait.Lifesaver);
            Assert(CrawlerTechniques.Get(lifesaver, skills, SkillWork.Tend(), TechniqueStat.Pace) == 15 && CrawlerTechniques.Get(lifesaver, skills, SkillWork.Tend(), TechniqueStat.Heal) == 10, "Lifesaver: carries faster and heals more, at tending.");
            Assert(CrawlerTechniques.Get(lifesaver, skills, SkillWork.Fight(ranged: false), TechniqueStat.Pace) == 0, "Not in a fight.");
            var teacher = default(CrawlerTraits);
            teacher.AddTrait(EarnedTrait.PatientTeacher);
            Assert(CrawlerTechniques.Get(teacher, skills, SkillWork.Harvest(ItemType.IronOre), TechniqueStat.Teach) == 10 && CrawlerTechniques.Get(teacher, skills, SkillWork.Fight(ranged: false), TechniqueStat.Teach) == 10, "Patient Teacher: in any work.");

            // Summed with a technique, within bounds: Shield Wall's +3 and Battle-Hardened's +1
            var both = hardened;
            both.SetTechnique(Skill.Melee, SkillBand.Apprentice, Technique.ShieldWall);
            Assert(CrawlerTechniques.Get(both, skills, SkillWork.Fight(ranged: false), TechniqueStat.Defense) == 4, "Shield Wall and Battle-Hardened: +4.");
            TestLog.Log("[PASS] Test 3: a help through the techniques' door, on its own work only, summed with techniques.");
        }

        private static void ValidateAHurt()
        {
            var skills = default(SkillSet);
            skills.Set(Skill.Melee, 5);
            var worn = new Simpiens.Cognition.Inventory.AgentInventory();
            worn.TryEquip(Simpiens.Simulation.Items.Outfitting.NewPiece(ItemType.IronBlade, Simpiens.Simulation.Items.Grades.Standard));
            var lair = new LairDefinition("rats", CreatureKind.BurrowRat, Vec2.Zero);
            var even = default(CrawlerTraits);
            var shaken = default(CrawlerTraits);
            shaken.AddTrait(EarnedTrait.Shaken);
            float calm = Simpiens.Cognition.Vitality.HarmToClear(lair, false, skills, worn, even);
            float feared = Simpiens.Cognition.Vitality.HarmToClear(lair, false, skills, worn, shaken);
            // Shaken's blows are weaker too, so the lair takes longer: at least a quarter more, never less
            Assert(calm > 0f && feared >= calm * 1.25f - 0.01f, $"Shaken judges a lair at least a quarter more dangerous: {calm:F1} against {feared:F1}.");
            var calmFighter = Simpiens.Simulation.Combat.Combatant.ForCrawler(default, default, 0f, 100f, true, skills, worn, even);
            var shakenFighter = Simpiens.Simulation.Combat.Combatant.ForCrawler(default, default, 0f, 100f, true, skills, worn, shaken);
            Assert(shakenFighter.Attack < calmFighter.Attack, "Its blows land weaker.");
            TestLog.Log($"[PASS] Test 4: Shaken, a hurt: a rats' lair judged {feared / calm:F2}x as dangerous, its blows weaker.");
        }

        private static void ValidateADowningSurvivedEarnsOne()
        {
            var world = new WorldRegistry();
            var clock = new SimulationClock();
            var chronicle = new NarrativeChronicleService();
            var sim = new SimulationManager(world, null, clock: clock, chronicleService: chronicle) { Seed = 7 };
            sim.SetEconomy(EconomyTuning.Default);
            clock.AdvanceTicks(100);
            var crawler = HeadlessEntities.Crawler(world, sim, clock, Vec2.Zero, id: EntityId.Generate());
            var agent = crawler.Agent;
            agent.Personality = Cautious;
            agent.IsDowned = true;
            agent.DownedBy = DeathCause.Hazard;
            agent.DownedDetail = (byte)HazardKind.FallingRock;
            agent.BleedsOutAt = (uint)clock.CurrentTick + 1000u;
            agent.IsDowned = false;
            agent.Health = 10f;
            sim.TickCreatures((uint)clock.CurrentTick);
            Assert(agent.Traits.HasTrait(EarnedTrait.Shaken) && agent.Traits.ScarAt(0) == Scar.Rockfall, "A cautious crawler up from a rockfall: Shaken, and a rockfall's mark.");
            crawler.Tick(clock.CurrentTick);
            var events = new List<ChronicleEvent>();
            chronicle.GetAllRecentEvents(events, 64);
            var told = events.FindAll(e => e.EventType == ChronicleEventType.TraitEarned && e.ActorAId == crawler.Id);
            Assert(told.Count == 1 && agent.Traits.News == LegendNews.None, "Told once by its runtime.");
            string line = new TemplateNarrativeFormatter().FormatEvent(told[0]);
            Assert(line.Contains("Shaken") && line.Contains("a downing survived"), $"The chronicle says it: \"{line}\".");
            TestLog.Log($"[PASS] Test 5: a downing survived earns a cautious crawler Shaken, told once: \"{line}\"");
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("[EarnedTraitValidator] FAILED: " + message);
        }
    }
}
