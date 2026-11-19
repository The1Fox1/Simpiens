using System;
using System.Collections.Generic;
using Simpiens.Cognition.Contracts;
using Simpiens.Simulation;
using Simpiens.Simulation.Economy;
using Simpiens.Simulation.Items;
using Simpiens.Simulation.Skills;

namespace Simpiens.Testing.Skills
{
    /// <summary>
    /// The skills' table, rule and draw (Epic 10 Part 3, K9.1, Q17–Q19): the table's rows are sound and each kind of work
    /// finds its skill; levels cost more each and fall into their bands; a skill set holds 16 experiences in 32 bytes and
    /// gains levels by practice up to the last; at level 0 no factor changes anything; the draw at spawn is seeded, gives
    /// most skills a Novice's level and one or two an Apprentice's, and leans by personality. Runs as a test (ValidatorTests).
    /// </summary>
    public class SkillsValidator
    {
        public void RunAllValidationTests()
        {
            TestLog.Log("[SkillsValidator] Beginning skills validation...");
            ValidateTheTable();
            ValidateLevelsAndBands();
            ValidateTheSkillSet();
            ValidateTheSeam();
            ValidatePracticeIsObserved();
            ValidatePracticeByChallenge();
            ValidateTheMasteryBudget();
            ValidateLevelZeroChangesNothing();
            ValidateTheDraw();
            ValidatePersonalityLeansTheDraw();
            TestLog.Log("<color=green>[SkillsValidator] ALL 10 TESTS PASSED SUCCESSFULLY!</color>");
        }

        private void ValidateTheTable()
        {
            var all = SkillTable.All;
            Assert(all.Count == 7, $"Seven skills (Q17), {all.Count} in the table.");
            var names = new HashSet<string>();
            byte last = 0;
            foreach (var skill in all)
            {
                Assert((byte)skill.Id > last && (byte)skill.Id < SkillTable.Capacity, $"{skill.Name}: ids rise, under {SkillTable.Capacity}.");
                last = (byte)skill.Id;
                Assert(names.Add(skill.Name) && names.Add("display:" + skill.DisplayName), $"{skill.Name}: a name of its own.");
                Assert(skill.Use != SkillUse.None, $"{skill.Name}: practised by a kind of work.");
                Assert(skill.Use != SkillUse.Harvest || skill.Harvests != 0, $"{skill.Name}: a harvest skill names what it harvests.");
                Assert(skill.Use != SkillUse.Make || skill.Station != StationKind.None, $"{skill.Name}: a making skill names its station.");
                Assert(SkillTable.Get(skill.Id) == skill && SkillTable.IsDefined(skill.Id), $"{skill.Name}: found by id.");
            }
            Assert(SkillTable.Get(Skill.None) == null && SkillTable.Get((Skill)15) == null, "None and undefined ids have no row.");
            Assert(SkillTable.SkillFor(SkillWork.Fight(ranged: false)) == Skill.Melee && SkillTable.SkillFor(SkillWork.Fight(ranged: true)) == Skill.Ranged, "A fight's skill, in reach and from range.");
            Assert(SkillTable.SkillFor(SkillWork.Harvest(ItemType.IronOre)) == Skill.Mining && SkillTable.SkillFor(SkillWork.Harvest(ItemType.Glowstone)) == Skill.Mining, "Minerals are mined.");
            Assert(SkillTable.SkillFor(SkillWork.Harvest(ItemType.MedicinalHerb)) == Skill.Foraging, "Herbs are foraged.");
            Assert(SkillTable.SkillFor(SkillWork.Harvest(ItemType.Ration)) == Skill.None && SkillTable.SkillFor(SkillWork.Harvest(ItemType.None)) == Skill.None, "Food at a plot takes no skill.");
            Assert(SkillTable.SkillFor(SkillWork.Make(StationKind.Forge)) == Skill.Smithing && SkillTable.SkillFor(SkillWork.Make(StationKind.Still)) == Skill.Alchemy && SkillTable.SkillFor(SkillWork.Make(StationKind.None)) == Skill.None, "Each station's skill.");
            Assert(SkillTable.SkillFor(SkillWork.Tend()) == Skill.Medicine && SkillTable.SkillFor(default(SkillWork)) == Skill.None, "Tending the fallen is medicine; no work, no skill.");
            Assert(SkillTable.InDisplayOrder.Count == all.Count, "Every skill is listed.");
            TestLog.Log("[PASS] Test 1: seven skills, each with an id, a name and a kind of work, and each kind of work finds its skill.");
        }

