using System;
using Simpiens.Cognition.Contracts;
using Simpiens.Simulation;
using Simpiens.Simulation.Economy;
using Simpiens.Simulation.Items;
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
            ValidatePassionSpeedsPractice();
            ValidatePassionWeighsTheWork();
            TestLog.Log("<color=green>[PassionValidator] ALL 5 TESTS PASSED SUCCESSFULLY!</color>");
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

        private void ValidatePassionSpeedsPractice()
        {
            // Epic 11, S1.2 (D4): practice's one entry multiplies by the passion, holds the ceiling, and eases frustration
            var sim = new SimulationManager(new WorldRegistry(), null);
            Simpiens.Cognition.AgentState Crawler(Passion passion)
            {
                var state = new Simpiens.Cognition.AgentState(EntityId.Generate()) { Frustration = 50f };
                var traits = default(CrawlerTraits);
                traits.SetPassion(Skill.Mining, passion);
                state.Traits = traits;
                return state;
            }
            var plain = Crawler(Passion.None);
            var keen = Crawler(Passion.Keen);
            var burning = Crawler(Passion.Burning);
            foreach (var c in new[] { plain, keen, burning }) sim.Practise(c, SkillWork.Harvest(ItemType.IronOre), 40, PracticeSource.Work);
            Assert(plain.Skills.Experience(Skill.Mining) == 40 && keen.Skills.Experience(Skill.Mining) == 60 && burning.Skills.Experience(Skill.Mining) == 80,
                $"Practice x1, x1.5, x2 ({plain.Skills.Experience(Skill.Mining)}, {keen.Skills.Experience(Skill.Mining)}, {burning.Skills.Experience(Skill.Mining)}).");
            Assert(plain.Frustration == 50f && keen.Frustration == 50f - PassionTable.Get(Passion.Keen).FrustrationEased
                && burning.Frustration == 50f - PassionTable.Get(Passion.Burning).FrustrationEased, "Work at a passion eases frustration; work without one doesn't.");
            sim.Practise(burning, SkillWork.Harvest(ItemType.IronOre), 5000, new PracticeSource(PracticeKind.Work, 2));
            Assert(burning.Skills.Level(Skill.Mining) == 2 && burning.Skills.Experience(Skill.Mining) == SkillRules.ExperienceFor(2), "A passion speeds practice but never past the source's ceiling.");
            sim.Practise(burning, SkillWork.Fight(ranged: false), 40, PracticeSource.Work);
            Assert(burning.Skills.Experience(Skill.Melee) == 40, "A passion speeds only the skill it is for.");
            TestLog.Log("[PASS] Test 4: a passion speeds its skill's practice, x1.5 or x2, to the ceiling and no further, and eases frustration.");
        }

        private void ValidatePassionWeighsTheWork()
        {
            // Epic 11, S1.4 (D6): two salvage nodes alike and as far, one ore and one herbs; the passionless pick by today's score
            // (here a tie, broken by id), the burning go for the work they love
            var ore = new EntityId(1, 0, 0, 1);
            var herbs = new EntityId(2, 0, 0, 1);
            Simpiens.Cognition.Memory.AgentMemory Memory()
            {
                var memory = new Simpiens.Cognition.Memory.AgentMemory();
                memory.SpatialMemoryMap[ore] = new Simpiens.Cognition.Memory.SpatialMemoryRecord(ore, Simpiens.Simulation.Spatial.EntityType.Resource, new GridPoint(5, 0), 1, ItemType.IronOre);
                memory.SpatialMemoryMap[herbs] = new Simpiens.Cognition.Memory.SpatialMemoryRecord(herbs, Simpiens.Simulation.Spatial.EntityType.Resource, new GridPoint(-5, 0), 1, ItemType.MedicinalHerb);
                return memory;
            }
            EntityId Choice(Skill loved, Passion passion)
            {
                var traits = default(CrawlerTraits);
                traits.SetPassion(loved, passion);
                var context = new AgentContext(EntityId.Generate(), Vec2.Zero, new AgentNeeds(20f, 100f, 100f), 0f, null, Memory(), 10, traits: traits);
                Assert(Simpiens.Cognition.Planning.ResourceTargeting.TrySelectSalvage(context, ItemType.None, out var best), "Salvage is chosen.");
                return best.EntityId;
            }
            Assert(Choice(Skill.None, Passion.None) == ore, "Alike and as far, the passionless take the lower id.");
            Assert(Choice(Skill.Foraging, Passion.Burning) == herbs && Choice(Skill.Foraging, Passion.Keen) == herbs, "Burning or keen for foraging, it goes for the herbs.");
            Assert(Choice(Skill.Mining, Passion.Burning) == ore, "Burning for mining, the ore.");
            Assert(Simpiens.Cognition.Planning.Considerations.PassionWeight(default, SkillWork.Harvest(ItemType.IronOre)) == 1f, "No passion weighs nothing.");
            TestLog.Log("[PASS] Test 5: a passion weighs the salvage choice toward the work it loves.");
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
