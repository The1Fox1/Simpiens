using System;
using System.Collections.Generic;
using Simpiens.Narrative;
using Simpiens.Simulation;
using Simpiens.Simulation.Creatures;
using Simpiens.Simulation.Economy;
using Simpiens.Simulation.Skills;
using Simpiens.Testing.Environment;

namespace Simpiens.Testing.Skills
{
    /// <summary>
    /// Epithets (Epic 11, S6.1, A2, D27): the table (ids, words in Latin-1, a deed and a count each, ranks, every form, every
    /// kind's word); the rule (earned at the deed that reaches its count, the highest rank held, the same rank never replacing,
    /// a kind's word from the kill that earned it, a crawler's name kept); the words and a name with its epithet; the live path,
    /// a crawler's third kill earning a kind's word and its runtime telling the chronicle once; and the record keeping it all
    /// within its 256 bytes. Runs as a test (ValidatorTests).
    /// </summary>
    public class EpithetValidator
    {
        public void RunAllValidationTests()
        {
            TestLog.Log("[EpithetValidator] Beginning epithet validation...");
            ValidateTheTable();
            ValidateTheRule();
            ValidateTheWords();
            ValidateAKillEarnsOneAndItIsTold();
            ValidateTheSkillsVoices();
            TestLog.Log("<color=green>[EpithetValidator] ALL 5 TESTS PASSED SUCCESSFULLY!</color>");
        }

        private static void ValidateTheTable()
        {
            var all = EpithetTable.All;
            Assert(all.Count >= 8, $"The first eight epithets (D27): {all.Count}.");
            var seen = new HashSet<Epithet>();
            var words = new HashSet<string>();
            foreach (var row in all)
            {
                Assert(row.Id != Epithet.None && seen.Add(row.Id), $"{row.Name}: an id of its own.");
                Assert(EpithetTable.Get(row.Id) == row && EpithetTable.NameOf(row.Id) == row.Name, $"{row.Name} is found by its id.");
                Assert(DeedTable.Get(row.Deed) != null && row.Count >= 1 && row.Rank >= 1, $"{row.Name}: a deed, a count and a rank.");
                Assert(row.Form == EpithetForm.KindWord ? row.Words == null : !string.IsNullOrEmpty(row.Words) && words.Add(row.Words), $"{row.Name}: its own words, unless a kind's.");
                if (row.Form == EpithetForm.CrawlerName) Assert(row.Words.Contains("{0}"), $"{row.Name}: a place for the name.");
                foreach (char c in row.Words ?? string.Empty) Assert(c < 256, $"{row.Name}: Latin-1, as the fonts are.");
            }
            foreach (Epithet id in Enum.GetValues(typeof(Epithet))) Assert(id == Epithet.None || EpithetTable.Get(id) != null, $"Epithet {id} has a row.");
            foreach (var kind in CreatureCatalog.All)
            {
                Assert(!string.IsNullOrEmpty(kind.SlayerEpithet), $"{kind.Name}: a slayer's word.");
                foreach (char c in kind.SlayerEpithet) Assert(c < 256, $"{kind.Name}: its word in Latin-1.");
            }
            Assert(EpithetTable.Get(Epithet.None) == null, "No epithet, no row.");
            TestLog.Log($"[PASS] Test 1: {all.Count} epithets, each a deed, a count and a rank; every creature kind has its slayer's word.");
        }

        private static void ValidateTheRule()
        {
            byte beetle = (byte)CreatureKind.StoneBeetle, rat = (byte)CreatureKind.BurrowRat, matriarch = (byte)CreatureKind.Matriarch;
            var traits = default(CrawlerTraits);
            int kindCount = EpithetTable.Get(Epithet.KindSlayer).Count;
            for (int i = 1; i < kindCount; i++) Count(ref traits, Deed.CreatureSlain, rat);
            Assert(traits.Epithet == Epithet.None && traits.News == LegendNews.None, $"Short of {kindCount} kills: none.");
            Count(ref traits, Deed.CreatureSlain, beetle);
            Assert(traits.Epithet == Epithet.KindSlayer && traits.EpithetKind == beetle && (traits.News & LegendNews.Epithet) != 0, "The kill that reaches the count earns the kind's word: that kill's kind, with news.");
            traits.ClearNews(LegendNews.Epithet);
            Count(ref traits, Deed.CreatureSlain, rat);
            Assert(traits.Epithet == Epithet.KindSlayer && traits.EpithetKind == beetle && traits.News == LegendNews.None, "Past the count, it keeps the word it earned.");

            // A higher rank replaces it; the same rank or lower never does
            int teach = EpithetTable.Get(Epithet.Teacher).Count;
            for (int i = 0; i < teach; i++) Count(ref traits, Deed.Taught, 0);
            Assert(traits.Epithet == Epithet.KindSlayer, "The Teacher (the same rank) doesn't replace Beetlebane.");
            for (int i = 0; i < EpithetTable.Get(Epithet.Lairbreaker).Count; i++) Count(ref traits, Deed.ClearWon, 0);
            Assert(traits.Epithet == Epithet.Lairbreaker && traits.EpithetKind == 0, "Lairbreaker (a higher rank) does, and names no kind.");
            Count(ref traits, Deed.BossSlain, matriarch);
            Assert(traits.Epithet == Epithet.BossSlayer && traits.EpithetKind == matriarch, "A boss slain: the boss's word, the highest yet.");
            for (int i = 0; i < 40; i++) Count(ref traits, Deed.Make, 0);
            Assert(traits.Epithet == Epithet.BossSlayer, "The Maker (rank 1) never replaces it.");
            Assert(EpithetRules.Judge(ref traits, Deed.None, 0, default) == false, "No deed, no epithet.");
            TestLog.Log("[PASS] Test 2: earned at the deed that reaches its count, the kill's kind named; the highest rank held, the same rank never replacing.");
        }

