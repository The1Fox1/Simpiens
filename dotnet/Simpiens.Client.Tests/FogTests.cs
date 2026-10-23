using System.Collections.Generic;
using System.Linq;
using Simpiens.Core.Session;
using Simpiens.Hosting;
using Simpiens.Presentation;
using Simpiens.Simulation;
using Simpiens.Simulation.World;
using Xunit;

namespace Simpiens.Client.Tests
{
    /// <summary>Fog of war, ported from Unity's ShroudView (Epic CLIENT M1.3).</summary>
    public class FogTests
    {
        private static LocalSimulation Outpost()
        {
            var sim = LocalSimulation.Create(SimulationSettings.DefaultSeed);
            sim.Respawn(OutpostSession.SessionId);
            return sim;
        }

        [Fact]
        public void EveryUnchartedSectorIsCoveredInsideItsWalls()
        {
            using var sim = Outpost();
            var map = sim.Host.Current.Map;
            var fog = new FogOfWar();
            fog.Load(map, TestFrames.Of(sim).Current);

            var uncharted = map.Sectors.Where(s => !sim.Sectors.IsExplored(s.Id)).ToList();
            Assert.NotEmpty(uncharted);
            Assert.Equal(uncharted.Count, fog.Overlays.Count);
            foreach (var sector in uncharted)
            {
                var overlay = fog.Overlays.Single(o => o.SectorId == sector.Id);
                Assert.True(fog.IsCovered(sector.Id));
                Assert.Equal(sector.Bounds.Width - 2f * MapDefinitionBuilder.WallHalfExtent, overlay.Area.Width, 4);
                Assert.Equal(sector.Bounds.Center.X, overlay.Area.Center.X, 4);
            }
        }

        [Fact]
        public void ChartingASectorFadesItsFogOverASecondAndAHalfThenRemovesIt()
        {
            using var sim = Outpost();
            var map = sim.Host.Current.Map;
            var fog = new FogOfWar();
            fog.Load(map, TestFrames.Of(sim).Current);
            fog.Update(TestFrames.Of(sim).Current, 10f);
            int sector = fog.Overlays[0].SectorId;
            int before = fog.Overlays.Count;

            sim.Sectors.SetExplored(sector, true); // a scout's report
            fog.Update(TestFrames.Of(sim).Current, 10f);
            Assert.False(fog.IsCovered(sector));
            Assert.Equal(before, fog.Overlays.Count); // still drawn while it fades

            var list = new DrawList();
            fog.Draw(list, 10.75f, TopDownProjection.Instance);
            var cloud = Enumerable.Range(0, list.Count).Select(i => list[i]).First(i => i.Sheet == DrawSheet.Fog && i.X == fog.Overlays.Single(o => o.SectorId == sector).Area.Center.X);
            Assert.InRange(cloud.Tint.A, 118, 130); // half of 0.97 x 255

            fog.Update(TestFrames.Of(sim).Current, 11.5f);
            Assert.Equal(before - 1, fog.Overlays.Count);
            Assert.DoesNotContain(fog.Overlays, o => o.SectorId == sector);
        }

        [Fact]
        public void TheSceneDrawsACloudOverEachUnchartedSectorWithUnchartedUnderTheRoomsName()
        {
            using var sim = Outpost();
            var scene = new WorldScene();
            scene.Build(sim.Host.Current.Map, TestFrames.Of(sim, 0), 0f);

            var items = Enumerable.Range(0, scene.Items.Count).Select(i => scene.Items[i]).ToList();
            var clouds = items.Where(i => i.Sheet == DrawSheet.Fog).ToList();
            Assert.Equal(scene.Fog.Overlays.Count, clouds.Count);
            Assert.All(clouds, c => Assert.Equal(RenderLayer.Fog, DrawSortKey.Layer(c.SortKey)));

            // "UNCHARTED" hangs on each uncharted room's name (laid out on the next line, in screen space: MapTextTests)
            var uncharted = scene.Labels.Where(l => l.Kind == LabelKind.Uncharted).ToList();
            Assert.Equal(scene.Fog.Overlays.Count, uncharted.Count);
            Assert.All(uncharted, u => Assert.Contains(sim.Host.Current.Map.Labels, l => l.Position == u.At));
            Assert.All(uncharted, u => Assert.Equal(FogOfWar.UnchartedText, u.Text));
        }

