using System.Linq;
using Simpiens.Presentation;
using Simpiens.Presentation.Cutscenes;
using Xunit;

namespace Simpiens.Client.Tests
{
    /// <summary>The demonstration cutscenes of development builds (M3 plan step 3.5): one of each kind, in real play.</summary>
    public class DemoSceneTests
    {
        private static UiDriver Running(bool demos, int ticksPerUpdate = 10)
        {
            var ui = UiDriver.Start(new ClientOptions { TicksPerUpdate = ticksPerUpdate, DemoScenes = demos }, 1280, 720);
            ui.Game.Session.Simulation.Clock.SetTimeMultiplier(1f);
            ui.Game.Play.Dashboard.TogglePanels(); // to the map
            ui.Frame();
            return ui;
        }

        private static void Press(UiDriver ui, KeyInput keys)
        {
            ui.Script.Keys = keys;
            ui.Frame();
            ui.Script.Keys = default;
        }

        [Fact]
        public void F9PlaysTheHardDemonstrationThroughToItsEndInDevelopmentBuildsOnly()
        {
            var off = Running(demos: false);
            Press(off, new KeyInput { DemoCutscenePressed = true });
            Assert.Same(off.Game.Play, off.Game.Screens.Top);

            var ui = Running(demos: true);
            var game = ui.Game;
            Press(ui, new KeyInput { DemoCutscenePressed = true });
            var screen = Assert.IsType<CutsceneScreen>(game.Screens.Top);
            ui.Frame(); // its first update: the line appears
            long held = game.Session.Simulation.Clock.CurrentTick;
            Assert.Equal(CutsceneArt.Outpost, screen.Player.Backdrop);
            Assert.Equal("Tara", screen.TextBox.Speaker.Text);

            var backdrops = new System.Collections.Generic.HashSet<string>();
            for (int i = 0; i < 60 && !screen.Player.Finished; i++)
            {
                backdrops.Add(screen.Player.Backdrop);
                Press(ui, new KeyInput { AdvancePressed = true }); // each line shown whole, then the next
            }
            Assert.True(screen.Player.Finished);
            Assert.Equal(new[] { CutsceneArt.Outpost, CutsceneArt.DeepSeam }, backdrops.OrderBy(b => b == CutsceneArt.DeepSeam ? 1 : 0));
            Assert.Equal(held, game.Session.Simulation.Clock.CurrentTick); // held throughout, to the frame it ended on
            Assert.Same(game.Play, game.Screens.Top);
            ui.Frame();
            Assert.Equal(held + 10, game.Session.Simulation.Clock.CurrentTick); // and the world goes on
        }

        [Fact]
        public void InRealPlayWithNothingInjectedBothDynamicDemonstrationsStartWithinAThousandTicks()
        {
            // The outpost as it plays: no hand-placed crawlers, no recorded events
            var ui = Running(demos: true);
            var game = ui.Game;
            var started = new System.Collections.Generic.List<DynamicScene>();
            game.Session.Scenes.Started += started.Add;
            for (int i = 0; i < 100 && started.Count < 2; i++)
            {
                ui.Frame();
                if (game.Session.Scenes.Current != null) Assert.True(game.Play.Scenes.Bubble.Shown || game.Play.Scenes.Box.Shown || game.Session.Scenes.Current.Player.Line == null);
            }
            Assert.Contains(started, s => s.Trigger.Script == DemoScenes.DeepSeamEntry);
            var gossip = Assert.Single(started, s => s.Trigger.Script == DemoScenes.FirstGossip);
            Assert.InRange(started.Max(s => s.StartedTick), 1L, 1000L);
            Assert.NotEqual(gossip.ActorOf("talker"), gossip.ActorOf("listener"));
            Assert.All(gossip.Actors, a => Assert.True(game.Session.Frames.Current.TryGetIndex(a, out int i) && game.Session.Frames.Current[i].IsCrawler));
        }

        [Fact]
        public void TheBackdropsAreUnchanged()
        {
            ulong hash = 14695981039346656037UL;
            foreach (var sprite in CutsceneArt.Backdrops)
                foreach (var p in sprite.Pixels)
                {
                    hash = (hash ^ p.R) * 1099511628211UL;
                    hash = (hash ^ p.G) * 1099511628211UL;
                    hash = (hash ^ p.B) * 1099511628211UL;
                    hash = (hash ^ p.A) * 1099511628211UL;
                }
            Assert.Equal(PinnedBackdrops, hash);
        }

        /// <summary>The placeholder backdrops' pixels, pinned after a look (2026-10-21); a deliberate art change re-pins in its own commit.</summary>
        private const ulong PinnedBackdrops = 4721926688796089773UL;

        // The references: the hard demonstration at the outpost and at the deep seam, and the first gossip over the world
        private const int Width = 1280, Height = 720;

        [Fact]
        public void TheHardDemonstrationLooksAsRecorded()
        {
            var ui = Running(demos: true);
            Press(ui, new KeyInput { DemoCutscenePressed = true });
            ui.Frame(8); // Tara's first line, appearing
            RenderTests.CompareWithReference("render-cutscene-outpost", FrameCapture.Capture(ui.Game, Width, Height), Width, Height);

            var screen = (CutsceneScreen)ui.Game.Screens.Top;
            while (screen.Player.Backdrop != CutsceneArt.DeepSeam) Press(ui, new KeyInput { AdvancePressed = true });
            Press(ui, new KeyInput { AdvancePressed = true }); // Bob's line shown whole
            RenderTests.CompareWithReference("render-cutscene-deep-seam", FrameCapture.Capture(ui.Game, Width, Height), Width, Height);
        }

        [Fact]
        public void TheFirstGossipOverTheIsometricWorldLooksAsRecorded()
        {
            var ui = Running(demos: true);
            var game = ui.Game;
            game.World.Rig.ZoomAt(game.World.Rig.Frame.Center, 0.4f);
            for (int i = 0; i < 100 && game.Session.Scenes.Current?.Trigger.Script != DemoScenes.FirstGossip; i++) ui.Frame();
            Assert.Equal(DemoScenes.FirstGossip, game.Session.Scenes.Current?.Trigger.Script);
            ui.Frame(6); // the camera on its way to the talker, the line appearing
            Assert.True(game.Play.Scenes.Bubble.Shown);
            RenderTests.CompareWithReference("render-iso-scene-gossip", FrameCapture.Capture(game, Width, Height), Width, Height);
        }
    }
}
