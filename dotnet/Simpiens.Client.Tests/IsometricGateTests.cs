using System;
using System.Collections.Generic;
using System.Linq;
using Simpiens.Core.Session;
using Simpiens.Core.Session.Layouts;
using Simpiens.Hosting;
using Simpiens.Presentation;
using Simpiens.Simulation;
using Simpiens.Simulation.Spatial;
using Simpiens.Simulation.World;
using Simpiens.Testing.Sessions;
using Xunit;

namespace Simpiens.Client.Tests
{
    /// <summary>
    /// The isometric gate on the real outpost (M3 plan Phase 2; proposal: depth-order tests pass, walls and crawlers
    /// interleave): depth order wherever things overlap on screen, a crawler walking past a wall, picking, and labels.
    /// </summary>
    public class IsometricGateTests
    {
        private static readonly Lazy<FontLibrary> Fonts = new Lazy<FontLibrary>(() => FontLibrary.LoadFrom(AppContext.BaseDirectory));

        private static List<DrawItem> Items(WorldScene scene) => Enumerable.Range(0, scene.Items.Count).Select(i => scene.Items[i]).ToList();

        private static bool Overlap(ScreenQuad a, ScreenQuad b)
            => a.Left < b.Left + b.Width && b.Left < a.Left + a.Width && a.Top < b.Top + b.Height && b.Top < a.Top + a.Height;

        [Theory]
        [InlineData(3)]
        [InlineData(15)]
        public void WhereverAStandingThingOverlapsAWallTheNearerIsDrawnOnTop(int frames)
        {
            // Full walls, the strictest case: every wall block hides what is behind it
            var game = Offscreen.Start(new ClientOptions { TicksPerUpdate = 100 }, frames: frames, width: 1280, height: 720);
            game.World.Scene.WallStyle = WallStyle.Full;
            var map = game.Session.Simulation.Host.Current.Map;
            game.World.Rig.ZoomAt(map.CameraFrame.Center, 0.5f);
            FrameCapture.CaptureMap(game, 1280, 720);
            var view = game.World.View;
            var projection = view.Projection;
            var items = Items(game.World.Scene);

            int pairs = 0;
            for (int s = 0; s < items.Count; s++)
            {
                var role = DrawSortKey.Role(items[s].SortKey);
                if (role != DrawRole.Crawler && role != DrawRole.Prop) continue;
                var standing = ScreenQuad.Of(items[s], view);
                float depth = projection.Depth(new Vec2(items[s].X, items[s].Y));
                for (int w = 0; w < items.Count; w++)
                {
                    if (DrawSortKey.Role(items[w].SortKey) != DrawRole.Wall || !Overlap(standing, ScreenQuad.Of(items[w], view))) continue;
                    pairs++;
                    float wall = projection.Depth(new Vec2(items[w].X, items[w].Y));
                    if (depth < wall) Assert.True(s < w, $"a {role} behind a wall (depth {depth} < {wall}) is drawn after it");
                    else Assert.True(s > w, $"a {role} in front of a wall (depth {depth} >= {wall}) is drawn before it");
                }
            }
            Assert.True(pairs >= 5, $"only {pairs} overlaps to check");
        }

        [Fact]
        public void ACrawlerWalkingPastAWallGoesBehindItExactlyWhereItsDepthCrosses()
        {
            using var sim = HeadlessSimulation.Create(SimulationSettings.DefaultSeed);
            sim.Respawn(OutpostSession.SessionId);
            var map = sim.Host.Current.Map;
            var scene = new WorldScene { Mode = ViewMode.Isometric, WallStyle = WallStyle.Full };
            scene.Build(map, TestFrames.Of(sim), 0f);
            var tiles = scene.Tiles;
            // A wall in a run along y, with floor on both sides along x, in a charted room (the fog would hide the walker)
            var wall = tiles.Tiles.First(t => t.Kind == TileKind.Wall && tiles.IsFloor(t.X - 1, t.Y) && tiles.IsFloor(t.X + 1, t.Y)
                && sim.Sectors.IsExplored(sim.Sectors.GetSectorAt(tiles.CellCentre(t.X - 1, t.Y)).Id) && sim.Sectors.IsExplored(sim.Sectors.GetSectorAt(tiles.CellCentre(t.X + 1, t.Y)).Id));
            var at = tiles.CellCentre(wall.X, wall.Y);
            var crawler = sim.Registry.ActiveEntities.First(e => e.Type == EntityType.Pawn);
            var projection = ViewMode.Isometric.Projection;

            for (float t = -1f; t <= 1f; t += 0.125f)
            {
                crawler.SetPosition(at + new Vec2(t, 0f)); // placed by hand: a fixture walking straight past the block
                scene.Build(map, TestFrames.Of(sim), 0f);
                var items = Items(scene);
                int walker = items.FindIndex(i => i.Pick >= 0 && scene.Items.PickedEntity(i.Pick) == crawler.Id);
                int block = items.FindIndex(i => DrawSortKey.Role(i.SortKey) == DrawRole.Wall && i.X == at.X && i.Y == at.Y);
                bool behind = projection.Depth(crawler.Position) < projection.Depth(at);
                Assert.True(behind ? walker < block : walker > block, $"at {t:0.###}: behind={behind}, walker {walker}, wall {block}");
            }
        }

