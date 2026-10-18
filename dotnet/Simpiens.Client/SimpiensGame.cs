using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Simpiens.Hosting;
using Simpiens.Presentation;
using Simpiens.Simulation;

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
        private SpriteBatch _batch;
        private SpriteAtlas _atlas;
        private WorldRenderer _renderer;

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

        /// <summary>What the view shows this frame: rebuilt from the simulation's state on every draw.</summary>
        public WorldScene Scene { get; } = new WorldScene();

        /// <summary>The camera of the last frame drawn.</summary>
        public TopDownCamera Camera { get; private set; }

        /// <summary>The view's clock in seconds: what animations and fades run on (<see cref="ClientOptions.ViewClockFromTicks"/>).</summary>
        public float ViewSeconds => Options.ViewClockFromTicks ? Simulation.Clock.CurrentTick * SimulationClock.FixedTickDeltaTime : (float)_realSeconds;

        private double _realSeconds;

        protected override void Initialize()
        {
            Window.Title = "Simpiens";
            base.Initialize();
        }

        protected override void LoadContent()
        {
            _batch = new SpriteBatch(GraphicsDevice);
            _atlas = new SpriteAtlas(GraphicsDevice, ProceduralSprites.All);
            _renderer = new WorldRenderer(_batch, _atlas);
        }

        protected override void Update(GameTime gameTime)
        {
            _realSeconds += gameTime.ElapsedGameTime.TotalSeconds;
            _driver.Advance(gameTime.ElapsedGameTime);
            base.Update(gameTime);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _atlas?.Dispose();
                _batch?.Dispose();
                Simulation.Dispose();
            }
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

            var map = Simulation.Host.Current?.Map;
            Scene.Build(map, Simulation.Registry.ActiveEntities, Simulation.Clock.CurrentTick, ViewSeconds);
            if (map == null) return;

            // M0: a fixed camera framing the map's camera frame at whole pixels per art pixel
            Camera = TopDownCamera.Framing(map.CameraFrame, width, height);
            _renderer.Draw(Scene.Items, Camera);
        }
    }
}
