using System.Linq;
using Simpiens.Core.Session;
using Simpiens.Hosting;
using Simpiens.Presentation;
using Simpiens.Simulation;
using Simpiens.Simulation.Spatial;
using Xunit;

namespace Simpiens.Client.Tests
{
    /// <summary>Picking and selection, ported with Unity's CameraRigValidator picker checks (Epic CLIENT M1.5).</summary>
    public class SelectionTests
    {
        [Fact]
        public void ClicksPickTheNearestCrawlerWithinReachTiesGoToTheLowerId()
        {
            var a = new EntityId(1u, 0u, 0u, 1u);
            var b = new EntityId(1u, 0u, 0u, 2u);
            var c = new EntityId(1u, 0u, 0u, 3u);
            var crowd = new[] { new PickCandidate(c, new Vec2(0.4f, 0f)), new PickCandidate(b, new Vec2(0f, 0.3f)), new PickCandidate(a, new Vec2(0f, -0.3f)) };
            float r = WorldSelection.PickRadius;
            Assert.True(WorldPicker.TryPickNearest(new Vec2(0f, 0.25f), crowd, r, out var id) && id == b, "The nearest crawler is picked.");
            Assert.True(WorldPicker.TryPickNearest(Vec2.Zero, crowd, r, out id) && id == a, "Equal distances go to the lower id.");
            Assert.False(WorldPicker.TryPickNearest(new Vec2(5f, 5f), crowd, r, out _), "Nothing beyond the radius is picked.");
            Assert.False(WorldPicker.TryPickNearest(Vec2.Zero, new PickCandidate[0], r, out _) || WorldPicker.TryPickNearest(Vec2.Zero, null, r, out _), "An empty map picks nothing.");
            Assert.True(WorldPicker.TryPickNearest(new Vec2(0.4f + r - 0.01f, 0f), crowd, r, out id) && id == c, "A click just inside the radius picks.");
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
            var selection = new WorldSelection(sim.Registry, sim.Sectors, identity);

            var crawler = sim.Registry.ActiveEntities.First(e => e.Type == EntityType.Pawn);
            selection.ClickAt(crawler.Position + new Vec2(0.2f, 0.1f));
            Assert.Equal(crawler.Id, selection.SelectedCrawler);
            Assert.True(selection.Following);
            Assert.Equal(sim.Sectors.GetSectorAt(crawler.Position).Id, selection.SelectedSector);
            Assert.Equal(identity.GetFriendlyName(crawler.Id), selection.CrawlerName());
            Assert.False(string.IsNullOrEmpty(selection.CrawlerName()));

            var hub = sim.Host.Current.Map.Labels.First(l => l.Text == "Outpost Hub").Position; // floor, no crawler at t0
            selection.ClickAt(hub);
            Assert.Equal(default, selection.SelectedCrawler);
            Assert.False(selection.Following);
            Assert.Equal(sim.Sectors.GetSectorAt(hub).Id, selection.SelectedSector);
        }

        [Fact]
        public void TheGameDrawsTheRingAndNameAndItsCameraFollowsTheSelection()
        {
            var game = Offscreen.Start(new ClientOptions { TicksPerUpdate = 1 }, frames: 1);
            var crawler = game.Simulation.Registry.ActiveEntities.First(e => e.Type == EntityType.Pawn);
            game.Rig.ZoomAt(game.Rig.Frame.Center, 0.2f); // close enough that following has room to move
            game.Selection.ClickAt(crawler.Position);
            Assert.True(game.Selection.Following);

            float before = Vec2.Distance(game.Rig.Position, crawler.Position);
            for (int i = 0; i < 20; i++) game.RunOneFrame();
            Assert.True(Vec2.Distance(game.Rig.Position, crawler.Position) < before, "the camera moved toward the followed crawler");

            var items = Enumerable.Range(0, game.Scene.Items.Count).Select(i => game.Scene.Items[i]).ToList();
            Assert.Single(items, i => DrawSortKey.Layer(i.SortKey) == RenderLayer.Selection && i.SpriteIndex == ProceduralSprites.SelectionRing.Index);
        }
    }
}
