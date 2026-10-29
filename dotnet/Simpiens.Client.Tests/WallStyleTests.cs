using System.Linq;
using Simpiens.Core.Session;
using Simpiens.Hosting;
using Simpiens.Presentation;
using Simpiens.Presentation.Art;
using Simpiens.Simulation;
using Xunit;

namespace Simpiens.Client.Tests
{
    /// <summary>Walls in the way (M3 design D7, plan step 1.4; producer P2: cutaway).</summary>
    public class WallStyleTests
    {
        private static WorldScene Outpost(ViewMode mode, WallStyle style)
        {
            using var sim = LocalSimulation.Create(SimulationSettings.DefaultSeed);
            sim.Respawn(OutpostSession.SessionId);
            var scene = new WorldScene { Mode = mode, WallStyle = style };
            scene.Build(sim.Host.Current.Map, TestFrames.Of(sim), 0f);
            return scene;
        }

        private static (int Full, int Low) Walls(WorldScene scene)
        {
            var walls = Enumerable.Range(0, scene.Items.Count).Select(i => scene.Items[i]).Where(i => DrawSortKey.Role(i.SortKey) == DrawRole.Wall).ToList();
            return (walls.Count(w => w.SpriteIndex == SpriteSheets.World.IndexOf(IsometricSprites.Wall)), walls.Count(w => w.SpriteIndex == SpriteSheets.World.IndexOf(IsometricSprites.LowWall)));
        }

        [Fact]
        public void TheCutawayKeepsExactlyTheWallsWithNoFloorBehindThem()
        {
            var scene = Outpost(ViewMode.Isometric, WallStyle.Cutaway);
            var tiles = scene.Tiles;
            var walls = Enumerable.Range(0, scene.Items.Count).Select(i => scene.Items[i]).Where(i => DrawSortKey.Role(i.SortKey) == DrawRole.Wall).ToList();
            int standing = 0;
            foreach (var tile in tiles.Tiles.Where(t => t.Kind == TileKind.Wall))
            {
                var centre = tiles.CellCentre(tile.X, tile.Y);
                var item = walls.Single(w => w.X == centre.X && w.Y == centre.Y);
                // Behind, in the fixed isometric view (depth x + y): the cells one step down in x, in y, or both
                bool floorBehind = tiles.IsFloor(tile.X - 1, tile.Y) || tiles.IsFloor(tile.X, tile.Y - 1) || tiles.IsFloor(tile.X - 1, tile.Y - 1);
                Assert.Equal(floorBehind ? SpriteSheets.World.IndexOf(IsometricSprites.LowWall) : SpriteSheets.World.IndexOf(IsometricSprites.Wall), item.SpriteIndex);
                if (!floorBehind) standing++;
            }
            Assert.InRange(standing, 1, walls.Count - 1); // the far walls stand, the rest are stubs
        }

        [Fact]
        public void FullAndLowAreEveryWallAlikeAndTopDownIsUnchanged()
        {
            var full = Walls(Outpost(ViewMode.Isometric, WallStyle.Full));
            var low = Walls(Outpost(ViewMode.Isometric, WallStyle.Low));
            Assert.Equal(0, full.Low);
            Assert.Equal(0, low.Full);
            Assert.Equal(full.Full, low.Low);

            foreach (WallStyle style in new[] { WallStyle.Cutaway, WallStyle.Full, WallStyle.Low })
            {
                var scene = Outpost(ViewMode.TopDown, style);
                Assert.All(Enumerable.Range(0, scene.Items.Count).Select(i => scene.Items[i]).Where(i => DrawSortKey.Role(i.SortKey) == DrawRole.Wall),
                    w => Assert.Equal(SpriteSheets.World.IndexOf(ProceduralSprites.WallSprite), w.SpriteIndex));
            }
        }

        [Fact]
        public void TheCKeyCyclesTheWallStyles()
        {
            var ui = UiDriver.Start();
            Assert.Equal(WallStyle.Cutaway, ui.Game.World.Scene.WallStyle);
            foreach (var next in new[] { WallStyle.Full, WallStyle.Low, WallStyle.Cutaway })
            {
                ui.Script.Keys = new KeyInput { WallStylePressed = true };
                ui.Frame();
                ui.Script.Keys = default;
                Assert.Equal(next, ui.Game.World.Scene.WallStyle);
            }
        }
    }
}