        /// <summary>The outpost at t300 (the crawlers have spread out from where they woke), close up on the hub.</summary>
        private static (WorldScene Scene, View View, HeadlessSimulation Sim) CloseUp(WallStyle style)
        {
            var sim = HeadlessSimulation.Create(SimulationSettings.DefaultSeed);
            sim.Respawn(OutpostSession.SessionId);
            sim.Step(300);
            var scene = new WorldScene { Mode = ViewMode.Isometric, WallStyle = style };
            scene.Build(sim.Host.Current.Map, TestFrames.Of(sim), 0f);
            var hub = sim.Host.Current.Map.Labels.First(l => l.Text == "Outpost Hub").Position;
            var view = new View(ViewMode.Isometric.Projection, new ViewCamera(hub, 48f, 1280, 720)).SnappedToPixels();
            return (scene, view, sim);
        }

        [Fact]
        public void AClickOnACrawlersHeadPicksIt()
        {
            var (scene, view, sim) = CloseUp(WallStyle.Cutaway);
            using (sim)
            {
                var items = Items(scene).Where(i => i.Pick >= 0).ToList();
                int checkedHeads = 0;
                foreach (var item in items)
                {
                    // The head: 12 art pixels above the feet, where only this crawler is drawn
                    var head = view.WorldToScreen(new Vec2(item.X, item.Y)) - new Vec2(0f, 12f * view.Zoom);
                    var quad = ScreenQuad.Of(item, view);
                    if (items.Any(o => o.Pick != item.Pick && Contains(ScreenQuad.Of(o, view), head))) continue; // another crawler there too
                    if (head.X < 0 || head.Y < 0 || head.X >= 1280 || head.Y >= 720) continue;
                    Assert.True(Contains(quad, head));
                    Assert.True(WorldPicker.TryPick(scene.Items, view, head, out var picked));
                    Assert.Equal(scene.Items.PickedEntity(item.Pick), picked);
                    checkedHeads++;
                }
                Assert.True(checkedHeads > 0);
            }
        }

        [Fact]
        public void AClickOnEmptyFloorSelectsTheRoomUnderIt()
        {
            var (scene, view, sim) = CloseUp(WallStyle.Cutaway);
            using (sim)
            {
                var frames = TestFrames.Of(sim);
                var selection = new WorldSelection(frames, sim.Sectors);
                var hub = sim.Host.Current.Map.Labels.First(l => l.Text == "Outpost Hub").Position;
                // A floor point in the hub away from every crawler and prop
                var spot = Enumerable.Range(-6, 13).SelectMany(dx => Enumerable.Range(-6, 13).Select(dy => hub + new Vec2(dx * 0.5f, dy * 0.5f)))
                    .First(p => sim.Sectors.GetSectorAt(p).Id == sim.Sectors.GetSectorAt(hub).Id && !WorldPicker.TryPick(scene.Items, view, view.WorldToScreen(p), out _));
                var screen = view.WorldToScreen(spot);
                WorldPicker.TryPick(scene.Items, view, screen, out var picked);
                selection.ClickAt(view.ScreenToFloor(screen), picked);
                Assert.Equal(default, selection.SelectedCrawler);
                Assert.Equal(sim.Sectors.GetSectorAt(hub).Id, selection.SelectedSector);
            }
        }

        [Fact]
        public void ACrawlerHiddenBehindAFullWallCanStillBeClicked()
        {
            using var sim = HeadlessSimulation.Create(SimulationSettings.DefaultSeed);
            sim.Respawn(OutpostSession.SessionId);
            var map = sim.Host.Current.Map;
            var scene = new WorldScene { Mode = ViewMode.Isometric, WallStyle = WallStyle.Full };
            scene.Build(map, TestFrames.Of(sim), 0f);
            var tiles = scene.Tiles;
            // Just behind a block whose front neighbour is floor: the block covers the crawler on screen
            var wall = tiles.Tiles.First(t => t.Kind == TileKind.Wall && tiles.IsFloor(t.X - 1, t.Y - 1) && tiles.IsFloor(t.X + 1, t.Y + 1)
                && sim.Sectors.IsExplored(sim.Sectors.GetSectorAt(tiles.CellCentre(t.X - 1, t.Y - 1)).Id));
            var behind = tiles.CellCentre(wall.X - 1, wall.Y - 1) + new Vec2(0.4f, 0.4f);
            var crawler = sim.Registry.ActiveEntities.First(e => e.Type == EntityType.Pawn);
            crawler.SetPosition(behind); // placed by hand: a fixture
            scene.Build(map, TestFrames.Of(sim), 0f);
            var view = new View(ViewMode.Isometric.Projection, new ViewCamera(behind, 48f, 1280, 720)).SnappedToPixels();
            var items = Items(scene);
            int walker = items.FindIndex(i => i.Pick >= 0 && scene.Items.PickedEntity(i.Pick) == crawler.Id);
            var centre = ScreenQuad.Of(items[walker], view).Centre;
            Assert.Contains(items.Skip(walker + 1), i => DrawSortKey.Role(i.SortKey) == DrawRole.Wall && Contains(ScreenQuad.Of(i, view), centre)); // drawn over it

            Assert.True(WorldPicker.TryPick(scene.Items, view, centre, out var picked));
            Assert.Equal(crawler.Id, picked);
        }