        private void ValidateLevelsAndBands()
        {
            Assert(SkillRules.ExperienceFor(0) == 0 && SkillRules.ExperienceFor(1) == 100 && SkillRules.ExperienceFor(3) == 600 && SkillRules.ExperienceFor(10) == 5500, "100, 600 and 5,500 reach levels 1, 3 and 10.");
            int cost = 0;
            for (int level = 1; level <= SkillRules.MaxLevel; level++)
            {
                int step = SkillRules.ExperienceFor(level) - SkillRules.ExperienceFor(level - 1);
                Assert(step > cost, $"Level {level} costs more than the last ({step} after {cost}).");
                cost = step;
                Assert(SkillRules.LevelOf(SkillRules.ExperienceFor(level)) == level && SkillRules.LevelOf(SkillRules.ExperienceFor(level) - 1) == level - 1, $"Level {level} begins at its experience.");
            }
            Assert(SkillRules.LevelOf(60000) == SkillRules.MaxLevel && SkillRules.MaxExperience <= ushort.MaxValue, "Levels stop at 10, and the most experience fits its record.");
            var bands = new[] { SkillBand.Novice, SkillBand.Novice, SkillBand.Novice, SkillBand.Apprentice, SkillBand.Apprentice, SkillBand.Apprentice,
                SkillBand.Journeyman, SkillBand.Journeyman, SkillBand.Journeyman, SkillBand.Master, SkillBand.Master };
            for (int level = 0; level <= SkillRules.MaxLevel; level++) Assert(SkillRules.BandOf(level) == bands[level], $"Level {level} is a {bands[level]}.");
            Assert(SkillRules.GradeOf(2) == Grades.Standard && SkillRules.GradeOf(3) == Grades.Fine && SkillRules.GradeOf(5) == Grades.Fine && SkillRules.GradeOf(6) == Grades.Masterwork && SkillRules.GradeOf(10) == Grades.Masterwork,
                "A Novice makes Standard, an Apprentice Fine, a Journeyman and a Master Masterwork.");
            Assert(SkillRules.ExperienceFor(3) / SkillRules.MakeExperience == 10, "Ten makes take a new hand to Apprentice, as W4's ten took it to Fine.");
            TestLog.Log("[PASS] Test 2: each level costs more than the last, 5,500 in all; the bands and the grades they make.");
        }

