using System;
using System.Linq;
using Simpiens.Core.Session;
using Simpiens.Core.Session.Layouts;
using Simpiens.Entities;
using Simpiens.Hosting;
using Simpiens.Presentation;
using Xunit;

namespace Simpiens.Client.Tests
{
    /// <summary>What the client draws, as data: no GPU (Epic CLIENT M0.2).</summary>
    public class DrawOrderTests
    {
        [Fact]
        public void LayerComesFirstThenDepthThenInsertion()
        {
            Assert.True(DrawSortKey.Make(RenderLayer.Floor, 1000f, 9) < DrawSortKey.Make(RenderLayer.Walls, -1000f, 0));
            Assert.True(DrawSortKey.Make(RenderLayer.Crawlers, -3.5f, 9) < DrawSortKey.Make(RenderLayer.Crawlers, -2f, 0));
            Assert.True(DrawSortKey.Make(RenderLayer.Crawlers, -2f, 0) < DrawSortKey.Make(RenderLayer.Crawlers, 0f, 0));
            Assert.True(DrawSortKey.Make(RenderLayer.Crawlers, 0f, 0) < DrawSortKey.Make(RenderLayer.Crawlers, 2.25f, 0));
            Assert.True(DrawSortKey.Make(RenderLayer.Props, 1f, 3) < DrawSortKey.Make(RenderLayer.Props, 1f, 4));
            Assert.Equal(DrawSortKey.Make(RenderLayer.Props, -0f, 1), DrawSortKey.Make(RenderLayer.Props, 0f, 1));

            ulong key = DrawSortKey.Make(RenderLayer.Selection, 7f, 1234);
            Assert.Equal(RenderLayer.Selection, DrawSortKey.Layer(key));
            Assert.Equal(1234, DrawSortKey.Sequence(key));
        }

        [Fact]
        public void TheDrawListSortsByLayerAndDepthAndKeepsInsertionOrderForTies()
        {
            var list = new DrawList();
            list.Add(RenderLayer.Crawlers, 0f, 1, 0f, 0f, 1f, 1f, Rgba32.Clear);
            list.Add(RenderLayer.Floor, 5f, 2, 0f, 0f, 1f, 1f, Rgba32.Clear);
            list.Add(RenderLayer.Crawlers, -1f, 3, 0f, 0f, 1f, 1f, Rgba32.Clear);
            list.Add(RenderLayer.Floor, 5f, 4, 0f, 0f, 1f, 1f, Rgba32.Clear);
            for (int i = 0; i < 600; i++) list.Add(RenderLayer.Props, 0f, 100 + i, 0f, 0f, 1f, 1f, Rgba32.Clear); // grows the arrays
            list.Sort();

            Assert.Equal(604, list.Count);
            Assert.Equal(2, list[0].SpriteIndex);   // floor, inserted first
            Assert.Equal(4, list[1].SpriteIndex);   // floor, same depth, inserted later
            for (int i = 0; i < 600; i++) Assert.Equal(100 + i, list[2 + i].SpriteIndex);
            Assert.Equal(3, list[602].SpriteIndex); // crawler at depth -1 (higher on screen) before the one at 0
            Assert.Equal(1, list[603].SpriteIndex);
        }
    }

    public class ProceduralSpriteTests
    {
        [Fact]
        public void EveryCrawlerStateHasTwoFramesAndTheMapAndPropsHaveTheirSprites()
        {
            // 12 crawler states x 2 frames, berry bush, ore vein, herb patch, wall, campfire x 2, bunk, floor, rock
            Assert.Equal(33, ProceduralSprites.All.Count);
            for (int i = 0; i < ProceduralSprites.All.Count; i++)
            {
                var sprite = ProceduralSprites.All[i];
                Assert.Equal(i, sprite.Index);
                Assert.Equal(16, sprite.Width);
                Assert.Equal(16, sprite.Height);
            }
            foreach (AgentVisualState state in Enum.GetValues(typeof(AgentVisualState)))
            {
                Assert.NotSame(ProceduralSprites.GetAgentSprite(state, 0), ProceduralSprites.GetAgentSprite(state, 1));
            }
        }

