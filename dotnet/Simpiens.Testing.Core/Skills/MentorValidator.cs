using System;
using System.Collections.Generic;
using Simpiens.Cognition;
using Simpiens.Narrative;
using Simpiens.Simulation;
using Simpiens.Simulation.Economy;
using Simpiens.Simulation.Persistence;
using Simpiens.Simulation.Skills;
using Simpiens.Testing.Environment;

namespace Simpiens.Testing.Skills
{
    /// <summary>
    /// Mentors (Epic 11, S3.1, Q10, D9): a crawler practising beside a better hand at the same work is taught by it, half the
    /// act at source Mentor, to the mentor's level less two, multiplied by its passion; the first lesson in a skill records the
    /// mentor as its first teacher (saved), counts the mentor's "taught" deed and is told in the chronicle. Nobody teaches
    /// across works, from afar, after the hour, or without a lead of three levels. Runs as a test (ValidatorTests).
    /// </summary>
    public class MentorValidator
    {
        private SimulationManager _sim;
        private WorldRegistry _world;
        private SimulationClock _clock;
        private NarrativeChronicleService _chronicle;
        private readonly List<(AgentState Who, int Experience, PracticeSource Source)> _lessons = new List<(AgentState, int, PracticeSource)>();

        private static readonly SkillWork Ore = SkillWork.Harvest(ItemType.IronOre);

        public void RunAllValidationTests()
        {
            TestLog.Log("[MentorValidator] Beginning mentor validation...");
            ValidateABetterHandTeaches();
            ValidateTheMentorsCeiling();
            ValidateNobodyTeachesAcrossWorksOrFromAfar();
            ValidateAPassionMultipliesTheLesson();
            ValidateLineageIsSaved();
            TestLog.Log("<color=green>[MentorValidator] ALL 5 TESTS PASSED SUCCESSFULLY!</color>");
        }

        private void ValidateABetterHandTeaches()
        {
            NewOutpost();
            var mentor = Crawler(new Vec2(0f, 0f), Skill.Mining, 7);
            var learner = Crawler(new Vec2(3f, 0f), Skill.Mining, 2);
            _sim.Practise(mentor, Ore, SkillRules.UnitExperience, PracticeSource.WorkAt(4));
            _clock.AdvanceTicks(10);
            int had = learner.Skills.Experience(Skill.Mining);
            _sim.Practise(learner, Ore, SkillRules.UnitExperience, PracticeSource.WorkAt(4));
            Assert(learner.Skills.Experience(Skill.Mining) - had == SkillRules.UnitExperience + SkillRules.UnitExperience / 2,
                $"The learner takes its own act and half of it again from the mentor ({learner.Skills.Experience(Skill.Mining) - had}).");
            var lesson = Lessons(learner);
            Assert(lesson.Count == 1 && lesson[0].Experience == 5 && lesson[0].Source.Ceiling == 5, "The lesson is told at source Mentor: 5, to the mentor's 7 less two.");
            Assert(learner.Traits.TeacherOf(Skill.Mining) == mentor.AgentId, "The learner records its first teacher in mining.");
            Assert(mentor.Traits.Count(Deed.Taught) == 1, "The mentor counts a crawler taught.");
            Assert(Told(ChronicleEventType.FirstLesson) == 1, "The first lesson is told in the chronicle.");
            _sim.Practise(learner, Ore, SkillRules.UnitExperience, PracticeSource.WorkAt(4));
            Assert(mentor.Traits.Count(Deed.Taught) == 1 && Told(ChronicleEventType.FirstLesson) == 1 && Lessons(learner).Count == 2, "A second lesson counts no deed and tells nothing more.");
            had = mentor.Skills.Experience(Skill.Mining);
            _sim.Practise(mentor, Ore, SkillRules.UnitExperience, PracticeSource.WorkAt(4));
            Assert(Lessons(mentor).Count == 0, "The lesser hand teaches the better nothing.");
            TestLog.Log("[PASS] Test 1: a better hand at the same seam teaches: half the act at source Mentor; the first lesson records the teacher, counts its deed and is told.");
        }