        private void ValidateTheSkillSet()
        {
            Assert(System.Runtime.CompilerServices.Unsafe.SizeOf<SkillSet>() == 2 * SkillTable.Capacity, "A skill set is 16 experiences, 32 bytes.");
            var set = default(SkillSet);
            Assert(set.Level(Skill.Mining) == 0 && set.Best() == Skill.None && set.ToString() == "unskilled", "A new set has no skill.");
            Assert(!set.Practise(Skill.Mining, 99, out int level) && level == 0 && set.Experience(Skill.Mining) == 99, "99 experience: not yet a level.");
            Assert(set.Practise(Skill.Mining, 1, out level) && level == 1, "The hundredth: level 1.");
            Assert(set.Practise(Skill.Mining, 1000, out level) && level == 4 && set.Experience(Skill.Mining) == 1100, "Several levels at once are one gain.");
            Assert(set.Level(Skill.Foraging) == 0 && set.Best() == Skill.Mining, "Practice in one skill leaves the others.");
            Assert(set.Practise(Skill.Mining, 60000, out level) && level == SkillRules.MaxLevel && set.Experience(Skill.Mining) == SkillRules.MaxExperience, "Experience stops at the last level's.");
            Assert(!set.Practise(Skill.Mining, 50, out level) && level == SkillRules.MaxLevel, "A master gains no more.");
            Assert(!set.Practise(Skill.None, 50, out _) && !set.Practise((Skill)16, 50, out _) && !set.Practise(Skill.Melee, 0, out _), "No skill, an id past the room and no experience change nothing.");
            // What a source can teach (K9.8a): to its ceiling and no further; a skill past it keeps what it has
            var taught = default(SkillSet);
            Assert(taught.Practise(Skill.Alchemy, 5000, out level, ceiling: 3) && level == 3 && taught.Experience(Skill.Alchemy) == SkillRules.ExperienceFor(3), "A source that teaches to 3 brings a skill to level 3 and no further.");
            Assert(!taught.Practise(Skill.Alchemy, 50, out level, ceiling: 3) && level == 3 && taught.Experience(Skill.Alchemy) == SkillRules.ExperienceFor(3), "At its ceiling, a source teaches nothing more.");
            taught.Set(Skill.Melee, 7);
            Assert(!taught.Practise(Skill.Melee, 50, out level, ceiling: 5) && level == 7 && taught.Experience(Skill.Melee) == SkillRules.ExperienceFor(7), "A skill past a source's ceiling keeps what it has.");
            Assert(PracticeSource.Work.Kind == PracticeKind.Work && PracticeSource.Work.Ceiling == SkillRules.MaxLevel
                && new PracticeSource(PracticeKind.Work, 99).Ceiling == SkillRules.MaxLevel && new PracticeSource(PracticeKind.Work, -1).Ceiling == 0, "The work teaches to the last level; a ceiling is held within the levels.");
            set.Set(Skill.Melee, 6);
            Assert(set.Level(Skill.Melee) == 6 && set.Band(Skill.Melee) == SkillBand.Journeyman && set.ToString() == "Melee 6, Mining 10", "Set to a level; listed in display order.");
            var copy = set;
            Assert(copy == set && copy.Equals(set) && copy.GetHashCode() == set.GetHashCode(), "Two sets alike are equal.");
            copy.Practise(Skill.Medicine, 1, out _);
            Assert(copy != set, "One point of experience tells two sets apart.");
            TestLog.Log("[PASS] Test 3: a skill set gains levels by practice, to the last level (or a source's ceiling) and no further.");
        }

        private void ValidateTheSeam()
        {
            // How good a crawler is at each kind of work (K9.8a): today its learned level in the skill the table gives the work
            var set = default(SkillSet);
            set.Set(Skill.Melee, 4);
            set.Set(Skill.Ranged, 2);
            set.Set(Skill.Mining, 6);
            set.Set(Skill.Smithing, 3);
            set.Set(Skill.Medicine, 9);
            Assert(CrawlerSkills.Level(set, null, SkillWork.Fight(ranged: false)) == 4 && CrawlerSkills.Level(set, null, SkillWork.Fight(ranged: true)) == 2, "A fight reads melee in reach, ranged from range.");
            Assert(CrawlerSkills.Level(set, null, SkillWork.Harvest(ItemType.IronOre)) == 6 && CrawlerSkills.Level(set, null, SkillWork.Harvest(ItemType.MedicinalHerb)) == 0
                && CrawlerSkills.Level(set, null, SkillWork.Harvest(ItemType.Ration)) == 0, "A harvest reads the skill of the item's group; food takes none.");
            Assert(CrawlerSkills.Level(set, null, SkillWork.Make(StationKind.Forge)) == 3 && CrawlerSkills.Level(set, null, SkillWork.Make(StationKind.Still)) == 0
                && CrawlerSkills.Level(set, null, SkillWork.Tend()) == 9, "Making reads the station's skill; tending, medicine.");
            Assert(CrawlerSkills.Level(set, null, default(SkillWork)) == 0 && CrawlerSkills.Level(null, SkillWork.Tend()) == 0, "No work, or nobody: nothing.");
            Assert(SkillWork.Make(StationKind.Forge) == SkillWork.Make(StationKind.Forge) && SkillWork.Make(StationKind.Forge) != SkillWork.Make(StationKind.Still)
                && SkillWork.Harvest(ItemType.IronOre).ToString() == "Harvest IronOre", "A kind of work is a value.");
            TestLog.Log("[PASS] Test 4: the seam reads how good a crawler is at each kind of work: today, its learned level in the work's skill.");
        }

