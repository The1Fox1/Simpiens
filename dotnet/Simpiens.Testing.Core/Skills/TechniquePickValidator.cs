using System;
using System.Collections.Generic;
using Simpiens.Cognition;
using Simpiens.Cognition.Contracts;
using Simpiens.Narrative;
using Simpiens.Simulation;
using Simpiens.Simulation.Economy;
using Simpiens.Simulation.Skills;
using Simpiens.Testing.Environment;

namespace Simpiens.Testing.Skills
{
    /// <summary>
    /// The pick by nature (Epic 11, S4.2, Q3, D15): over many crawlers the bold take up Reckless Fury and the cautious Shield
    /// Wall more often; the pick is seeded, the same whenever the band is reached; a band with nothing to offer picks
    /// nothing; a slot already held is never picked over; no earlier draw moves; a band reached in practice picks and is told,
    /// and a crawler's talents at spawn hold their Apprentice techniques. Runs as a test (ValidatorTests).
    /// </summary>
    public class TechniquePickValidator
    {
        private const int Crawlers = 10000;

        public void RunAllValidationTests()
        {
            TestLog.Log("[TechniquePickValidator] Beginning technique pick validation...");
            ValidateNatureLeansThePick();
            ValidateThePickIsSeeded();
            ValidateTheSpawnPick();
            ValidateABandReachedPicks();
            TestLog.Log("<color=green>[TechniquePickValidator] ALL 4 TESTS PASSED SUCCESSFULLY!</color>");
        }

        private static void ValidateNatureLeansThePick()
        {
            // The bold (risk tolerance 0.8) and the cautious (-0.8): Reckless Fury leans bold, Shield Wall cautious
            var bold = new AgentPersonality(0f, 0.8f, 0f, 0f, 0f);
            var cautious = new AgentPersonality(0f, -0.8f, 0f, 0f, 0f);
            var even = default(AgentPersonality);
            int boldFury = 0, cautiousWall = 0, evenFury = 0;
            for (int c = 0; c < Crawlers; c++)
            {
                var id = new EntityId((uint)c + 1, 11, 5, 3);
                if (TechniqueRules.Pick(7, id, bold, Skill.Melee, SkillBand.Apprentice) == Technique.RecklessFury) boldFury++;
                if (TechniqueRules.Pick(7, id, cautious, Skill.Melee, SkillBand.Apprentice) == Technique.ShieldWall) cautiousWall++;
                if (TechniqueRules.Pick(7, id, even, Skill.Melee, SkillBand.Apprentice) == Technique.RecklessFury) evenFury++;
            }
            // Weights 1 + 2 × 0.8 = 2.6 against 1: about 72%; an even nature halves it
            Assert(boldFury > Crawlers * 0.68 && boldFury < Crawlers * 0.76, $"The bold take up Reckless Fury about 72% of the time ({boldFury} of {Crawlers}).");
            Assert(cautiousWall > Crawlers * 0.68 && cautiousWall < Crawlers * 0.76, $"The cautious take up Shield Wall about 72% of the time ({cautiousWall}).");
            Assert(evenFury > Crawlers * 0.46 && evenFury < Crawlers * 0.54, $"An even nature splits them ({evenFury}).");
            TestLog.Log($"[PASS] Test 1: nature leans the pick: the bold take up Reckless Fury {100 * boldFury / Crawlers}%, the cautious Shield Wall {100 * cautiousWall / Crawlers}%, the even split {100 * evenFury / Crawlers}%.");
        }

        private static void ValidateThePickIsSeeded()
        {
            var personality = new AgentPersonality(0.3f, -0.2f, 0.5f, 0.1f, 0.6f);
            int differs = 0;
            for (int c = 0; c < 200; c++)
            {
                var id = new EntityId((uint)c + 1, 3, 9, 1);
                foreach (var skill in SkillTable.All)
                    for (var band = SkillBand.Apprentice; band <= SkillBand.Master; band++)
                    {
                        var pick = TechniqueRules.Pick(7, id, personality, skill.Id, band);
                        Assert(pick == TechniqueRules.Pick(7, id, personality, skill.Id, band), "The same crawler picks the same at the same band, whenever it gets there.");
                        if (TechniqueTable.Options(skill.Id, band).Count == 0)
                        {
                            Assert(pick == Technique.None, $"{skill.Name} offers nothing at {band}: it picks nothing.");
                            continue;
                        }
                        Assert(pick != Technique.None && TechniqueTable.Get(pick).Skill == skill.Id && TechniqueTable.Get(pick).Band == band, $"{skill.Name} at {band}: one of the band's own.");
                        if (pick != TechniqueRules.Pick(8, id, personality, skill.Id, band)) differs++;
                    }
            }
            Assert(differs > 0, "Another seed picks otherwise.");
            Assert(TechniqueRules.Pick(7, new EntityId(1, 2, 3, 4), personality, Skill.None, SkillBand.Apprentice) == Technique.None
                && TechniqueRules.Pick(7, new EntityId(1, 2, 3, 4), personality, Skill.Melee, SkillBand.Novice) == Technique.None, "A band that offers nothing picks nothing.");

            // A slot already held is never picked over; the bands below are filled with the one reached
            var skills = default(SkillSet);
            skills.Set(Skill.Melee, 7);
            var traits = default(CrawlerTraits);
            traits.SetTechnique(Skill.Melee, SkillBand.Apprentice, Technique.ShieldWall);
            traits.SetTechnique(Skill.Melee, SkillBand.Journeyman, Technique.HeavyBlows);
            var id2 = new EntityId(77, 1, 1, 1);
            var picked = TechniqueRules.FillReached(7, id2, personality, skills, Skill.Melee, ref traits);
            Assert(picked == Technique.None && traits.TechniqueIn(Skill.Melee, SkillBand.Apprentice) == Technique.ShieldWall && traits.TechniqueIn(Skill.Melee, SkillBand.Journeyman) == Technique.HeavyBlows,
                "A held slot is never picked over.");
            traits = default;
            picked = TechniqueRules.FillReached(7, id2, personality, skills, Skill.Melee, ref traits);
            Assert(traits.TechniqueIn(Skill.Melee, SkillBand.Apprentice) != Technique.None && traits.TechniqueIn(Skill.Melee, SkillBand.Journeyman) == picked && picked != Technique.None
                && traits.TechniqueIn(Skill.Melee, SkillBand.Master) == Technique.None, "A Journeyman holds a technique at Apprentice and Journeyman, none at Master.");
            TestLog.Log("[PASS] Test 2: the pick is seeded and one of the band's own; nothing for a band that offers none; a held slot is never picked over.");
        }

