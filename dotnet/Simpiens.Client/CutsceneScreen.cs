using System;
using Microsoft.Xna.Framework;
using Simpiens.Client.Ui;
using Simpiens.Presentation;
using Simpiens.Presentation.Cutscenes;
using Simpiens.Simulation;

namespace Simpiens.Client
{
    /// <summary>
    /// A hard cutscene (M3 design D9, producer P3): it covers the world and holds it (the simulation driver stops, so
    /// nothing is written to the clock), hides the dashboard, and plays its script in screen space: the backdrop and
    /// characters through the renderer (<see cref="CutsceneStage"/>), the line in a Gum text box at the letterbox's
    /// bottom. A click, Space or Enter advances; Esc skips. When it ends it leaves the stack and the game goes on.
    /// </summary>
    public sealed class CutsceneScreen : IScreen
    {
        private readonly SimpiensGame _game;
        private bool _wasPressed = true; // the press that started it doesn't advance it

        internal CutsceneScreen(SimpiensGame game, CutsceneScript script)
        {
            if (script.Kind != CutsceneKind.Hard) throw new ArgumentException($"{script.Id} is a dynamic cutscene: it plays over the world.", nameof(script));
            _game = game;
            Player = new CutscenePlayer(script);
            TextBox = new CutsceneTextBox("cutscene-text-box");
        }

        public CutscenePlayer Player { get; }
        public CutsceneTextBox TextBox { get; }

        /// <summary>The last picture drawn, as data.</summary>
        public CutsceneStage Stage { get; } = new CutsceneStage();

        public bool Covers => true;

        public bool AdvancesWorld => false;

        internal void Open()
        {
            _game.Ui.Root.Children.Add(TextBox.Root);
            _game.Play.Dashboard?.Hide();
        }

        internal void Close()
        {
            _game.Ui.Root.Children.Remove(TextBox.Root);
            _game.Play.Dashboard?.Show();
        }

        public void Update(float seconds, float cameraSeconds, bool active)
        {
            if (active)
            {
                _game.Input.Read(out var pointer, out var keys);
                bool click = pointer.Left && !_wasPressed;
                _wasPressed = pointer.Left;
                if (keys.EscapePressed) Player.Skip();
                else if (click || keys.AdvancePressed) Player.Advance();
            }
            // Live, real time; with the tick-based view clock (tests), a tick's time a frame, since the world stands still
            Player.Update(_game.Options.ViewClockFromTicks ? SimulationClock.FixedTickDeltaTime : seconds);
            if (Player.Finished)
            {
                _game.EndCutscene(this);
                return;
            }

            var viewport = _game.GraphicsDevice.Viewport;
            var view = ScreenProjection.Fit(viewport.Width, viewport.Height);
            var box = CutsceneStage.TextBox;
            var topLeft = view.WorldToScreen(new Vec2(box.XMin, box.YMin));
            if (Player.Line == null) TextBox.Hide();
            else
            {
                Player.Script.TryGetCharacter(Player.Speaker, out var speaker);
                TextBox.Show(topLeft.X, topLeft.Y, box.Width * view.PixelsPerUnit, box.Height * view.PixelsPerUnit, speaker.Name, Player.Line, Player.LettersShown);
            }
        }

        public void Draw(int width, int height)
        {
            _game.GraphicsDevice.Clear(Color.Black);
            Stage.Build(Player);
            _game.Graphics.Renderer.Draw(Stage.Items, ScreenProjection.Fit(width, height));
        }
    }
}