        private void ValidatePracticeIsObserved()
        {
            // Epic 11, S0.2 (D12): practice's one entry tells observers what it granted, after the source's ceiling
            var sim = new SimulationManager(new WorldRegistry(), null);
            var crawler = new Simpiens.Cognition.AgentState(EntityId.Generate());
            var seen = new List<(Skill Skill, int Experience, PracticeKind Kind)>();
            sim.Practised += (agent, skill, experience, source) =>
            {
                Assert(agent == crawler, "The event names the crawler that practised.");
                seen.Add((skill, experience, source.Kind));
            };
            sim.Practise(crawler, SkillWork.Harvest(ItemType.IronOre), 40, PracticeSource.Work);
            Assert(seen.Count == 1 && seen[0] == (Skill.Mining, 40, PracticeKind.Work), "A harvest's practice is told: mining, 40, from the work.");
            sim.Practise(crawler, SkillWork.Fight(ranged: false), 5000, new PracticeSource(PracticeKind.Work, 2));
            Assert(seen.Count == 2 && seen[1] == (Skill.Melee, SkillRules.ExperienceFor(2), PracticeKind.Work), "What is told is what was granted, held to the ceiling.");
            sim.Practise(crawler, SkillWork.Fight(ranged: false), 50, new PracticeSource(PracticeKind.Work, 2));
            sim.Practise(crawler, SkillWork.Harvest(ItemType.Ration), 50, PracticeSource.Work);
            sim.Practise(null, SkillWork.Tend(), 50, PracticeSource.Work);
            Assert(seen.Count == 2, "Nothing granted, no skill for the work, or nobody: nothing is told.");
            TestLog.Log("[PASS] Test 5: practice's one entry tells observers the experience it granted, after the ceiling.");
        }

        private void ValidatePracticeByChallenge()
        {
            // Epic 11, S1.3 (Q8, D5): full practice at or below an act's challenge, less the further a crawler stands above it
            Assert(SkillRules.ChallengePercent(0, 2) == 100 && SkillRules.ChallengePercent(2, 2) == 100 && SkillRules.ChallengePercent(6, 2) == 25
                && SkillRules.ChallengePercent(6, 7) == 100 && SkillRules.ChallengePercent(3, 2) == 75 && SkillRules.ChallengePercent(10, 0) == 10,
                "A rat (2) teaches a Novice fully and a Journeyman 6 a quarter; a beetle (7) teaches a Journeyman fully; a tenth at most far above.");
            int last = 101;
            for (int above = 0; above <= SkillRules.MaxLevel; above++)
            {
                int share = SkillRules.ChallengePercent(above, 0);
                Assert(share <= last && share > 0, $"The share falls with each level above the challenge, never to nothing ({above} above: {share}).");
                last = share;
            }
            var sim = new SimulationManager(new WorldRegistry(), null);
            var veteran = new Simpiens.Cognition.AgentState(EntityId.Generate());
            var skills = default(SkillSet);
            skills.Set(Skill.Melee, 6);
            veteran.Skills = skills;
            int had = veteran.Skills.Experience(Skill.Melee);
            sim.Practise(veteran, SkillWork.Fight(ranged: false), 100, PracticeSource.WorkAt(Simpiens.Simulation.Creatures.CreatureCatalog.Get(Simpiens.Simulation.Creatures.CreatureKind.BurrowRat).Challenge));
            Assert(veteran.Skills.Experience(Skill.Melee) - had == 25, $"A Journeyman takes a quarter of a rat's practice ({veteran.Skills.Experience(Skill.Melee) - had} of 100).");
            had = veteran.Skills.Experience(Skill.Melee);
            sim.Practise(veteran, SkillWork.Fight(ranged: false), 100, PracticeSource.WorkAt(Simpiens.Simulation.Creatures.CreatureCatalog.Get(Simpiens.Simulation.Creatures.CreatureKind.StoneBeetle).Challenge));
            Assert(veteran.Skills.Experience(Skill.Melee) - had == 100, "And all of a beetle's.");
            Assert(PracticeSource.Work.Challenge == SkillRules.MaxLevel && PracticeSource.WorkAt(-3).Challenge == 0 && PracticeSource.WorkAt(40).Challenge == SkillRules.MaxLevel, "The work alone is the highest challenge; a challenge is held within the levels.");
            // Every creature, salvage and recipe says what it demands
            foreach (var kind in Simpiens.Simulation.Creatures.CreatureCatalog.All)
                Assert(kind.Challenge > 0 && (!kind.IsBoss || kind.Challenge >= 9), $"{kind.Name} demands a level ({kind.Challenge}); a boss a Master's.");
            Assert(ItemCatalog.Get(ItemType.MedicinalHerb).HarvestChallenge == 3 && ItemCatalog.Get(ItemType.IronOre).HarvestChallenge == 4 && ItemCatalog.Get(ItemType.Glowstone).HarvestChallenge == 7,
                "Herbs 3, ore 4, glowstone 7.");
            foreach (var recipe in RecipeBook.All)
                Assert(recipe.Challenge > 0 && recipe.Challenge <= SkillRules.MaxLevel, $"The {recipe} demands a level ({recipe.Challenge}).");
            TestLog.Log("[PASS] Test 6: practice by challenge: full at or below an act's demand, a quarter of a rat's for a Journeyman, all of a beetle's; every creature, salvage and recipe has one.");
        }