        private static void ValidateTheWords()
        {
            Assert(EpithetRules.WordsOf(Epithet.KindSlayer, (byte)CreatureKind.StoneBeetle, null) == "Beetlebane", "A beetle's slayer: Beetlebane.");
            Assert(EpithetRules.WordsOf(Epithet.Steadfast, 0, null) == "the Steadfast", "Plain words.");
            Assert(EpithetRules.WordsOf(Epithet.None, 0, null) == string.Empty, "None, no words.");
            Assert(EpithetRules.NameWith("Corrin", "Beetlebane") == "Corrin Beetlebane", "A name with its epithet.");
            Assert(EpithetRules.NameWith("Corrin", "Beetlebane", "Warden") == "Corrin Beetlebane the Warden", "With a title.");
            Assert(EpithetRules.NameWith("Mara", "the Steadfast", "Warden") == "Mara the Steadfast, Warden", "After an epithet with \"the\", a comma.");
            Assert(EpithetRules.NameWith("Ama", string.Empty, "Warden") == "Ama the Warden" && EpithetRules.NameWith("Ama", null) == "Ama", "No epithet, the name and title as before.");
            Assert(DeedTable.Get(Deed.BossSlain).CountOf(1) == "a boss slain" && DeedTable.Get(Deed.CreatureSlain).CountOf(3) == "three creatures slain", "A deed's count in words.");
            TestLog.Log("[PASS] Test 3: the words, a kind's word, and a name with its epithet and title.");
        }

        private static void ValidateAKillEarnsOneAndItIsTold()
        {
            var world = new WorldRegistry();
            var clock = new SimulationClock();
            var chronicle = new NarrativeChronicleService();
            var sim = new SimulationManager(world, null, clock: clock, chronicleService: chronicle) { Seed = 7 };
            sim.SetEconomy(EconomyTuning.Default);
            clock.AdvanceTicks(100);
            var crawler = HeadlessEntities.Crawler(world, sim, clock, Vec2.Zero, id: EntityId.Generate());
            var creatures = (ICreatureWorld)sim;
            int count = EpithetTable.Get(Epithet.KindSlayer).Count;
            for (int i = 0; i < count; i++)
                creatures.CreatureSlain(crawler.Id, new CreatureState(EntityId.Generate(), CreatureKind.StoneBeetle, -1, 0, Vec2.Zero), ReadOnlySpan<ItemInstance>.Empty, (uint)clock.CurrentTick);
            var traits = crawler.Agent.Traits;
            Assert(traits.Count(Deed.CreatureSlain) == count && traits.Epithet == Epithet.KindSlayer && traits.EpithetKind == (byte)CreatureKind.StoneBeetle, $"{count} beetles slain: Beetlebane.");
            int Told()
            {
                var all = new List<ChronicleEvent>();
                chronicle.GetAllRecentEvents(all, 64);
                return all.FindAll(e => e.EventType == ChronicleEventType.EpithetEarned && e.ActorAId == crawler.Id).Count;
            }
            Assert(Told() == 0, "Not told before its runtime runs.");
            crawler.Tick(clock.CurrentTick);
            crawler.Tick(clock.CurrentTick + 1);
            Assert(Told() == 1 && crawler.Agent.Traits.News == LegendNews.None, "Its runtime tells it once, and clears the news.");
            var told = new List<ChronicleEvent>();
            chronicle.GetAllRecentEvents(told, 64);
            var line = new TemplateNarrativeFormatter().FormatEvent(told.Find(e => e.EventType == ChronicleEventType.EpithetEarned));
            Assert(line.Contains("Beetlebane") && line.Contains("three creatures slain"), $"The chronicle says it: \"{line}\".");
            Assert(System.Runtime.CompilerServices.Unsafe.SizeOf<CrawlerTraits>() == CrawlerTraits.Size, "The record keeps its 256 bytes.");
            TestLog.Log($"[PASS] Test 4: three beetles slain earn Beetlebane, told once by the runtime: \"{line}\"");
        }

