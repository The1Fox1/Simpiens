using System;
using System.Collections.Generic;
using Simpiens.Narrative;
using Simpiens.Simulation;
using Simpiens.Simulation.Economy;
using Simpiens.Simulation.Skills;
using Simpiens.Testing.Environment;

namespace Simpiens.Testing.Skills
{
    /// <summary>
    /// The calling table (Epic 11, S5.2, A2, Q11, D20, D23): ids and words unique and Latin-1; every Apprentice technique of every
    /// skill earns exactly one calling of that skill; a skill without techniques has one calling earned by none (Leadership's
    /// Captain); the fighters', medics' and captain's roles; every Master title and capstone sound. A calling earned (S5.3,
    /// D21): the best skill decides, ties by experience and never flickering, a technique changed changes it, nothing below
    /// Apprentice; a band reached in practice judges it again and tells a change. Runs as a test (ValidatorTests).
    /// </summary>
    public class CallingValidator
    {
        public void RunAllValidationTests()
        {
            TestLog.Log("[CallingValidator] Beginning calling validation...");
            ValidateTheTable();
            ValidateWhatEarnsThem();
            ValidateRolesTitlesAndCapstones();
            ValidateACallingEarned();
            ValidateABandReachedJudgesItAgain();
            TestLog.Log("<color=green>[CallingValidator] ALL 5 TESTS PASSED SUCCESSFULLY!</color>");
        }

        private static void ValidateTheTable()
        {
            var all = CallingTable.All;
            Assert(all.Count == 15, $"Fifteen callings (D20): {all.Count}.");
            var names = new HashSet<string>();
            byte last = 0;
            foreach (var row in all)
            {
                Assert((byte)row.Id > last, $"{row.Name}: ids rise.");
                last = (byte)row.Id;
                Assert(!string.IsNullOrEmpty(row.DisplayName) && names.Add(row.DisplayName) && names.Add(row.MasterName), $"{row.Name}: its word and Master title its own.");
                foreach (char c in row.DisplayName + row.MasterName) Assert(c < 256, $"{row.Name}: Latin-1, as the fonts are.");
                Assert(SkillTable.IsDefined(row.Skill), $"{row.Name}: its skill is in the table.");
                Assert(CallingTable.Get(row.Id) == row && CallingTable.NameOf(row.Id) == row.Name, $"{row.Name} is found by its id.");
            }
            foreach (Calling id in Enum.GetValues(typeof(Calling)))
                Assert(id == Calling.None || CallingTable.Get(id) != null, $"Calling {id} has a row.");
            Assert(CallingTable.Get(Calling.None) == null && CallingTable.RoleOf(Calling.None) == PartyRole.None, "No calling, no row, no role.");
            TestLog.Log($"[PASS] Test 1: {all.Count} callings, ids and words unique, in Latin-1.");
        }

        private static void ValidateWhatEarnsThem()
        {
            foreach (var skill in SkillTable.All)
            {
                var options = TechniqueTable.Options(skill.Id, SkillBand.Apprentice);
                if (options.Count == 0)
                {
                    var any = CallingTable.For(skill.Id, Technique.None);
                    Assert(any != Calling.None && CallingTable.Get(any).EarnedBy == Technique.None, $"{skill.Name}, with no techniques, has a calling earned by none.");
                    continue;
                }
                var seen = new HashSet<Calling>();
                foreach (var technique in options)
                {
                    var calling = CallingTable.For(skill.Id, technique.Id);
                    Assert(calling != Calling.None, $"{technique.Name} earns a calling of {skill.Name}.");
                    var row = CallingTable.Get(calling);
                    Assert(row.Skill == skill.Id && row.EarnedBy == technique.Id, $"{technique.Name}'s calling, {row.Name}, names it.");
                    Assert(seen.Add(calling), $"{skill.Name}'s two Apprentice techniques earn two callings.");
                }
                Assert(CallingTable.For(skill.Id, Technique.None) == Calling.None, $"{skill.Name} with no technique at Apprentice earns none.");
            }
            foreach (var row in CallingTable.All)
            {
                if (row.EarnedBy == Technique.None) continue;
                var technique = TechniqueTable.Get(row.EarnedBy);
                Assert(technique != null && technique.Skill == row.Skill && technique.Band == SkillBand.Apprentice, $"{row.Name} is earned by an Apprentice technique of its own skill.");
            }
            TestLog.Log("[PASS] Test 2: each Apprentice technique earns one calling of its skill; Leadership's Captain is earned by none.");
        }

