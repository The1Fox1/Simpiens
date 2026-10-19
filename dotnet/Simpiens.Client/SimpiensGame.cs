using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Simpiens.Client.Ui;
using Simpiens.Hosting;
using Simpiens.Narrative;
using Simpiens.Presentation;
using Simpiens.Presentation.Dashboard;
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

            Selection = new WorldSelection(Simulation.Registry, Simulation.Sectors, Simulation.Services.GetService(typeof(IAgentIdentityService)) as IAgentIdentityService);
            Selection.Changed += OnSelectionChanged;
            Ui = new UiLayer(this);
            DashboardModel = new DashboardModel(DashboardContext.From(Simulation.Services), Selection);
            Simulation.Host.SessionChanged += _ => DashboardModel.SessionChanged();
        }

        private bool _wasFollowing;

        /// <summary>A newly followed crawler starts the follow afresh, so the camera doesn't carry the last one's motion.</summary>
        private void OnSelectionChanged()
        {
            if (Selection.Following && !_wasFollowing) Rig.ResetFollow();
            _wasFollowing = Selection.Following;
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

        /// <summary>The player's selection: a crawler (followed or not) and a sector.</summary>
        public WorldSelection Selection { get; }

        /// <summary>The UI layer over the map (Gum).</summary>
        public UiLayer Ui { get; }

        /// <summary>The dashboard as data: what its views show, and where the player's clicks go.</summary>
        public DashboardModel DashboardModel { get; }

        /// <summary>The dashboard's views (built with the UI, in LoadContent).</summary>
        public DashboardView Dashboard { get; private set; }

        /// <summary>Where the pointer and keys come from while the window has focus: the devices, or a script in tests.</summary>
        public IClientInputSource Input { get; set; } = new ClientInput();

        /// <summary>The view's clock in seconds: what animations and fades run on (<see cref="ClientOptions.ViewClockFromTicks"/>).</summary>
        public float ViewSeconds => Options.ViewClockFromTicks ? Simulation.Clock.CurrentTick * SimulationClock.FixedTickDeltaTime : (float)_realSeconds;

        private double _realSeconds;
        private object _framedMap;
        private long _lastTick = long.MinValue;

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
            Ui.Initialize();
            Dashboard = new DashboardView(Ui, DashboardModel, Options.DashboardRefreshSeconds);
        }

        protected override void Update(GameTime gameTime)
        {
            // KNI can report a slightly negative frame time (seen offscreen, epic F5): time never runs backwards here
            var elapsed = gameTime.ElapsedGameTime < System.TimeSpan.Zero ? System.TimeSpan.Zero : gameTime.ElapsedGameTime;
            float seconds = (float)elapsed.TotalSeconds;
            _realSeconds += seconds;
            _driver.Advance(elapsed);
            Ui.Update(gameTime); // first, so the map knows what is under UI this frame
            UpdateView(seconds, IsActive);
            if (Ui.IsOwner) Dashboard?.Update(seconds);
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
            long tick = Simulation.Clock.CurrentTick;
            if (tick < _lastTick) Selection.Clear(); // a respawn or a load: the selected crawler is gone
            _lastTick = tick;
            if (!ReferenceEquals(map, _framedMap))
            {
                Rig.Configure(map.CameraFrame, viewport.AspectRatio, viewport.Height);
                Selection.Clear();
                _framedMap = map;
            }
            Rig.SetPixelHeight(viewport.Height);
            Rig.SetAspect(viewport.AspectRatio);

            LastInput = default;
            if (readInput)
            {
                Input.Read(out var pointer, out var keys);
                pointer.OverUi |= Ui.PointerOverUi;
                if (Ui.HasKeyboardFocus) keys = default;
                if (Ui.IsOwner && Dashboard != null)
                {
                    if (keys.TabPressed) Dashboard.TogglePanels();
                    if (keys.EscapePressed && Dashboard.CloseWindow()) keys.EscapePressed = false; // Esc closes a window before it stops the camera
                }
                LastInput = Controls.Update(pointer, keys, seconds, Rig, Rig.ToCamera(viewport.Width, viewport.Height));
                if (LastInput.StopFollowing) Selection.StopFollowing();
                if (LastInput.Clicked) Selection.ClickAt(LastInput.ClickWorld);
            }

            // The camera follows the selected crawler (Unity did this after the input, in LateUpdate)
            if (Selection.Following && Selection.TryGetCrawlerPosition(out var crawler)) Rig.Follow(crawler, seconds);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_sheets != null) foreach (var sheet in _sheets) sheet.Dispose();
                _batch?.Dispose();
                Ui.Shutdown();
                Simulation.Dispose();
            }
            base.Dispose(disposing);
        }

        protected override void Draw(GameTime gameTime)
        {
            var viewport = GraphicsDevice.Viewport;
            DrawFrame(viewport.Width, viewport.Height);
            base.Draw(gameTime);
        }

        /// <summary>Draws the whole frame, the map and then the UI over it, into the bound render target (the back buffer, or a capture).</summary>
        public void DrawFrame(int width, int height)
        {
            DrawScene(width, height);
            Ui.Draw();
        }

        /// <summary>Draws the map alone into the bound render target.</summary>
        public void DrawScene(int width, int height)
        {
            GraphicsDevice.Clear(Background);

            var map = Simulation.Host.Current?.Map;
            Scene.Build(map, Simulation.Sectors, Simulation.Registry.ActiveEntities, Simulation.Clock.CurrentTick, ViewSeconds, Selection);
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