        private void ValidateTheMasteryBudget()
        {
            // Epic 11, S2.1 (Q6, D7): two skills past Apprentice and one Master at most, first come, first served
            var sim = new SimulationManager(new WorldRegistry(), null);
            var crawler = new Simpiens.Cognition.AgentState(EntityId.Generate());
            var skills = default(SkillSet);
            skills.Set(Skill.Mining, 5);
            skills.Set(Skill.Smithing, 5);
            skills.Set(Skill.Foraging, 5);
            crawler.Skills = skills;
            Assert(SkillRules.BudgetCeiling(crawler.Skills, Skill.Mining) == SkillRules.MaxLevel, "With nothing past Apprentice, every skill may climb to the last level.");
            sim.Practise(crawler, SkillWork.Harvest(ItemType.IronOre), 600, PracticeSource.Work);
            sim.Practise(crawler, SkillWork.Make(StationKind.Forge), 600, PracticeSource.Work);
            Assert(crawler.Skills.Level(Skill.Mining) == 6 && crawler.Skills.Level(Skill.Smithing) == 6, "The first two skills past Apprentice take the slots.");
            sim.Practise(crawler, SkillWork.Harvest(ItemType.MedicinalHerb), 600, PracticeSource.Work);
            Assert(crawler.Skills.Level(Skill.Foraging) == SkillRules.ApprenticeTop && crawler.Skills.Experience(Skill.Foraging) == SkillRules.ExperienceFor(SkillRules.ApprenticeTop),
                $"The third stops at the top of Apprentice ({crawler.Skills.Level(Skill.Foraging)}).");
            Assert(SkillRules.HeldByBudget(crawler.Skills, Skill.Foraging, out int ceiling, out var hold) && ceiling == 5 && hold == BudgetHold.PastApprentice, "It reads as held at 5 by two skills past Apprentice.");
            Assert(!SkillRules.HeldByBudget(crawler.Skills, Skill.Mining, out _, out _), "A skill holding a slot isn't held.");
            // The Master's slot: the first to reach 9 takes it, the other stops at 8
            sim.Practise(crawler, SkillWork.Harvest(ItemType.IronOre), SkillRules.ExperienceFor(9) - crawler.Skills.Experience(Skill.Mining), PracticeSource.WorkAt(SkillRules.MaxLevel));
            sim.Practise(crawler, SkillWork.Make(StationKind.Forge), SkillRules.MaxExperience, PracticeSource.Work);
            Assert(crawler.Skills.Level(Skill.Mining) == 9 && crawler.Skills.Level(Skill.Smithing) == SkillRules.JourneymanTop, $"The second to reach Master stops at 8 ({crawler.Skills.Level(Skill.Smithing)}).");
            Assert(SkillRules.HeldByBudget(crawler.Skills, Skill.Smithing, out ceiling, out hold) && ceiling == 8 && hold == BudgetHold.Master, "It reads as held at 8 by a Master.");
            Assert(SkillRules.KeepsTheBudget(crawler.Skills), "The crawler keeps the budget.");
            // A skill already past its budget keeps what it has (nobody gives a skill up, Q6)
            var over = default(SkillSet);
            over.Set(Skill.Mining, 7);
            over.Set(Skill.Smithing, 7);
            over.Set(Skill.Foraging, 7);
            var veteran = new Simpiens.Cognition.AgentState(EntityId.Generate()) { Skills = over };
            sim.Practise(veteran, SkillWork.Harvest(ItemType.MedicinalHerb), 100, PracticeSource.Work);
            Assert(veteran.Skills.Level(Skill.Foraging) == 7 && veteran.Skills.Experience(Skill.Foraging) == SkillRules.ExperienceFor(7), "A skill past its budget keeps what it has and gains nothing.");
            Assert(!SkillRules.KeepsTheBudget(veteran.Skills), "Three past Apprentice break the budget, which a live scenario would catch.");
            // A starting talent is at most an Apprentice's, so no draw breaks the budget
            Assert(SkillRules.TalentLevel + SkillRules.TalentSpread - 1 <= SkillRules.ApprenticeTop, "A starting talent is at most 5.");
            TestLog.Log("[PASS] Test 7: the mastery budget: the third skill past Apprentice stops at 5, the second Master at 8, first come, first served; a skill past it keeps what it has.");
        }