        [Fact]
        public void TheIdleCrawlerMatchesItsPatternRowsFromTheTop()
        {
            var idle = ProceduralSprites.GetAgentSprite(AgentVisualState.Idle, 0);
            var outline = new Rgba32(20, 24, 33);
            // "................" then ".....######.....": the head's outline starts on the second row from the top
            Assert.Equal(Rgba32.Clear, idle.Pixels[0 * 16 + 5]);
            Assert.Equal(outline, idle.Pixels[1 * 16 + 5]);
            Assert.Equal(outline, idle.Pixels[1 * 16 + 10]);
            Assert.Equal(Rgba32.Clear, idle.Pixels[1 * 16 + 4]);
        }

        /// <summary>
        /// Pins every generated pixel. The art was ported pixel for pixel from Unity's AgentSpriteLibrary; a change
        /// here is a deliberate art change, re-pinned in its own commit.
        /// </summary>
        [Fact]
        public void TheGeneratedArtIsUnchanged()
        {
            ulong hash = 14695981039346656037UL;
            foreach (var sprite in ProceduralSprites.All)
            {
                foreach (var p in sprite.Pixels)
                {
                    hash = (hash ^ p.R) * 1099511628211UL;
                    hash = (hash ^ p.G) * 1099511628211UL;
                    hash = (hash ^ p.B) * 1099511628211UL;
                    hash = (hash ^ p.A) * 1099511628211UL;
                }
            }
            Assert.Equal(PinnedArtHash, hash);
        }

        internal const ulong PinnedArtHash = 12981420596316165723UL; // recorded 2026-10-18 (M0.2) from the port of AgentSpriteLibrary
    }

    public class MapTileTests
    {
        private static readonly Simpiens.Simulation.World.MapDefinition Floor1 = Floor1Layout.Definition;

        [Fact]
        public void EveryCellInsideAWallIsWallOrRockAndNeverFloor()
        {
            var layer = MapTileLayer.Build(Floor1);
            var floor = layer.Tiles.Where(t => t.Kind == TileKind.Floor).Select(t => (t.X, t.Y)).ToHashSet();
            var solid = layer.Tiles.Where(t => t.Kind != TileKind.Floor).Select(t => (t.X, t.Y)).ToHashSet();
            Assert.NotEmpty(floor);
            Assert.NotEmpty(solid);

            foreach (var wall in Floor1.Walls)
            {
                MapTileLayer.CellRange(wall.Solid, layer.Origin, out int minX, out int maxX, out int minY, out int maxY);
                for (int y = minY; y <= maxY; y++)
                {
                    for (int x = minX; x <= maxX; x++)
                    {
                        Assert.Contains((x, y), solid);
                        Assert.DoesNotContain((x, y), floor);
                    }
                }
            }
        }

        [Fact]
        public void FloorLiesInsideThePlayableBoundsAndIsTintedByItsSector()
        {
            var layer = MapTileLayer.Build(Floor1);
            foreach (var tile in layer.Tiles.Where(t => t.Kind == TileKind.Floor))
            {
                var centre = layer.CellCentre(tile.X, tile.Y);
                Assert.True(centre.X >= Floor1.PlayableBounds.XMin && centre.X <= Floor1.PlayableBounds.XMax
                    && centre.Y >= Floor1.PlayableBounds.YMin && centre.Y <= Floor1.PlayableBounds.YMax, $"floor at {centre}");
                var expected = MapTileLayer.TryGetSectorIndex(Floor1, centre, out int sector) ? MapTileLayer.FloorTint(Floor1, Floor1.Sectors[sector]) : MapTileLayer.NeutralFloor;
                Assert.Equal(expected, tile.Tint);
            }
            // Floor 1's walls are centred on whole numbers
            Assert.Equal(Vec2.Zero, layer.Origin);
        }

