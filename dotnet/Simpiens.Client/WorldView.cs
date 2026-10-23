using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Simpiens.Presentation;

namespace Simpiens.Client
{
    /// <summary>
    /// How the world is drawn (Epic CLIENT, review A4): the projection, the camera rig and its controls, the scene, the
    /// map's text and picking, over a <see cref="ClientSession"/>. Its state moves on in <see cref="Update"/>; a draw only
    /// presents it (review A6). Everything but <see cref="LoadContent"/> and <see cref="Draw"/> works without a GPU.
    /// </summary>
    public sealed class WorldView : IDisposable
    {
        /// <summary>The colour behind the map (outside the floor and rock).</summary>
        public static readonly Color Background = new Color(12, 14, 20);

        private readonly ClientSession _session;
        private readonly MapLabelLayout _labelLayout = new MapLabelLayout();
        private readonly List<ScreenGlyph> _glyphs = new List<ScreenGlyph>(512);
        private ClientGraphics _graphics;
        private object _framedMap;
        private bool _wasFollowing;
        private EntityId _directed;

        public WorldView(ClientSession session)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _session.Selection.Changed += OnSelectionChanged;
            Mode = _session.Options.View ?? ViewMode.Isometric;

            // The first map framed for the window asked for; updates keep the rig on the real window and later maps,
            // so drawing never changes the camera (review A6)
            var map = _session.Simulation.Host.Current?.Map;
            if (map != null)
            {
                var options = _session.Options;
                Rig.Configure(map.CameraFrame, (float)options.Width / options.Height, options.Height);
                _framedMap = map;
            }
        }

        /// <summary>What the view shows: its state advanced once per update, its picture presented from the frames on every draw.</summary>
        public WorldScene Scene { get; } = new WorldScene();

        /// <summary>The view of the last frame drawn: its projection and camera.</summary>
        public View View { get; private set; }

        /// <summary>
        /// How the world is looked at: its projection and its art (M3 design D4). The camera rig and the scene take it
        /// together, so they can't disagree.
        /// </summary>
        public ViewMode Mode
        {
            get => _mode;
            set
            {
                _mode = value ?? throw new ArgumentNullException(nameof(value));
                Rig.Projection = _mode.Projection;
                Scene.Mode = _mode;
            }
        }

        private ViewMode _mode = ViewMode.TopDown;

        /// <summary>The mode's projection.</summary>
        public IViewProjection Projection => _mode.Projection;

        /// <summary>The player's view of the map: where it looks and how close (Unity's camera rig).</summary>
        public CameraRig Rig { get; } = new CameraRig();

        /// <summary>The player's camera controls, fed from the mouse and keyboard each update.</summary>
        public CameraInput Controls { get; } = new CameraInput();

        /// <summary>What the controls asked for in the last update (a click, a stop to following).</summary>
        public CameraInputResult LastInput { get; private set; }

        /// <summary>
        /// The crawler a dynamic scene sent the camera to, or default (M3 design D9). The player keeps control (producer
        /// P8): their own pan, Home, or following a crawler of their own takes the camera back.
        /// </summary>
        public EntityId DirectedAt => _directed;

        /// <summary>Sends the camera after <paramref name="crawler"/> (a dynamic scene's focus), until the player takes it back.</summary>
        public void DirectCamera(EntityId crawler)
        {
            if (crawler != _directed) Rig.ResetFollow();
            _directed = crawler;
        }

        public void ReleaseCamera()
        {
            if (_directed == default) return;
            _directed = default;
            Rig.ResetFollow(); // a follow resumed afterwards starts afresh, without the scene's last target
        }

        /// <summary>The baked fonts' metrics (loaded with the content).</summary>
        public FontLibrary Fonts => _graphics?.Fonts;

        /// <summary>How the map's text was placed in the last frame drawn.</summary>
        public MapLabelLayout LabelLayout => _labelLayout;

        /// <summary>What it draws with: the sheets, the renderer and the fonts, made by the shell once the device exists.</summary>
        public void LoadContent(ClientGraphics graphics) => _graphics = graphics;

