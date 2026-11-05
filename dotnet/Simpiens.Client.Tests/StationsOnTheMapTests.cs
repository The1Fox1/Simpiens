using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Simpiens.Core.Session;
using Simpiens.Hosting;
using Simpiens.Intervention;
using Simpiens.Presentation;
using Simpiens.Presentation.Art;
using Simpiens.Simulation;
using Simpiens.Simulation.Economy;
using Simpiens.Simulation.Items;
using Xunit;

namespace Simpiens.Client.Tests
{
    /// <summary>
    /// The stations on the map (Epic 10 Part 2, W1.6a): each frame carries the map's stations; the scene draws an empty
    /// plot lying on the floor until one is built, then the station standing; a click on either picks the station.
    /// </summary>
    public class StationsOnTheMapTests
    {
        public static TheoryData<string> Modes => new TheoryData<string> { "top-down", "isometric" };

        private static ViewMode Mode(string mode) => mode == "top-down" ? ViewMode.TopDown : ViewMode.Isometric;

        [Theory]
        [MemberData(nameof(Modes))]
        public void APlotUntilBuiltThenTheStationAndAClickPicksEither(string mode)
        {
            using var sim = LocalSimulation.Create(SimulationSettings.DefaultSeed, services =>
            {
                services.RemoveAll<ISessionDefinition>();
                services.AddSingleton<ISessionDefinition>(new OutpostSession(OutpostSession.DefaultCrawlerCount, EconomyTuning.Default.WithStallTreasury(1000)));
            });
            sim.Respawn(OutpostSession.SessionId);
            var map = sim.Host.Current.Map;
            var workshops = sim.Simulation.Workshops;
            int forge = workshops.IndexOf(StationKind.Forge);
            Assert.True(forge >= 0 && map.Stations.Count == workshops.Count);

            var scene = new WorldScene { Mode = Mode(mode) };
            var frames = TestFrames.Of(sim);
            Assert.Equal(workshops.Count, frames.Current.StationCount);
            Assert.True(Enumerable.Range(0, frames.Current.StationCount).All(i => !frames.Current.Station(i).Built), "Nothing is built at the start.");

            scene.Build(map, frames, 0f);
            var plot = StationItem(scene, forge);
            Assert.Equal(DrawRole.FloorMark, DrawSortKey.Role(plot.SortKey));
            Assert.Equal(scene.Catalog.Piece(scene.Visuals.Station(StationKind.Forge, built: false), 0f).Sprite, plot.SpriteIndex);
            AssertClickPicks(scene, map.Stations[forge].Position, forge, frames.Current);

            // Built: the forge stands where its plot lay
            var commands = (IPlayerInterventionService)sim.Services.GetService(typeof(IPlayerInterventionService));
            commands.QueueInfluence(InfluenceEvent.CreateBuildStation("test", (byte)forge));
            sim.Step(1);
            Assert.True(workshops[forge].Built);
            frames = TestFrames.Of(sim);
            Assert.True(frames.Current.Station(forge).Built);
            scene.Build(map, frames, 0f);
            var built = StationItem(scene, forge);
            Assert.Equal(DrawRole.Prop, DrawSortKey.Role(built.SortKey));
            Assert.Equal(scene.Catalog.Piece(scene.Visuals.Station(StationKind.Forge, built: true), 0f).Sprite, built.SpriteIndex);
            AssertClickPicks(scene, map.Stations[forge].Position, forge, frames.Current);
        }

        [Fact]
        public void StationPicksAreTheirOwnAndNeverASimulationEntitysId()
        {
            using var sim = LocalSimulation.Create(SimulationSettings.DefaultSeed);
            sim.Respawn(OutpostSession.SessionId);
            var frame = TestFrames.Of(sim).Current;
            var ids = new HashSet<EntityId>(sim.Registry.ActiveEntities.Select(e => e.Id));
            for (int i = 0; i < frame.StationCount; i++)
            {
                Assert.DoesNotContain(StationPick.Of(i), ids);
                Assert.True(StationPick.TryIndex(StationPick.Of(i), frame, out int index) && index == i);
            }
            Assert.False(StationPick.TryIndex(ids.First(), frame, out _), "A crawler's or a prop's id picks no station.");
            Assert.False(StationPick.TryIndex(StationPick.Of(frame.StationCount), frame, out _), "Past the map's stations, none.");
        }

        private static DrawItem StationItem(WorldScene scene, int station)
        {
            var id = StationPick.Of(station);
            for (int i = 0; i < scene.Items.Count; i++)
                if (scene.Items[i].Pick >= 0 && scene.Items.PickedEntity(scene.Items[i].Pick) == id) return scene.Items[i];
            Assert.Fail($"The scene draws no station {station}.");
            return default;
        }

        private static void AssertClickPicks(WorldScene scene, Vec2 at, int station, WorldViewFrame frame)
        {
            var view = new View(scene.Projection, new ViewCamera(at, 48f, 1280, 720)).SnappedToPixels();
            Assert.True(WorldPicker.TryPick(scene.Items, view, view.WorldToScreen(at), out var picked), "A click on the station picks something.");
            Assert.True(StationPick.TryIndex(picked, frame, out int index) && index == station, "A click on the station picks it.");
        }
    }
}
