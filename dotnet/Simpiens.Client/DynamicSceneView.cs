using Microsoft.Xna.Framework;
using Simpiens.Client.Ui;
using Simpiens.Presentation;
using Simpiens.Presentation.Cutscenes;

namespace Simpiens.Client
{
    /// <summary>
    /// Dynamic scenes over the live world (M3 design D9, producers P3, P7, P8): the scene the director started plays on
    /// view time while the world runs. Its lines show in a speech bubble over the speaking crawler's head, following it as
    /// it moves; while the speaker is hidden by the fog or off screen, they show in a box at the bottom instead, and a
    /// click on that box brings the camera back to the speaker. The scene's focus sends the camera to an actor until the
    /// player takes it back. Space or Enter hurries a line. It reads frames and writes nothing to the simulation.
    /// </summary>
    public sealed class DynamicSceneView
    {
        public const float BubbleWidth = 300f, BubbleHeight = 74f;
        public const float BoxHeight = 84f, Margin = 16f;

        private readonly SimpiensGame _game;
        private DynamicScene _scene;
        private EntityId _speaker;

        public DynamicSceneView(SimpiensGame game)
        {
            _game = game;
            Bubble = new CutsceneTextBox("scene-bubble");
            Box = new CutsceneTextBox("scene-box");
            Box.Root.HasEvents = true; // it takes clicks: one brings the camera back to the speaker
            Box.Root.Click += (_, _) => { if (_speaker != default) _game.World.DirectCamera(_speaker); };
        }

        public CutsceneTextBox Bubble { get; }
        public CutsceneTextBox Box { get; }

        /// <summary>The scene playing, or null.</summary>
        public DynamicScene Scene => _scene;

        /// <summary>The crawler speaking now, or default.</summary>
        public EntityId Speaker => _speaker;

        public void LoadContent()
        {
            _game.Ui.Root.Children.Add(Bubble.Root);
            _game.Ui.Root.Children.Add(Box.Root);
        }

        /// <summary>After the world view's update: the scene moves on, the camera follows its focus, the line is placed.</summary>
        public void Update(float seconds, float cameraSeconds, KeyInput keys)
        {
            var director = _game.Session.Scenes;
            var scene = director.Current;
            if (!ReferenceEquals(scene, _scene)) Begin(scene);
            if (scene == null) return;

            var player = scene.Player;
            if (keys.AdvancePressed) player.Advance();
            // Live, real time (text reads the same at 10x); with the tick-based view clock (tests), the view's own step
            player.Update(_game.Options.ViewClockFromTicks ? cameraSeconds : seconds);
            if (player.Finished)
            {
                director.End();
                Begin(null);
                return;
            }
            Place(scene);
        }

        private void Begin(DynamicScene scene)
        {
            if (_scene != null)
            {
                _scene.Player.Focused -= OnFocused;
                _game.World.ReleaseCamera();
            }
            _scene = scene;
            _speaker = default;
            Bubble.Hide();
            Box.Hide();
            if (scene == null) return;
            scene.Player.Focused += OnFocused;
            if (scene.Player.Focus != null) OnFocused(scene.Player.Focus); // a script that opens on a focus
        }

        private void OnFocused(string role)
        {
            var crawler = _scene.ActorOf(role);
            if (crawler != default) _game.World.DirectCamera(crawler);
        }

        /// <summary>Over the speaker's head when it is drawn on screen, else at the bottom of the window.</summary>
        private void Place(DynamicScene scene)
        {
            var player = scene.Player;
            if (player.Line == null)
            {
                _speaker = default;
                Bubble.Hide();
                Box.Hide();
                return;
            }
            _speaker = scene.ActorOf(player.Speaker);
            string name = _game.Session.Identity?.GetFriendlyName(_speaker) ?? player.Speaker;
            var viewport = _game.GraphicsDevice.Viewport;
            if (TryGetHead(_speaker, viewport.Width, viewport.Height, out var head))
            {
                float x = MathHelper.Clamp(head.X - BubbleWidth * 0.5f, Margin, viewport.Width - Margin - BubbleWidth);
                float y = MathHelper.Clamp(head.Y - BubbleHeight, Margin, viewport.Height - Margin - BubbleHeight);
                Bubble.Show(x, y, BubbleWidth, BubbleHeight, name, player.Line, player.LettersShown);
                Box.Hide();
            }
            else
            {
                Box.Show(Margin, viewport.Height - Margin - BoxHeight, viewport.Width - 2f * Margin, BoxHeight, name, player.Line, player.LettersShown);
                Bubble.Hide();
            }
        }

        /// <summary>
        /// The top of the speaker's head on screen, in the view this frame will draw (the camera has moved already): false
        /// when it isn't drawn (fog), isn't in the world, or is off screen.
        /// </summary>
        private bool TryGetHead(EntityId crawler, int width, int height, out Vec2 head)
        {
            head = default;
            var world = _game.World;
            if (!_game.Session.Frames.TryGetDrawnPosition(crawler, out var at)) return false;
            if (world.Scene.Fog.Visibility(at, _game.Session.ViewSeconds) <= 0f) return false;
            head = world.Rig.ToView(width, height).WorldToScreen(at, world.Mode.Art.NameHeight + 0.25f);
            return head.X >= 0f && head.X < width && head.Y >= BubbleHeight * 0.5f && head.Y < height;
        }
    }
}
