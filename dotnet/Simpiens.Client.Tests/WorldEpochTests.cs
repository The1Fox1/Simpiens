using System.Linq;
using Simpiens.Cognition;
using Simpiens.Core.Session;
using Simpiens.Entities;
using Simpiens.Hosting;
using Simpiens.Presentation;
using Simpiens.Simulation;
using Simpiens.Simulation.Spatial;
using Xunit;

namespace Simpiens.Client.Tests
{
    /// <summary>
    /// One world epoch (review A5, M3 plan step 0.1): the view caches (the fog, the crawlers' poses, the selection) are
    /// rebuilt when the frames show a new world, and only then, whatever the tick or the map did.
    /// </summary>
    public class WorldEpochTests
    {
        private static HeadlessSimulation Outpost()
        {
            var sim = HeadlessSimulation.Create(SimulationSettings.DefaultSeed);
            sim.Respawn(OutpostSession.SessionId);
            return sim;
        }

        [Fact]
        public void EveryRestartIsANewWorldAndEpochsAreUniqueAcrossFramesObjects()
        {
            using var sim = Outpost();
            var a = TestFrames.Of(sim);
            var b = TestFrames.Of(sim);
            Assert.NotEqual(0u, a.Epoch);
            Assert.NotEqual(a.Epoch, b.Epoch);

            uint before = a.Epoch;
            a.Capture(sim.Registry.ActiveEntities, sim.Sectors, 1);
            Assert.Equal(before, a.Epoch); // a tick is the same world
            a.Restart(sim.Registry.ActiveEntities, sim.Sectors, 1);
            Assert.NotEqual(before, a.Epoch); // a respawn, a switch or a load at any tick is a new one
        }

        [Fact]
        public void ANewEpochReloadsTheFogAndForgetsThePosesWhateverTheTick()
        {
            using var sim = Outpost();
            var map = sim.Host.Current.Map;
            var frames = TestFrames.Of(sim, 10);
            var scene = new WorldScene();
            var crawler = sim.Registry.ActiveEntities.First(e => e.Type == EntityType.Pawn);
            crawler.Agent.VisualState = AgentVisualState.Thinking;
            frames.Capture(sim.Registry.ActiveEntities, sim.Sectors, 10);
            scene.Build(map, frames, 10f);
            Assert.Equal(Sprite(AgentVisualState.Thinking), CrawlerSprite(scene, crawler.Id));

            // The same world a tick later: a scout's report starts a fade, and the dwell holds the shown state
            int sector = scene.Fog.Overlays[0].SectorId;
            sim.Sectors.SetExplored(sector, true);
            crawler.Agent.VisualState = AgentVisualState.Walking;
            frames.Capture(sim.Registry.ActiveEntities, sim.Sectors, 11);
            scene.Build(map, frames, 10f);
            Assert.True(scene.Fog.HasOverlay(sector) && !scene.Fog.IsCovered(sector), "a newly charted sector fades");
            Assert.Equal(Sprite(AgentVisualState.Thinking), CrawlerSprite(scene, crawler.Id));

            // A new world at a later tick (a load the tick check would miss): the sector is simply charted, the pose is new
            frames.Restart(sim.Registry.ActiveEntities, sim.Sectors, 600);
            scene.Build(map, frames, 10f);
            Assert.False(scene.Fog.HasOverlay(sector), "a world loaded charted has no fog to fade");
            Assert.Equal(Sprite(AgentVisualState.Walking), CrawlerSprite(scene, crawler.Id));
        }

        [Fact]
        public void ARebuiltWorldDropsTheSelectionAtOnce()
        {
            using var sim = Outpost();
            var frames = TestFrames.Of(sim);
            var selection = new WorldSelection(frames);
            var crawler = sim.Registry.ActiveEntities.First(e => e.Type == EntityType.Pawn);
            selection.SelectCrawler(crawler.Id, follow: true);
            selection.SelectSector(3);
            Assert.True(selection.TryGetCrawlerPosition(out _));
            int changes = 0;
            selection.Changed += () => changes++;

            frames.Restart(sim.Registry.ActiveEntities, sim.Sectors, 0); // the same ids, reissued in a new world
            Assert.False(selection.TryGetCrawlerPosition(out _), "its id may name another crawler now");
            selection.Update();
            Assert.Equal(default, selection.SelectedCrawler);
            Assert.Equal(-1, selection.SelectedSector);
            Assert.False(selection.Following);
            Assert.Equal(1, changes);

            selection.SelectCrawler(crawler.Id, follow: false); // a selection made in the new world stays
            selection.Update();
            Assert.Equal(crawler.Id, selection.SelectedCrawler);
        }

        [Fact]
        public void ALoadFromALaterTickRebuildsTheGamesViews()
        {
            // Saved at t1500, when the scout has charted rooms; loaded at t100 of a fresh respawn, where they are uncharted
            var game = Offscreen.Start(new ClientOptions { TicksPerUpdate = 100 }, frames: 15);
            Assert.True(game.Session.SaveGame.QuickSave().Succeeded);
            var charted = game.Session.Simulation.Host.Current.Map.Sectors.Where(s => !s.StartsExplored && game.Session.Simulation.Sectors.IsExplored(s.Id)).Select(s => s.Id).ToList();
            Assert.NotEmpty(charted);

            game.Session.Simulation.Respawn(OutpostSession.SessionId);
            game.RunOneFrame();
            Assert.Equal(100L, game.Session.Simulation.Clock.CurrentTick);
            Assert.All(charted, id => Assert.True(game.World.Scene.Fog.IsCovered(id)));
            var crawler = game.Session.Simulation.Registry.ActiveEntities.First(e => e.Type == EntityType.Pawn);
            game.Session.Selection.SelectCrawler(crawler.Id, follow: true);

            Assert.True(game.Session.SaveGame.QuickLoad().Succeeded);
            game.RunOneFrame();
            Assert.Equal(default, game.Session.Selection.SelectedCrawler); // ids are reissued
            Assert.All(charted, id => Assert.False(game.World.Scene.Fog.HasOverlay(id), "a loaded world shows its charting at once, with no fade"));
        }

        [Fact]
        public void ARespawnAtTheSameTickOnTheSameMapDropsTheSelection()
        {
            var game = Offscreen.Start(frames: 0);
            game.Session.Simulation.Clock.SetTimeMultiplier(0f);
            game.RunOneFrame();
            var crawler = game.Session.Simulation.Registry.ActiveEntities.First(e => e.Type == EntityType.Pawn);
            game.Session.Selection.SelectCrawler(crawler.Id, follow: true);
            game.RunOneFrame();
            Assert.Equal(crawler.Id, game.Session.Selection.SelectedCrawler);

            game.Session.Simulation.Respawn(OutpostSession.SessionId); // tick 0 again, the same map object
            game.RunOneFrame();
            Assert.Equal(0L, game.Session.Simulation.Clock.CurrentTick);
            Assert.Equal(default, game.Session.Selection.SelectedCrawler);
        }

        private static int Sprite(AgentVisualState state) => ProceduralSprites.GetAgentSprite(state, CrawlerAppearance.AnimationFrame(state, 10f)).Index;

        private static int CrawlerSprite(WorldScene scene, EntityId id)
            => Enumerable.Range(0, scene.Items.Count).Select(i => scene.Items[i]).Single(i => i.Pick >= 0 && scene.Items.PickedEntity(i.Pick) == id).SpriteIndex;
    }
}