        [Fact]
        public void Floor1TilesAreUnchanged()
        {
            var layer = MapTileLayer.Build(Floor1);
            int floor = layer.Tiles.Count(t => t.Kind == TileKind.Floor);
            int rock = layer.Tiles.Count(t => t.Kind == TileKind.Rock);
            int walls = layer.Tiles.Count(t => t.Kind == TileKind.Wall);
            Assert.Equal((PinnedFloor, PinnedRock, PinnedWalls), (floor, rock, walls));
        }

        internal const int PinnedFloor = 1175, PinnedRock = 384, PinnedWalls = 254; // recorded 2026-10-18 (M0.2)
    }

    public class TopDownCameraTests
    {
        [Fact]
        public void ScreenAndWorldRoundTripAndWorldYPointsUp()
        {
            var camera = new TopDownCamera(new Vec2(3f, -2f), 32f, 800, 600);
            var world = new Vec2(5.25f, 1.5f);
            var screen = camera.WorldToScreen(world);
            var back = camera.ScreenToWorld(screen);
            Assert.Equal(world.X, back.X, 4);
            Assert.Equal(world.Y, back.Y, 4);
            Assert.Equal(new Vec2(400f, 300f), camera.WorldToScreen(camera.Centre));
            Assert.True(camera.WorldToScreen(new Vec2(3f, 0f)).Y < 300f); // higher in the world is higher on screen
        }

        [Fact]
        public void FramingShowsTheWholeFrameAtAWholeNumberOfPixelsPerArtPixel()
        {
            var frame = Floor1Layout.Definition.CameraFrame;
            var camera = TopDownCamera.Framing(frame, 1280, 720);
            Assert.True(frame.Width * camera.PixelsPerUnit <= 1280 && frame.Height * camera.PixelsPerUnit <= 720);
            Assert.Equal(0f, camera.PixelsPerUnit % TopDownCamera.BasePixelsPerUnit);
            Assert.True(camera.PixelsPerUnit >= TopDownCamera.BasePixelsPerUnit);
        }
    }

    public class WorldSceneTests
    {
        [Fact]
        public void TheOutpostSceneIsTheMapsTilesThenItsCrawlersOnTop()
        {
            using var sim = HeadlessSimulation.Create(Simpiens.Simulation.SimulationSettings.DefaultSeed);
            sim.Respawn(OutpostSession.SessionId);
            var scene = new WorldScene();
            scene.Build(sim.Host.Current.Map, sim.Registry.ActiveEntities, sim.Clock.CurrentTick, 0f);

            int crawlers = sim.Registry.ActiveEntities.Count(e => e.Agent != null);
            int props = sim.Registry.ActiveEntities.Count(e => e.Agent == null && (e.Type == Simpiens.Simulation.Spatial.EntityType.Resource
                || e.Type == Simpiens.Simulation.Spatial.EntityType.Anchor || e.Type == Simpiens.Simulation.Spatial.EntityType.Bunk));
            Assert.True(crawlers > 0);
            Assert.True(props > 0);
            Assert.Equal(scene.Tiles.Tiles.Count + props + crawlers, scene.Items.Count);

            int previousLayer = int.MinValue;
            for (int i = 0; i < scene.Items.Count; i++)
            {
                int layer = DrawSortKey.Layer(scene.Items[i].SortKey);
                Assert.True(layer >= previousLayer, $"item {i} is on layer {layer} after layer {previousLayer}");
                previousLayer = layer;
            }
            Assert.Equal(RenderLayer.Crawlers, DrawSortKey.Layer(scene.Items[scene.Items.Count - 1].SortKey));
        }
    }

    public class PngTests
    {
        [Fact]
        public void APngRoundTripsExactly()
        {
            const int width = 7, height = 5;
            var rgba = new byte[width * height * 4];
            var random = new Random(12345);
            random.NextBytes(rgba);

            byte[] file = Png.Encode(width, height, rgba);
            Assert.Equal(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }, file.Take(8).ToArray());
            byte[] back = Png.Decode(file, out int w, out int h);
            Assert.Equal((width, height), (w, h));
            Assert.Equal(rgba, back);
        }
    }
}
