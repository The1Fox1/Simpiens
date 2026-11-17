using System;
using Simpiens.Cognition.Contracts;
using Simpiens.Simulation;
using Simpiens.Simulation.Skills;

namespace Simpiens.Testing.Skills
{
    /// <summary>
    /// Passions (Epic 11, S1.1, Q7, D3): the table's three steps and their numbers; the draw at spawn is seeded, holds its caps
    /// (two keen at most, one burning, one passion a skill), lands near its odds over many crawlers, leans by personality and
    /// toward talents, and moves no earlier draw. Runs as a test (ValidatorTests).
    /// </summary>
    public class PassionValidator
    {
        private const int Crawlers = 10000;

        public void RunAllValidationTests()
        {
            TestLog.Log("[PassionValidator] Beginning passions validation...");
            ValidateTheTable();
            ValidateTheDrawAndItsCaps();
            ValidateTheLeans();
            TestLog.Log("<color=green>[PassionValidator] ALL 3 TESTS PASSED SUCCESSFULLY!</color>");
        }

        private void ValidateTheTable()
        {
            var all = PassionTable.All;
            Assert(all.Count == 3 && all[0].Id == Passion.None && all[1].Id == Passion.Keen && all[2].Id == Passion.Burning, "Three steps, none, keen and burning, in id order.");
            Assert(all[0].PracticePercent == 100 && all[1].PracticePercent == 150 && all[2].PracticePercent == 200, "Practice ×1, ×1.5, ×2 (Q7).");
            Assert(all[0].FrustrationEased == 0f && all[1].FrustrationEased > 0f && all[2].FrustrationEased > all[1].FrustrationEased, "Work at a passion eases frustration, more for the burning.");
            Assert(all[0].Glyph == null && all[1].Glyph != null && all[2].Glyph != null, "The loved steps carry a glyph.");
            Assert(PassionTable.Get((Passion)9) == all[0], "An id past the table reads as none.");
            TestLog.Log("[PASS] Test 1: three steps of passion, each with its practice, its ease and its glyph.");
        }

        private void ValidateTheDrawAndItsCaps()
        {
            int burning = 0, none = 0, oneKeen = 0, twoKeen = 0;
            var personality = default(AgentPersonality);
            for (int c = 0; c < Crawlers; c++)
            {
                var id = new EntityId((uint)c + 1, 7, 2, 3);
                var skills = SkillSet.Generate(7, id, personality);
                var traits = PassionTable.Draw(7, id, personality, skills);
                Assert(traits == PassionTable.Draw(7, id, personality, skills), "The draw is seeded: the same crawler draws the same passions.");
                Assert(SkillSet.Generate(7, id, personality) == skills, "Drawing passions moves no skill.");
                int keen = 0, burn = 0;
                foreach (var row in SkillTable.All)
                {
                    var passion = traits.PassionFor(row.Id);
                    if (passion == Passion.Keen) keen++;
                    else if (passion == Passion.Burning) burn++;
                }
                Assert(keen <= 2 && burn <= 1, $"At most two keen and one burning ({keen}, {burn}).");
                if (burn == 1) burning++;
                if (keen == 0) none++;
                else if (keen == 1) oneKeen++;
                else twoKeen++;
                Assert(traits.TeacherOf(Skill.Melee) == default && traits.Count(Deed.Make) == 0, "The draw writes passions only.");
            }
            Near(burning, PassionTable.BurningChance, "burning");
            Near(none, PassionTable.NoKeenBelow, "no keen passion");
            Near(oneKeen, PassionTable.OneKeenBelow - PassionTable.NoKeenBelow, "one keen passion");
            Near(twoKeen, 1f - PassionTable.OneKeenBelow, "two keen passions");
            TestLog.Log($"[PASS] Test 2: of {Crawlers} crawlers, {burning} burn for a skill, and {none}, {oneKeen} and {twoKeen} hold none, one or two keen passions; seeded, capped, one a skill.");
        }

        private void ValidateTheLeans()
        {
            // The bold love melee more than the cautious do; a talent is loved more often than the same skill untalented
            int boldMelee = 0, cautiousMelee = 0, talentLoved = 0, plainLoved = 0;
            var bold = new AgentPersonality(0f, 1f, 0f, 0f, 0f);
            var cautious = new AgentPersonality(0f, -1f, 0f, 0f, 0f);
            var neutral = default(AgentPersonality);
            var talented = default(SkillSet);
            talented.Set(Skill.Alchemy, 4);
            var plain = default(SkillSet);
            for (int c = 0; c < Crawlers; c++)
            {
                var id = new EntityId((uint)c + 1, 11, 2, 3);
                if (PassionTable.Draw(11, id, bold, default).PassionFor(Skill.Melee) != Passion.None) boldMelee++;
                if (PassionTable.Draw(11, id, cautious, default).PassionFor(Skill.Melee) != Passion.None) cautiousMelee++;
                if (PassionTable.Draw(11, id, neutral, talented).PassionFor(Skill.Alchemy) != Passion.None) talentLoved++;
                if (PassionTable.Draw(11, id, neutral, plain).PassionFor(Skill.Alchemy) != Passion.None) plainLoved++;
            }
            Assert(boldMelee > cautiousMelee * 1.5, $"The bold love melee more often ({boldMelee} against the cautious's {cautiousMelee}).");
            Assert(talentLoved > plainLoved * 1.5, $"A talent is loved more often ({talentLoved} against {plainLoved} untalented).");
            TestLog.Log($"[PASS] Test 3: personality and talent lean the draw: melee loved by {boldMelee} bold against {cautiousMelee} cautious; alchemy by {talentLoved} talented against {plainLoved}.");
        }

        private static void Near(int count, float share, string what)
        {
            float seen = count / (float)Crawlers;
            Assert(Math.Abs(seen - share) < 0.02f, $"{what}: {seen:P1} of crawlers, expected about {share:P0}.");
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("[PassionValidator] FAILED: " + message);
        }
    }
}
