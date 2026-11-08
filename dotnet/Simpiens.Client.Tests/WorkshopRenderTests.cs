using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Simpiens.Cognition.Social;
using Simpiens.Core.Session;
using Simpiens.Intervention;
using Simpiens.Presentation;
using Simpiens.Simulation;
using Simpiens.Simulation.Economy;
using Simpiens.Simulation.Items;
using Simpiens.Simulation.Workshops;
using Xunit;

namespace Simpiens.Client.Tests
{
    /// <summary>
    /// The workshop row in the real client (Epic 10 Part 2, W1.6a): the still's empty plot, and the forge built beside it,
    /// in the game's isometric view and in top-down; and the station window, at a game's start and with the forge at work.
    /// Recorded in the pinned image like every render (<see cref="RenderTests"/>).
    /// </summary>
    public class WorkshopRenderTests
    {
        private const int Width = 1280, Height = 720;

        // The stall opens with less than a forge costs (the player earns it first); here it can pay at once
        private static ClientOptions Wealthy(ViewMode mode) => new ClientOptions
        {
            View = mode,
            TicksPerUpdate = 100,
            ConfigureServices = services =>
            {
                services.RemoveAll<ISessionDefinition>();
                services.AddSingleton<ISessionDefinition>(new OutpostSession(OutpostSession.DefaultCrawlerCount, EconomyTuning.Default.WithStallTreasury(1000)));
            },
        };

        private static SimpiensGame TheRowWithTheForgeBuilt(ViewMode mode)
        {
            var game = Offscreen.Start(Wealthy(mode), frames: 1, width: Width, height: Height);
            var simulation = game.Session.Simulation;
            int forge = simulation.Simulation.Workshops.IndexOf(StationKind.Forge);
            var commands = (IPlayerInterventionService)simulation.Services.GetService(typeof(IPlayerInterventionService));
            commands.QueueInfluence(InfluenceEvent.CreateBuildStation("render", (byte)forge));
            game.RunOneFrame();
            Assert.True(simulation.Simulation.Workshops[forge].Built, "The forge is built.");
            var row = simulation.Host.Current.Map.Labels.First(l => l.Text == "Workshop Row").Position;
            game.World.Rig.ZoomAt(row, 0.3f);
            game.World.Rig.Pan(row - game.World.Rig.Position);
            game.RunOneFrame();
            return game;
        }

        [Fact]
        public void TheWorkshopRowInIsometricLooksAsRecorded()
            => RenderTests.CompareWithReference("render-iso-workshop-row", FrameCapture.CaptureMap(TheRowWithTheForgeBuilt(ViewMode.Isometric), Width, Height), Width, Height);

        [Fact]
        public void TheStationWindowAtTheStartLooksAsRecorded()
        {
            // The game as it starts: the forge's plot, which the stall can't pay for yet
            var game = Offscreen.Start(new ClientOptions { View = ViewMode.Isometric, TicksPerUpdate = 100 }, frames: 1, width: Width, height: Height);
            game.Session.DashboardModel.OpenStation();
            game.Play.Dashboard.Refresh();
            game.RunOneFrame();
            RenderTests.CompareWithReference("render-iso-station-window-start", FrameCapture.Capture(game, Width, Height), Width, Height);
        }

        [Fact]
        public void TheStationWindowWithTheForgeAtWorkLooksAsRecorded()
        {
            // Built, a willing resident (its trust by hand) hired on the day shift, two pickaxes ordered; then into the shift
            var game = Offscreen.Start(Wealthy(ViewMode.Isometric), frames: 1, width: Width, height: Height);
            var sim = game.Session.Simulation;
            var model = game.Session.DashboardModel;
            model.OpenStation(sim.Simulation.Workshops.IndexOf(StationKind.Forge));
            model.Stations.Build();
            game.RunOneFrame();
            var smith = sim.Host.CrawlerStates.First(s => !s.IsDrifter && s.Personality.RiskTolerancePercent < WorkshopTuning.Default.MaxRiskTolerancePercent);
            smith.Memory.SetAffinity(WorldRegistry.PlayerEntityId, new AffinityRecord(warmth: 60, trust: 80, fear: 0, respect: 40, interactionCount: 1, lastInteractionTick: 0));
            model.Refresh();
            model.Stations.Hire(smith.AgentId);
            model.Stations.MoreCount(0);
            model.Stations.Order(0);
            while (sim.Clock.CurrentTick < 11 * DayClock.TicksPerHour / 2) game.RunOneFrame(); // 11:30, into the day shift
            Assert.True(sim.Simulation.Workshops.IsArtisan(smith.AgentId));
            game.Play.Dashboard.Refresh();
            game.RunOneFrame();
            RenderTests.CompareWithReference("render-iso-station-window-at-work", FrameCapture.Capture(game, Width, Height), Width, Height);
        }

        [Fact]
        public void TheWorkshopRowInTopDownLooksAsRecorded()
            => RenderTests.CompareWithReference("render-floor1-workshop-row", FrameCapture.CaptureMap(TheRowWithTheForgeBuilt(ViewMode.TopDown), Width, Height), Width, Height);
    }
}