        private void ValidateLevelZeroChangesNothing()
        {
            Assert(SkillRules.HitBonus(0) == 0f && SkillRules.DamageFactor(0) == 1f && SkillRules.MakeFactor(0) == 1f
                && SkillRules.CarryBonus(0) == 0f && SkillRules.HealFactor(0) == 1f, "At level 0 a fight's, a make's and a rescue's factors are 1 and their bonuses 0.");
            Assert(Math.Abs(SkillRules.HarvestFactor(0) - 0.92f) < 1e-6f && Math.Abs(SkillRules.HarvestFactor(2) - 1f) < 1e-6f && Math.Abs(SkillRules.HarvestFactor(10) - 1.32f) < 1e-6f,
                "The harvest is centred: 8% slower unskilled, the pace before skills at Novice 2, a third faster at Master 10.");
            Assert(Math.Abs(SkillRules.HitBonus(10) - 0.2f) < 1e-6f && Math.Abs(SkillRules.DamageFactor(10) - 1.2f) < 1e-6f
                && Math.Abs(SkillRules.MakeFactor(10) - 1.5f) < 1e-6f && Math.Abs(SkillRules.CarryBonus(10) - 0.3f) < 1e-6f && Math.Abs(SkillRules.HealFactor(10) - 1.5f) < 1e-6f, "A Master 10's factors.");
            Assert(SkillRules.HarvestFactor(99) == SkillRules.HarvestFactor(10) && SkillRules.HarvestFactor(-3) == SkillRules.HarvestFactor(0), "Levels outside 0 to 10 count as the nearest.");
            TestLog.Log("[PASS] Test 8: unskilled, a crawler fights, makes and tends by the numbers before skills; the harvest is centred on a Novice 2.");
        }

