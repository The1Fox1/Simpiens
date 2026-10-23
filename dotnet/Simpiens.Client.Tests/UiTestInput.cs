using System;
using Simpiens.Presentation;

namespace Simpiens.Client.Tests
{
    /// <summary>A pointer and keys the test moves by hand, read by the game as if they were the devices.</summary>
    internal sealed class ScriptedInput : IClientInputSource
    {
        public Vec2 Position;
        public bool Left;
        public bool Right;
        public bool Middle;
        public KeyInput Keys;

        public void Read(out PointerInput pointer, out KeyInput keys)
        {
            pointer = new PointerInput { Position = Position, Left = Left, Right = Right, Middle = Middle };
            keys = Keys;
        }
    }

    /// <summary>A game in the test process driven by a scripted pointer, with its simulation paused so nothing moves under it.</summary>
    internal sealed class UiDriver
    {
        public SimpiensGame Game { get; }
        public ScriptedInput Script { get; } = new ScriptedInput();

        private UiDriver(SimpiensGame game)
        {
            Game = game;
            game.Input = Script; // the game's one input frame: Gum's cursor and the map both take it (review U1)
            Offscreen.MakeActive(game);
        }

        /// <summary>A paused game after one frame (the UI is up), taking scripted input.</summary>
        public static UiDriver Start(ClientOptions options = null, int width = 800, int height = 600)
        {
            var game = Offscreen.Start(options, frames: 0, width, height);
            game.Session.Simulation.Clock.SetTimeMultiplier(0f);
            game.RunOneFrame();
            return new UiDriver(game);
        }

        /// <summary>
        /// Runs <paramref name="ticks"/> real ticks, then a frame. The game is paused (so nothing moves under the
        /// pointer), and a paused simulation skips its ticks while the clock still counts them (epic F6): the clock
        /// runs at speed for the steps.
        /// </summary>
        public void Ticks(long ticks)
        {
            Game.Session.Simulation.Clock.SetTimeMultiplier(1f);
            Game.Session.Simulation.Step(ticks);
            Game.Session.Simulation.Clock.SetTimeMultiplier(0f);
            Frame();
        }

        public void Frame(int count = 1)
        {
            for (int i = 0; i < count; i++) Game.RunOneFrame();
        }

        public void MoveTo(Vec2 screen)
        {
            Script.Position = screen;
            Frame();
        }

        /// <summary>A left click at a screen point: move there, press, release.</summary>
        public void Click(Vec2 screen)
        {
            MoveTo(screen);
            Script.Left = true;
            Frame();
            Script.Left = false;
            Frame();
        }
    }
}
