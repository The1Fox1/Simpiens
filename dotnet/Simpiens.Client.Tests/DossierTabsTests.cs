using System.Linq;
using Gum.Wireframe;
using Simpiens.Client.Ui;
using Simpiens.Simulation.Life;
using Xunit;

namespace Simpiens.Client.Tests
{
    /// <summary>
    /// The dossier's tabs (Epic LIFE-1, L1.7, D10; the epic's F4): Crawler, Mind and Today beside the state's badge, one open at a
    /// time and its button lit; Mind shows the mood with its reason and what weighs on it; Today the day's lines; and each tab
    /// ends inside the frame at 720 rows.
    /// </summary>
    public class DossierTabsTests
    {
        [Fact]
        public void TheTabsShowTheCrawlerItsMindAndItsDay()
        {
            var ui = UiDriver.Start(new ClientOptions(), 1280, 720);
            var crawler = ui.Game.Session.Simulation.Host.CrawlerStates.First(c => !c.IsDrifter);
            long now = ui.Game.Session.Simulation.Clock.CurrentTick;
            var life = crawler.Life; // a thought, so the day has a line (a view test; the live scenario thinks them)
            ThoughtRules.Think(ref life, Thought.SleptInABunk, default, (uint)now);
            crawler.Life = life;
            ui.Game.Session.DashboardModel.SelectCrawler(crawler.AgentId);
            ui.Frame(10);
            var panels = ui.Game.Play.Dashboard.Panels;
            Assert.Equal(0, panels.TabShown); // the Crawler tab first, as the dossier was

            panels.ShowTab(1);
            ui.Frame(5);
            Assert.Equal(1, panels.TabShown);
            Assert.Contains("(", panels.MindMoodShown); // "Good (+12): Slept in a bunk"
            Assert.Contains(panels.MindLinesShown, l => l.StartsWith("Slept in a bunk +3"));

            panels.ShowTab(2);
            ui.Frame(5);
            Assert.Contains(panels.TodayLinesShown, l => l.EndsWith("Slept in a bunk"));

            panels.ShowTab(0);
            ui.Frame(2);
            Assert.Equal(0, panels.TabShown);
            Assert.True(panels.RecruitButton.Visual.Visible || panels.RecruitButton.Visual.Parent != null);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        public void EachTabFitsAt720Rows(int tab)
        {
            var ui = UiDriver.Start(new ClientOptions(), 1280, 720);
            var crawler = ui.Game.Session.Simulation.Host.CrawlerStates.First(c => !c.IsDrifter);
            ui.Game.Session.DashboardModel.SelectCrawler(crawler.AgentId);
            ui.Frame(10);
            var panels = ui.Game.Play.Dashboard.Panels;
            panels.ShowTab(tab);
            ui.Frame(5);
            float frame = panels.Root.AbsoluteTop + panels.Root.AbsoluteHeight - DashboardPanels.Outer - DashboardPanels.Margin;
            var list = tab == 1 ? panels.MindList : panels.TodayList;
            Assert.True(Bottom(list) <= frame, $"Inside the frame: the tab's list ends at {Bottom(list)}, the frame at {frame}.");
        }

        private static float Bottom(GraphicalUiElement control) => control.AbsoluteTop + control.AbsoluteHeight;
    }
}