        private void ValidateTheMentorsCeiling()
        {
            // A Master 9 teaches to 7: a Journeyman 6 a little short of 7 takes its own act, then only what the lesson has room for
            NewOutpost();
            var mentor = Crawler(new Vec2(0f, 0f), Skill.Mining, 9);
            var learner = Crawler(new Vec2(1f, 0f), Skill.Mining, 6);
            learner.Practise(Skill.Mining, 650, out _); // 2,750 of the 2,800 that reach 7
            _sim.Practise(mentor, Ore, SkillRules.UnitExperience, PracticeSource.WorkAt(SkillRules.MaxLevel));
            _sim.Practise(learner, Ore, 40, PracticeSource.WorkAt(SkillRules.MaxLevel));
            var lesson = Lessons(learner);
            Assert(lesson.Count == 1 && lesson[0].Experience == 10 && learner.Skills.Experience(Skill.Mining) == SkillRules.ExperienceFor(7),
                $"Its own 40, then 10 of the lesson's 20: held to the mentor's 9 less two ({(lesson.Count > 0 ? lesson[0].Experience : 0)}, at {learner.Skills.Experience(Skill.Mining)}).");
            _sim.Practise(mentor, Ore, SkillRules.UnitExperience, PracticeSource.WorkAt(SkillRules.MaxLevel));
            _sim.Practise(learner, Ore, 40, PracticeSource.WorkAt(SkillRules.MaxLevel));
            Assert(Lessons(learner).Count == 1, "Within three levels of the mentor, no lesson.");
            TestLog.Log("[PASS] Test 2: a mentor teaches to its own level less two, and nobody within three levels of it.");
        }

        private void ValidateNobodyTeachesAcrossWorksOrFromAfar()
        {
            NewOutpost();
            var herbalist = Crawler(new Vec2(0f, 0f), Skill.Mining, 8);
            var far = Crawler(new Vec2(SkillRules.MentorRange + 2f, 0f), Skill.Mining, 8); // 7 units from the learner
            var near = Crawler(new Vec2(-1f, 0f), Skill.Mining, 4);
            var learner = Crawler(new Vec2(1f, 0f), Skill.Mining, 2);
            _sim.Practise(herbalist, SkillWork.Harvest(ItemType.Glowstone), SkillRules.UnitExperience, PracticeSource.WorkAt(7)); // mining too, but another work
            _sim.Practise(far, Ore, SkillRules.UnitExperience, PracticeSource.WorkAt(4));
            _sim.Practise(near, Ore, SkillRules.UnitExperience, PracticeSource.WorkAt(4)); // a lead of two only
            _sim.Practise(learner, Ore, SkillRules.UnitExperience, PracticeSource.WorkAt(4));
            Assert(Lessons(learner).Count == 0 && learner.Traits.TeacherOf(Skill.Mining) == default, "Not across works, not from afar, not without a lead of three.");
            // After the hour, a mentor has gone cold
            NewOutpost();
            var mentor = Crawler(new Vec2(0f, 0f), Skill.Mining, 8);
            learner = Crawler(new Vec2(1f, 0f), Skill.Mining, 2);
            _sim.Practise(mentor, Ore, SkillRules.UnitExperience, PracticeSource.WorkAt(4));
            _clock.AdvanceTicks(SkillRules.MentorRecentHours * DayClock.TicksPerHour + 1);
            _sim.Practise(learner, Ore, SkillRules.UnitExperience, PracticeSource.WorkAt(4));
            Assert(Lessons(learner).Count == 0, "A mentor that last worked over an hour ago teaches nothing.");
            mentor.IsDowned = true;
            _sim.Practise(mentor, Ore, SkillRules.UnitExperience, PracticeSource.WorkAt(4));
            _sim.Practise(learner, Ore, SkillRules.UnitExperience, PracticeSource.WorkAt(4));
            Assert(Lessons(learner).Count == 0, "Nor one that is downed.");
            TestLog.Log("[PASS] Test 3: nobody teaches across works, from afar, after the hour, downed, or without a lead of three levels.");
        }

