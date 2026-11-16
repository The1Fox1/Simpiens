using System.Collections.Generic;
using System.Linq;
using Simpiens.Core.Session.Layouts;
using Simpiens.Hosting;
using Simpiens.Presentation;
using Simpiens.Presentation.Art;
using Simpiens.Simulation;
using Simpiens.Simulation.Creatures;
using Xunit;

namespace Simpiens.Client.Tests
{
    /// <summary>
    /// The Old Stair on the map (Epic 10 Part 3, K10.5): the descent gate standing sealed, then breached once its keeper is
    /// slain (the world's flag in the frames); a boss's health bar over its head, its fill the share of health it has left,
    /// ember in its fury, and none over the vermin; and the pictures, both view modes.
    /// </summary>
    public class GateViewTests
    {
        private const int Width = 800, Height = 600;

        private static readonly int SealedGate = SpriteSheets.World.IndexOf(GateSprites.Sealed);
        private static readonly int OpenGate = SpriteSheets.World.IndexOf(GateSprites.Open);
        private static readonly int Bar = SpriteSheets.World.IndexOf(BarSprites.Name);

        [Theory]
        [InlineData("top-down")]
        [InlineData("isometric")]
        public void TheGateStandsSealedUntilItsKeeperFalls(string mode)
        {
            var game = AtTheGate(mode == "top-down" ? ViewMode.TopDown : ViewMode.Isometric);
            FrameCapture.CaptureMap(game, Width, Height);
            var items = Drawn(game);
            Assert.Contains(items, d => d.SpriteIndex == SealedGate);
            Assert.DoesNotContain(items, d => d.SpriteIndex == OpenGate);

            SlayTheKeeper(game.Session.Simulation);
            game.RunOneFrame();
            Assert.True(game.Session.Frames.Current.GateBreached, "The frames carry the breach.");
            FrameCapture.CaptureMap(game, Width, Height);
            items = Drawn(game);
            Assert.Contains(items, d => d.SpriteIndex == OpenGate);
            Assert.DoesNotContain(items, d => d.SpriteIndex == SealedGate);
        }

        [Fact]
        public void ABossCarriesItsHealthOverItsHead()
        {
            var game = AtTheGate(ViewMode.TopDown);
            var sim = game.Session.Simulation;
            var keeper = TheKeeper(sim);
            keeper.Health = 0.6f * keeper.Definition.MaxHealth;
            sim.Step(1);
            FrameCapture.CaptureMap(game, Width, Height);
            var bars = Drawn(game).Where(d => d.SpriteIndex == Bar).ToList();
            Assert.Equal(2, bars.Count); // the keeper's back and fill; none over the spinners of the web, which are no bosses
            Assert.Equal(0.6f, bars[1].Width / bars[0].Width, 3);
            Assert.True(bars[1].Tint.R > bars[1].Tint.G * 2, "Red while it isn't raging.");

            keeper.Health = 0.2f * keeper.Definition.MaxHealth; // under its fury's line
            sim.Step(1);
            FrameCapture.CaptureMap(game, Width, Height);
            bars = Drawn(game).Where(d => d.SpriteIndex == Bar).ToList();
            Assert.Equal(0.2f, bars[1].Width / bars[0].Width, 3);
            Assert.True(bars[1].Tint.G > 100, "Ember in its fury.");
        }

        /// <summary>The Old Stair at t300 (charted by hand), close up in isometric: the sealed gate and its keeper, hurt to a fifth, its bar ember.</summary>
        [Fact]
        public void TheGateAndItsKeeperInIsometricLookAsRecorded()
        {
            var game = AtTheGate(ViewMode.Isometric);
            var sim = game.Session.Simulation;
            var keeper = TheKeeper(sim);
            keeper.Health = 0.2f * keeper.Definition.MaxHealth;
            sim.Step(1);
            RenderTests.CompareWithReference("render-iso-gate-t300", FrameCapture.CaptureMap(game, Width, Height), Width, Height);
        }

        /// <summary>The breached gate in top-down: its keeper slain, a door hanging aside on stairs going down into the dark.</summary>
        [Fact]
        public void TheBreachedGateInTopDownLooksAsRecorded()
        {
            var game = AtTheGate(ViewMode.TopDown);
            SlayTheKeeper(game.Session.Simulation);
            game.RunOneFrame();
            RenderTests.CompareWithReference("render-floor1-gate-breached", FrameCapture.CaptureMap(game, Width, Height), Width, Height);
        }

