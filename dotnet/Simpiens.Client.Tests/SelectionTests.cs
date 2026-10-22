using System.Linq;
using Simpiens.Core.Session;
using Simpiens.Hosting;
using Simpiens.Presentation;
using Simpiens.Simulation;
using Simpiens.Simulation.Spatial;
using Xunit;

namespace Simpiens.Client.Tests
{
    /// <summary>Picking and selection (Epic CLIENT M1.5; clicks pick what is drawn since review A1).</summary>
    public class SelectionTests
    {
        private static readonly View Close = new View(TopDownProjection.Instance, new ViewCamera(Vec2.Zero, 32f, 800, 600));

        private static void AddCrawler(DrawList list, EntityId id, Vec2 at)
        {
            float size = TopDownArt.CrawlerSize * TopDownProjection.BasePixelsPerUnit;
            list.Add(RenderLayer.Crawlers, DrawRole.Crawler, TopDownProjection.Instance.Depth(at), 0, at.X, at.Y, size, size, Rgba32.Clear).Pick = list.Pickable(id);
        }

        private static bool Pick(DrawList list, Vec2 world, out EntityId id) => WorldPicker.TryPick(list, Close, Close.WorldToScreen(world), out id);

        [Fact]
        public void ClicksPickWhatIsDrawnOnTopOrTheNearestCrawlerWithinReach()
        {
            var a = new EntityId(1u, 0u, 0u, 1u);
            var b = new EntityId(1u, 0u, 0u, 2u);
            var c = new EntityId(1u, 0u, 0u, 3u);
            var list = new DrawList();
            list.Add(RenderLayer.Floor, DrawRole.Floor, 0f, 0, 3f, 3f, 16f, 16f, Rgba32.Clear); // a floor tile: drawn, never picked
            AddCrawler(list, c, new Vec2(0.4f, 0f));
            AddCrawler(list, b, new Vec2(0f, 0.3f));
            AddCrawler(list, a, new Vec2(0f, -0.3f)); // lowest on screen: drawn last, on top
            list.Sort();

            Assert.True(Pick(list, new Vec2(0f, 0.25f), out var id) && id == b, "A click on a crawler picks it.");
            Assert.True(Pick(list, Vec2.Zero, out id) && id == a, "Where crawlers overlap, the one drawn on top is picked.");
            Assert.True(Pick(list, new Vec2(0.4f + 0.35f + 0.2f, 0f), out id) && id == c, "A click beside a crawler, within reach of its centre, picks it.");
            Assert.False(Pick(list, new Vec2(0.4f + WorldPicker.Reach + 0.05f, 0f), out _), "Nothing beyond reach is picked.");
            Assert.False(Pick(list, new Vec2(3f, 3f), out _), "What isn't pickable is never picked.");
            Assert.False(Pick(new DrawList(), Vec2.Zero, out _) || WorldPicker.TryPick(null, Close, Vec2.Zero, out _), "An empty map picks nothing.");
            Assert.False(WorldPicker.TryPick(list, default, Vec2.Zero, out _), "Before the first frame there is no view to pick through.");
        }

        [Fact]
        public void SelectionStateFollowsTheRules()
        {
            var b = new EntityId(1u, 0u, 0u, 2u);
            var selection = new WorldSelection();
            int changes = 0;
            selection.Changed += () => changes++;
            selection.SelectCrawler(b, follow: true);
            Assert.True(selection.SelectedCrawler == b && selection.Following && changes == 1, "Selecting a crawler follows it.");
            selection.StopFollowing();
            Assert.True(selection.SelectedCrawler == b && !selection.Following && changes == 2, "Stopping keeps the crawler selected.");
            selection.SelectSector(3);
            selection.SelectSector(3);
            Assert.True(selection.SelectedSector == 3 && changes == 3, "A sector selection changes once.");
            selection.ClickAt(Vec2.Zero); // no registry: no crawlers, no sectors
            Assert.True(selection.SelectedCrawler == default && !selection.Following, "A click on empty floor drops the crawler.");
            selection.Clear();
            Assert.True(selection.SelectedSector == -1 && selection.SelectedCrawler == default, "Clear drops everything.");
        }

        [Fact]
        public void OnTheOutpostAClickSelectsAndFollowsACrawlerAndItsRoomOrJustTheRoom()
        {
            using var sim = HeadlessSimulation.Create(SimulationSettings.DefaultSeed);
            sim.Respawn(OutpostSession.SessionId);
            var identity = (Simpiens.Narrative.IAgentIdentityService)sim.Services.GetService(typeof(Simpiens.Narrative.IAgentIdentityService));
            var frames = TestFrames.Of(sim);
            var selection = new WorldSelection(frames, sim.Sectors, identity);

            var scene = new WorldScene();
            scene.Build(sim.Host.Current.Map, frames, 0f);
            var view = new View(TopDownProjection.Instance, new ViewCamera(sim.Host.Current.Map.CameraFrame.Center, 32f, 1280, 720));
            void Click(Vec2 world)
            {
                var screen = view.WorldToScreen(world);
                WorldPicker.TryPick(scene.Items, view, screen, out var picked);
                selection.ClickAt(view.ScreenToFloor(screen), picked);
            }

            var crawler = sim.Registry.ActiveEntities.First(e => e.Type == EntityType.Pawn);
            Click(crawler.Position + new Vec2(0.2f, 0.1f));
            Assert.Equal(crawler.Id, selection.SelectedCrawler);
            Assert.True(selection.Following);
            Assert.Equal(sim.Sectors.GetSectorAt(crawler.Position).Id, selection.SelectedSector);
            Assert.Equal(identity.GetFriendlyName(crawler.Id), selection.CrawlerName());
            Assert.False(string.IsNullOrEmpty(selection.CrawlerName()));

            var hub = sim.Host.Current.Map.Labels.First(l => l.Text == "Outpost Hub").Position; // floor, no crawler at t0
            Click(hub);
            Assert.Equal(default, selection.SelectedCrawler);
            Assert.False(selection.Following);
            Assert.Equal(sim.Sectors.GetSectorAt(hub).Id, selection.SelectedSector);
        }

        [Theory]
        [InlineData("isometric")]
        [InlineData("top-down")]
        public void TheGameDrawsTheRingAndNameAndItsCameraFollowsTheSelection(string mode)
        {
            var view = mode == "isometric" ? ViewMode.Isometric : ViewMode.TopDown;
            var game = Offscreen.Start(new ClientOptions { View = view, TicksPerUpdate = 1 }, frames: 1);
            var crawler = game.Session.Simulation.Registry.ActiveEntities.First(e => e.Type == EntityType.Pawn);
            game.World.Rig.ZoomAt(game.World.Rig.Frame.Center, 0.2f); // close enough that following has room to move
            game.World.ClickMap(game.World.View.WorldToScreen(crawler.Position));
            Assert.True(game.Session.Selection.Following);

            float before = Vec2.Distance(game.World.Rig.Position, crawler.Position);
            for (int i = 0; i < 20; i++) game.RunOneFrame();
            Assert.True(Vec2.Distance(game.World.Rig.Position, crawler.Position) < before, "the camera moved toward the followed crawler");

            var items = Enumerable.Range(0, game.World.Scene.Items.Count).Select(i => game.World.Scene.Items[i]).ToList();
            Assert.Single(items, i => DrawSortKey.Layer(i.SortKey) == view.Projection.LayerOf(DrawRole.Selection) && i.SpriteIndex == view.Art.SelectionRing.Sprite);
            Assert.Single(game.World.Scene.Labels, l => l.Kind == LabelKind.Name);
        }
    }
}
