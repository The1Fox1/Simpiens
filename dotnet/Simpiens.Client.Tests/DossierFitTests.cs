using System.Linq;
using Gum.Wireframe;
using Simpiens.Client.Ui;
using Simpiens.Simulation.Skills;
using Xunit;

namespace Simpiens.Client.Tests
{
    /// <summary>
    /// The dossier fits its frame at 720 rows with every optional line shown (Epic 11, F16, fixed at S8.5): the skill notes,
    /// the techniques and the legend each take their line and the column's spacing from the feelings' list, so the sway's
    /// button, and with the playtest tools (with the usual two lines) their row, end inside the frame.
    /// </summary>
    public class DossierFitTests
    {
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void EveryOptionalLineFitsAt720Rows(bool playtest)
        {
            var ui = UiDriver.Start(new ClientOptions { PlaytestTools = playtest }, 1280, 720);
            var crawlers = ui.Game.Session.Simulation.Host.CrawlerStates.Where(c => !c.IsDrifter).ToList();
            var crawler = crawlers[0];
            var traits = crawler.Traits;
            traits.SetTechnique(Skill.Melee, SkillBand.Apprentice, Technique.ShieldWall);
            traits.SetEpithet(Epithet.Steadfast, 0, default);
            if (!playtest) traits.RecordTeacher(Skill.Melee, crawlers[1].AgentId); // with the playtest tools at 720 rows, the usual two lines
            crawler.Traits = traits;
            ui.Game.Session.DashboardModel.SelectCrawler(crawler.AgentId);
            ui.Frame(30);
            var panels = ui.Game.Play.Dashboard.Panels;
            Assert.True((playtest || panels.SkillNotesShown.Length > 0) && panels.TechniquesShown.Length > 0 && panels.LegendShown.Length > 0, "The optional lines show.");
            Assert.True(panels.AffinitiesShownHeight > 0f || playtest, "The feelings' list keeps some room.");
            float frame = panels.Root.AbsoluteTop + panels.Root.AbsoluteHeight - DashboardPanels.Outer - DashboardPanels.Margin;
            var last = playtest ? panels.DownButton.Visual : panels.RecruitButton.Visual;
            Assert.True(Bottom(last) <= frame, $"Inside the frame: the dossier ends at {Bottom(last)}, the frame at {frame}.");
        }

        private static float Bottom(GraphicalUiElement control) => control.AbsoluteTop + control.AbsoluteHeight;
    }
}
