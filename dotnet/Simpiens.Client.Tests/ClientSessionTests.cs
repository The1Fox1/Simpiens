using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Simpiens.Presentation;
using Simpiens.Simulation.Spatial;
using Xunit;

namespace Simpiens.Client.Tests
{
    /// <summary>
    /// The game split in parts (review A4, M3 plan step 0.3): the session and the world view work alone, without a game
    /// or a GPU, and the screen stack decides what updates, what draws and whether the world advances.
    /// </summary>
    public class ClientSessionTests
    {
        private static ClientOptions Options(int ticksPerUpdate) => new ClientOptions
        {
            TicksPerUpdate = ticksPerUpdate,
            SaveDirectory = Path.Combine(Path.GetTempPath(), "simpiens-client-tests", Guid.NewGuid().ToString("N")), // never the player's slot
        };

        [Fact]
        public void ASessionPlaysWithoutAGameOrAGpu()
        {
            using var session = new ClientSession(Options(10));
            for (int i = 0; i < 3; i++) session.Update(TimeSpan.FromMilliseconds(16));
            Assert.Equal(30L, session.Simulation.Clock.CurrentTick);
            Assert.Equal(30L, session.Frames.Current.Tick); // the frames follow every tick
        }

        [Fact]
        public void AHeldWorldStandsStillWhileTheViewsClockRuns()
        {
            using var session = new ClientSession(Options(0));
            session.Update(TimeSpan.FromSeconds(1), advanceWorld: false); // a hard cutscene holds it
            Assert.Equal(0L, session.Simulation.Clock.CurrentTick);
            Assert.Equal(1f, session.ViewSeconds, 3);

            for (int i = 0; i < 4; i++) session.Update(TimeSpan.FromSeconds(0.25)); // a second in frames under the stall cap
            Assert.InRange(session.Simulation.Clock.CurrentTick, 9L, 10L); // 10 Hz
            Assert.Equal(2f, session.ViewSeconds, 3);
        }

        [Fact]
        public void AWorldViewFramesFollowsAndPresentsWithoutADevice()
        {
            using var session = new ClientSession(Options(1));
            var world = new WorldView(session);
            var map = session.Simulation.Host.Current.Map;
            Assert.Equal(map.CameraFrame, world.Rig.Frame); // framed at construction, for the window asked for

            var crawler = session.Simulation.Registry.ActiveEntities.First(e => e.Type == EntityType.Pawn);
            world.Rig.ZoomAt(crawler.Position, 0.3f); // the whole map's framing leaves the camera no room to move
            session.Selection.SelectCrawler(crawler.Id, follow: true);
            for (int i = 0; i < 30; i++)
            {
                session.Update(TimeSpan.FromMilliseconds(100));
                world.Update(0.1f, 800, 600, default, default, hasInput: false);
            }
            Assert.True(session.Selection.TryGetCrawlerPosition(out var at));
            Assert.True((world.Rig.Position - at).Magnitude < 1f, "the camera follows the selected crawler");
            Assert.NotEmpty(world.Scene.Fog.Overlays); // the scene's state moved on in the update

            world.Scene.Present(map, session.Frames, session.ViewSeconds, session.Selection);
            Assert.True(world.Scene.Items.Count > 1000);
            world.Dispose();
        }

        [Fact]
        public void TheStackUpdatesItsTopAndDrawsFromTheTopmostThatCovers()
        {
            var log = new List<string>();
            var play = new FakeScreen("play", covers: true, advances: true, log);
            var overlay = new FakeScreen("overlay", covers: false, advances: true, log);
            var cutscene = new FakeScreen("cutscene", covers: true, advances: false, log);
            var stack = new ScreenStack();
            stack.Push(play);
            stack.Push(overlay);
            Assert.True(stack.WorldAdvances);

            stack.Update(0.1f, 0.1f, true);
            stack.Draw(800, 600);
            Assert.Equal(new[] { "overlay.update", "play.draw", "overlay.draw" }, log);

            log.Clear();
            stack.Push(cutscene);
            Assert.False(stack.WorldAdvances);
            stack.Update(0.1f, 0.1f, true);
            stack.Draw(800, 600);
            Assert.Equal(new[] { "cutscene.update", "cutscene.draw" }, log); // nothing under a covering screen is drawn

            Assert.True(stack.Remove(cutscene));
            Assert.Same(overlay, stack.Top);
            Assert.True(stack.WorldAdvances);
        }

        private sealed class FakeScreen : IScreen
        {
            private readonly string _name;
            private readonly List<string> _log;

            public FakeScreen(string name, bool covers, bool advances, List<string> log)
            {
                _name = name;
                Covers = covers;
                AdvancesWorld = advances;
                _log = log;
            }

            public bool Covers { get; }
            public bool AdvancesWorld { get; }
            public void Update(float seconds, float cameraSeconds, bool active) => _log.Add(_name + ".update");
            public void Draw(int width, int height) => _log.Add(_name + ".draw");
        }
    }
}