        /// <summary>
        /// One update of the view, in this order: the selection catches up with the frames, the rig follows the map and
        /// the window, the player's input applies (when <paramref name="hasInput"/>: the screen read and filtered it), the
        /// camera follows the selected crawler, and the scene's state moves on. <paramref name="cameraSeconds"/> is
        /// unscaled, so the camera feels the same paused or at 10x.
        /// </summary>
        public void Update(float cameraSeconds, int viewportWidth, int viewportHeight, PointerInput pointer, KeyInput keys, bool hasInput)
        {
            var map = _session.Simulation.Host.Current?.Map;
            var selection = _session.Selection;
            if (map != null && viewportWidth > 0 && viewportHeight > 0)
            {
                // A rebuilt world drops the selection (a new epoch of the frames); a new map is framed whole
                selection.Update();
                float aspect = (float)viewportWidth / viewportHeight;
                if (!ReferenceEquals(map, _framedMap))
                {
                    Rig.Configure(map.CameraFrame, aspect, viewportHeight);
                    _framedMap = map;
                }
                Rig.SetPixelHeight(viewportHeight);
                Rig.SetAspect(aspect);

                LastInput = default;
                if (hasInput)
                {
                    LastInput = Controls.Update(pointer, keys, cameraSeconds, Rig, Rig.ToView(viewportWidth, viewportHeight));
                    if (LastInput.StopFollowing)
                    {
                        selection.StopFollowing();
                        ReleaseCamera(); // the player's own pan takes the camera back from a scene
                    }
                    if (LastInput.Clicked) ClickMap(LastInput.ClickScreen);
                }

                // The camera follows the selected crawler (Unity did this after the input, in LateUpdate); one that left the world is dropped
                selection.Update();
                if (_directed != default && _session.Frames.TryGetDrawnPosition(_directed, out var directed)) Rig.Follow(directed, cameraSeconds);
                else if (selection.Following && selection.TryGetCrawlerPosition(out var crawler)) Rig.Follow(crawler, cameraSeconds);
            }
            Scene.Advance(map, _session.Frames, _session.ViewSeconds); // fades and poses move on here, never in a draw
        }

        /// <summary>
        /// Isometric to top-down and back (M3 design D8). The camera keeps the floor point at the centre and its scale in
        /// screen pixels per art pixel (both art sets have 16 art pixels a unit), clamped to the new view's limits.
        /// </summary>
        public void ToggleView() => Mode = ReferenceEquals(Mode, ViewMode.Isometric) ? ViewMode.TopDown : ViewMode.Isometric;

        /// <summary>The next wall style: cutaway, full, low, and round again (M3 design D7).</summary>
        public void CycleWallStyle() => Scene.WallStyle = (WallStyle)(((int)Scene.WallStyle + 1) % 3);

        /// <summary>
        /// A left click on the map at a screen point: it picks what the last frame drew there (the picture the player
        /// clicked on), and selects that crawler or the room under the point. Nothing is picked before the first frame.
        /// </summary>
        public void ClickMap(Vec2 screen)
        {
            if (View.Projection == null) return;
            WorldPicker.TryPick(Scene.Items, View, screen, out var crawler);
            _session.Selection.ClickAt(View.ScreenToFloor(screen), crawler);
        }

        /// <summary>
        /// Draws the map into the bound render target. It presents the scene from the state the last update left and
        /// changes none (review A6), so two draws, or a capture at another size, never move a fade or a pose.
        /// </summary>
        public void Draw(int width, int height)
        {
            _graphics.Device.Clear(Background);

            var map = _session.Simulation.Host.Current?.Map;
            View = Rig.ToView(width, height);
            Scene.Present(map, _session.Frames, _session.ViewSeconds, _session.Selection, View); // culled to the view
            if (map == null) return;

            _graphics.Renderer.Draw(Scene.Items, View);
            _labelLayout.Layout(Scene.Labels, View, Fonts, _glyphs);
            _graphics.Renderer.DrawGlyphs(_glyphs, _graphics.FontPages);
        }

        /// <summary>
        /// A newly followed crawler starts the follow afresh, so the camera doesn't carry the last one's motion; the
        /// player following a crawler of their own takes the camera back from a scene.
        /// </summary>
        private void OnSelectionChanged()
        {
            var selection = _session.Selection;
            if (selection.Following && !_wasFollowing)
            {
                Rig.ResetFollow();
                ReleaseCamera();
            }
            _wasFollowing = selection.Following;
        }

        public void Dispose() => _session.Selection.Changed -= OnSelectionChanged;
    }
}