        // The skills' voices (Epic 11, S6.4, D30): every skill speaks, in Latin-1; a band reached in its voice, an epithet and first
        // blood in the crawler's best skill's; plain where the event names none; and the first kill told as first blood, once
        private static void ValidateTheSkillsVoices()
        {
            foreach (var skill in SkillTable.All)
            {
                var voice = skill.Voice;
                Assert(voice != null, $"{skill.Name} has a voice.");
                foreach (var words in new[] { voice.Rising, voice.Named, voice.Blooded })
                {
                    Assert(words.Contains("{0}"), $"{skill.Name}: \"{words}\" names the crawler.");
                    foreach (char c in words) Assert(c < 256, $"{skill.Name}: Latin-1.");
                }
                Assert(!string.IsNullOrEmpty(voice.FirstKill), $"{skill.Name}: a first kill's line.");
            }
            var formatter = new TemplateNarrativeFormatter();
            var who = EntityId.Generate();
            string band = formatter.FormatEvent(new ChronicleEvent(ChronicleEventType.SkillBandReached, who, default, 0, 1, ChronicleIntensity.Notable, (int)Skill.Mining * 256 + (int)SkillBand.Journeyman));
            Assert(band.EndsWith("'s pick rings truer: a journeyman miner now."), $"A band reached in the skill's voice: \"{band}\".");
            string named = formatter.FormatEvent(new ChronicleEvent(ChronicleEventType.EpithetEarned, who, default, 0, 1, ChronicleIntensity.Dramatic,
                (int)Skill.Melee * 65536 + (int)Epithet.KindSlayer * 256 + (int)CreatureKind.StoneBeetle));
            Assert(named.Contains("'s blade has won a name: ") && named.EndsWith(" Beetlebane, for three creatures slain."), $"An epithet in the best skill's voice: \"{named}\".");
            string plain = formatter.FormatEvent(new ChronicleEvent(ChronicleEventType.EpithetEarned, who, default, 0, 1, ChronicleIntensity.Dramatic, (int)Epithet.Steadfast * 256));
            Assert(plain.Contains(" is ") && plain.EndsWith(" the Steadfast now, for two rescues."), $"With no skill, plainly: \"{plain}\".");
            string blooded = formatter.FormatEvent(new ChronicleEvent(ChronicleEventType.FirstBlood, who, default, 0, 1, ChronicleIntensity.Dramatic, (int)CreatureKind.BurrowRat + 256 * (int)Skill.Ranged));
            Assert(blooded.Contains("'s first shot to kill flew true: a burrow rat, in "), $"First blood in the best skill's voice: \"{blooded}\".");

            // The first kill is first blood, told once; the second isn't
            var world = new WorldRegistry();
            var clock = new SimulationClock();
            var chronicle = new NarrativeChronicleService();
            var sim = new SimulationManager(world, null, clock: clock, chronicleService: chronicle) { Seed = 7 };
            sim.SetEconomy(EconomyTuning.Default);
            clock.AdvanceTicks(100);
            var crawler = HeadlessEntities.Crawler(world, sim, clock, Vec2.Zero, id: EntityId.Generate());
            var skills = default(SkillSet);
            skills.Set(Skill.Smithing, 4);
            crawler.Agent.Skills = skills;
            for (int i = 0; i < 2; i++)
                ((ICreatureWorld)sim).CreatureSlain(crawler.Id, new CreatureState(EntityId.Generate(), CreatureKind.BurrowRat, -1, 0, Vec2.Zero), ReadOnlySpan<ItemInstance>.Empty, (uint)clock.CurrentTick);
            var all = new List<ChronicleEvent>();
            chronicle.GetAllRecentEvents(all, 64);
            var first = all.FindAll(e => e.EventType == ChronicleEventType.FirstBlood && e.ActorAId == crawler.Id);
            Assert(first.Count == 1 && (int)first[0].ExtraData == (int)CreatureKind.BurrowRat + 256 * (int)Skill.Smithing, "The first kill is first blood, once, in its best skill (smithing).");
            Assert(CallingRules.BestSkill(skills) == Skill.Smithing && CallingRules.BestSkill(default) == Skill.None, "The best skill; none with nothing learned.");
            TestLog.Log($"[PASS] Test 5: every skill has a voice; \"{band}\" \"{named}\" \"{blooded}\"");
        }

        private static void Count(ref CrawlerTraits traits, Deed deed, byte kind)
        {
            traits.Add(deed);
            EpithetRules.Judge(ref traits, deed, kind, default);
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("[EpithetValidator] FAILED: " + message);
        }
    }
}
