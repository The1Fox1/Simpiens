using Gum.Wireframe;
using Microsoft.Xna.Framework;
using MonoGameGum;
using Simpiens.Diagnostics;
using ZLogger;

namespace Simpiens.Client.Ui
{
    /// <summary>
    /// The dashboard's layer (Epic CLIENT M2): Gum, drawn over the map. Gum keeps its state in statics, so one game at a
    /// time owns it: the game that initialized it last (tests run many games in one process). A game that no longer
    /// owns it skips its UI.
    /// </summary>
    public sealed class UiLayer
    {
        private static UiLayer _owner;
        private static bool _gumErrorsLogged;
        private readonly Game _game;

        public UiLayer(Game game) => _game = game;

        /// <summary>True while this game's UI is the one Gum shows.</summary>
        public bool IsOwner => ReferenceEquals(_owner, this);

        /// <summary>The whole window. It lets clicks through: only the controls in it take them.</summary>
        public InteractiveGue Root => GumService.Default.Root;

        /// <summary>Starts Gum for this game (taking it over from another game, if one had it).</summary>
        public void Initialize()
        {
            if (GumService.Default.IsInitialized) GumService.Default.Uninitialize();
            GumService.Default.Initialize(_game, Gum.Forms.DefaultVisualsVersion.V3);
            if (!_gumErrorsLogged)
            {
                // A font Gum can't find (or any property it can't apply) is a warning, never a silent fallback
                Gum.Wireframe.CustomSetPropertyOnRenderable.PropertyAssignmentError += message => SimLog.Bootstrap.ZLogWarning($"Gum: {message}");
                _gumErrorsLogged = true;
            }
            UiStyle.Apply();
            _owner = this;
        }

        /// <summary>Lays out the UI for the window and handles the pointer and keyboard (Gum reads them only while the game has focus).</summary>
        public void Update(GameTime gameTime)
        {
            if (IsOwner) GumService.Default.Update(gameTime);
        }

        public void Draw()
        {
            if (IsOwner) GumService.Default.Draw();
        }

        /// <summary>The pointer is over a control or a panel that takes clicks: the map ignores it there.</summary>
        public bool PointerOverUi => IsOwner && (GumService.Default.Cursor.WindowOver != null || GumService.Default.Cursor.WindowPushed != null);

        /// <summary>A control has the keyboard: the camera ignores the keys.</summary>
        public bool HasKeyboardFocus => IsOwner && InteractiveGue.CurrentInputReceiver != null;

        public void Shutdown()
        {
            if (!IsOwner) return;
            GumService.Default.Uninitialize();
            _owner = null;
        }
    }
}
