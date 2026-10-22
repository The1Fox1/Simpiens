using System;
using System.IO;
using System.Linq;
using Simpiens.Entities;
using Simpiens.Core.Session;
using Simpiens.Hosting;
using Simpiens.Presentation;
using Simpiens.Simulation;
using Xunit;

namespace Simpiens.Client.Tests
{
    /// <summary>The isometric placeholders and their art set (M3 design D5, plan step 1.2).</summary>
    public class IsometricArtTests
    {
        private static readonly IsometricArt Art = IsometricArt.Instance;

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
            var pose = new CrawlerPose(AgentVisualState.Idle, ProceduralSprites.GetAgentSprite(AgentVisualState.Idle, 0), 0f, 1f, 1f);
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
                    var pose = new CrawlerPose(state, sprite, 0f, 1f, 1f);
                    Assert.Same(sprite, ProceduralSprites.All[TopDownArt.Instance.Crawler(pose).Sprite]);
                    Assert.Same(sprite, WorldSprites.All[Art.Crawler(pose).Sprite]);
                }
            }
        }

        [Fact]
        public void TheIsometricOutpostDrawsFromOneSheetBesideTheFog()
        {
            using var sim = HeadlessSimulation.Create(SimulationSettings.DefaultSeed);
            sim.Respawn(OutpostSession.SessionId);
            var scene = new WorldScene { Mode = ViewMode.Isometric };
            scene.Build(sim.Host.Current.Map, TestFrames.Of(sim), 0f);
            var items = Enumerable.Range(0, scene.Items.Count).Select(i => scene.Items[i]).ToList();
            Assert.All(items, i => Assert.Contains(i.Sheet, new[] { DrawSheet.Isometric, DrawSheet.Fog }));
            Assert.Contains(items, i => i.SpriteIndex == IsometricSprites.Wall.Index);
            Assert.All(items.Where(i => i.Sheet == DrawSheet.Isometric), i => Assert.InRange(i.SpriteIndex, 0, WorldSprites.All.Count - 1));
        }

        [Fact]
        public void TheSheetsShareIndicesAndThePlaceholdersAreUnchanged()
        {
            for (int i = 0; i < WorldSprites.All.Count; i++) Assert.Equal(i, WorldSprites.All[i].Index);
            for (int i = 0; i < ProceduralSprites.All.Count; i++) Assert.Same(ProceduralSprites.All[i], WorldSprites.All[i]);
            Assert.Equal(PinnedPlaceholders, HashOf(IsometricSprites.All));
        }

        /// <summary>The placeholders, magnified, for a person (or an agent) to look at: render-output/iso-placeholders.png.</summary>
        [Fact(Explicit = true)]
        public void WriteTheContactSheet()
        {
            const int scale = 6, gap = 4;
            int width = IsometricSprites.All.Sum(s => s.Width * scale + gap) + gap;
            int height = IsometricSprites.All.Max(s => s.Height) * scale + 2 * gap;
            var rgba = new byte[width * height * 4];
            for (int i = 0; i < width * height; i++) { rgba[i * 4] = 40; rgba[i * 4 + 1] = 44; rgba[i * 4 + 2] = 52; rgba[i * 4 + 3] = 255; }
            int left = gap;
            foreach (var sprite in IsometricSprites.All)
            {
                for (int y = 0; y < sprite.Height * scale; y++)
                    for (int x = 0; x < sprite.Width * scale; x++)
                    {
                        var p = sprite.Pixels[y / scale * sprite.Width + x / scale];
                        if (p.A == 0) continue;
                        int o = ((gap + y) * width + left + x) * 4;
                        float a = p.A / 255f;
                        rgba[o] = (byte)(p.R * a + rgba[o] * (1 - a));
                        rgba[o + 1] = (byte)(p.G * a + rgba[o + 1] * (1 - a));
                        rgba[o + 2] = (byte)(p.B * a + rgba[o + 2] * (1 - a));
                    }
                left += sprite.Width * scale + gap;
            }
            Png.Write(Path.Combine(AppContext.BaseDirectory, "render-output", "iso-placeholders.png"), width, height, rgba);
        }

        private static ulong HashOf(System.Collections.Generic.IEnumerable<PixelSprite> sprites)
        {
            ulong hash = 14695981039346656037UL;
            foreach (var sprite in sprites)
            {
                hash = (hash ^ (ulong)sprite.Width) * 1099511628211UL;
                hash = (hash ^ (ulong)sprite.Height) * 1099511628211UL;
                foreach (var p in sprite.Pixels)
                {
                    hash = (hash ^ p.R) * 1099511628211UL;
                    hash = (hash ^ p.G) * 1099511628211UL;
                    hash = (hash ^ p.B) * 1099511628211UL;
                    hash = (hash ^ p.A) * 1099511628211UL;
                }
            }
            return hash;
        }

        /// <summary>The placeholders' pixels, pinned after a look (2026-10-21); a deliberate art change re-pins in its own commit.</summary>
        private const ulong PinnedPlaceholders = 14523392418641480320UL;
    }
}
