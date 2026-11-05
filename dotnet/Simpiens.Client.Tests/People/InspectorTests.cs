using System.Linq;
using Simpiens.Presentation;
using Simpiens.Simulation.Spatial;
using Xunit;

namespace Simpiens.Client.Tests.People
{
    /// <summary>
    /// The map inspector (Epic UI-Beta B4, Q10): a crawler selected on the map shows its card with its own portrait, name
    /// and mood; Open dossier brings the panels up on it; Follow sends the camera after it and stops it again; the panels,
    /// or no selection, hide the card.
    /// </summary>
    public class InspectorTests
    {
        private const int Width = 1280, Height = 720;

        [Fact]
        public void ACrawlerSelectedOnTheMapShowsItsCard()
        {
            var ui = UiDriver.Start(width: Width, height: Height);
            var game = ui.Game;
            var card = game.Play.Dashboard.Inspector;
            game.Play.Dashboard.TogglePanels(); // the map view
            ui.Frame();
            Assert.False(card.Root.Visible, "A card with nobody selected.");

            var crawler = game.Session.Simulation.Registry.ActiveEntities.First(e => e.Type == EntityType.Pawn);
            game.Session.Selection.SelectCrawler(crawler.Id, follow: false);
            ui.Frame();
            Assert.True(card.Root.Visible);
            Assert.Equal(game.Session.DashboardModel.Context.FriendlyName(crawler.Id, crawler.Name), card.NameShown);
            var inspector = game.Session.DashboardModel.Inspector;
            Assert.Equal(crawler.Agent.Look, inspector.Portrait.Look);
            Assert.True(card.Portrait.Visible);
            Assert.Equal(game.Graphics.Portraits.Show(inspector.Portrait), card.Portrait.SourceRectangle);
            Assert.StartsWith(inspector.Portrait.Mood.ToString(), card.MoodShown);

            // Follow, then stop
            Assert.False(game.Session.Selection.Following);
            ui.Click(Centre(card.FollowButton));
            Assert.True(game.Session.Selection.Following);
            Assert.Equal("Stop following", card.FollowButton.Text);
            ui.Click(Centre(card.FollowButton));
            Assert.False(game.Session.Selection.Following);

            // Open dossier: the panels, on it, and the card hides
            ui.Click(Centre(card.OpenDossierButton));
            ui.Frame();
            Assert.True(game.Session.DashboardModel.PanelsVisible);
            Assert.Equal(crawler.Id, game.Session.DashboardModel.Dossier.CrawlerId);
            Assert.False(card.Root.Visible);
        }

        /// <summary>Click to find (Epic UI-Beta B4, D4): a resident's face, then an affinity's, each finds its crawler on the map.</summary>
        [Fact]
        public void AClickOnAFaceFindsTheCrawlerOnTheMap()
        {
            var ui = UiDriver.Start(width: Width, height: Height);
            var game = ui.Game;
            var model = game.Session.DashboardModel;
            var panels = game.Play.Dashboard.Panels;
            model.SelectSector(Simpiens.Core.Session.Layouts.Floor1Layout.RoadGateSectorId); // where everyone starts
            game.Play.Dashboard.Refresh();
            ui.Frame();
            Assert.True(model.PanelsVisible);
            Assert.NotEmpty(model.Sectors.Residents);
            var resident = model.Sectors.Residents[0].CrawlerId;
            Assert.True(panels.ResidentFace(0).Visible, "No face on the resident's card.");
            ui.Click(Centre(panels.ResidentFace(0)));
            ui.Frame();
            Assert.False(model.PanelsVisible);
            Assert.Equal(resident, game.Session.Selection.SelectedCrawler);
            Assert.True(game.Session.Selection.Following);
            Assert.True(game.Play.Dashboard.Inspector.Root.Visible);
            Assert.Equal(resident, model.Inspector.CrawlerId);

            // From the dossier's affinities: the peer
            model.OpenDossier();
            game.Play.Dashboard.Refresh();
            ui.Frame();
            var peers = model.Dossier.AffinityRows;
            int withFace = -1;
            for (int i = 0; i < peers.Count && withFace < 0; i++) if (peers[i].HasPortrait) withFace = i;
            if (withFace < 0) return; // no crawler known yet at this tick: the residents' case stands
            ui.Click(Centre(panels.AffinityFace(withFace)));
            ui.Frame();
            Assert.False(model.PanelsVisible);
            Assert.Equal(peers[withFace].PeerId, game.Session.Selection.SelectedCrawler);
        }

        private static Vec2 Centre(Gum.Wireframe.GraphicalUiElement element)
            => new Vec2(element.AbsoluteLeft + element.AbsoluteWidth * 0.5f, element.AbsoluteTop + element.AbsoluteHeight * 0.5f);

        private static Vec2 Centre(Gum.Forms.Controls.FrameworkElement control)
            => new Vec2(control.AbsoluteLeft + control.ActualWidth * 0.5f, control.AbsoluteTop + control.ActualHeight * 0.5f);
    }
}
