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

        public void Clear() => _screens.Clear();

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
        /// <summary>Tooltips (Epic UI-Beta B5): a tip beside the cursor over a registered control.</summary>
        public Tooltips Tooltips { get; private set; }

        /// <summary>The key help (Epic UI-Beta B5): F1 shows every binding.</summary>
        public KeyHelpOverlay Help { get; private set; }

        public void LoadContent()
        {
            Dashboard = new DashboardView(_game.Ui, _game.Session.DashboardModel, _game.Options.DashboardRefreshSeconds, _game.Graphics.Icons, _game.Graphics.Portraits);
            Scenes = new DynamicSceneView(_game);
            Scenes.LoadContent();
            Help = new KeyHelpOverlay(_game.Ui);
            // Tooltips (Epic UI-Beta B5): over everything, on the header's and the inspector's buttons
            Tooltips = new Tooltips(_game.Ui);
            var header = Dashboard.Header;
            Tooltips.Register(header.MerchantButton.Visual, "The merchant terminal: the stall's prices and stock");
            Tooltips.Register(header.BountiesButton.Visual, "The bounty board: post and cancel bounties");
            Tooltips.Register(header.GuildButton.Visual, "The guild roster: members, wages and expeditions");
            Tooltips.Register(header.SaveButton.Visual, "Quick-save the outpost");
            Tooltips.Register(header.LoadButton.Visual, "Load the quick-save");
            Tooltips.Register(header.PauseButton.Visual, "Pause or resume the world (P)");
            Tooltips.Register(header.SpeedButton.Visual, "The world's speed (1 to 4)");
            Tooltips.Register(header.TabButton.Visual, "The dashboard or the map (Tab)");
            Tooltips.Register(Dashboard.Inspector.OpenDossierButton.Visual, "Its dossier: drives, plan, affinities");
            Tooltips.Register(Dashboard.Inspector.FollowButton.Visual, "The camera follows it, or stops (Esc stops too)");
            _game.Ui.Root.Children.Add(Tooltips.Panel);
        }

        public void Update(float seconds, float cameraSeconds, bool active)
        {
            var ui = _game.Ui;
            PointerInput pointer = default;
            KeyInput keys = default;
            if (active)
            {
                pointer = _game.CurrentInput.Pointer;
                keys = _game.CurrentInput.Keys;
                pointer.OverUi |= ui.PointerOverUi;
                if (ui.HasKeyboardFocus) keys = default;
                if (keys.WallStylePressed) _game.World.CycleWallStyle();
                if (keys.ViewTogglePressed) _game.World.ToggleView();
                if (keys.TimingsPressed) _game.TimingsOverlay?.Toggle();
                if (keys.HelpPressed) Help?.Toggle((_game.Input as ClientInput)?.Bindings);
                if (keys.DemoCutscenePressed && _game.Options.DemoScenes)
                {
                    _game.PlayCutscene(DemoScenes.Arrival);
                    return;
                }
                if (ui.IsOwner && Dashboard != null)
                {
                    if (keys.TabPressed) Dashboard.TogglePanels();
                    if (keys.PausePressed) Dashboard.Model.Header.TogglePause();
                    if (keys.SpeedKey > 0) Dashboard.Model.Header.SetSpeedStep(keys.SpeedKey - 1);
                    if (keys.EscapePressed && Dashboard.CloseWindow()) keys.EscapePressed = false; // Esc closes a window before it stops the camera
                }
            }

            var viewport = _game.GraphicsDevice.Viewport;
            _game.World.Update(cameraSeconds, viewport.Width, viewport.Height, pointer, keys, active);
            if (ui.IsOwner) Scenes?.Update(seconds, cameraSeconds, keys);
            if (ui.IsOwner) Dashboard?.Update(seconds);
            if (ui.IsOwner) Tooltips?.Update(seconds, viewport.Width, viewport.Height); // real time: a tip shows paused too
        }

        public void Draw(int width, int height) => _game.World.Draw(width, height);
    }
}