        /// <summary>
        /// K10.5d: with the story's scenes on (the desktop game's), the breach plays its hard cutscene (Q13) the frame the frames
        /// first show it, over the Old Stair's broken gate, and only once: played through, it doesn't come back.
        /// </summary>
        [Fact]
        public void TheBreachPlaysItsCutsceneOnce()
        {
            var ui = UiDriver.Start(new ClientOptions { TicksPerUpdate = 10, StoryScenes = true }, 1280, 720);
            ui.Game.Session.Simulation.Clock.SetTimeMultiplier(1f);
            ui.Game.Play.Dashboard.TogglePanels(); // to the map
            ui.Frame();
            Assert.False(ui.Game.Screens.Top is CutsceneScreen);
            SlayTheKeeper(ui.Game.Session.Simulation);
            ui.Frame();
            var screen = Assert.IsType<CutsceneScreen>(ui.Game.Screens.Top);
            Assert.Same(Simpiens.Presentation.Cutscenes.StoryScenes.Breach, screen.Player.Script);
            Assert.Equal(Simpiens.Presentation.Cutscenes.CutsceneArt.OldStair, screen.Player.Backdrop);
            ui.Frame(8); // the first line, appearing
            RenderTests.CompareWithReference("render-cutscene-breach", FrameCapture.Capture(ui.Game, 1280, 720), 1280, 720);
            for (int i = 0; i < 40 && ui.Game.Screens.Top is CutsceneScreen; i++)
            {
                ui.Script.Keys = new KeyInput { AdvancePressed = true };
                ui.Frame();
                ui.Script.Keys = default;
            }
            Assert.False(ui.Game.Screens.Top is CutsceneScreen, "Played through to its end.");
            ui.Frame(10);
            Assert.False(ui.Game.Screens.Top is CutsceneScreen, "Only once.");
        }

        // The game at t300 with the Old Stair charted, its fog faded, the view on the gate's lair
        private static SimpiensGame AtTheGate(ViewMode mode)
        {
            var game = Offscreen.Start(new ClientOptions { View = mode, TicksPerUpdate = 100 }, frames: 1, width: Width, height: Height);
            var sim = game.Session.Simulation;
            sim.Sectors.SetExplored(Floor1Layout.OldStairSectorId, true);
            game.RunOneFrame();
            game.RunOneFrame();
            Assert.Equal(300L, sim.Clock.CurrentTick);
            Assert.True(sim.Host.Current.Map.TryGetLair(Floor1Layout.GateLair, out var lair));
            game.World.Rig.ZoomAt(lair.Position, 0.3f);
            game.World.Rig.Pan(lair.Position - game.World.Rig.Position);
            return game;
        }

        private static CreatureState TheKeeper(LocalSimulation sim)
        {
            var service = sim.Simulation.Creatures;
            var gate = service.Lairs.First(l => l.Definition.GuardsGate);
            Assert.True(service.TryGet(gate.MemberIn(0), out var keeper));
            return keeper;
        }

        // A crawler's swings asked by hand at a keeper left at 1 health, until it falls and the gate is breached (as BossTests)
        private static void SlayTheKeeper(LocalSimulation sim)
        {
            var keeper = TheKeeper(sim);
            keeper.Health = 1f;
            var hero = sim.Host.CrawlerStates.First(c => !c.IsDrifter);
            var entity = sim.Registry.GetEntity(hero.AgentId);
            for (int t = 0; t < 200 && (sim.Simulation.WorldFlags & Simpiens.Simulation.World.WorldFlags.GateBreached) == 0; t++)
            {
                hero.Position = keeper.Position + new Vec2(1.3f, 0f);
                hero.NextSwingAt = 0;
                sim.Simulation.Creatures.AskSwing(entity, keeper.Id);
                sim.Step(1);
            }
            Assert.True((sim.Simulation.WorldFlags & Simpiens.Simulation.World.WorldFlags.GateBreached) != 0, "The keeper fell and the gate is breached.");
            hero.Position = new Vec2(0f, 0f); // back at the hub, out of the picture
        }

        private static List<DrawItem> Drawn(SimpiensGame game)
        {
            var items = game.World.Scene.Items;
            var drawn = new List<DrawItem>(items.Count);
            for (int i = 0; i < items.Count; i++) if (items[i].Sheet == DrawSheet.World) drawn.Add(items[i]);
            return drawn;
        }
    }
}
