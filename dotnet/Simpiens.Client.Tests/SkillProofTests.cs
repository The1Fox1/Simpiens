using System.Linq;
using Simpiens.Simulation.Skills;
using Simpiens.Simulation.Spatial;
using Simpiens.Testing.Economy;
using Xunit;

namespace Simpiens.Client.Tests
{
    /// <summary>
    /// The skill system's proof, the client's half (Epic 10 Part 3, K9.6): with Mycology added by data alone
    /// (<see cref="TestCatalogs.MycologyAndGlowcaps"/>), the dossier's skills line lists it by its row's word among the rest,
    /// the best first, with no code of its own. The simulation's half is the core tests' (SkillProofTests there).
    /// </summary>
    public class SkillProofTests
    {
        [Fact]
        public void TheDossierShowsANewSkill()
        {
            using var extended = TestCatalogs.MycologyAndGlowcaps();
            var ui = UiDriver.Start(width: 1280, height: 720);
            var game = ui.Game;
            var crawler = game.Session.Simulation.Registry.ActiveEntities.First(e => e.Type == EntityType.Pawn);
            var skills = default(SkillSet); // by hand
            skills.Set(Skill.Melee, 2);
            skills.Set(TestCatalogs.Mycology, 6);
            skills.Set(Skill.Smithing, 2);
            crawler.Agent.Skills = skills;
            game.Session.DashboardModel.SelectCrawler(crawler.Id);
            game.Play.Dashboard.Refresh();
            ui.Frame();

            Assert.Equal("Mycology 6 · Melee 2 · Smithing 2", game.Session.DashboardModel.Dossier.Skills); // the best first, ties in display order
            Assert.Equal("Skills: Mycology 6 · Melee 2 (keen) · Smithing 2", game.Play.Dashboard.Panels.SkillsShown); // a keen passion for melee, drawn at spawn (Epic 11, S1.1), shows its flame (S1.6)
        }
    }
}