        [Fact]
        public void TheFogHidesEverythingInItsSectorCrawlersIncludedUntilItLifts()
        {
            // Producer decision D1 = C (2026-10-21): a scout vanishes into the fog until the room is charted
            using var sim = Outpost();
            var map = sim.Host.Current.Map;
            var scene = new WorldScene();
            scene.Build(map, TestFrames.Of(sim, 0), 0f);
            var cloud = scene.Fog.Overlays.First(o => sim.Registry.ActiveEntities.Any(e => e.Agent == null && o.Area.Contains(e.Position)));
            var crawler = sim.Registry.ActiveEntities.First(e => e.Agent != null);
            crawler.SetPosition(cloud.Area.Center); // placed by hand: a fixture, not the simulation's path

            var frames = TestFrames.Of(sim, 0);
            var selection = new WorldSelection(frames);
            selection.SelectCrawler(crawler.Id, follow: true);
            scene.Build(map, frames, 0f, selection);
            List<DrawItem> Inside() => Enumerable.Range(0, scene.Items.Count).Select(i => scene.Items[i])
                .Where(i => i.Sheet == DrawSheet.Sprites && cloud.Area.Contains(new Vec2(i.X, i.Y)) && DrawSortKey.Role(i.SortKey) is DrawRole.Prop or DrawRole.Crawler or DrawRole.Selection).ToList();
            Assert.Empty(Inside());
            Assert.DoesNotContain(scene.Labels, l => l.Kind == LabelKind.Name); // its name would give it away
            var view = new View(TopDownProjection.Instance, new ViewCamera(cloud.Area.Center, 32f, 800, 600));
            Assert.False(WorldPicker.TryPick(scene.Items, view, view.WorldToScreen(cloud.Area.Center), out _), "what the fog hides can't be clicked");
            Assert.True(selection.TryGetCrawlerPosition(out _), "the selection (and the camera following it) keeps the crawler");

            sim.Sectors.SetExplored(cloud.SectorId, true); // a scout's report
            frames.Capture(sim.Registry.ActiveEntities, sim.Sectors, 1); // the same world, a tick later
            scene.Build(map, frames, 10f, selection); // the fade starts
            scene.Build(map, frames, 10.75f, selection); // halfway
            var halfway = Inside();
            Assert.Contains(halfway, i => DrawSortKey.Role(i.SortKey) == DrawRole.Crawler);
            Assert.Contains(halfway, i => DrawSortKey.Role(i.SortKey) == DrawRole.Prop);
            Assert.All(halfway, i => Assert.InRange(i.Tint.A, 120, 135)); // fading in with the cloud
            Assert.Contains(scene.Labels, l => l.Kind == LabelKind.Name && l.Color.A < 255);

            scene.Build(map, frames, 12f, selection); // the cloud is gone
            Assert.All(Inside(), i => Assert.Equal(255, i.Tint.A));
        }

        [Fact]
        public void ARespawnBringsTheFogBack()
        {
            using var sim = Outpost();
            var scene = new WorldScene();
            var frames = TestFrames.Of(sim, 100);
            scene.Build(sim.Host.Current.Map, frames, 10f);
            int covered = scene.Fog.Overlays.Count;
            foreach (var o in scene.Fog.Overlays.ToList()) sim.Sectors.SetExplored(o.SectorId, true);
            frames.Capture(sim.Registry.ActiveEntities, sim.Sectors, 101); // the same world: every cloud fades
            scene.Build(sim.Host.Current.Map, frames, 10f);
            Assert.Equal(covered, scene.Fog.Overlays.Count);
            frames.Capture(sim.Registry.ActiveEntities, sim.Sectors, 120);
            scene.Build(sim.Host.Current.Map, frames, 12f);
            Assert.Empty(scene.Fog.Overlays);

            sim.Respawn(OutpostSession.SessionId); // back to tick 0, sectors uncharted again
            scene.Build(sim.Host.Current.Map, TestFrames.Of(sim, sim.Clock.CurrentTick), 0f);
            Assert.Equal(covered, scene.Fog.Overlays.Count);
        }
    }
}
