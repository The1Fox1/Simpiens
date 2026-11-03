using System;
using System.Linq;
using Microsoft.Xna.Framework;
using Simpiens.Presentation;
using Simpiens.Presentation.Art.People;
using Simpiens.Simulation.Spatial;
using Xunit;

namespace Simpiens.Client.Tests.People
{
    /// <summary>
    /// The selected crawler's portrait is its own (Epic UI-Beta B3, Q12): selecting a crawler shows, in the dossier's
    /// header, the portrait baked from its look and its mood now, written into the portrait atlas pixel for pixel; selecting
    /// another shows its; a change of mood bakes a new portrait once the face's hold is over.
    /// </summary>
    public class DossierPortraitTests
    {
        private const int Width = 1280, Height = 720;

        [Fact]
        public void TheSelectedCrawlersPortraitIsItsOwn()
        {
            var ui = UiDriver.Start(width: Width, height: Height);
            var game = ui.Game;
            var panels = game.Play.Dashboard.Panels;
            var atlas = game.Graphics.Portraits;
            var crawlers = game.Session.Simulation.Registry.ActiveEntities.Where(e => e.Type == EntityType.Pawn).Take(3).ToList();
            foreach (var crawler in crawlers)
            {
                game.Session.DashboardModel.SelectCrawler(crawler.Id);
                ui.Frame();
                var dossier = game.Session.DashboardModel.Dossier;
                Assert.True(panels.Portrait.Visible, "No portrait in the dossier.");
                Assert.Equal(crawler.Agent.Look, dossier.Portrait.Look);
                Assert.True(game.Session.DashboardModel.Moods.TryGet(crawler.Id, out var mood) && mood == dossier.Portrait.Mood);
                Assert.Equal(WorldViewFrame.StandingOf(crawler.Agent), dossier.Portrait.Standing);
                Assert.True(atlas.Table.TrySlotOf(dossier.Portrait, out _), "The portrait isn't in the atlas.");
                AssertTheAtlasShows(atlas, dossier.Portrait, panels.Portrait.SourceRectangle);
            }
        }

        [Fact]
        public void AChangeOfMoodBakesANewPortrait()
        {
            var ui = UiDriver.Start(width: Width, height: Height);
            var game = ui.Game;
            var crawler = game.Session.Simulation.Registry.ActiveEntities.First(e => e.Type == EntityType.Pawn);
            game.Session.DashboardModel.SelectCrawler(crawler.Id);
            ui.Frame();
            var before = game.Session.DashboardModel.Dossier.Portrait;
            crawler.Agent.Health = 20f; // the precondition, by hand: this checks the header's path, and hurt shows at once
            ui.Frame();
            var after = game.Session.DashboardModel.Dossier.Portrait;
            Assert.Equal(Mood.Hurt, after.Mood);
            Assert.NotEqual(before, after);
            AssertTheAtlasShows(game.Graphics.Portraits, after, game.Play.Dashboard.Panels.Portrait.SourceRectangle);
            Assert.StartsWith("Hurt. Health 20%", game.Play.Dashboard.Panels.MoodShown);
        }

        // The atlas's pixels under the dossier's source rectangle are the key's portrait, magnified
        private static void AssertTheAtlasShows(PortraitAtlas atlas, in PortraitKey key, Rectangle source)
        {
            Assert.Equal((PortraitAtlas.Size, PortraitAtlas.Size), (source.Width, source.Height));
            var framed = new Rgba32[PortraitKit.FramedSize * PortraitKit.FramedSize];
            PortraitKit.BakeFramed(key, framed);
            var expected = new Rgba32[PortraitAtlas.Size * PortraitAtlas.Size];
            PortraitKit.Magnify(framed, PortraitKit.FramedSize, PortraitAtlas.Scale, expected);
            var actual = new Color[PortraitAtlas.Size * PortraitAtlas.Size];
            atlas.Texture.GetData(0, source, actual, 0, actual.Length);
            for (int i = 0; i < actual.Length; i++)
            {
                var e = expected[i];
                if (actual[i] != new Color(e.R, e.G, e.B, e.A)) throw new Xunit.Sdk.XunitException($"The atlas differs from the portrait at pixel {i % PortraitAtlas.Size}, {i / PortraitAtlas.Size}.");
            }
        }
    }
}
