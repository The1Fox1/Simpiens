using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Simpiens.Hosting;

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
        private readonly SimulationDriver _driver;

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

            // The core composed as every .NET host composes it, the session spawned at tick 0 before the first frame
            Simulation = HeadlessSimulation.Create(Options.Seed);
            Simulation.Respawn(Options.SessionId);
            _driver = new SimulationDriver(Simulation.Clock, Options.TicksPerUpdate);
        }

        public ClientOptions Options { get; }

        /// <summary>The simulation this client plays: the views read it, and only typed commands change it.</summary>
        public HeadlessSimulation Simulation { get; }

        protected override void Initialize()
        {
            Window.Title = "Simpiens";
            base.Initialize();
        }

        protected override void Update(GameTime gameTime)
        {
            _driver.Advance(gameTime.ElapsedGameTime);
            base.Update(gameTime);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) Simulation.Dispose();
            base.Dispose(disposing);
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
