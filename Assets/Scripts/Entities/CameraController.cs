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
    /// whole map. A left click on a crawler selects and follows it (ring and name); on the floor it selects the room.
    /// Esc or any pan stops following. Mouse input is ignored while the pointer is over UI (the dashboard, the HUD, a
    /// window).
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class CameraController : MonoBehaviour
    {
        /// <summary>One scroll notch zooms in to this fraction of the view (out by its inverse).</summary>
        public const float ZoomStep = 0.85f;

        /// <summary>Keyboard pan speed in view heights per second (so it feels the same at any zoom).</summary>
        public const float KeyPanViewsPerSecond = 0.6f;

        /// <summary>A press that moves further than this (pixels) before release is a drag, not a click.</summary>
        public const float ClickSlopPixels = 6f;

        private const float UiSearchIntervalSeconds = 1f;
        private static readonly Vector2 NameOffset = new Vector2(0f, 0.65f);

        public CameraRig Rig { get; } = new CameraRig();

        /// <summary>The player's selection (set by the map loader); null leaves clicks and follow off.</summary>
        public WorldSelection Selection
        {
            get => _selection;
            set
            {
                if (_selection == value) return;
                if (_selection != null) _selection.Changed -= OnSelectionChanged;
                _selection = value;
                if (_selection != null) _selection.Changed += OnSelectionChanged;
                OnSelectionChanged();
            }
        }

        private WorldSelection _selection;
        private bool _wasFollowing;
        private bool _clickArmed;
        private Vector2 _pressPosition;
        private GameObject _selectionRoot;
        private SpriteRenderer _ring;
        private TextMesh _name;
        private static Sprite _ringSprite;

        private Camera _camera;
        private UIDocument _ui;
        private float _uiSearchCooldown;
        private bool _dragging;
        private Vector2 _lastPointer;

        /// <summary>Adds (or reuses) the controller on the orthographic main camera and frames <paramref name="frame"/>.</summary>
        public static CameraController AttachToMain(Rect frame, WorldSelection selection = null)
        {
            var cam = Camera.main;
            if (cam == null || !cam.orthographic) return null;
            var controller = cam.GetComponent<CameraController>();
            if (controller == null) controller = cam.gameObject.AddComponent<CameraController>();
            controller.Configure(frame);
            if (selection != null) controller.Selection = selection;
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
            Vector2 crawler = default;
            bool hasCrawler = _selection != null && _selection.TryGetCrawlerPosition(out crawler);
            if (hasCrawler && _selection.Following) Rig.Follow(crawler, Time.unscaledDeltaTime);
            Apply();
            UpdateSelectionView(hasCrawler, crawler);
        }

        private void OnDestroy()
        {
            if (_selection != null) _selection.Changed -= OnSelectionChanged;
            if (_selectionRoot != null) Destroy(_selectionRoot);
        }

        private void OnSelectionChanged()
        {
            bool following = _selection != null && _selection.Following;
            if (following && !_wasFollowing) Rig.ResetFollow();
            _wasFollowing = following;
            if (_name != null && _selection != null) _name.text = _selection.CrawlerName();
        }

        /// <summary>A ring under the selected crawler and its name above it.</summary>
        private void UpdateSelectionView(bool visible, Vector2 at)
        {
            if (!visible)
            {
                if (_selectionRoot != null && _selectionRoot.activeSelf) _selectionRoot.SetActive(false);
                return;
            }
            if (_selectionRoot == null) CreateSelectionView();
            if (!_selectionRoot.activeSelf)
            {
                _selectionRoot.SetActive(true);
                _name.text = _selection.CrawlerName();
            }
            _ring.transform.position = new Vector3(at.x, at.y, 0f);
            _name.transform.position = new Vector3(at.x + NameOffset.x, at.y + NameOffset.y, 0f);
        }

        private void CreateSelectionView()
        {
            _selectionRoot = new GameObject("[Selection]");
            var ringGo = new GameObject("Ring");
            ringGo.transform.SetParent(_selectionRoot.transform, false);
            ringGo.transform.localScale = Vector3.one * 0.9f;
            _ring = ringGo.AddComponent<SpriteRenderer>();
            _ring.sprite = RingSprite();
            _ring.sortingOrder = RenderOrder.Selection;

            var nameGo = new GameObject("Name");
            nameGo.transform.SetParent(_selectionRoot.transform, false);
            _name = nameGo.AddComponent<TextMesh>();
            _name.fontSize = 24;
            _name.characterSize = 0.07f;
            _name.alignment = TextAlignment.Center;
            _name.anchor = TextAnchor.LowerCenter;
            _name.color = new Color(1f, 0.92f, 0.55f);
            var mr = nameGo.GetComponent<MeshRenderer>();
            if (mr != null) mr.sortingOrder = RenderOrder.Labels;
        }

        /// <summary>A thin gold circle, one unit across at 16 px.</summary>
        private static Sprite RingSprite()
        {
            if (_ringSprite != null) return _ringSprite;
            const int size = 16;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "Selection_Ring", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[size * size];
            var gold = new Color32(250, 204, 21, 255);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x + 0.5f - size * 0.5f, dy = y + 0.5f - size * 0.5f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    pixels[y * size + x] = d >= 6.2f && d <= 7.6f ? gold : new Color32(0, 0, 0, 0);
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            _ringSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
            _ringSprite.name = "Selection_Ring";
            return _ringSprite;
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

            // A left click (press and release without dragging, starting on the map) selects
            if (mouse.leftButton.wasPressedThisFrame)
            {
                _clickArmed = !overUi;
                _pressPosition = pointer;
            }
            else if (_clickArmed && mouse.leftButton.isPressed && (pointer - _pressPosition).sqrMagnitude > ClickSlopPixels * ClickSlopPixels)
            {
                _clickArmed = false;
            }
            if (mouse.leftButton.wasReleasedThisFrame && _clickArmed)
            {
                _clickArmed = false;
                _selection?.ClickAt(ScreenToWorld(pointer));
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
                var delta = (_lastPointer - pointer) * WorldUnitsPerPixel();
                if (delta != Vector2.zero)
                {
                    _selection?.StopFollowing();
                    Rig.Pan(delta);
                }
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
                _selection?.StopFollowing();
                Rig.Pan(dir.normalized * (KeyPanViewsPerSecond * 2f * Rig.Size * deltaSeconds));
            }
            if (kb.homeKey.wasPressedThisFrame)
            {
                _selection?.StopFollowing();
                Rig.Reframe();
            }
            if (kb.escapeKey.wasPressedThisFrame) _selection?.StopFollowing();
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
