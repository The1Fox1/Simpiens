using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Simpiens.Entities
{
    /// <summary>
    /// The player's camera on the 2D view (Epic MAP Phase 4): reads the Input System and drives a
    /// <see cref="CameraRig"/>, then applies it to the main camera. View only: it owns no simulation state and runs on
    /// unscaled time, so it works the same paused or at 10x.
    ///
    /// Controls: scroll zooms toward the cursor; right- or middle-drag pans; WASD or the arrows pan; Home reframes the
    /// whole map. Mouse input is ignored while the pointer is over UI (the dashboard, the HUD, a window).
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class CameraController : MonoBehaviour
    {
        /// <summary>One scroll notch zooms in to this fraction of the view (out by its inverse).</summary>
        public const float ZoomStep = 0.85f;

        /// <summary>Keyboard pan speed in view heights per second (so it feels the same at any zoom).</summary>
        public const float KeyPanViewsPerSecond = 0.6f;

        private const float UiSearchIntervalSeconds = 1f;

        public CameraRig Rig { get; } = new CameraRig();

        private Camera _camera;
        private UIDocument _ui;
        private float _uiSearchCooldown;
        private bool _dragging;
        private Vector2 _lastPointer;

        /// <summary>Adds (or reuses) the controller on the orthographic main camera and frames <paramref name="frame"/>.</summary>
        public static CameraController AttachToMain(Rect frame)
        {
            var cam = Camera.main;
            if (cam == null || !cam.orthographic) return null;
            var controller = cam.GetComponent<CameraController>();
            if (controller == null) controller = cam.gameObject.AddComponent<CameraController>();
            controller.Configure(frame);
            return controller;
        }

        public void Configure(Rect frame)
        {
            if (_camera == null) _camera = GetComponent<Camera>();
            Rig.Configure(frame, _camera.aspect);
            Apply();
        }

        private void Update()
        {
            if (_camera == null) return;
            Rig.SetAspect(_camera.aspect);
            HandlePointer();
            HandleKeyboard(Time.unscaledDeltaTime);
        }

        private void LateUpdate()
        {
            if (_camera == null) return;
            Apply();
        }

        private void Apply()
        {
            var p = Rig.Position;
            transform.position = new Vector3(p.x, p.y, transform.position.z);
            _camera.orthographicSize = Rig.Size;
        }

        private void HandlePointer()
        {
            var mouse = Mouse.current;
            if (mouse == null) return;

            Vector2 pointer = mouse.position.ReadValue();
            bool overUi = IsPointerOverUi(pointer);

            float scroll = mouse.scroll.ReadValue().y;
            if (!overUi && scroll != 0f)
            {
                Rig.ZoomAt(ScreenToWorld(pointer), scroll > 0f ? ZoomStep : 1f / ZoomStep);
            }

            bool panHeld = mouse.rightButton.isPressed || mouse.middleButton.isPressed;
            if (!panHeld)
            {
                _dragging = false;
            }
            else if (!_dragging)
            {
                // A drag starts only on the map, never on a button or a window
                _dragging = !overUi && (mouse.rightButton.wasPressedThisFrame || mouse.middleButton.wasPressedThisFrame);
            }
            else
            {
                Rig.Pan((_lastPointer - pointer) * WorldUnitsPerPixel());
            }
            _lastPointer = pointer;
        }

        private void HandleKeyboard(float deltaSeconds)
        {
            var kb = Keyboard.current;
            if (kb == null) return;

            Vector2 dir = Vector2.zero;
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) dir.x -= 1f;
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) dir.x += 1f;
            if (kb.sKey.isPressed || kb.downArrowKey.isPressed) dir.y -= 1f;
            if (kb.wKey.isPressed || kb.upArrowKey.isPressed) dir.y += 1f;
            if (dir != Vector2.zero)
            {
                Rig.Pan(dir.normalized * (KeyPanViewsPerSecond * 2f * Rig.Size * deltaSeconds));
            }
            if (kb.homeKey.wasPressedThisFrame) Rig.Reframe();
        }

        private float WorldUnitsPerPixel()
        {
            float h = _camera.pixelHeight > 0 ? _camera.pixelHeight : Screen.height;
            return h > 0f ? 2f * Rig.Size / h : 0f;
        }

        /// <summary>The world point under a screen position (pixels, origin bottom-left).</summary>
        public Vector2 ScreenToWorld(Vector2 screen)
        {
            float w = _camera.pixelWidth > 0 ? _camera.pixelWidth : Screen.width;
            float h = _camera.pixelHeight > 0 ? _camera.pixelHeight : Screen.height;
            return Rig.ViewportToWorld(new Vector2(w > 0f ? screen.x / w : 0.5f, h > 0f ? screen.y / h : 0.5f));
        }

        /// <summary>
        /// True when a pickable UI element is under the pointer. The document's own root and template container fill
        /// the screen without drawing anything, so they don't count.
        /// </summary>
        public bool IsPointerOverUi(Vector2 screen)
        {
            if (_ui == null)
            {
                _uiSearchCooldown -= Time.unscaledDeltaTime;
                if (_uiSearchCooldown > 0f) return false;
                _uiSearchCooldown = UiSearchIntervalSeconds;
                _ui = FindFirstObjectByType<UIDocument>();
                if (_ui == null) return false;
            }
            var root = _ui.rootVisualElement;
            var panel = root?.panel;
            if (panel == null) return false;

            var panelPos = RuntimePanelUtils.ScreenToPanel(panel, new Vector2(screen.x, Screen.height - screen.y));
            var picked = panel.Pick(panelPos);
            return picked != null && picked != root && !(picked is TemplateContainer);
        }
    }
}
