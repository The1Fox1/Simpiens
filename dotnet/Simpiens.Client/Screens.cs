using System;
using System.Collections.Generic;
using Simpiens.Client.Ui;
using Simpiens.Presentation;

namespace Simpiens.Client
{
    /// <summary>
    /// One mode of the client (review A4): play, and from M3 a hard cutscene; later the developers' map designer. The
    /// top screen of the <see cref="ScreenStack"/> gets the frame's input and update.
    /// </summary>
    public interface IScreen
    {
        /// <summary>True when it fills the window: the screens under it aren't drawn.</summary>
        bool Covers { get; }

        /// <summary>True when the world advances while it is on top; a hard cutscene holds it.</summary>
        bool AdvancesWorld { get; }

        /// <summary>
        /// One update on top of the stack. <paramref name="seconds"/> is the frame's real time; <paramref name="cameraSeconds"/>
        /// the view's (real time live, the tick's time with the tick-based view clock); <paramref name="active"/> whether
        /// the window has focus (input is read only then).
        /// </summary>
        void Update(float seconds, float cameraSeconds, bool active);

        /// <summary>Draws into the bound render target, changing no state.</summary>
        void Draw(int width, int height);
    }

    /// <summary>The client's screens, bottom to top. Only the top one updates; drawing starts at the topmost that covers the window.</summary>
    public sealed class ScreenStack
    {
        private readonly List<IScreen> _screens = new List<IScreen>(4);

        public int Count => _screens.Count;

        public IScreen Top => _screens.Count > 0 ? _screens[_screens.Count - 1] : null;

        /// <summary>True unless the top screen holds the world (a hard cutscene).</summary>
        public bool WorldAdvances => Top == null || Top.AdvancesWorld;

        public void Push(IScreen screen) => _screens.Add(screen ?? throw new ArgumentNullException(nameof(screen)));

        /// <summary>Removes <paramref name="screen"/> (normally the top one); false when it isn't on the stack.</summary>
        public bool Remove(IScreen screen) => _screens.Remove(screen);

        public bool Contains(IScreen screen) => _screens.Contains(screen);

        public void Update(float seconds, float cameraSeconds, bool active) => Top?.Update(seconds, cameraSeconds, active);

        public void Draw(int width, int height)
        {
            int first = 0;
            for (int i = _screens.Count - 1; i >= 0; i--)
            {
                if (_screens[i].Covers) { first = i; break; }
            }
            for (int i = first; i < _screens.Count; i++) _screens[i].Draw(width, height);
        }
    }

    /// <summary>
    /// Playing the outpost (Epic CLIENT): the world view and the dashboard over it. It routes the frame's input: the UI
    /// takes what lands on it, Tab and Esc go to the dashboard first, and the rest drives the camera and picking.
    /// </summary>
    public sealed class PlayScreen : IScreen
    {
        private readonly SimpiensGame _game;

        public PlayScreen(SimpiensGame game) => _game = game ?? throw new ArgumentNullException(nameof(game));

        /// <summary>The dashboard's views (built with the UI, in <see cref="LoadContent"/>).</summary>
        public DashboardView Dashboard { get; private set; }

        /// <summary>Dynamic scenes over the world: their speech bubbles and box (M3 design D9).</summary>
        public DynamicSceneView Scenes { get; private set; }

        public bool Covers => true;

        public bool AdvancesWorld => true;

        /// <summary>Builds the dashboard's views, once the UI layer is up.</summary>
        public void LoadContent()
        {
            Dashboard = new DashboardView(_game.Ui, _game.Session.DashboardModel, _game.Options.DashboardRefreshSeconds);
            Scenes = new DynamicSceneView(_game);
            Scenes.LoadContent();
        }

        public void Update(float seconds, float cameraSeconds, bool active)
        {
            var ui = _game.Ui;
            PointerInput pointer = default;
            KeyInput keys = default;
            if (active)
            {
                _game.Input.Read(out pointer, out keys);
                pointer.OverUi |= ui.PointerOverUi;
                if (ui.HasKeyboardFocus) keys = default;
                if (keys.WallStylePressed) _game.World.CycleWallStyle();
                if (keys.ViewTogglePressed) _game.World.ToggleView();
                if (keys.DemoCutscenePressed && _game.Options.DemoScenes)
                {
                    _game.PlayCutscene(DemoScenes.Arrival);
                    return;
                }
                if (ui.IsOwner && Dashboard != null)
                {
                    if (keys.TabPressed) Dashboard.TogglePanels();
                    if (keys.EscapePressed && Dashboard.CloseWindow()) keys.EscapePressed = false; // Esc closes a window before it stops the camera
                }
            }

            var viewport = _game.GraphicsDevice.Viewport;
            _game.World.Update(cameraSeconds, viewport.Width, viewport.Height, pointer, keys, active);
            if (ui.IsOwner) Scenes?.Update(seconds, cameraSeconds, keys);
            if (ui.IsOwner) Dashboard?.Update(seconds);
        }

        public void Draw(int width, int height) => _game.World.Draw(width, height);
    }
}