        private static void ValidateRolesTitlesAndCapstones()
        {
            Assert(CallingTable.RoleOf(Calling.Warden) == PartyRole.Front && CallingTable.RoleOf(Calling.Berserker) == PartyRole.Front, "The melee callings hold the front.");
            Assert(CallingTable.RoleOf(Calling.Marksman) == PartyRole.Back && CallingTable.RoleOf(Calling.Skirmisher) == PartyRole.Back, "The ranged callings shoot from behind.");
            Assert(CallingTable.RoleOf(Calling.FieldSurgeon) == PartyRole.Carrier && CallingTable.RoleOf(Calling.Bearer) == PartyRole.Carrier, "The medics carry the fallen.");
            Assert(CallingTable.RoleOf(Calling.Captain) == PartyRole.Leader, "The Captain leads.");
            foreach (var row in CallingTable.All)
            {
                var use = SkillTable.Get(row.Skill).Use;
                Assert((row.Role == PartyRole.Front) == (use == SkillUse.MeleeFight) && (row.Role == PartyRole.Back) == (use == SkillUse.RangedFight)
                    && (row.Role == PartyRole.Carrier) == (use == SkillUse.Tend) && (row.Role == PartyRole.Leader) == (use == SkillUse.Lead), $"{row.Name}'s role follows its skill's work.");
                Assert(row.TitleAt(SkillBand.Master) == row.MasterName && row.TitleAt(SkillBand.Journeyman) == row.DisplayName && row.TitleAt(SkillBand.Apprentice) == row.DisplayName, $"{row.Name}: its Master title at Master only.");
                var cap = row.Capstone;
                if (cap.Stat == TechniqueStat.None) continue;
                Assert(cap.IsUpside && TechniqueRules.AppliesTo(cap.Stat, use), $"{row.Name}'s capstone is an upside in its own work ({cap.Stat}).");
                var (min, max) = TechniqueRules.Bounds(cap.Stat);
                Assert(cap.Value >= min && cap.Value <= max, $"{row.Name}'s capstone within bounds.");
            }
            TestLog.Log("[PASS] Test 3: roles follow the skill's work (front, back, carrier, leader); Master titles at Master only; capstones are upsides in their own work, within bounds.");
        }

        private static void ValidateACallingEarned()
        {
            var traits = default(CrawlerTraits);
            traits.SetTechnique(Skill.Melee, SkillBand.Apprentice, Technique.ShieldWall);
            traits.SetTechnique(Skill.Mining, SkillBand.Apprentice, Technique.HardDriver);
            var skills = Levels(Skill.Melee, 4, Skill.Mining, 3);
            Assert(CallingRules.Earned(skills, traits) == Calling.Warden, "The best skill decides: Melee 4 with Shield Wall is a Warden.");
            Assert(CallingRules.Earned(Levels(Skill.Melee, 2, Skill.Mining, 2), traits) == Calling.None, "Below Apprentice in everything, no calling.");

            // A tie at the top: the most experience, then the display order; but a calling held keeps its skill while it ties
            var richer = Levels(Skill.Melee, 4, Skill.Mining, 4);
            // Mining 4 with 50 more experience than Melee 4
            var a = new Simpiens.Cognition.AgentState(EntityId.Generate()) { Skills = richer };
            a.Practise(Skill.Mining, 50, out _);
            Assert(CallingRules.Earned(a.Skills, traits) == Calling.Rockbreaker, "Tied at 4, the skill with more experience: Mining's Rockbreaker.");
            var held = traits;
            held.SetCalling(Calling.Warden);
            Assert(CallingRules.Earned(a.Skills, held) == Calling.Warden, "Holding Warden, a tie keeps it: no flicker.");
            a.Practise(Skill.Mining, SkillRules.ExperienceFor(5) - a.Skills.Experience(Skill.Mining), out _);
            Assert(CallingRules.Earned(a.Skills, held) == Calling.Rockbreaker, "Mining 5 above Melee 4: the calling moves.");

            // A technique changed (a sponsorship) changes the calling it earned
            var sponsored = held;
            sponsored.SetTechnique(Skill.Melee, SkillBand.Apprentice, Technique.RecklessFury);
            Assert(CallingRules.Earned(skills, sponsored) == Calling.Berserker, "Shield Wall sponsored to Reckless Fury: a Berserker.");
            Assert(CallingRules.Rejudge(skills, ref sponsored, out var was) && was == Calling.Warden && sponsored.Calling == Calling.Berserker, "Judged again, it changes and says what it was.");
            Assert(!CallingRules.Rejudge(skills, ref sponsored, out _), "Judged again unchanged, nothing changes.");

            // Leadership's Captain, earned by none; the Master title
            var captain = Levels(Skill.Leadership, 5, Skill.Melee, 3);
            Assert(CallingRules.Earned(captain, default) == Calling.Captain, "Leadership 5 at the top: a Captain, with no technique.");
            var master = Levels(Skill.Melee, 9, Skill.Mining, 1);
            Assert(CallingRules.TitleOf(Calling.Warden, master) == "Master Warden" && CallingRules.TitleOf(Calling.Warden, skills) == "Warden" && CallingRules.TitleOf(Calling.None, skills) == string.Empty,
                "A Master Warden at Melee 9; a Warden below.");
            TestLog.Log("[PASS] Test 4: a calling earned: the best skill and its Apprentice technique; ties by experience, never flickering; a technique changed changes it; Leadership's Captain.");
        }

