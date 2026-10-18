using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Simpiens.Client
{
    /// <summary>
    /// The game client (Epic CLIENT): an XNA <see cref="Game"/> that ticks the engine-free simulation and draws it. It
    /// reads the simulation's state and never writes it; the player's actions reach the simulation as typed commands
    /// (ROADMAP §1A.1).
    /// </summary>
    public sealed class SimpiensGame : Game
    {
        /// <summary>The colour behind the map (outside the floor and rock).</summary>
        public static readonly Color Background = new Color(12, 14, 20);

        private readonly GraphicsDeviceManager _graphics;

        public SimpiensGame(ClientOptions options = null)
        {
            Options = options ?? new ClientOptions();
            _graphics = new GraphicsDeviceManager(this)
            {
                PreferredBackBufferWidth = Options.Width,
                PreferredBackBufferHeight = Options.Height,
                SynchronizeWithVerticalRetrace = Options.VSync,
            };
            IsFixedTimeStep = false;
            IsMouseVisible = true;
        }

        public ClientOptions Options { get; }

        protected override void Initialize()
        {
            Window.Title = "Simpiens";
            base.Initialize();
        }

        protected override void Draw(GameTime gameTime)
        {
            var viewport = GraphicsDevice.Viewport;
            DrawScene(viewport.Width, viewport.Height);
            base.Draw(gameTime);
        }

        /// <summary>Draws the current frame into the bound render target (the back buffer, or a capture).</summary>
        public void DrawScene(int width, int height)
        {
            GraphicsDevice.Clear(Background);
        }
    }
}
