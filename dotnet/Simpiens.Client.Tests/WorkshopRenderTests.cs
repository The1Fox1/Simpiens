using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Simpiens.Core.Session;
using Simpiens.Intervention;
using Simpiens.Presentation;
using Simpiens.Simulation.Economy;
using Simpiens.Simulation.Items;
using Xunit;

namespace Simpiens.Client.Tests
{
    /// <summary>
    /// The workshop row in the real client (Epic 10 Part 2, W1.6a): the still's empty plot, and the forge built beside it,
    /// in the game's isometric view and in top-down. Recorded in the pinned image like every render (<see cref="RenderTests"/>).
    /// </summary>
    public class WorkshopRenderTests
    {
        private const int Width = 1280, Height = 720;

        private static SimpiensGame TheRowWithTheForgeBuilt(ViewMode mode)
        {
            // The stall opens with less than a forge costs (the player earns it first); here it can pay at once
            var options = new ClientOptions
            {
                View = mode,
                TicksPerUpdate = 100,
                ConfigureServices = services =>
                {
                    services.RemoveAll<ISessionDefinition>();
                    services.AddSingleton<ISessionDefinition>(new OutpostSession(OutpostSession.DefaultCrawlerCount, EconomyTuning.Default.WithStallTreasury(1000)));
                },
            };
            var game = Offscreen.Start(options, frames: 1, width: Width, height: Height);
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
        public void TheWorkshopRowInTopDownLooksAsRecorded()
            => RenderTests.CompareWithReference("render-floor1-workshop-row", FrameCapture.CaptureMap(TheRowWithTheForgeBuilt(ViewMode.TopDown), Width, Height), Width, Height);
    }
}
