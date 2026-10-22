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
            Options = options ?? new ClientOptions();
            _graphics = new GraphicsDeviceManager(this)
            {
                PreferredBackBufferWidth = Options.Width,
                PreferredBackBufferHeight = Options.Height,
                SynchronizeWithVerticalRetrace = Options.VSync,
            };
            IsFixedTimeStep = false;
            IsMouseVisible = true;

            Session = new ClientSession(Options);
            World = new WorldView(Session);
            Ui = new UiLayer(this);
            Play = new PlayScreen(this);
            Screens.Push(Play);
        }

        public ClientOptions Options { get; }

        /// <summary>What the client plays: the simulation, the frames, the selection, the dashboard model and the save slot.</summary>
        public ClientSession Session { get; }

        /// <summary>How the world is drawn: the projection, the camera, the scene, the map's text and picking.</summary>
        public WorldView World { get; }

        /// <summary>The UI layer over the screens (Gum).</summary>
        public UiLayer Ui { get; }

        /// <summary>The client's screens; the top one gets the input and the update.</summary>
        public ScreenStack Screens { get; } = new ScreenStack();

        /// <summary>Playing the outpost: the world view and the dashboard (the bottom screen).</summary>
        public PlayScreen Play { get; }

        /// <summary>What the screens draw with (made in LoadContent).</summary>
        public ClientGraphics Graphics { get; private set; }

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
            Ui.Initialize();
            Play.LoadContent();
        }

        protected override void Update(GameTime gameTime)
        {
            // KNI can report a slightly negative frame time (seen offscreen, epic F5): time never runs backwards here
            var elapsed = gameTime.ElapsedGameTime < System.TimeSpan.Zero ? System.TimeSpan.Zero : gameTime.ElapsedGameTime;
            float seconds = (float)elapsed.TotalSeconds;
            Session.Update(elapsed, Screens.WorldAdvances);
            // The camera's time: real time in the game; with the tick-based view clock (tests, captures), view time
            float view = Session.ViewSeconds;
            float cameraSeconds = Options.ViewClockFromTicks ? System.Math.Max(0f, view - _lastViewSeconds) : seconds;
            _lastViewSeconds = view;
            Ui.Update(gameTime); // first, so the screens know what is under UI this frame
            Screens.Update(seconds, cameraSeconds, IsActive);
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
            base.Draw(gameTime);
        }

        /// <summary>Draws the whole frame, the screens and then the UI over them, into the bound render target (the back buffer, or a capture).</summary>
        public void DrawFrame(int width, int height)
        {
            Screens.Draw(width, height);
            Ui.Draw();
        }

        /// <summary>Draws the map alone into the bound render target (the map's render references).</summary>
        public void DrawScene(int width, int height) => World.Draw(width, height);
    }
}
