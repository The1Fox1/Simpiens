using System.Linq;
using Simpiens.Core.Session;
using Simpiens.Hosting;
using Simpiens.Presentation;
using Simpiens.Presentation.Art;
using Simpiens.Simulation;
using Simpiens.Simulation.Economy;
using Xunit;

namespace Simpiens.Client.Tests
{
    /// <summary>
    /// The trade caravan on screen (Epic 10 Part 2, the stretch, D70): the frames carry it where the simulation's schedule
    /// puts it, and the scene draws it there, fading in from the road, walking, standing still at the stall while it trades,
    /// and gone between visits. A render shows it on its way in.
    /// </summary>
    public class CaravanViewTests
    {
        private const int Width = 1280, Height = 720;

        [Fact]
        public void TheSceneDrawsTheCaravanWhereTheScheduleSays()
        {
            using var sim = LocalSimulation.Create(SimulationSettings.DefaultSeed);
            sim.Respawn(OutpostSession.SessionId);
            uint interval = sim.Simulation.Economy.Supply.RestockIntervalTicks;
            var route = sim.Host.Current.Map.CaravanRoute;
            long walk = (long)System.Math.Ceiling(Caravan.Length(route) / Caravan.UnitsPerTick);
            var scene = new WorldScene { Mode = ViewMode.TopDown };
            var sprites = CaravanSprites.All.Select(s => SpriteSheets.World.IndexOf(s.Name)).ToHashSet();
            DrawItem? Drawn(float seconds)
            {
                scene.Build(sim.Host.Current.Map, TestFrames.Of(sim), seconds);
                for (int i = 0; i < scene.Items.Count; i++) if (scene.Items[i].Sheet == DrawSheet.World && sprites.Contains(scene.Items[i].SpriteIndex)) return scene.Items[i];
                return null;
            }

            sim.Step(interval / 2);
            Assert.Null(Drawn(0f)); // away between visits

            while (sim.Clock.CurrentTick < interval - walk + 3) sim.Step(1);
            var frame = TestFrames.Of(sim).Current;
            Assert.Equal(CaravanStage.Arriving, frame.Caravan.Stage);
            var setOut = Drawn(0f);
            Assert.True(setOut.HasValue && setOut.Value.Tint.A < 255, "Just in from the road, it fades in.");

            while (sim.Clock.CurrentTick < interval - walk / 2) sim.Step(1);
            var walking = Drawn(0f);
            Assert.True(walking.HasValue && walking.Value.Tint.A == 255, "On its way, it shows whole.");
            var position = TestFrames.Of(sim).Current.Caravan.Position;
            Assert.Equal(position.X, walking.Value.X, 3);
            Assert.Equal(position.Y, walking.Value.Y, 3);
            Assert.NotEqual(walking.Value.SpriteIndex, Drawn(1f / TopDownArtTable.CaravanFramesPerSecond).Value.SpriteIndex); // it strides

            while (sim.Clock.CurrentTick < interval + 10) sim.Step(1);
            var trading = Drawn(0f);
            Assert.True(trading.HasValue && trading.Value.SpriteIndex == Drawn(0.37f).Value.SpriteIndex, "At the stall it stands still while it trades.");
        }

        /// <summary>On its way in at t2400, up the lane from the Road Gate (the visit at t2520).</summary>
        [Fact]
        public void TheCaravanWalkingInLooksAsRecorded()
        {
            var game = Offscreen.Start(new ClientOptions { View = ViewMode.Isometric, TicksPerUpdate = 100 }, frames: 24, width: Width, height: Height);
            Assert.Equal(CaravanStage.Arriving, game.Session.Frames.Current.Caravan.Stage);
            RenderTests.CompareWithReference("render-iso-caravan-t2400", FrameCapture.CaptureMap(game, Width, Height), Width, Height);
        }
    }
}
