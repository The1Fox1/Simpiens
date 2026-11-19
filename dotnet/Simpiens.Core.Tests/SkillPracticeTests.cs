using System.Collections.Generic;
using System.Linq;
using Simpiens.Cognition;
using Simpiens.Core.Session;
using Simpiens.Hosting;
using Simpiens.Narrative;
using Simpiens.Simulation;
using Simpiens.Simulation.Creatures;
using Simpiens.Simulation.Economy;
using Simpiens.Simulation.Items;
using Simpiens.Simulation.Skills;
using Xunit;

namespace Simpiens.Core.Tests
{
    /// <summary>
    /// Practice (Epic 10 Part 3, K9.2, Q18), on the real outpost: mechanism tests, the fighters put in place by hand. A blow
    /// landed in reach is melee practice and a miss none; a shot is ranged practice, hit or miss; every unit of salvage the
    /// residents harvest in their own day is mining or foraging practice; a level gained within a band is only logged, and a
    /// band reached is told in the chronicle. (A make's practice is with the stations' tests, a rescue's with the rescues'.)
    /// </summary>
    public class SkillPracticeTests
    {
        [Fact]
        public void ABlowLandedIsMeleePracticeAndAShotRangedPractice()
        {
            using var sim = Outpost();
            var residents = sim.Host.CrawlerStates.Where(s => !s.IsDrifter).Take(2).ToList();
            var brawler = residents[0];
            var archer = residents[1];
            Assert.True(brawler.Inventory.TryEquip(Outfitting.NewPiece(ItemType.IronBlade, Grades.Standard)));
            Assert.True(archer.Inventory.TryEquip(Outfitting.NewPiece(ItemType.Crossbow, Grades.Standard)));
            int brawlersRanged = brawler.Skills.Experience(Skill.Ranged), archersMelee = archer.Skills.Experience(Skill.Melee);
            // Each act of practice at the work itself, as practice's one entry tells it (Epic 11, S0.2): its amount moves with
            // passions and challenge; a mentor's lesson beside it is an act of its own (S3.1), not counted here
            var acts = new List<(Simpiens.Cognition.AgentState Who, Skill Skill, int Experience)>();
            sim.Simulation.Practised += (who, skill, experience, source) => { if (source.Kind == PracticeKind.Work) acts.Add((who, skill, experience)); };
            var service = sim.Simulation.Creatures;
            var beetle = service.Creatures.First(c => c.Kind == CreatureKind.StoneBeetle);
            var brawlerNode = sim.Registry.GetEntity(brawler.AgentId);
            var archerNode = sim.Registry.GetEntity(archer.AgentId);
            int hits, shots = 0;
            for (int t = 0; t < 300 && beetle.IsAlive; t++)
            {
                // By hand: the beetle held in the hub, the brawler at its side, the archer four units off; both ask to strike
                beetle.Position = new Vec2(2f, 2f);
                brawler.Position = new Vec2(1.1f, 2f);
                archer.Position = new Vec2(2f, -2.5f);
                brawler.Health = archer.Health = 100f;
                beetle.Health = beetle.Definition.MaxHealth; // and kept whole, so the fight runs on
                service.AskSwing(brawlerNode, beetle.Id);
                service.AskSwing(archerNode, beetle.Id);
                sim.Step(1);
                shots += service.ShotsThisTick.Count(s => s.Shooter == archer.AgentId);
            }
            hits = acts.Count(a => a.Who == brawler && a.Skill == Skill.Melee);
            Assert.True(shots > 0 && hits > 0, $"Both struck ({hits} blows landed, {shots} shots).");
            Assert.Equal(shots, acts.Count(a => a.Who == archer && a.Skill == Skill.Ranged)); // every shot, hit or miss
            Assert.True(acts.Where(a => a.Who == brawler && a.Skill == Skill.Melee).All(a => a.Experience > 0 && a.Experience <= 2 * SkillRules.HitExperience), "A blow's practice, at most a burning passion's double.");
            Assert.True(acts.Where(a => a.Who == archer && a.Skill == Skill.Ranged).All(a => a.Experience > 0 && a.Experience <= 2 * SkillRules.ShotExperience), "A shot's practice, at most a burning passion's double.");
            Assert.Equal(brawlersRanged, brawler.Skills.Experience(Skill.Ranged));
            Assert.Equal(archersMelee, archer.Skills.Experience(Skill.Melee));
        }

