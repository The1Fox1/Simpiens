using System.Linq;
using Simpiens.Core.Session.Layouts;
using Simpiens.Presentation;
using Simpiens.Simulation.Spatial;
using Xunit;

namespace Simpiens.Client.Tests
{
    /// <summary>The Tab dashboard (Epic CLIENT M2.6): its three panels, in step with the map, driven with the scripted pointer.</summary>
    public class DashboardPanelsTests
    {
        private const int Width = 1280, Height = 720;

        private static Vec2 Centre(Gum.Wireframe.GraphicalUiElement element)
            => new Vec2(element.AbsoluteLeft + element.GetAbsoluteWidth() * 0.5f, element.AbsoluteTop + element.GetAbsoluteHeight() * 0.5f);

        private static Vec2 Centre(Gum.Forms.Controls.FrameworkElement control)
            => new Vec2(control.AbsoluteLeft + control.ActualWidth * 0.5f, control.AbsoluteTop + control.ActualHeight * 0.5f);

        [Fact]
        public void TheGameStartsOnThePanelsAndTheyKeepClicksOffTheMap()
        {
            var ui = UiDriver.Start(width: Width, height: Height);
            var game = ui.Game;
            Assert.True(game.DashboardModel.PanelsVisible, "Unity started on the dashboard.");
            Assert.True(game.Dashboard.Panels.IsShown);
            Assert.Equal(game.Simulation.Sectors.All.Count, game.Dashboard.Panels.SectorCards.Count(c => c.Root.Visible));

            var crawler = game.Simulation.Registry.ActiveEntities.First(e => e.Type == EntityType.Pawn);
            ui.Click(game.View.WorldToScreen(crawler.Position));
            Assert.Equal(default, game.Selection.SelectedCrawler);
            Assert.True(game.Ui.PointerOverUi);
        }

        [Fact]
        public void ASectorAndAResidentPickedHereAreSelectedOnTheMap()
        {
            var ui = UiDriver.Start(width: Width, height: Height);
            var game = ui.Game;
            var panels = game.Dashboard.Panels;

            var gate = panels.SectorCards.First(c => c.SectorId == Floor1Layout.RoadGateSectorId);
            ui.Click(Centre(gate.Root));
            Assert.Equal("ROAD GATE", panels.SectorTitle);
            Assert.True(gate.Selected);
            Assert.Equal(Floor1Layout.RoadGateSectorId, game.Selection.SelectedSector);
            Assert.Equal(12, panels.ResidentsShown);

            var resident = panels.ResidentCards[3];
            ui.Click(Centre(resident.Root));
            Assert.Equal(resident.CrawlerId, game.Selection.SelectedCrawler);
            Assert.True(game.Selection.Following, "Back on the map, the camera follows the dossier's crawler.");
            Assert.True(panels.DossierShown);
            var agent = game.Simulation.Registry.GetEntity(resident.CrawlerId).Agent;
            Assert.Equal(agent.Hunger, panels.HungerShown);
            Assert.Contains("[", panels.DossierName);
            Assert.True(panels.ResidentCards[3].Selected);
        }

        [Fact]
        public void AClickOnTheMapShowsThatCrawlerAndItsRoomOnThePanels()
        {
            var ui = UiDriver.Start(width: Width, height: Height);
            var game = ui.Game;
            ui.Ticks(300);
            game.Dashboard.TogglePanels();
            ui.Frame();
            var walker = game.Simulation.Registry.ActiveEntities.First(e => e.Type == EntityType.Pawn && game.Simulation.Sectors.GetSectorAt(e.Position).Id != Floor1Layout.RoadGateSectorId);
            ui.Click(game.View.WorldToScreen(walker.Position));
            Assert.Equal(walker.Id, game.Selection.SelectedCrawler);

            game.Dashboard.TogglePanels();
            ui.Frame();
            var room = game.Simulation.Sectors.GetSectorAt(walker.Position);
            Assert.Equal(room.Name.ToUpperInvariant(), game.Dashboard.Panels.SectorTitle);
            Assert.True(game.Dashboard.Panels.DossierShown);
            Assert.Contains(game.DashboardModel.Context.FriendlyName(walker.Id, walker.Name), game.Dashboard.Panels.DossierName);
        }

        [Fact]
        public void CalmFrustrationCalmsTheCrawlerOnTheNextTick()
        {
            var ui = UiDriver.Start(width: Width, height: Height);
            var game = ui.Game;
            var crawler = game.Simulation.Registry.ActiveEntities.First(e => e.Type == EntityType.Pawn);
            game.DashboardModel.SelectCrawler(crawler.Id);
            ui.Frame();
            crawler.Agent.Frustration = 90f; // the precondition, by hand: this checks the button's path
            ui.Click(Centre(game.Dashboard.Panels.CalmButton));
            Assert.Equal(90f, crawler.Agent.Frustration);
            ui.Ticks(1);
            Assert.Equal(0f, crawler.Agent.Frustration);
        }

        [Fact]
        public void TheDashboardLooksAsRecorded()
        {
            var game = Offscreen.Start(new ClientOptions { TicksPerUpdate = 100 }, frames: 3, Width, Height);
            game.Simulation.Clock.SetTimeMultiplier(1f);
            var crawler = game.Simulation.Registry.ActiveEntities.First(e => e.Type == EntityType.Pawn && game.Simulation.Sectors.GetSectorAt(e.Position).Id == Floor1Layout.HubSectorId);
            game.DashboardModel.SelectSector(Floor1Layout.HubSectorId);
            game.DashboardModel.SelectCrawler(crawler.Id);
            game.Dashboard.Refresh();
            game.RunOneFrame();
            RenderTests.CompareWithReference("render-outpost-dashboard-t300", FrameCapture.Capture(game, Width, Height), Width, Height);
        }
    }
}
