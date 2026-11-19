using System;
using System.Collections.Generic;
using Simpiens.Cognition;
using Simpiens.Cognition.Contracts;
using Simpiens.Simulation;
using Simpiens.Simulation.Guild;
using Simpiens.Simulation.Skills;

namespace Simpiens.Testing.Skills
{
    /// <summary>
    /// Leadership, an eighth skill (Epic 11, S5.1, Q11, Q15, D19): a row like the others, leaned by loyalty; a party's captain is
    /// the member on its feet with the best Leadership, the lowest id on a tie; it practises 40 for each mate brought home, at
    /// the trip's challenge; and it steadies its party, the retreat line 3% lower a level. The delivery's practice and the
    /// steadied line are seen live (S5.6, S5.9). Runs as a test (ValidatorTests).
    /// </summary>
    public class LeadershipValidator
    {
        public void RunAllValidationTests()
        {
            TestLog.Log("[LeadershipValidator] Beginning leadership validation...");
            ValidateTheRow();
            ValidateTheCaptain();
            ValidateTheLessonAndTheLine();
            TestLog.Log("<color=green>[LeadershipValidator] ALL 3 TESTS PASSED SUCCESSFULLY!</color>");
        }

        private static void ValidateTheRow()
        {
            var row = SkillTable.Get(Skill.Leadership);
            Assert(row != null && row.Use == SkillUse.Lead && row.Lean == SkillLean.Loyal && row.Practitioner == "captain", "Leadership: leading a party, leaned by loyalty, its practitioner a captain.");
            Assert(SkillTable.SkillFor(SkillWork.Lead()) == Skill.Leadership, "Leading finds Leadership.");
            Assert(TechniqueTable.Options(Skill.Leadership, SkillBand.Apprentice).Count == 0, "No techniques yet (D19): a band reached picks nothing.");
            // The loyal draw it as a talent more often than the disloyal
            int loyal = 0, disloyal = 0;
            var leans = new AgentPersonality(0f, 0f, 0f, 0.9f, 0f);
            var against = new AgentPersonality(0f, 0f, 0f, -0.9f, 0f);
            for (int c = 0; c < 4000; c++)
            {
                var id = new EntityId((uint)c + 1, 5, 5, 5);
                if (SkillSet.Generate(9, id, leans).Level(Skill.Leadership) >= SkillRules.TalentLevel) loyal++;
                if (SkillSet.Generate(9, id, against).Level(Skill.Leadership) >= SkillRules.TalentLevel) disloyal++;
            }
            Assert(loyal > disloyal * 2, $"The loyal draw a Leadership talent far more often ({loyal} against {disloyal} of 4000).");
            TestLog.Log($"[PASS] Test 1: Leadership is a row: leading a party, leaned by loyalty (a talent for {loyal} loyal crawlers of 4000, {disloyal} disloyal).");
        }

        private static void ValidateTheCaptain()
        {
            var a = Crawler(1, 1);
            var b = Crawler(2, 4);
            var c = Crawler(3, 4);
            var d = Crawler(4, 6);
            d.IsDowned = true;
            Assert(GuildRules.Captain(new List<AgentState> { c, a, d, b }) == b, "The best Leadership on its feet, the lowest id on a tie: not the downed 6, not the later 4.");
            Assert(GuildRules.Captain(new List<AgentState> { a }) == a && GuildRules.Captain(new List<AgentState>()) == null && GuildRules.Captain(null) == null, "Alone, it leads itself; nobody leads nobody.");
            d.IsDowned = false;
            Assert(GuildRules.Captain(new List<AgentState> { c, a, d, b }) == d, "On its feet again, the 6 leads.");
            TestLog.Log("[PASS] Test 2: the captain is the best Leadership on its feet, the lowest id on a tie.");
        }

        private static void ValidateTheLessonAndTheLine()
        {
            Assert(GuildRules.LeadLesson(4) == 120 && GuildRules.LeadLesson(2) == 40 && GuildRules.LeadLesson(1) == 0 && GuildRules.LeadLesson(0) == 0,
                "40 for each mate brought home: 120 for a four, nothing for one alone.");
            Assert(SkillRules.ChallengePercent(2, SkillRules.GatherChallenge) == 100 && SkillRules.ChallengePercent(5, SkillRules.GatherChallenge) == 55,
                "A gathering teaches a Novice fully and an Apprentice 5 a little over half; a beetles' clear (7) teaches both fully.");
            Assert(SkillRules.SteadyFactor(0) == 1f && Math.Abs(SkillRules.SteadyFactor(5) - 0.85f) < 1e-5f && Math.Abs(SkillRules.SteadyFactor(10) - 0.7f) < 1e-5f, "3% a level: 0.85 at 5, 0.7 at 10.");
            Assert(GuildRules.Steadied(40f, 0) == 40f && Math.Abs(GuildRules.Steadied(40f, 5) - 34f) < 1e-4f && Math.Abs(GuildRules.Steadied(40f, 10) - 28f) < 1e-4f,
                "A line of 40 under a captain of 0, 5 and 10: 40, 34, 28.");
            TestLog.Log("[PASS] Test 3: a captain practises 40 for each mate brought home at the trip's challenge, and steadies its party's line 3% a level.");
        }

        private static AgentState Crawler(uint n, int leadership)
        {
            var crawler = new AgentState(new EntityId(n, 1, 1, 1));
            var skills = default(SkillSet);
            skills.Set(Skill.Leadership, leadership);
            crawler.Skills = skills;
            return crawler;
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("[LeadershipValidator] FAILED: " + message);
        }
    }
}