        [Fact]
        public void EveryUnitHarvestedIsMiningOrForagingPractice()
        {
            using var sim = Outpost();
            var residents = sim.Host.CrawlerStates.Where(s => !s.IsDrifter).ToList();
            var harvestsAtStart = residents.ToDictionary(r => r.AgentId, r => r.Activity.SalvageHarvests);
            // Each act of practice at the work itself, as practice's one entry tells it (Epic 11, S0.2): its amount moves with
            // passions and challenge; a mentor's lesson beside it is an act of its own (S3.1), not counted here
            var acts = new List<(Simpiens.Cognition.AgentState Who, Skill Skill, int Experience)>();
            sim.Simulation.Practised += (who, skill, experience, source) => { if (source.Kind == PracticeKind.Work) acts.Add((who, skill, experience)); };
            sim.Step(3000); // their own morning: nothing by hand
            int units = 0;
            foreach (var r in residents)
            {
                int harvested = r.Activity.SalvageHarvests - harvestsAtStart[r.AgentId];
                var mine = acts.Where(a => a.Who == r && (a.Skill == Skill.Mining || a.Skill == Skill.Foraging)).ToList();
                units += harvested;
                Assert.Equal(harvested, mine.Count); // one act of practice a unit (a skill at its ceiling would tell none: nobody is at 10)
                Assert.True(mine.All(a => a.Experience > 0 && a.Experience <= 2 * SkillRules.UnitExperience), "A unit's practice, at most a burning passion's double.");
            }
            Assert.True(units > 10, $"The residents harvested in their morning ({units} units).");
        }

        [Fact]
        public void ABandReachedIsToldAndALevelWithinItIsNot()
        {
            using var sim = Outpost();
            var crawler = sim.Host.CrawlerStates.First(s => !s.IsDrifter);
            var skills = default(SkillSet);
            crawler.Skills = skills; // by hand: unskilled
            ((ISkillPractice)sim.Simulation).Practise(crawler, Mining, SkillRules.ExperienceFor(2), PracticeSource.Work); // Novice 2: a level, not a band
            Assert.Equal(2, crawler.Skills.Level(Skill.Mining));
            Assert.DoesNotContain(Told(sim), e => e.EventType == ChronicleEventType.SkillBandReached);
            ((ISkillPractice)sim.Simulation).Practise(crawler, Mining, SkillRules.ExperienceFor(3) - SkillRules.ExperienceFor(2), PracticeSource.Work); // Apprentice
            var told = Assert.Single(Told(sim), e => e.EventType == ChronicleEventType.SkillBandReached);
            Assert.Equal(crawler.AgentId, told.ActorAId);
            Assert.Equal((int)Skill.Mining * 256 + (int)SkillBand.Apprentice, (int)told.ExtraData);
            Assert.EndsWith(" is now an apprentice miner.", ((INarrativeFormatter)sim.Services.GetService(typeof(INarrativeFormatter))).FormatEvent(told));
            ((ISkillPractice)sim.Simulation).Practise(crawler, default, 500, PracticeSource.Work);
            ((ISkillPractice)sim.Simulation).Practise(null, Mining, 500, PracticeSource.Work);
            Assert.Equal(SkillRules.ExperienceFor(3), crawler.Skills.Experience(Skill.Mining)); // no work, nobody: nothing
        }

        /// <summary>
        /// Practice enters by the work done and its source (K9.8a): the simulation raises the work's skill, no higher than
        /// what the source can teach; the work itself teaches to the last level.
        /// </summary>
        [Fact]
        public void PracticeIsHeldToWhatItsSourceCanTeach()
        {
            using var sim = Outpost();
            var crawler = sim.Host.CrawlerStates.First(s => !s.IsDrifter);
            crawler.Skills = default; // by hand: unskilled
            var practice = (ISkillPractice)sim.Simulation;
            practice.Practise(crawler, Mining, SkillRules.MaxExperience, new PracticeSource(PracticeKind.Work, 5));
            Assert.Equal(SkillRules.ExperienceFor(5), crawler.Skills.Experience(Skill.Mining));
            Assert.Equal(0, crawler.Skills.Experience(Skill.Foraging)); // the work's skill alone
            practice.Practise(crawler, Mining, 100, new PracticeSource(PracticeKind.Work, 5));
            Assert.Equal(SkillRules.ExperienceFor(5), crawler.Skills.Experience(Skill.Mining)); // at the ceiling: nothing more
            practice.Practise(crawler, Mining, SkillRules.MaxExperience, PracticeSource.Work);
            Assert.Equal(SkillRules.MaxLevel, crawler.Skills.Level(Skill.Mining));
            Assert.Equal(SkillRules.MaxLevel, CrawlerSkills.Level(crawler, Mining)); // what every effect reads
        }

        private static readonly SkillWork Mining = SkillWork.Harvest(ItemType.IronOre);

        private static List<ChronicleEvent> Told(LocalSimulation sim)
        {
            var told = new List<ChronicleEvent>();
            sim.Chronicle.GetAllRecentEvents(told, 256);
            return told;
        }

        private static LocalSimulation Outpost()
        {
            var sim = LocalSimulation.Create(SimulationSettings.DefaultSeed);
            sim.Respawn(OutpostSession.SessionId);
            sim.Step(20);
            return sim;
        }
    }
}