        private static void ValidateABandReachedJudgesItAgain()
        {
            var world = new WorldRegistry();
            var clock = new SimulationClock();
            var chronicle = new NarrativeChronicleService();
            var sim = new SimulationManager(world, null, clock: clock, chronicleService: chronicle) { Seed = 7 };
            sim.SetEconomy(EconomyTuning.Default);
            clock.AdvanceTicks(100);
            var crawler = HeadlessEntities.Crawler(world, sim, null, Vec2.Zero, id: EntityId.Generate()).Agent;
            var traits = default(CrawlerTraits);
            traits.SetTechnique(Skill.Mining, SkillBand.Apprentice, Technique.SeamStudent);
            traits.SetCalling(Calling.Prospector);
            crawler.Traits = traits;
            crawler.Skills = Levels(Skill.Mining, 3, Skill.Melee, 2);
            int Told()
            {
                var all = new List<ChronicleEvent>();
                chronicle.GetAllRecentEvents(all, 64);
                return all.FindAll(e => e.EventType == ChronicleEventType.CallingEarned && e.ActorAId == crawler.AgentId).Count;
            }

            // Melee to 3, a band reached: it picks a technique there, but ties Mining at 3, so the Prospector keeps its calling
            crawler.Practise(Skill.Melee, SkillRules.ExperienceFor(3) - crawler.Skills.Experience(Skill.Melee) - 5, out _);
            sim.Practise(crawler, SkillWork.Fight(ranged: false), SkillRules.HitExperience, PracticeSource.WorkAt(SkillRules.MaxLevel));
            var melee = crawler.Traits.TechniqueIn(Skill.Melee, SkillBand.Apprentice);
            Assert(crawler.Skills.Level(Skill.Melee) == 3 && melee != Technique.None, "Melee 3, its Apprentice technique picked.");
            Assert(crawler.Traits.Calling == Calling.Prospector && Told() == 0, "Tied at 3, the Prospector keeps its calling; nothing told.");

            // Melee to 6, a band reached and above Mining: its calling moves to melee's, told once
            crawler.Practise(Skill.Melee, SkillRules.ExperienceFor(6) - crawler.Skills.Experience(Skill.Melee) - 5, out _);
            sim.Practise(crawler, SkillWork.Fight(ranged: false), SkillRules.HitExperience, PracticeSource.WorkAt(SkillRules.MaxLevel));
            Assert(crawler.Skills.Level(Skill.Melee) == 6, "Journeyman in melee.");
            Assert(crawler.Traits.Calling == CallingTable.For(Skill.Melee, melee) && Told() == 1, $"Melee 6 above Mining 3: its calling is melee's now ({crawler.Traits.Calling}), told once.");
            TestLog.Log("[PASS] Test 5: a band reached judges the calling again; a tie keeps it, a rise past it moves it, and the chronicle tells a change.");
        }

        private static SkillSet Levels(Skill a, int levelA, Skill b, int levelB)
        {
            var skills = default(SkillSet);
            skills.Set(a, levelA);
            skills.Set(b, levelB);
            return skills;
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("[CallingValidator] FAILED: " + message);
        }
    }
}
