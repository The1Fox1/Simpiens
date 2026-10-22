using System.Collections.Generic;
using System.Linq;
using Simpiens.Core.Session;
using Simpiens.Hosting;
using Simpiens.Presentation;
using Simpiens.Simulation;
using Xunit;

namespace Simpiens.Client.Tests
{
    /// <summary>
    /// What lies on the floor (M3 design D6, plan step 1.3): drawn in floor units under the view's affine transform, so
    /// the fog's cloud takes the projection's shape; and the fog rule D1 = C holds in isometric.
    /// </summary>
    public class FloorQuadTests
    {
        public static IEnumerable<object[]> Modes => new[] { new object[] { "top-down" }, new object[] { "isometric" } };

        private static ViewMode Mode(string name) => name == "isometric" ? ViewMode.Isometric : ViewMode.TopDown;

        private static HeadlessSimulation Outpost()
        {
            var sim = HeadlessSimulation.Create(SimulationSettings.DefaultSeed);
            sim.Respawn(OutpostSession.SessionId);
            return sim;
        }

        [Theory]
        [MemberData(nameof(Modes))]
        public void TheFloorTransformIsTheViewsFloorToScreen(string mode)
        {
            var projection = Mode(mode).Projection;
            var view = new View(projection, new ViewCamera(new Vec2(3.25f, -1.5f), 37f, 800, 600)).SnappedToPixels();
            var affine = view.FloorToScreen();
            foreach (var p in new[] { Vec2.Zero, new Vec2(10f, -4f), new Vec2(-7.5f, 12.25f), new Vec2(31f, 19f) })
            {
                var expected = view.WorldToScreen(p);
                var actual = affine.Apply(p);
                Assert.Equal(expected.X, actual.X, 2);
                Assert.Equal(expected.Y, actual.Y, 2);
            }
        }

        [Theory]
        [MemberData(nameof(Modes))]
        public void EachCloudsCornersLandWhereTheProjectionPutsItsSectorsInsetCorners(string mode)
        {
            using var sim = Outpost();
            var scene = new WorldScene { Mode = Mode(mode) };
            scene.Build(sim.Host.Current.Map, TestFrames.Of(sim), 0f);
            var view = new View(scene.Projection, new ViewCamera(sim.Host.Current.Map.CameraFrame.Center, 24f, 1280, 720)).SnappedToPixels();
            var affine = view.FloorToScreen();
            var clouds = Enumerable.Range(0, scene.Items.Count).Select(i => scene.Items[i]).Where(i => i.Sheet == DrawSheet.Fog).ToList();
            Assert.Equal(scene.Fog.Overlays.Count, clouds.Count);
            foreach (var overlay in scene.Fog.Overlays)
            {
                var area = overlay.Area;
                var cloud = clouds.Single(c => c.X == area.Center.X && c.Y == area.Center.Y);
                Assert.True(cloud.OnFloor);
                // The renderer draws from the near corner up the rectangle's size, in floor units, under the affine
                var corners = new[] { new Vec2(cloud.X - cloud.Width / 2, cloud.Y - cloud.Height / 2), new Vec2(cloud.X + cloud.Width / 2, cloud.Y + cloud.Height / 2) };
                var expected = new[] { new Vec2(area.XMin, area.YMin), new Vec2(area.XMax, area.YMax) };
                for (int k = 0; k < 2; k++)
                {
                    var drawn = affine.Apply(corners[k]);
                    var wanted = view.WorldToScreen(expected[k]);
                    Assert.Equal(wanted.X, drawn.X, 2);
                    Assert.Equal(wanted.Y, drawn.Y, 2);
                }
                // Its screen bounds (culling) hold all four corners
                var quad = ScreenQuad.Of(cloud, view);
                foreach (var corner in new[] { new Vec2(area.XMin, area.YMax), new Vec2(area.XMax, area.YMin) }.Concat(expected))
                {
                    var at = view.WorldToScreen(corner);
                    Assert.InRange(at.X, quad.Left - 0.01f, quad.Left + quad.Width + 0.01f);
                    Assert.InRange(at.Y, quad.Top - 0.01f, quad.Top + quad.Height + 0.01f);
                }
            }
        }

        [Fact]
        public void InIsometricTheFogStillHidesEverythingInItsSectorUntilItLifts()
        {
            // Producer decision D1 = C, in both projections: a scene rule, not a layer
            using var sim = Outpost();
            var map = sim.Host.Current.Map;
            var frames = TestFrames.Of(sim, 0);
            var scene = new WorldScene { Mode = ViewMode.Isometric };
            scene.Build(map, frames, 0f);
            var cloud = scene.Fog.Overlays.First(o => sim.Registry.ActiveEntities.Any(e => e.Agent == null && o.Area.Contains(e.Position)));
            var crawler = sim.Registry.ActiveEntities.First(e => e.Agent != null);
            crawler.SetPosition(cloud.Area.Center); // placed by hand: a fixture, not the simulation's path
            frames.Capture(sim.Registry.ActiveEntities, sim.Sectors, 1);
            var selection = new WorldSelection(frames);
            selection.SelectCrawler(crawler.Id, follow: true);

            scene.Build(map, frames, 0f, selection);
            List<DrawItem> Inside() => Enumerable.Range(0, scene.Items.Count).Select(i => scene.Items[i])
                .Where(i => !i.OnFloor && cloud.Area.Contains(new Vec2(i.X, i.Y)) && DrawSortKey.Role(i.SortKey) is DrawRole.Prop or DrawRole.Crawler or DrawRole.Selection or DrawRole.Shadow).ToList();
            Assert.Empty(Inside());
            Assert.DoesNotContain(scene.Labels, l => l.Kind == LabelKind.Name);

            sim.Sectors.SetExplored(cloud.SectorId, true); // a scout's report
            frames.Capture(sim.Registry.ActiveEntities, sim.Sectors, 2);
            scene.Build(map, frames, 10f, selection);
            scene.Build(map, frames, 10.75f, selection); // halfway
            var halfway = Inside();
            Assert.Contains(halfway, i => DrawSortKey.Role(i.SortKey) == DrawRole.Crawler);
            Assert.Contains(halfway, i => DrawSortKey.Role(i.SortKey) == DrawRole.Shadow);
            Assert.All(halfway.Where(i => DrawSortKey.Role(i.SortKey) != DrawRole.Shadow), i => Assert.InRange(i.Tint.A, 120, 135));

            scene.Build(map, frames, 12f, selection);
            Assert.All(Inside(), i => Assert.Equal(255, i.Tint.A));
        }
    }
}
