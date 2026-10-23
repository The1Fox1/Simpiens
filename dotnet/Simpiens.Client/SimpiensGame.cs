using Microsoft.Xna.Framework;
using Simpiens.Client.Ui;

namespace Simpiens.Client
{
    /// <summary>
    /// The game client (Epic CLIENT): the XNA shell around what it plays (review A4). It owns the window, the device,
    /// the content, the UI layer and a stack of screens; the <see cref="ClientSession"/> holds the simulation and the
    /// player's state, and the <see cref="WorldView"/> draws the world. It reads the simulation's state and never writes
    /// it; the player's actions reach the simulation as typed commands (ROADMAP §1A.1).
    /// </summary>
    public sealed class SimpiensGame : Game
    {
        private readonly GraphicsDeviceManager _graphics;
        private float _lastViewSeconds;

        public SimpiensGame(ClientOptions options = null)
        {
            _graphics = new GraphicsDeviceManager(this);
            IsFixedTimeStep = false;
            IsMouseVisible = true;
            Ui = new UiLayer(this);
            Compose(options ?? new ClientOptions());
        }

        /// <summary>A client session and its views, for <paramref name="options"/>: the simulation, the world view, the play screen.</summary>
        private void Compose(ClientOptions options)
        {
            Options = options;
            _graphics.PreferredBackBufferWidth = Options.Width;
            _graphics.PreferredBackBufferHeight = Options.Height;
            _graphics.SynchronizeWithVerticalRetrace = Options.VSync;
            _lastViewSeconds = 0f;
            Session = new ClientSession(Options);
            World = new WorldView(Session);
            Play = new PlayScreen(this);
            Screens.Push(Play);
        }

        /// <summary>
        /// Starts a new client session in this window, on this device: a new simulation, world view and play screen, as a
        /// new game would, without opening a new window (a "new game" menu later; the tests reuse a few windows for all
        /// their games, epic-client F15). Whatever was playing ends, cutscenes included, and the UI starts afresh.
        /// </summary>
        public void Restart(ClientOptions options)
        {
            while (Screens.Top is CutsceneScreen cutscene) EndCutscene(cutscene);
            Screens.Clear();
            World.Dispose();
            Session.Dispose();
            Timings.Clear(); // a new game's frames
            Compose(options ?? new ClientOptions());
            if (Graphics == null) return; // the content isn't loaded yet: LoadContent does the rest
            _graphics.ApplyChanges();
            World.LoadContent(Graphics);
            InitializeUi();
        }

        public ClientOptions Options { get; private set; }

        /// <summary>What the client plays: the simulation, the frames, the selection, the dashboard model and the save slot.</summary>
        public ClientSession Session { get; private set; }

        /// <summary>How the world is drawn: the projection, the camera, the scene, the map's text and picking.</summary>
        public WorldView World { get; private set; }

        /// <summary>The UI layer over the screens (Gum).</summary>
        public UiLayer Ui { get; }

        /// <summary>The client's screens; the top one gets the input and the update.</summary>
        public ScreenStack Screens { get; } = new ScreenStack();

        /// <summary>Playing the outpost: the world view and the dashboard (the bottom screen).</summary>
        public PlayScreen Play { get; private set; }

        /// <summary>What the screens draw with (made in LoadContent).</summary>
        public ClientGraphics Graphics { get; private set; }

        /// <summary>How long each part of a frame takes, over the last few seconds (review D1).</summary>
        public FrameTimings Timings { get; } = new FrameTimings();

        /// <summary>The timings in a corner, in development builds (F3); null without <see cref="ClientOptions.TimingsOverlay"/>.</summary>
        public TimingsOverlay TimingsOverlay { get; private set; }

        /// <summary>Where the pointer and keys come from while the window has focus: the devices, or a script in tests.</summary>
        public IClientInputSource Input { get; set; } = new ClientInput();

        /// <summary>Plays a hard cutscene over the game (M3): it covers the world and holds it until it ends.</summary>
        public CutsceneScreen PlayCutscene(Simpiens.Presentation.Cutscenes.CutsceneScript script)
        {
            var screen = new CutsceneScreen(this, script);
            Screens.Push(screen);
            screen.Open();
            return screen;
        }

        /// <summary>A cutscene ended (or was skipped): it leaves the stack and the game goes on.</summary>
        internal void EndCutscene(CutsceneScreen screen)
        {
            if (Screens.Remove(screen)) screen.Close();
        }

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
            Graphics = new ClientGraphics(GraphicsDevice);
            World.LoadContent(Graphics);
            InitializeUi();
        }

        /// <summary>Starts the UI afresh: Gum, then what lives in it (the dashboard, the timings).</summary>
        private void InitializeUi()
        {
            Ui.Initialize();
            Play.LoadContent();
            TimingsOverlay = Options.TimingsOverlay ? new TimingsOverlay(Ui, Timings) : null;
        }

        protected override void Update(GameTime gameTime)
        {
            // KNI can report a slightly negative frame time (seen offscreen, epic F5): time never runs backwards here
            var elapsed = gameTime.ElapsedGameTime < System.TimeSpan.Zero ? System.TimeSpan.Zero : gameTime.ElapsedGameTime;
            float seconds = (float)elapsed.TotalSeconds;
            long start = FrameTimings.Start();
            Session.Update(elapsed, Screens.WorldAdvances);
            Timings.Add(FramePhase.Tick, start);
            // The camera's time: real time in the game; with the tick-based view clock (tests, captures), view time
            float view = Session.ViewSeconds;
            float cameraSeconds = Options.ViewClockFromTicks ? System.Math.Max(0f, view - _lastViewSeconds) : seconds;
            _lastViewSeconds = view;
            start = FrameTimings.Start();
            Ui.Update(gameTime); // first, so the screens know what is under UI this frame
            Timings.Add(FramePhase.UiUpdate, start);
            start = FrameTimings.Start();
            Screens.Update(seconds, cameraSeconds, IsActive);
            Timings.Add(FramePhase.ScreenUpdate, start);
            TimingsOverlay?.Update(seconds);
            base.Update(gameTime);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                World.Dispose();
                Graphics?.Dispose();
                Ui.Shutdown();
                Session.Dispose();
            }
            base.Dispose(disposing);
        }

        protected override void Draw(GameTime gameTime)
        {
            var viewport = GraphicsDevice.Viewport;
            DrawFrame(viewport.Width, viewport.Height);
            Timings.EndFrame();
            base.Draw(gameTime);
        }

        /// <summary>Draws the whole frame, the screens and then the UI over them, into the bound render target (the back buffer, or a capture).</summary>
        public void DrawFrame(int width, int height)
        {
            long start = FrameTimings.Start();
            Screens.Draw(width, height);
            Timings.Add(FramePhase.ScreenDraw, start);
            start = FrameTimings.Start();
            Ui.Draw();
            Timings.Add(FramePhase.UiDraw, start);
        }

        /// <summary>Draws the map alone into the bound render target (the map's render references).</summary>
        public void DrawScene(int width, int height) => World.Draw(width, height);
    }
}
