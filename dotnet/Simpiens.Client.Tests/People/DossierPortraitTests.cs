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

        /// <summary>
        /// Portraits grow with the window in whole steps (the producer, 2026-11-03): 2x in a 720-row window, 3x at 1080, 4x
        /// at 1440, the speaker's 1x then 2x. A new step bakes the seated portraits again, pixel for pixel, and the dossier
        /// and the inspector widen by as much as their portrait grew.
        /// </summary>
        [Fact]
        public void PortraitsGrowWithTheWindowInWholeSteps()
        {
            Assert.Equal((2, 1), PortraitAtlas.ScalesFor(600));
            Assert.Equal((2, 1), PortraitAtlas.ScalesFor(720));
            Assert.Equal((3, 2), PortraitAtlas.ScalesFor(1080));
            Assert.Equal((4, 2), PortraitAtlas.ScalesFor(1372)); // a 1440 screen less its panel
            Assert.Equal((4, 2), PortraitAtlas.ScalesFor(1440));
            Assert.Equal((4, 2), PortraitAtlas.ScalesFor(2160)); // never past the largest

            var ui = UiDriver.Start(width: Width, height: Height);
            var game = ui.Game;
            var panels = game.Play.Dashboard.Panels;
            var card = game.Play.Dashboard.Inspector;
            var atlas = game.Graphics.Portraits;
            var crawler = game.Session.Simulation.Registry.ActiveEntities.First(e => e.Type == EntityType.Pawn);
            game.Session.DashboardModel.SelectCrawler(crawler.Id);
            ui.Frame();
            Assert.Equal(104, atlas.Size);
            float dossierWidth = DossierPanel(panels).Width, cardWidth = card.Root.Width;

            foreach (var (width, height, scale) in new[] { (1920, 1080, 3), (2560, 1440, 4), (1280, 720, 2) })
            {
                game.RequestWindowSize(width, height);
                ui.Frame(2);
                Assert.Equal(scale, atlas.Scale);
                int size = 52 * scale;
                Assert.Equal((float)size, panels.Portrait.Width);
                Assert.Equal((float)size, card.Portrait.Width);
                Assert.Equal(dossierWidth + size - 104, DossierPanel(panels).Width);
                Assert.Equal(cardWidth + size - 104, card.Root.Width);
                AssertTheAtlasShows(atlas, game.Session.DashboardModel.Dossier.Portrait, panels.Portrait.SourceRectangle);
            }
        }

        // The dossier's column: the panel the portrait sits in
        private static Gum.Wireframe.GraphicalUiElement DossierPanel(Simpiens.Client.Ui.DashboardPanels panels)
        {
            Gum.Wireframe.GraphicalUiElement element = panels.Portrait;
            while (element.Parent is Gum.Wireframe.GraphicalUiElement parent && !ReferenceEquals(parent, panels.Root)) element = parent;
            return element;
        }

        /// <summary>A crawler's face for the lists (Epic UI-Beta B4, D1) is its portrait's head, cropped from the same bake.</summary>
        [Fact]
        public void AFaceIsItsPortraitsHeadFromTheSameBake()
        {
            var game = Offscreen.Start(frames: 1);
            var atlas = game.Graphics.Portraits;
            var crawler = game.Session.Simulation.Registry.ActiveEntities.First(e => e.Type == EntityType.Pawn);
            var key = new PortraitKey(crawler.Agent.Look, Mood.Tired, CrawlerStanding.GuildMember, Simpiens.Simulation.Economy.ItemType.None);
            atlas.Show(key);
            int bakes = atlas.Table.Bakes;
            var source = atlas.ShowFace(key);
            Assert.Equal(bakes, atlas.Table.Bakes); // no second bake
            Assert.Equal((PortraitAtlas.FaceSize, PortraitAtlas.FaceSize), (source.Width, source.Height));
            var framed = new Rgba32[PortraitKit.FramedSize * PortraitKit.FramedSize];
            PortraitKit.BakeFramed(key, framed);
            var expected = new Rgba32[PortraitKit.FaceSize * PortraitKit.FaceSize];
            PortraitKit.Face(framed, key.Standing, expected);
            Assert.Equal(PortraitKit.FrameOf(CrawlerStanding.GuildMember).Outer, expected[0]);
            var actual = new Color[expected.Length];
            atlas.Texture.GetData(0, source, actual, 0, actual.Length);
            for (int i = 0; i < actual.Length; i++)
                Assert.Equal(new Color(expected[i].R, expected[i].G, expected[i].B, expected[i].A), actual[i]);
        }

        // The atlas's pixels under the dossier's source rectangle are the key's portrait, magnified
        private static void AssertTheAtlasShows(PortraitAtlas atlas, in PortraitKey key, Rectangle source)
        {
            int size = atlas.Size;
            Assert.Equal((size, size), (source.Width, source.Height));
            var framed = new Rgba32[PortraitKit.FramedSize * PortraitKit.FramedSize];
            PortraitKit.BakeFramed(key, framed);
            var expected = new Rgba32[size * size];
            PortraitKit.Magnify(framed, PortraitKit.FramedSize, atlas.Scale, expected);
            var actual = new Color[size * size];
            atlas.Texture.GetData(0, source, actual, 0, actual.Length);
            for (int i = 0; i < actual.Length; i++)
            {
                var e = expected[i];
                if (actual[i] != new Color(e.R, e.G, e.B, e.A)) throw new Xunit.Sdk.XunitException($"The atlas differs from the portrait at pixel {i % size}, {i / size}.");
            }
        }
    }
}
