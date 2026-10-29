using System;
using System.IO;
using System.Linq;
using Simpiens.Entities;
using Simpiens.Core.Session;
using Simpiens.Hosting;
using Simpiens.Presentation;
using Simpiens.Presentation.Art;
using Simpiens.Simulation;
using Xunit;

namespace Simpiens.Client.Tests
{
    /// <summary>The isometric placeholders and their art catalogue (M3 design D5, plan step 1.2; graphics V2).</summary>
    public class IsometricArtTests
    {
        private static readonly ArtCatalog Art = IsometricArtTable.Catalog;

        [Fact]
        public void FloorTilesMeetWithNoGapAndNoOverlap()
        {
            // A 5x5 patch of floor placed by the projection at zoom 1: each pixel is covered at most once, and around
            // the middle cell (whose neighbours fill its corners) every pixel is covered
            var projection = ViewMode.Isometric.Projection;
            var view = new View(projection, new ViewCamera(new Vec2(2f, 2f), projection.ArtPixelsPerUnit, 160, 96)).SnappedToPixels();
            var floor = Art.Tile(TileKind.Floor);
            var sprite = IsometricSprites.Floor;
            var coverage = new int[160 * 96];
            for (int cy = 0; cy < 5; cy++)
            {
                for (int cx = 0; cx < 5; cx++)
                {
                    var quad = ScreenQuad.Of(new DrawItem { X = cx, Y = cy, Width = floor.Width, Height = floor.Height, PivotX = floor.PivotX, PivotY = floor.PivotY }, view);
                    int left = (int)MathF.Round(quad.Left), top = (int)MathF.Round(quad.Top);
                    Assert.Equal(quad.Left, left, 3); // whole pixels at zoom 1
                    Assert.Equal(quad.Top, top, 3);
                    for (int y = 0; y < sprite.Height; y++)
                        for (int x = 0; x < sprite.Width; x++)
                            if (sprite.Pixels[y * sprite.Width + x].A > 0) coverage[(top + y) * 160 + left + x]++;
                }
            }
            Assert.True(coverage.Max() == 1, "two floor tiles share a pixel");
            var centre = view.WorldToScreen(new Vec2(2f, 2f));
            for (int y = (int)centre.Y - 8; y < (int)centre.Y + 8; y++)
                for (int x = (int)centre.X - 16; x < (int)centre.X + 16; x++)
                    Assert.True(coverage[y * 160 + x] == 1, $"a gap at {x}, {y}");
        }

        [Fact]
        public void StandingThingsStandOnTheirFeetAndNothingIsWiderThanACell()
        {
            int cell = IsometricSprites.TileWidth;
            var pose = new CrawlerPose(AgentVisualState.Idle, 0, 0f, 1f, 1f);
            foreach (var piece in new[] { Art.Tile(TileKind.Wall), Art.LowWall, Art.Bunk, Art.Hearth(0f), Art.Resource(Simpiens.Simulation.Economy.ItemType.IronOre), Art.Crawler(pose) })
            {
                Assert.True(piece.Width <= cell, $"a piece {piece.Width} wide");
                Assert.True(piece.PivotY > 0.5f, "standing things hang above their point");
                Assert.Equal(DrawSheet.Isometric, piece.Sheet);
            }
            Assert.Equal(15f / 16f, Art.Crawler(pose).PivotY); // the bottom of its feet, row 14
            // A wall block's pivot is its footprint's centre: the cell's diamond, at the bottom of the block
            var wall = Art.Tile(TileKind.Wall);
            Assert.Equal(IsometricSprites.WallRise + IsometricSprites.TileHeight / 2, wall.PivotY * wall.Height, 3);
            foreach (var flat in new[] { Art.Tile(TileKind.Floor), Art.Tile(TileKind.Rock), Art.SelectionRing, Art.CrawlerShadow })
                Assert.Equal((0.5f, 0.5f), (flat.PivotX, flat.PivotY));
        }

        [Fact]
        public void EveryCrawlerStateHasFramesInBothArtSets()
        {
            // Review R5: a new AgentVisualState without frames would draw as another
            foreach (AgentVisualState state in Enum.GetValues(typeof(AgentVisualState)))
            {
                for (int frame = 0; frame < 2; frame++)
                {
                    var sprite = ProceduralSprites.GetAgentSprite(state, frame);
                    Assert.NotNull(sprite);
                    var pose = new CrawlerPose(state, frame, 0f, 1f, 1f);
                    Assert.Same(sprite, SpriteSheets.Sprites[TopDownArtTable.Catalog.Crawler(pose).Sprite]);
                    Assert.Same(sprite, SpriteSheets.Isometric[Art.Crawler(pose).Sprite]);
                }
            }
        }

        [Fact]
        public void TheIsometricOutpostDrawsFromOneSheetBesideTheFog()
        {
            using var sim = LocalSimulation.Create(SimulationSettings.DefaultSeed);
            sim.Respawn(OutpostSession.SessionId);
            var scene = new WorldScene { Mode = ViewMode.Isometric };
            scene.Build(sim.Host.Current.Map, TestFrames.Of(sim), 0f);
            var items = Enumerable.Range(0, scene.Items.Count).Select(i => scene.Items[i]).ToList();
            Assert.All(items, i => Assert.Contains(i.Sheet, new[] { DrawSheet.Isometric, DrawSheet.Fog }));
            Assert.Contains(items, i => i.SpriteIndex == SpriteSheets.Isometric.IndexOf(IsometricSprites.Wall));
            Assert.All(items.Where(i => i.Sheet == DrawSheet.Isometric), i => Assert.InRange(i.SpriteIndex, 0, SpriteSheets.Isometric.Count - 1));
        }

        [Fact]
        public void TheSheetsShareIndices()
        {
            // Top-down's sprites at the same indices in both sheets, then the placeholders
            Assert.Equal(ProceduralSprites.All.Count + IsometricSprites.All.Count, SpriteSheets.Isometric.Count);
            for (int i = 0; i < SpriteSheets.Sprites.Count; i++) Assert.Same(SpriteSheets.Sprites[i], SpriteSheets.Isometric[i]);
        }
    }
}
