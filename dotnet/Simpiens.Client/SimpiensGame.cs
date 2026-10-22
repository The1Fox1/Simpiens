using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Simpiens.Client.Ui;
using Simpiens.Core.Session;
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
        private FontAtlases _fontAtlases;
        private readonly MapLabelLayout _labelLayout = new MapLabelLayout();
        private readonly List<ScreenGlyph> _glyphs = new List<ScreenGlyph>(512);
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
            Simulation = HeadlessSimulation.Create(Options.Seed, Options.ConfigureServices);
            Simulation.Respawn(Options.SessionId);
            _driver = new SimulationDriver(Simulation.Clock, Options.TicksPerUpdate);
            // What the views draw, copied when each tick completes (and afresh on every respawn, switch or load)
            Frames.Follow(Simulation.Host, Simulation.Registry, Simulation.Sectors, Simulation.Clock);

            Selection = new WorldSelection(Frames, Simulation.Sectors, Simulation.Services.GetService(typeof(IAgentIdentityService)) as IAgentIdentityService);
            Selection.Changed += OnSelectionChanged;
            Ui = new UiLayer(this);
            SaveGame = new SaveGameService(Simulation.Host, Options.SaveDirectory ?? ClientOptions.DefaultSaveDirectory());
            DashboardModel = new DashboardModel(DashboardContext.From(Simulation.Services, SaveGame), Selection);
            Simulation.Host.SessionChanged += OnSessionChanged;

            // The first map framed for the window asked for; updates keep the rig on the real window and later maps,
            // so drawing never changes the camera (review A6)
            var map = Simulation.Host.Current?.Map;
            if (map != null)
            {
                Rig.Configure(map.CameraFrame, (float)Options.Width / Options.Height, Options.Height);
                _framedMap = map;
            }
        }

        private bool _wasFollowing;

        /// <summary>
        /// A respawn, a session switch or a load replaced every entity (ids are reissued). The views see it as a new epoch
        /// of their frames (review A5); the dashboard still reads the live simulation, so it is told here.
        /// </summary>
        private void OnSessionChanged(ISessionDefinition session) => DashboardModel.SessionChanged();

        /// <summary>A newly followed crawler starts the follow afresh, so the camera doesn't carry the last one's motion.</summary>
        private void OnSelectionChanged()
        {
            if (Selection.Following && !_wasFollowing) Rig.ResetFollow();
            _wasFollowing = Selection.Following;
        }

        public ClientOptions Options { get; }

        /// <summary>The simulation this client plays: the views read it, and only typed commands change it.</summary>
        public HeadlessSimulation Simulation { get; }

        /// <summary>The world as the views draw it: the last two completed ticks, as plain data (review A3).</summary>
        public WorldViewFrames Frames { get; } = new WorldViewFrames();

        /// <summary>What the view shows: its state advanced once per update, its picture presented from <see cref="Frames"/> on every draw.</summary>
        public WorldScene Scene { get; } = new WorldScene();

        /// <summary>The view of the last frame drawn: its projection and camera.</summary>
        public View View { get; private set; }

        /// <summary>How the map is projected (top-down until M3): the camera rig and the scene use the same one.</summary>
        public IViewProjection Projection
        {
            get => Rig.Projection;
            set
            {
                Rig.Projection = value;
                Scene.Projection = Rig.Projection;
            }
        }

        /// <summary>The player's view of the map: where it looks and how close (Unity's camera rig).</summary>
        public CameraRig Rig { get; } = new CameraRig();

        /// <summary>The player's camera controls, fed from the mouse and keyboard each update.</summary>
        public CameraInput Controls { get; } = new CameraInput();

        /// <summary>What the controls asked for in the last update (a click, a stop to following).</summary>
        public CameraInputResult LastInput { get; private set; }

        /// <summary>The player's selection: a crawler (followed or not) and a sector.</summary>
        public WorldSelection Selection { get; }

        /// <summary>The baked fonts' metrics (loaded with the content).</summary>
        public FontLibrary Fonts { get; private set; }

        /// <summary>How the map's text was placed in the last frame drawn.</summary>
        public MapLabelLayout LabelLayout => _labelLayout;

        /// <summary>The UI layer over the map (Gum).</summary>
        public UiLayer Ui { get; }

        /// <summary>The quick-save slot (<see cref="ClientOptions.SaveDirectory"/>).</summary>
        public ISaveGameService SaveGame { get; }

        /// <summary>The dashboard as data: what its views show, and where the player's clicks go.</summary>
        public DashboardModel DashboardModel { get; }

        /// <summary>The dashboard's views (built with the UI, in LoadContent).</summary>
        public DashboardView Dashboard { get; private set; }

        /// <summary>Where the pointer and keys come from while the window has focus: the devices, or a script in tests.</summary>
        public IClientInputSource Input { get; set; } = new ClientInput();

        /// <summary>The view's clock in seconds: what animations and fades run on (<see cref="ClientOptions.ViewClockFromTicks"/>).</summary>
        public float ViewSeconds => Options.ViewClockFromTicks ? Simulation.Clock.CurrentTick * SimulationClock.FixedTickDeltaTime : (float)_realSeconds;

        /// <summary>
        /// How far the clock is between the last two ticks, for drawing crawlers between them: the clock's own fraction
        /// live; 1 (the last tick exactly) when the view runs on ticks or every update steps whole ticks.
        /// </summary>
        public float ViewAlpha => Options.ViewClockFromTicks || Options.TicksPerUpdate > 0 ? 1f : Simulation.Clock.InterpolationAlpha;

        private double _realSeconds;
        private float _lastViewSeconds;
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
            // By DrawSheet value: the generated art, the fog's cloud
            _sheets = new[]
            {
                new SpriteAtlas(GraphicsDevice, ProceduralSprites.All),
                new SpriteAtlas(GraphicsDevice, new[] { FogCloud.Sprite }, smooth: true, gutter: 0),
            };
            _renderer = new WorldRenderer(_batch, _sheets);
            // The baked fonts (Simpiens.FontBaker), through the platform's content: the map's text is drawn from them
            Fonts = FontLibrary.Load(TitleContainer.OpenStream);
            _fontAtlases = new FontAtlases(GraphicsDevice, Fonts, TitleContainer.OpenStream);
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
            Frames.Alpha = ViewAlpha;
            // The camera's time: real time in the game; with the tick-based view clock (tests, captures), view time
            float view = ViewSeconds;
            float cameraSeconds = Options.ViewClockFromTicks ? System.Math.Max(0f, view - _lastViewSeconds) : seconds;
            _lastViewSeconds = view;
            Ui.Update(gameTime); // first, so the map knows what is under UI this frame
            UpdateView(cameraSeconds, IsActive);
            Scene.Advance(Simulation.Host.Current?.Map, Frames, view); // fades and poses move on here, never in a draw
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
            // A rebuilt world drops the selection (a new epoch of the frames); a new map is framed whole
            Selection.Update();
            if (!ReferenceEquals(map, _framedMap))
            {
                Rig.Configure(map.CameraFrame, viewport.AspectRatio, viewport.Height);
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
                LastInput = Controls.Update(pointer, keys, seconds, Rig, Rig.ToView(viewport.Width, viewport.Height));
                if (LastInput.StopFollowing) Selection.StopFollowing();
                if (LastInput.Clicked) ClickMap(LastInput.ClickScreen);
            }

            // The camera follows the selected crawler (Unity did this after the input, in LateUpdate); one that left the world is dropped
            Selection.Update();
            if (Selection.Following && Selection.TryGetCrawlerPosition(out var crawler)) Rig.Follow(crawler, seconds);
        }

        /// <summary>
        /// A left click on the map at a screen point: it picks what the last frame drew there (the picture the player
        /// clicked on), and selects that crawler or the room under the point. Nothing is picked before the first frame.
        /// </summary>
        public void ClickMap(Vec2 screen)
        {
            if (View.Projection == null) return;
            WorldPicker.TryPick(Scene.Items, View, screen, out var crawler);
            Selection.ClickAt(View.ScreenToFloor(screen), crawler);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_sheets != null) foreach (var sheet in _sheets) sheet.Dispose();
                _fontAtlases?.Dispose();
                _batch?.Dispose();
                Ui.Shutdown();
                Frames.Dispose();
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

        /// <summary>
        /// Draws the map alone into the bound render target. It presents the scene from the state the last update left
        /// and changes none (review A6), so two draws, or a capture at another size, never move a fade or a pose.
        /// </summary>
        public void DrawScene(int width, int height)
        {
            GraphicsDevice.Clear(Background);

            var map = Simulation.Host.Current?.Map;
            Scene.Present(map, Frames, ViewSeconds, Selection);
            if (map == null) return;

            View = Rig.ToView(width, height);
            _renderer.Draw(Scene.Items, View);
            _labelLayout.Layout(Scene.Labels, View, Fonts, _glyphs);
            _renderer.DrawGlyphs(_glyphs, _fontAtlases);
        }
    }
}
