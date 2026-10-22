using System.Linq;
using Simpiens.Core.Session;
using Simpiens.Hosting;
using Simpiens.Presentation;
using Simpiens.Simulation;
using Simpiens.Simulation.Economy;
using Simpiens.Simulation.Spatial;
using Xunit;

namespace Simpiens.Client.Tests
{
    /// <summary>
    /// Art sets (M3 design D4, plan step 1.1): the scene draws what its view mode's art says (sprite, size in art pixels,
    /// pivot) and where the projection says, deciding neither itself.
    /// </summary>
    public class ArtSetTests
    {
        /// <summary>Every piece distinct and recognisable: sprite ids 100 and up, odd sizes, pivots on the feet.</summary>
        private sealed class MarkedArt : WorldArt
        {
            public static ArtPiece Piece(int sprite) => new ArtPiece(sprite, sprite / 10f, sprite / 5f, 0.5f, 1f);

            public override ArtPiece Tile(TileKind kind) => Piece(100 + (int)kind);
            public override ArtPiece Resource(ItemType kind) => Piece(110);
            public override ArtPiece Hearth(float viewSeconds) => Piece(111);
            public override ArtPiece Bunk => Piece(112);
            public override ArtPiece Crawler(in CrawlerPose pose) => Piece(113);
            public override ArtPiece SelectionRing => Piece(114);
            public override ArtPiece CrawlerShadow => Piece(115);
            public override Vec2 NameOffset => new Vec2(0.25f, 0f);
            public override float NameHeight => 1.5f;
        }

        [Fact]
        public void TheSceneDrawsWhatItsArtSetSays()
        {
            using var sim = HeadlessSimulation.Create(SimulationSettings.DefaultSeed);
            sim.Respawn(OutpostSession.SessionId);
            var frames = TestFrames.Of(sim);
            var crawler = sim.Registry.ActiveEntities.First(e => e.Type == EntityType.Pawn && sim.Sectors.IsExplored(sim.Sectors.GetSectorAt(e.Position).Id));
            var selection = new WorldSelection(frames);
            selection.SelectCrawler(crawler.Id, follow: false);

            var scene = new WorldScene { Mode = new ViewMode("marked", TopDownProjection.Instance, new MarkedArt()) };
            scene.Build(sim.Host.Current.Map, frames, 0f, selection);

            var items = Enumerable.Range(0, scene.Items.Count).Select(i => scene.Items[i]).Where(i => i.Sheet == DrawSheet.Sprites).ToList();
            Assert.NotEmpty(items);
            Assert.All(items, item =>
            {
                Assert.InRange(item.SpriteIndex, 100, 115);
                var expected = MarkedArt.Piece(item.SpriteIndex);
                Assert.Equal((expected.Width, expected.Height, expected.PivotX, expected.PivotY), (item.Width, item.Height, item.PivotX, item.PivotY));
            });
            foreach (int sprite in new[] { 100, 101, 102, 110, 111, 112, 113, 114, 115 }) Assert.Contains(items, i => i.SpriteIndex == sprite);

            var name = scene.Labels.Single(l => l.Kind == LabelKind.Name);
            Assert.True(selection.TryGetCrawlerPosition(out var at));
            Assert.Equal(at + new Vec2(0.25f, 0f), name.At);
            Assert.Equal(1.5f, name.Height);
        }

        [Fact]
        public void TopDownsArtIsUnitysSizesCentredOnTheirPoints()
        {
            var art = TopDownArt.Instance;
            Assert.Equal((16f, 16f, 0.5f, 0.5f), Shape(art.Tile(TileKind.Wall)));
            Assert.Equal((12.8f, 12.8f, 0.5f, 0.5f), Shape(art.Resource(ItemType.IronOre)));
            Assert.Equal((14.4f, 14.4f, 0.5f, 0.5f), Shape(art.Bunk));
            Assert.Equal((14.4f, 14.4f, 0.5f, 0.5f), Shape(art.SelectionRing));
            Assert.Equal(new Vec2(0f, 0.65f), art.NameOffset);
            Assert.Equal(0f, art.NameHeight);
        }

        private static (float, float, float, float) Shape(ArtPiece piece) => (piece.Width, piece.Height, piece.PivotX, piece.PivotY);
    }
}
