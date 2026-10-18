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
        private SpriteAtlas[] _sheets;
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

        /// <summary>The player's view of the map: where it looks and how close (Unity's camera rig).</summary>
        public CameraRig Rig { get; } = new CameraRig();

        /// <summary>The player's camera controls, fed from the mouse and keyboard each update.</summary>
        public CameraInput Controls { get; } = new CameraInput();

        /// <summary>What the controls asked for in the last update (a click, a stop to following).</summary>
        public CameraInputResult LastInput { get; private set; }

        /// <summary>The view's clock in seconds: what animations and fades run on (<see cref="ClientOptions.ViewClockFromTicks"/>).</summary>
        public float ViewSeconds => Options.ViewClockFromTicks ? Simulation.Clock.CurrentTick * SimulationClock.FixedTickDeltaTime : (float)_realSeconds;

        private double _realSeconds;
        private readonly ClientInput _input = new ClientInput();
        private object _framedMap;

        protected override void Initialize()
        {
            Window.Title = "Simpiens";
            Window.AllowUserResizing = true;
            Window.ClientSizeChanged += OnClientSizeChanged;
            base.Initialize();
        }

        private void OnClientSizeChanged(object sender, System.EventArgs e)
        {
            var bounds = Window.ClientBounds;
            if (bounds.Width <= 0 || bounds.Height <= 0) return;
            if (bounds.Width == _graphics.PreferredBackBufferWidth && bounds.Height == _graphics.PreferredBackBufferHeight) return;
            _graphics.PreferredBackBufferWidth = bounds.Width;
            _graphics.PreferredBackBufferHeight = bounds.Height;
            _graphics.ApplyChanges();
        }

        protected override void LoadContent()
        {
            _batch = new SpriteBatch(GraphicsDevice);
            // By DrawSheet value: the generated art, the pixel font, the fog's cloud
            _sheets = new[]
            {
                new SpriteAtlas(GraphicsDevice, ProceduralSprites.All),
                new SpriteAtlas(GraphicsDevice, PixelFont.Glyphs),
                new SpriteAtlas(GraphicsDevice, new[] { FogCloud.Sprite }, smooth: true, gutter: 0),
            };
            _renderer = new WorldRenderer(_batch, _sheets);
        }

        protected override void Update(GameTime gameTime)
        {
            float seconds = (float)gameTime.ElapsedGameTime.TotalSeconds;
            _realSeconds += seconds;
            _driver.Advance(gameTime.ElapsedGameTime);
            UpdateView(seconds, IsActive);
            base.Update(gameTime);
        }

        /// <summary>
        /// Keeps the camera rig on the current map and the window's shape, then applies the player's input (only while
        /// the window has focus). Unscaled time: the camera feels the same paused or at 10x.
        /// </summary>
        private void UpdateView(float seconds, bool readInput)
        {
            var map = Simulation.Host.Current?.Map;
            if (map == null) return;
            var viewport = GraphicsDevice.Viewport;
            if (!ReferenceEquals(map, _framedMap))
            {
                Rig.Configure(map.CameraFrame, viewport.AspectRatio, viewport.Height);
                _framedMap = map;
            }
            Rig.SetPixelHeight(viewport.Height);
            Rig.SetAspect(viewport.AspectRatio);

            if (!readInput)
            {
                LastInput = default;
                return;
            }
            _input.Read(out var pointer, out var keys);
            LastInput = Controls.Update(pointer, keys, seconds, Rig, Rig.ToCamera(viewport.Width, viewport.Height));
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_sheets != null) foreach (var sheet in _sheets) sheet.Dispose();
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
            Scene.Build(map, Simulation.Sectors, Simulation.Registry.ActiveEntities, Simulation.Clock.CurrentTick, ViewSeconds);
            if (map == null) return;

            if (!ReferenceEquals(map, _framedMap))
            {
                Rig.Configure(map.CameraFrame, (float)width / height, height); // before the first update
                _framedMap = map;
            }
            Camera = Rig.ToCamera(width, height);
            _renderer.Draw(Scene.Items, Camera);
        }
    }
}
