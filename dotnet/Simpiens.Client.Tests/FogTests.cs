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
        private static HeadlessSimulation Outpost()
        {
            var sim = HeadlessSimulation.Create(SimulationSettings.DefaultSeed);
            sim.Respawn(OutpostSession.SessionId);
            return sim;
        }

        [Fact]
        public void EveryUnchartedSectorIsCoveredInsideItsWalls()
        {
            using var sim = Outpost();
            var map = sim.Host.Current.Map;
            var fog = new FogOfWar();
            fog.Load(map, sim.Sectors);

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
            fog.Update(map, sim.Sectors, 10f);
            int sector = fog.Overlays[0].SectorId;
            int before = fog.Overlays.Count;

            sim.Sectors.SetExplored(sector, true); // a scout's report
            fog.Update(map, sim.Sectors, 10f);
            Assert.False(fog.IsCovered(sector));
            Assert.Equal(before, fog.Overlays.Count); // still drawn while it fades

            var list = new DrawList();
            fog.Draw(list, 10.75f);
            var cloud = Enumerable.Range(0, list.Count).Select(i => list[i]).First(i => i.Sheet == DrawSheet.Fog && i.X == fog.Overlays.Single(o => o.SectorId == sector).Area.Center.X);
            Assert.InRange(cloud.Tint.A, 118, 130); // half of 0.97 x 255

            fog.Update(map, sim.Sectors, 11.5f);
            Assert.Equal(before - 1, fog.Overlays.Count);
            Assert.DoesNotContain(fog.Overlays, o => o.SectorId == sector);
        }

        [Fact]
        public void TheSceneDrawsTheFogOverPropsAndUnderCrawlersWithUnchartedUnderTheRoomsName()
        {
            using var sim = Outpost();
            var scene = new WorldScene();
            scene.Build(sim.Host.Current.Map, sim.Sectors, sim.Registry.ActiveEntities, 0, 0f);

            var items = Enumerable.Range(0, scene.Items.Count).Select(i => scene.Items[i]).ToList();
            var clouds = items.Where(i => i.Sheet == DrawSheet.Fog).ToList();
            Assert.Equal(scene.Fog.Overlays.Count, clouds.Count);
            Assert.All(clouds, c => Assert.Equal(RenderLayer.Fog, DrawSortKey.Layer(c.SortKey)));
            Assert.True(RenderLayer.Props < RenderLayer.Fog && RenderLayer.Fog < RenderLayer.Crawlers);

            int unchartedGlyphs = FogOfWar.UnchartedText.Length * scene.Fog.Overlays.Count;
            int nameGlyphs = sim.Host.Current.Map.Labels.Sum(l => l.Text.Count(c => c != ' '));
            Assert.Equal(nameGlyphs + unchartedGlyphs, items.Count(i => i.Sheet == DrawSheet.Font));
        }

        [Fact]
        public void ARespawnBringsTheFogBack()
        {
            using var sim = Outpost();
            var scene = new WorldScene();
            scene.Build(sim.Host.Current.Map, sim.Sectors, sim.Registry.ActiveEntities, 100, 10f);
            int covered = scene.Fog.Overlays.Count;
            foreach (var o in scene.Fog.Overlays.ToList()) sim.Sectors.SetExplored(o.SectorId, true);
            scene.Build(sim.Host.Current.Map, sim.Sectors, sim.Registry.ActiveEntities, 101, 10f);
            scene.Build(sim.Host.Current.Map, sim.Sectors, sim.Registry.ActiveEntities, 120, 12f);
            Assert.Empty(scene.Fog.Overlays);

            sim.Respawn(OutpostSession.SessionId); // back to tick 0, sectors uncharted again
            scene.Build(sim.Host.Current.Map, sim.Sectors, sim.Registry.ActiveEntities, sim.Clock.CurrentTick, 0f);
            Assert.Equal(covered, scene.Fog.Overlays.Count);
        }
    }
}
