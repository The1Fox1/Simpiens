using System;
using System.Reflection;
using Gum.Forms;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Input.Touch;
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

        /// <summary>The same pointer as XNA's mouse state, for Gum's cursor.</summary>
        public MouseState Mouse => new MouseState((int)Position.X, (int)Position.Y, 0,
            Left ? ButtonState.Pressed : ButtonState.Released,
            Middle ? ButtonState.Pressed : ButtonState.Released,
            Right ? ButtonState.Pressed : ButtonState.Released,
            ButtonState.Released, ButtonState.Released);
    }

    /// <summary>
    /// Gum's own cursor, fed from a <see cref="ScriptedInput"/> instead of the mouse: every rule of Gum's (push, click,
    /// what is under the pointer) runs as in the game. Gum reads the mouse through a private call, so this reaches its
    /// internal per-frame method; it fails loudly if a Gum update moves it.
    /// </summary>
    internal sealed class ScriptedGumCursor : MonoGameGum.Input.Cursor, Gum.Wireframe.ICursor
    {
        private static readonly MethodInfo FrameFromInput = typeof(MonoGameGum.Input.Cursor).GetMethod(
            "Activity", BindingFlags.NonPublic | BindingFlags.Instance, null,
            new[] { typeof(double), typeof(MouseState?), typeof(TouchCollection), typeof(bool) }, null)
            ?? throw new MissingMethodException("Gum's Cursor.Activity(double, MouseState?, TouchCollection, bool) is gone: update ScriptedGumCursor for this Gum version.");

        private readonly ScriptedInput _script;

        public ScriptedGumCursor(GameWindow window, ScriptedInput script) : base(window) => _script = script;

        void Gum.Wireframe.ICursor.Activity(double gameTime)
            => FrameFromInput.Invoke(this, new object[] { gameTime, (MouseState?)_script.Mouse, new TouchCollection(), false });
    }

    /// <summary>A game in the test process driven by a scripted pointer, with its simulation paused so nothing moves under it.</summary>
    internal sealed class UiDriver
    {
        public SimpiensGame Game { get; }
        public ScriptedInput Script { get; } = new ScriptedInput();

        private UiDriver(SimpiensGame game)
        {
            Game = game;
            game.Input = Script;
            Offscreen.MakeActive(game);
            FormsUtilities.SetCursor(new ScriptedGumCursor(game.Window, Script));
        }

        /// <summary>A paused game after one frame (the UI is up), taking scripted input.</summary>
        public static UiDriver Start(ClientOptions options = null, int width = 800, int height = 600)
        {
            var game = Offscreen.Start(options, frames: 0, width, height);
            game.Simulation.Clock.SetTimeMultiplier(0f);
            game.RunOneFrame();
            return new UiDriver(game);
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