        private void ValidateAPassionMultipliesTheLesson()
        {
            NewOutpost();
            var mentor = Crawler(new Vec2(0f, 0f), Skill.Mining, 8);
            var learner = Crawler(new Vec2(1f, 0f), Skill.Mining, 1);
            var traits = default(CrawlerTraits);
            traits.SetPassion(Skill.Mining, Passion.Burning);
            learner.Traits = traits;
            _sim.Practise(mentor, Ore, SkillRules.UnitExperience, PracticeSource.WorkAt(4));
            _sim.Practise(learner, Ore, SkillRules.UnitExperience, PracticeSource.WorkAt(4));
            var lesson = Lessons(learner);
            Assert(lesson.Count == 1 && lesson[0].Experience == SkillRules.UnitExperience, $"Burning for mining, the lesson is doubled: 5 to 10 ({(lesson.Count > 0 ? lesson[0].Experience : 0)}).");
            Assert(learner.Traits.PassionFor(Skill.Mining) == Passion.Burning && learner.Traits.TeacherOf(Skill.Mining) == mentor.AgentId, "Recording the teacher keeps the passion.");
            TestLog.Log("[PASS] Test 4: a passion multiplies a mentor's lesson as it does the work.");
        }

        private void ValidateLineageIsSaved()
        {
            NewOutpost();
            var mentor = Crawler(new Vec2(0f, 0f), Skill.Melee, 8);
            var learner = Crawler(new Vec2(1f, 0f), Skill.Melee, 1);
            _sim.Practise(mentor, SkillWork.Fight(ranged: false), SkillRules.HitExperience, PracticeSource.WorkAt(5));
            _sim.Practise(learner, SkillWork.Fight(ranged: false), SkillRules.HitExperience, PracticeSource.WorkAt(5));
            Assert(learner.Traits.TeacherOf(Skill.Melee) == mentor.AgentId, "Taught at the fight.");
            var back = SaveSerializer.Deserialize<Simpiens.Simulation.Persistence.CrawlerRecord>(SaveSerializer.Serialize(learner.CaptureRecord()));
            var fresh = new AgentState(learner.AgentId);
            fresh.RestoreRecord(back);
            Assert(fresh.Traits.TeacherOf(Skill.Melee) == mentor.AgentId, "The learner's first teacher comes back from the save.");
            var mentorBack = SaveSerializer.Deserialize<Simpiens.Simulation.Persistence.CrawlerRecord>(SaveSerializer.Serialize(mentor.CaptureRecord()));
            var freshMentor = new AgentState(mentor.AgentId);
            freshMentor.RestoreRecord(mentorBack);
            Assert(freshMentor.Traits.Count(Deed.Taught) == 1, "And the mentor's deed.");
            Assert(freshMentor.LastPracticedAt == 0 && freshMentor.LastPracticed.Equals(default(SkillWork)), "Its last practice isn't saved: after a load nobody mentors until they practise again.");
            TestLog.Log("[PASS] Test 5: lineage is saved with the traits record; the last practice, work in progress, is not.");
        }

        private void NewOutpost()
        {
            _world = new WorldRegistry();
            _clock = new SimulationClock();
            _chronicle = new NarrativeChronicleService();
            _sim = new SimulationManager(_world, null, clock: _clock, chronicleService: _chronicle);
            _sim.SetEconomy(EconomyTuning.Default);
            _clock.AdvanceTicks(100);
            _lessons.Clear();
            _sim.Practised += (who, skill, experience, source) =>
            {
                if (source.Kind == PracticeKind.Mentor) _lessons.Add((who, experience, source));
            };
        }

        private AgentState Crawler(Vec2 at, Skill skill, int level)
        {
            var crawler = HeadlessEntities.Crawler(_world, _sim, null, at, id: EntityId.Generate()).Agent;
            crawler.Position = at;
            crawler.Traits = default;
            var skills = default(SkillSet);
            skills.Set(skill, level);
            crawler.Skills = skills;
            return crawler;
        }

        private List<(AgentState Who, int Experience, PracticeSource Source)> Lessons(AgentState who) => _lessons.FindAll(l => l.Who == who);

        private int Told(ChronicleEventType type)
        {
            var all = new List<ChronicleEvent>();
            _chronicle.GetAllRecentEvents(all, 256);
            return all.FindAll(e => e.EventType == type).Count;
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("[MentorValidator] FAILED: " + message);
        }
    }
}