        private static void ValidateTheSpawnPick()
        {
            int talents = 0;
            for (int c = 0; c < 500; c++)
            {
                var id = new EntityId((uint)c + 1, 7, 2, 3);
                var personality = AgentPersonality.Generate(7, id);
                var skills = SkillSet.Generate(7, id, personality);
                var passions = PassionTable.Draw(7, id, personality, skills);
                var traits = TechniqueRules.PickAtSpawn(7, id, personality, skills, passions);
                foreach (var row in SkillTable.All)
                {
                    Assert(traits.PassionFor(row.Id) == passions.PassionFor(row.Id), "Picking techniques moves no passion.");
                    var held = traits.TechniqueIn(row.Id, SkillBand.Apprentice);
                    bool talent = skills.Band(row.Id) >= SkillBand.Apprentice && TechniqueTable.Options(row.Id, SkillBand.Apprentice).Count > 0; // a skill offering none (Leadership) picks none
                    Assert(talent == (held != Technique.None), $"{row.Name}: a talent holds its Apprentice technique, and nothing else does ({skills.Level(row.Id)}, {held}).");
                    Assert(traits.TechniqueIn(row.Id, SkillBand.Journeyman) == Technique.None, "No talent starts past Apprentice.");
                    if (talent) talents++;
                }
                Assert(SkillSet.Generate(7, id, personality) == skills, "Picking techniques moves no skill.");
            }
            Assert(talents >= 500, $"Every crawler holds a technique for each talent ({talents} for 500 crawlers).");
            TestLog.Log($"[PASS] Test 3: at spawn each talent holds its Apprentice technique ({talents} over 500 crawlers), and no passion or skill moves.");
        }

        private static void ValidateABandReachedPicks()
        {
            var world = new WorldRegistry();
            var clock = new SimulationClock();
            var chronicle = new NarrativeChronicleService();
            var sim = new SimulationManager(world, null, clock: clock, chronicleService: chronicle) { Seed = 424242 };
            sim.SetEconomy(EconomyTuning.Default);
            clock.AdvanceTicks(100);
            var crawler = HeadlessEntities.Crawler(world, sim, null, Vec2.Zero, id: EntityId.Generate()).Agent;
            crawler.Traits = default;
            var skills = default(SkillSet);
            skills.Set(Skill.Melee, 2);
            crawler.Skills = skills;
            crawler.Practise(Skill.Melee, SkillRules.ExperienceFor(3) - SkillRules.ExperienceFor(2) - 5, out _); // 5 short of Apprentice
            sim.Practise(crawler, SkillWork.Fight(ranged: false), SkillRules.HitExperience, PracticeSource.WorkAt(SkillRules.MaxLevel));
            var held = crawler.Traits.TechniqueIn(Skill.Melee, SkillBand.Apprentice);
            Assert(crawler.Skills.Level(Skill.Melee) == 3, "It reached Apprentice.");
            Assert(held != Technique.None && held == TechniqueRules.Pick(424242, crawler.AgentId, crawler.Personality, Skill.Melee, SkillBand.Apprentice),
                $"It took up the technique its nature picks ({held}).");
            var all = new List<ChronicleEvent>();
            chronicle.GetAllRecentEvents(all, 64);
            var told = all.FindAll(e => e.EventType == ChronicleEventType.TechniqueChosen);
            Assert(told.Count == 1 && told[0].ActorAId == crawler.AgentId && (Technique)(byte)told[0].ExtraData == held, "The chronicle tells it.");
            sim.Practise(crawler, SkillWork.Fight(ranged: false), SkillRules.HitExperience, PracticeSource.WorkAt(SkillRules.MaxLevel));
            chronicle.GetAllRecentEvents(all, 64);
            Assert(all.FindAll(e => e.EventType == ChronicleEventType.TechniqueChosen).Count == 1 && crawler.Traits.TechniqueIn(Skill.Melee, SkillBand.Apprentice) == held,
                "More practice within the band picks nothing more.");
            TestLog.Log($"[PASS] Test 4: a band reached in practice takes up the technique its nature picks ({TechniqueTable.Get(held).DisplayName}), told once in the chronicle.");
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("[TechniquePickValidator] FAILED: " + message);
        }
    }
}