        private static bool Contains(ScreenQuad quad, Vec2 p) => p.X >= quad.Left && p.X < quad.Left + quad.Width && p.Y >= quad.Top && p.Y < quad.Top + quad.Height;

        public static IEnumerable<object[]> MapsAndZooms()
        {
            foreach (var zoom in new[] { 16f, 24f, 32f, 48f, 64f, 96f, 120f })
            {
                yield return new object[] { "Floor1", zoom };
                yield return new object[] { "DevMap", zoom };
            }
        }

        [Theory]
        [MemberData(nameof(MapsAndZooms))]
        public void NoTwoRoomNamesOverlapInIsometric(string mapName, float pixelsPerUnit)
        {
            var map = mapName == "Floor1" ? Floor1Layout.Definition : DevMapLayout.Definition;
            var labels = map.Labels.Select(l => new WorldLabel(l.Text, l.Position, LabelKind.Room, Rgba32.From(l.Color))).ToList();
            var layout = new MapLabelLayout();
            var view = new View(ViewMode.Isometric.Projection, new ViewCamera(map.CameraFrame.Center, pixelsPerUnit, 1280, 720));
            layout.Layout(labels, view, Fonts.Value, new List<ScreenGlyph>());
            var shown = layout.Placed.Where(p => p.Shown).ToList();
            for (int a = 0; a < shown.Count; a++)
                for (int b = a + 1; b < shown.Count; b++)
                {
                    var p = shown[a];
                    var q = shown[b];
                    bool overlap = p.X < q.X + q.Width && q.X < p.X + p.Width && p.Y < q.Y + q.Height && q.Y < p.Y + p.Height;
                    Assert.False(overlap, $"{mapName} at {pixelsPerUnit} px/unit: '{labels[p.Index].Text}' overlaps '{labels[q.Index].Text}'.");
                }
            if (mapName == "Floor1" && pixelsPerUnit >= 16f) Assert.Equal(labels.Count, shown.Count);
        }

        [Fact]
        public void UnchartedSitsUnderItsRoomAndTheNameAboveTheStandingCrawlersHead()
        {
            using var sim = HeadlessSimulation.Create(SimulationSettings.DefaultSeed);
            sim.Respawn(OutpostSession.SessionId);
            var frames = TestFrames.Of(sim, 0);
            var identity = (Simpiens.Narrative.IAgentIdentityService)sim.Services.GetService(typeof(Simpiens.Narrative.IAgentIdentityService));
            var selection = new WorldSelection(frames, sim.Sectors, identity);
            var crawler = sim.Registry.ActiveEntities.First(e => e.Agent != null && sim.Sectors.IsExplored(sim.Sectors.GetSectorAt(e.Position).Id));
            selection.SelectCrawler(crawler.Id, follow: false);
            var scene = new WorldScene { Mode = ViewMode.Isometric };
            scene.Build(sim.Host.Current.Map, frames, 0f, selection);
            var view = new View(ViewMode.Isometric.Projection, new ViewCamera(crawler.Position, 32f, 1280, 720)).SnappedToPixels();
            var layout = new MapLabelLayout();
            layout.Layout(scene.Labels, view, Fonts.Value, new List<ScreenGlyph>());

            for (int i = 0; i < scene.Labels.Count; i++)
            {
                if (scene.Labels[i].Kind != LabelKind.Uncharted) continue;
                var uncharted = layout.Placed.Single(p => p.Index == i);
                var room = layout.Placed.Single(p => p.Index != i && scene.Labels[p.Index].Kind == LabelKind.Room && scene.Labels[p.Index].At == scene.Labels[i].At);
                Assert.Equal(room.Y + room.Height, uncharted.Y);
            }

            var name = layout.Placed.Single(p => scene.Labels[p.Index].Kind == LabelKind.Name);
            var walker = Items(scene).Single(i => i.Pick >= 0 && scene.Items.PickedEntity(i.Pick) == crawler.Id);
            var head = ScreenQuad.Of(walker, view);
            Assert.True(name.Y + name.Height <= head.Top + 2f * view.Zoom, $"the name ({name.Y + name.Height}) sits above the head ({head.Top})");
            Assert.True(name.Y + name.Height >= head.Top - 6f * view.Zoom, "and close to it");
            Assert.InRange(name.X + name.Width / 2f, head.Centre.X - 2f, head.Centre.X + 2f);
        }
    }
}