        private void ValidateTheDraw()
        {
            const ulong Seed = 424242UL;
            var neutral = AgentPersonality.Neutral;
            var id = new EntityId(7, 0, 0, 0);
            Assert(SkillSet.Generate(Seed, id, neutral) == SkillSet.Generate(Seed, id, neutral), "The same seed and crawler draw the same skills.");
            int differing = 0, talents1 = 0, talents2 = 0, zero = 0, total = 0;
            const int Crawlers = 4000;
            var talentOf = new int[SkillTable.Capacity];
            for (uint i = 1; i <= Crawlers; i++)
            {
                var set = SkillSet.Generate(Seed, new EntityId(i, 1, 2, 3), neutral);
                if (set != SkillSet.Generate(Seed + 1, new EntityId(i, 1, 2, 3), neutral)) differing++;
                int talents = 0;
                foreach (var skill in SkillTable.All)
                {
                    int level = set.Level(skill.Id);
                    Assert(set.Experience(skill.Id) == SkillRules.ExperienceFor(level), "A starting skill sits at its level's first experience.");
                    Assert(level <= SkillRules.TalentLevel + SkillRules.TalentSpread - 1, $"No skill starts above {SkillRules.TalentLevel + SkillRules.TalentSpread - 1} ({level}).");
                    total++;
                    if (level == 0) zero++;
                    if (level < SkillRules.TalentLevel) continue;
                    talents++;
                    talentOf[(byte)skill.Id]++;
                }
                Assert(talents == 1 || talents == 2, $"One talent or two ({talents}).");
                if (talents == 1) talents1++; else talents2++;
            }
            Assert(differing > Crawlers * 9 / 10, "Another seed draws other skills.");
            Assert(Math.Abs(talents2 - Crawlers / 2) < Crawlers / 20, $"About half have a second talent ({talents2} of {Crawlers}).");
            // Not a talent (5.5 of 7 on average), half start at 0
            float zeroShare = zero / (float)total;
            Assert(zeroShare > 0.35f && zeroShare < 0.45f, $"About two in five skills start at nothing ({zeroShare:P0}).");
            foreach (var skill in SkillTable.All)
            {
                float share = talentOf[(byte)skill.Id] / (float)(talents1 + 2 * talents2);
                Assert(share > 0.11f && share < 0.18f, $"With no lean each skill is a talent as often as the next ({skill.Name}: {share:P0}).");
            }
            TestLog.Log($"[PASS] Test 9: the draw is seeded; one talent ({talents1}) or two ({talents2}) at an Apprentice's level, the rest a Novice's.");
        }

        private void ValidatePersonalityLeansTheDraw()
        {
            const ulong Seed = 20261009UL;
            const int Crawlers = 4000;
            var bold = new AgentPersonality(0f, 1f, 0f, 0f, 0f);
            var timid = new AgentPersonality(0f, -1f, 0f, 0f, 0f);
            int boldMelee = 0, timidMelee = 0, boldRanged = 0, timidRanged = 0;
            for (uint i = 1; i <= Crawlers; i++)
            {
                var id = new EntityId(i, 9, 9, 9);
                var a = SkillSet.Generate(Seed, id, bold);
                var b = SkillSet.Generate(Seed, id, timid);
                if (a.Level(Skill.Melee) >= SkillRules.TalentLevel) boldMelee++;
                if (b.Level(Skill.Melee) >= SkillRules.TalentLevel) timidMelee++;
                if (a.Level(Skill.Ranged) >= SkillRules.TalentLevel) boldRanged++;
                if (b.Level(Skill.Ranged) >= SkillRules.TalentLevel) timidRanged++;
            }
            Assert(boldMelee > timidMelee * 2, $"The bold lean toward melee ({boldMelee} against {timidMelee} of {Crawlers}).");
            Assert(timidRanged > boldRanged * 2, $"The cautious lean toward ranged ({timidRanged} against {boldRanged}).");
            Assert(SkillSet.Lean(SkillLean.None, bold) == 0f && SkillSet.Lean(SkillLean.Bold, bold) == 1f && SkillSet.Lean(SkillLean.Cautious, bold) == -1f, "A lean reads its trait.");
            TestLog.Log($"[PASS] Test 10: personality leans the talents: the bold toward melee ({boldMelee} against {timidMelee}), the cautious toward ranged ({timidRanged} against {boldRanged}).");
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("[SkillsValidator] FAILED: " + message);
        }
    }
}
