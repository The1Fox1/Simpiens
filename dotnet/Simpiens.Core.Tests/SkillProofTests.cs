using System.Linq;
using Simpiens.Cognition;
using Simpiens.Cognition.Contracts;
using Simpiens.Cognition.Planning.Handlers;
using Simpiens.Core.Session;
using Simpiens.Hosting;
using Simpiens.Simulation;
using Simpiens.Simulation.Economy;
using Simpiens.Simulation.Persistence;
using Simpiens.Simulation.Skills;
using Simpiens.Simulation.Spatial;
using Simpiens.Testing.Economy;
using Simpiens.Testing.Environment;
using Xunit;

namespace Simpiens.Core.Tests
{
    /// <summary>
    /// The skill system's proof (Epic 10 Part 3, K9.6; Q17, "appendable, like the slots"): a skill is added to the game by
    /// data alone. Mycology, the harvest of fungi, and glowcaps, a salvage of that group, join through the catalogues' test
    /// hooks (<see cref="TestCatalogs.MycologyAndGlowcaps"/>); no other code knows them. Every crawler draws it at spawn, a
    /// glowcap harvested is practice in it and nothing else, its level quickens that harvest, and a live save holds it with
    /// no change of format. The client's half, the dossier's line, is the client's tests' (<c>SkillProofTests</c> there).
    /// </summary>
    public class SkillProofTests
    {
        [Fact]
        public void EveryCrawlerDrawsANewSkillAtSpawn()
        {
            using var extended = TestCatalogs.MycologyAndGlowcaps();
            Assert.Equal(9, SkillTable.All.Count); // the eight, Leadership among them (Epic 11, S5.1), and Mycology
            Assert.Equal(TestCatalogs.Mycology, SkillTable.SkillFor(SkillWork.Harvest(TestCatalogs.Glowcap)));
            Assert.Equal(Skill.Mining, SkillTable.SkillFor(SkillWork.Harvest(ItemType.IronOre))); // the others' work keeps its skill

            using var sim = LocalSimulation.Create(SimulationSettings.DefaultSeed);
            sim.Respawn(OutpostSession.SessionId);
            var residents = sim.Host.CrawlerStates.Where(c => !c.IsDrifter).ToList();
            var levels = residents.Select(c => c.Skills.Level(TestCatalogs.Mycology)).ToList();
            TestContext.Current.TestOutputHelper?.WriteLine("Mycology at spawn: " + string.Join(", ", levels));
            Assert.Contains(levels, l => l > 0); // a Novice's 1 or 2 for half the outpost, or a talent
            Assert.All(levels, l => Assert.InRange(l, 0, SkillRules.TalentLevel + SkillRules.TalentSpread - 1));
            Assert.All(residents, c => Assert.True(c.Skills == SkillSet.Generate(SimulationSettings.DefaultSeed, c.AgentId, c.Personality), "The draw is the table's, row and all."));
        }

        [Fact]
        public void ANewSkillIsPractisedByItsWorkAndQuickensIt()
        {
            using var extended = TestCatalogs.MycologyAndGlowcaps();
            (int Ticks, SkillSet After) Harvest(int level)
            {
                var registry = new WorldRegistry();
                var sim = new SimulationManager(registry, null);
                var nodeId = new EntityId(0x6901, 0x6901, 0x3001, 0xC0DE);
                HeadlessEntities.Prop(registry, EntityType.Resource, Vec2.Zero, id: nodeId, resourceKind: TestCatalogs.Glowcap, name: "[Test_GlowcapNode]");
                sim.RegisterResource(new ResourceData { EntityId = nodeId, RemainingYield = 5, MaxYield = 5, YieldItem = TestCatalogs.Glowcap, RegrowTicks = 0 });
                var crawler = HeadlessEntities.Crawler(registry, sim, null, new Vec2(0.5f, 0f), id: new EntityId(0x6902, 0x6901, 0x3001, 0xC0DE), name: "[Test_Mycologist]");
                var skills = default(SkillSet);
                skills.Set(TestCatalogs.Mycology, level);
                crawler.Agent.Skills = skills;
                Assert.Equal(SkillRules.HarvestFactor(level), HarvestResourceIntentHandler.MiningSpeed(crawler.Agent, TestCatalogs.Glowcap), 4);
                var state = new ActiveIntentState { Intent = new HarvestResourceIntent(crawler.Node.Id, nodeId, null), LastSampledPosition = crawler.Node.Position };
                int ticks = 0;
                var result = IntentResult.InProgress;
                while (result == IntentResult.InProgress && ticks < HeadlessIntents.MaxTicks)
                {
                    result = sim.ExecuteIntent(crawler.Node, state);
                    ticks++;
                }
                Assert.True(result == IntentResult.Success && crawler.Agent.Inventory.GetItemCount(TestCatalogs.Glowcap) == 1, $"A glowcap harvested ({result}).");
                return (ticks, crawler.Agent.Skills);
            }

            var (unskilled, practised) = Harvest(0);
            var (master, _) = Harvest(SkillRules.MaxLevel);
            TestContext.Current.TestOutputHelper?.WriteLine($"a glowcap takes {unskilled} ticks unskilled, {master} at Mycology {SkillRules.MaxLevel}");
            Assert.Equal(SkillRules.UnitExperience, practised.Experience(TestCatalogs.Mycology)); // a unit is practice in the skill its row names
            Assert.Equal(0, practised.Experience(Skill.Mining));
            Assert.Equal(0, practised.Experience(Skill.Foraging));
            float pace = SkillRules.HarvestFactor(SkillRules.MaxLevel) / SkillRules.HarvestFactor(0);
            Assert.True(master < unskilled && System.Math.Abs(master * pace - unskilled) <= 2f, $"A master takes {master} ticks to an unskilled hand's {unskilled}: {pace:F2} times as fast.");
        }

        [Fact]
        public void ALiveSaveHoldsTheNewSkill()
        {
            using var extended = TestCatalogs.MycologyAndGlowcaps();
            using var played = LocalSimulation.Create(SimulationSettings.DefaultSeed);
            played.Respawn(OutpostSession.SessionId);
            var crawler = played.Host.CrawlerStates[2];
            crawler.Practise(TestCatalogs.Mycology, 437, out _); // by hand, on what it drew
            int experience = crawler.Skills.Experience(TestCatalogs.Mycology);
            played.Step(10);
            byte[] saved = SaveSerializer.Serialize(played.Host.Capture());

            using var loaded = LocalSimulation.Create(SimulationSettings.DefaultSeed);
            loaded.Respawn(OutpostSession.SessionId);
            Assert.True(loaded.Host.TryRestore(SaveSerializer.Deserialize<SessionSave>(saved), out string refusal), $"The save was refused: {refusal}");
            var back = loaded.Host.CrawlerStates.First(c => c.AgentId == crawler.AgentId);
            Assert.Equal(experience, back.Skills.Experience(TestCatalogs.Mycology));
            Assert.True(back.Skills == crawler.Skills, "Every skill came back.");
            Assert.Equal(saved, SaveSerializer.Serialize(loaded.Host.Capture()));
            Assert.Equal(SessionSaveHeader.CurrentFormatVersion, loaded.Host.Capture().Header.FormatVersion); // no change of format (K9.1)
        }
    }
}
