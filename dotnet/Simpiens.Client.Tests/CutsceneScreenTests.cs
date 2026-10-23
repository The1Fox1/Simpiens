using System.Linq;
using Simpiens.Entities;
using Simpiens.Presentation;
using Simpiens.Presentation.Cutscenes;
using Xunit;

namespace Simpiens.Client.Tests
{
    /// <summary>Hard cutscenes in the game (M3 design D9, plan step 3.3; producer P3: they pause the game).</summary>
    public class CutsceneScreenTests
    {
        private static CutsceneScript Scene() => new CutsceneBuilder("test-scene", CutsceneKind.Hard)
            .Character("ada", "Ada", AgentVisualState.Idle)
            .Character("bo", "Bo", AgentVisualState.Walking)
            .Backdrop(CutsceneArt.Outpost)
            .Enter("ada", StageSlot.Left)
            .Enter("bo", StageSlot.Right, facingLeft: true)
            .Say("ada", "The deep seam is charted.")
            .Say("bo", "Then we dig.")
            .Build();

        /// <summary>A game ticking 10 a frame, taking scripted input, at the game's size.</summary>
        private static UiDriver Running()
        {
            var ui = UiDriver.Start(new ClientOptions { TicksPerUpdate = 10 }, 1280, 720);
            ui.Game.Session.Simulation.Clock.SetTimeMultiplier(1f);
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
        public void AHardCutsceneHoldsTheWorldHidesTheDashboardAndTellsItsLine()
        {
            var ui = Running();
            var game = ui.Game;
            long before = game.Session.Simulation.Clock.CurrentTick;
            ui.Frame();
            Assert.Equal(before + 10, game.Session.Simulation.Clock.CurrentTick); // the world runs

            var screen = game.PlayCutscene(Scene());
            ui.Frame(5);
            long held = game.Session.Simulation.Clock.CurrentTick;
            Assert.Equal(before + 10, held);
            Assert.Same(screen, game.Screens.Top);
            Assert.False(game.Screens.WorldAdvances);
            Assert.False(game.Play.Dashboard.Shown);
            Assert.False(game.Play.Dashboard.Header.Root.Visible);

            // A tick's time a frame with the tick-based view clock: 4 letters a frame
            Assert.True(screen.TextBox.Shown);
            Assert.Equal("Ada", screen.TextBox.Speaker.Text);
            Assert.Equal("The deep seam is charted.", screen.TextBox.Line.Text);
            Assert.Equal(20, screen.Player.LettersShown);
            Assert.Equal(20, screen.TextBox.Line.MaxLettersToShow);

            // The picture: the backdrop, and both characters, Bo facing left
            FrameCapture.Capture(game, 1280, 720);
            var items = Enumerable.Range(0, screen.Stage.Items.Count).Select(i => screen.Stage.Items[i]).ToList();
            Assert.Single(items, i => i.Sheet == DrawSheet.Backdrop);
            Assert.Equal(2, items.Count(i => i.Sheet == DrawSheet.Sprites));
            Assert.Single(items, i => i.FlipX);
        }

        [Fact]
        public void AClickSpaceOrEnterAdvancesAndTheSceneEndsByItself()
        {
            var ui = Running();
            var game = ui.Game;
            var screen = game.PlayCutscene(Scene());
            ui.Frame();

            ui.Click(new Vec2(640f, 360f)); // the line shown whole
            Assert.True(screen.Player.LineComplete);
            Assert.Equal("ada", screen.Player.Speaker);
            Press(ui, new KeyInput { AdvancePressed = true }); // Bo's line
            Assert.Equal("Bo", screen.TextBox.Speaker.Text);
            Press(ui, new KeyInput { AdvancePressed = true });
            Press(ui, new KeyInput { AdvancePressed = true }); // past the last line: it ends

            Assert.True(screen.Player.Finished);
            Assert.False(game.Screens.Contains(screen));
            Assert.Same(game.Play, game.Screens.Top);
            Assert.True(game.Play.Dashboard.Shown);
            Assert.True(game.Play.Dashboard.Header.Root.Visible);
            Assert.DoesNotContain(screen.TextBox.Root, game.Ui.Root.Children);

            long resumed = game.Session.Simulation.Clock.CurrentTick;
            ui.Frame();
            Assert.Equal(resumed + 10, game.Session.Simulation.Clock.CurrentTick); // the world goes on
        }

        [Fact]
        public void EscSkipsIt()
        {
            var ui = Running();
            var screen = ui.Game.PlayCutscene(Scene());
            ui.Frame();
            Press(ui, new KeyInput { EscapePressed = true });
            Assert.True(screen.Player.Finished);
            Assert.Same(ui.Game.Play, ui.Game.Screens.Top);
        }

        [Fact]
        public void ADynamicScriptIsNotAHardCutscene()
        {
            var ui = Running();
            var dynamic = new CutsceneBuilder("over-the-world", CutsceneKind.Dynamic).Actors("a").Say("a", "Hi").Build();
            Assert.Throws<System.ArgumentException>(() => ui.Game.PlayCutscene(dynamic));
        }
    }
}
