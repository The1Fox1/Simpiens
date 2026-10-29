using System;
using System.Linq;
using Simpiens.Core.Session;
using Simpiens.Entities;
using Simpiens.Hosting;
using Simpiens.Presentation;
using Simpiens.Presentation.Art;
using Simpiens.Simulation;
using Simpiens.Simulation.Economy;
using Simpiens.Simulation.Spatial;
using Xunit;

namespace Simpiens.Client.Tests
{
    /// <summary>
    /// Art catalogues (M3 design D4, plan step 1.1; graphics V2): the scene draws what its view mode's catalogue says
    /// (sprite, size, pivot) and where the projection says, deciding neither itself.
    /// </summary>
    public class ArtSetTests
    {
        /// <summary>
        /// A catalogue whose every row is recognisable by its size: mark m (100 and up) is m/10 by m/5 art pixels, with
        /// its pivot on its feet. The sprites are real (any will do); the marks are what the test reads back.
        /// </summary>
        private static ArtCatalog Marked()
        {
            var art = new ArtCatalog.Builder("marked") { NameOffset = new Vec2(0.25f, 0f), NameHeight = 1.5f };
            void Row(string key, int mark, int frames = 1)
            {
                var sprites = new string[frames];
                for (int f = 0; f < frames; f++) sprites[f] = "Map_Floor";
                art.Add(key, DrawSheet.World, sprites, mark / 10f, mark / 5f, pivotY: 1f);
            }
            Row(ArtKeys.TileFloor, 100);
            Row(ArtKeys.TileRock, 101);
            Row(ArtKeys.TileWall, 102);
            Row(ArtKeys.TileWallLow, 102); // top-down's: the wall
            Row(ArtKeys.Missing, 109); // never drawn on a shipped map
            Row(ArtKeys.Node("ration"), 110);
            Row(ArtKeys.Node("iron-ore"), 110);
            Row(ArtKeys.Node("medicinal-herb"), 110);
            Row(ArtKeys.Node("glowstone"), 110);
            Row(ArtKeys.PropHearth, 111, frames: 2);
            Row(ArtKeys.PropBunk, 112);
            foreach (AgentVisualState state in Enum.GetValues(typeof(AgentVisualState))) Row(ArtKeys.Crawler(state), 113, frames: 2);
            Row(ArtKeys.SelectionRing, 114);
            Row(ArtKeys.CrawlerShadow, 115);
            return art.Build();
        }

        [Fact]
        public void TheSceneDrawsWhatItsCatalogueSays()
        {
            using var sim = LocalSimulation.Create(SimulationSettings.DefaultSeed);
            sim.Respawn(OutpostSession.SessionId);
            var frames = TestFrames.Of(sim);
            var crawler = sim.Registry.ActiveEntities.First(e => e.Type == EntityType.Pawn && sim.Sectors.IsExplored(sim.Sectors.GetSectorAt(e.Position).Id));
            var selection = new WorldSelection(frames);
            selection.SelectCrawler(crawler.Id, follow: false);

            var scene = new WorldScene { Mode = new ViewMode("marked", TopDownProjection.Instance, Marked()) };
            scene.Build(sim.Host.Current.Map, frames, 0f, selection);

            var items = Enumerable.Range(0, scene.Items.Count).Select(i => scene.Items[i]).Where(i => i.Sheet == DrawSheet.World).ToList();
            Assert.NotEmpty(items);
            var marks = items.Select(item =>
            {
                int mark = (int)MathF.Round(item.Width * 10f);
                Assert.InRange(mark, 100, 115);
                Assert.Equal((mark / 10f, mark / 5f, 0.5f, 1f), (item.Width, item.Height, item.PivotX, item.PivotY));
                return mark;
            }).ToHashSet();
            // Floor and wall tiles; Floor 1 has no rock tiles since Epic 10 G2 (the rock became the hazard sectors)
            foreach (int mark in new[] { 100, 102, 110, 111, 112, 113, 114, 115 }) Assert.Contains(mark, marks);
            Assert.DoesNotContain(109, marks); // nothing on Floor 1 lacks art

            var name = scene.Labels.Single(l => l.Kind == LabelKind.Name);
            Assert.True(selection.TryGetCrawlerPosition(out var at));
            Assert.Equal(at + new Vec2(0.25f, 0f), name.At);
            Assert.Equal(1.5f, name.Height);
        }

        [Fact]
        public void TopDownsArtIsUnitysSizesCentredOnTheirPoints()
        {
            var art = TopDownArtTable.Catalog;
            Assert.Equal((16f, 16f, 0.5f, 0.5f), Shape(art.Tile(TileKind.Wall)));
            Assert.Equal((12.8f, 12.8f, 0.5f, 0.5f), Shape(art.Resource(ItemType.IronOre)));
            Assert.Equal((14.4f, 14.4f, 0.5f, 0.5f), Shape(art.Bunk));
            Assert.Equal((14.4f, 14.4f, 0.5f, 0.5f), Shape(art.SelectionRing));
            Assert.True(art.CrawlerShadow.IsNone);
            Assert.Equal(new Vec2(0f, 0.65f), art.NameOffset);
            Assert.Equal(0f, art.NameHeight);
        }

        private static (float, float, float, float) Shape(ArtPiece piece) => (piece.Width, piece.Height, piece.PivotX, piece.PivotY);
    }
}
