using System.Collections.Generic;
using Simpiens.Simulation.World;
using UnityEngine;

namespace Simpiens.Entities
{
    /// <summary>
    /// Fog of war on the map (Epic MAP Phase 4, producer decision D1 = A): an opaque cloud over each uncharted
    /// sector's interior, under the crawlers and over its props, with an "Uncharted" line under the room's name. The
    /// walls and the name stay visible. When the sector is charted (a scout's successful report), its fog fades out
    /// over <see cref="FadeSeconds"/> of real time and is removed.
    ///
    /// A view: it reads <see cref="ISectorRegistry.ExploredMask"/>, the same charted state cognition sees, and owns
    /// none. It is rebuilt with the map on every session load.
    /// </summary>
    public sealed class ShroudView : MonoBehaviour
    {
        public const float FadeSeconds = 1.5f;
        public const float FogAlpha = 0.97f;
        public const string UnchartedText = "UNCHARTED";

        private static readonly Color FogColor = new Color(0.07f, 0.08f, 0.11f, FogAlpha);
        private static readonly Color UnchartedColor = new Color(0.75f, 0.77f, 0.82f, 1f);
        private static Sprite _cloudSprite;

        private sealed class Overlay
        {
            public int SectorId;
            public SpriteRenderer Fog;
            public TextMesh Label;
            public float FadeElapsed = -1f; // < 0: covering
        }

        private readonly List<Overlay> _overlays = new List<Overlay>(8);
        private ISectorRegistry _sectors;
        private ulong _lastMask;

        /// <summary>Overlays still on the map (covering or fading).</summary>
        public int OverlayCount => _overlays.Count;

        /// <summary>True while the sector's fog is fully in place (not fading).</summary>
        public bool IsCovered(int sectorId)
        {
            var overlay = Find(sectorId);
            return overlay != null && overlay.FadeElapsed < 0f;
        }

        /// <summary>True while any of the sector's fog is still drawn (covering or fading).</summary>
        public bool HasOverlay(int sectorId) => Find(sectorId) != null;

        internal void Initialize(MapDefinition map, ISectorRegistry sectors)
        {
            _sectors = sectors;
            _lastMask = sectors != null ? sectors.ExploredMask : 0UL;
            if (map == null) return;

            for (int i = 0; i < map.Sectors.Count; i++)
            {
                var sector = map.Sectors[i];
                bool charted = sectors != null ? sectors.IsExplored(sector.Id) : sector.StartsExplored;
                if (!charted) _overlays.Add(CreateOverlay(map, sector));
            }
        }

        private void Update()
        {
            if (_sectors == null || _overlays.Count == 0) return;

            ulong mask = _sectors.ExploredMask;
            if (mask != _lastMask)
            {
                _lastMask = mask;
                for (int i = 0; i < _overlays.Count; i++)
                {
                    if (_overlays[i].FadeElapsed < 0f && _sectors.IsExplored(_overlays[i].SectorId)) _overlays[i].FadeElapsed = 0f;
                }
            }

            for (int i = _overlays.Count - 1; i >= 0; i--)
            {
                var overlay = _overlays[i];
                if (overlay.FadeElapsed < 0f) continue;

                overlay.FadeElapsed += Time.unscaledDeltaTime; // real time: the fade reads the same at any sim speed
                float t = Mathf.Clamp01(overlay.FadeElapsed / FadeSeconds);
                if (t >= 1f)
                {
                    if (overlay.Fog != null) Destroy(overlay.Fog.gameObject);
                    if (overlay.Label != null) Destroy(overlay.Label.gameObject);
                    _overlays.RemoveAt(i);
                    continue;
                }
                if (overlay.Fog != null)
                {
                    var c = FogColor;
                    c.a = FogAlpha * (1f - t);
                    overlay.Fog.color = c;
                }
                if (overlay.Label != null)
                {
                    var c = UnchartedColor;
                    c.a = 1f - t;
                    overlay.Label.color = c;
                }
            }
        }

        private Overlay Find(int sectorId)
        {
            for (int i = 0; i < _overlays.Count; i++)
            {
                if (_overlays[i].SectorId == sectorId) return _overlays[i];
            }
            return null;
        }

        private Overlay CreateOverlay(MapDefinition map, in SectorDefinition sector)
        {
            // Sector bounds meet on wall centre lines: inset by the wall's half-thickness so the room's walls show
            float inset = MapDefinitionBuilder.WallHalfExtent;
            var b = sector.Bounds.ToUnity();
            var fogGo = new GameObject($"Fog_{sector.Name}");
            fogGo.transform.SetParent(transform, false);
            fogGo.transform.position = new Vector3(b.center.x, b.center.y, 0f);
            fogGo.transform.localScale = new Vector3(Mathf.Max(0.1f, b.width - 2f * inset), Mathf.Max(0.1f, b.height - 2f * inset), 1f);
            var fog = fogGo.AddComponent<SpriteRenderer>();
            fog.sprite = CloudSprite();
            fog.color = FogColor;
            fog.sortingOrder = RenderOrder.Fog;

            // "Uncharted" under the room's name (the map label inside the sector), or at its centre
            Vector2 at = b.center;
            for (int i = 0; i < map.Labels.Count; i++)
            {
                if (sector.Contains(map.Labels[i].Position)) { at = map.Labels[i].Position.ToUnity() + new Vector2(0f, -0.9f); break; }
            }
            var labelGo = new GameObject($"Uncharted_{sector.Name}");
            labelGo.transform.SetParent(transform, false);
            labelGo.transform.position = new Vector3(at.x, at.y, 0f);
            var label = labelGo.AddComponent<TextMesh>();
            label.text = UnchartedText;
            label.fontSize = 22;
            label.characterSize = 0.08f;
            label.alignment = TextAlignment.Center;
            label.anchor = TextAnchor.MiddleCenter;
            label.color = UnchartedColor;
            var mr = labelGo.GetComponent<MeshRenderer>();
            if (mr != null) mr.sortingOrder = RenderOrder.Labels;

            return new Overlay { SectorId = sector.Id, Fog = fog, Label = label };
        }

        /// <summary>A soft grey cloud (value noise), stretched over each sector: one unit at 32 px.</summary>
        private static Sprite CloudSprite()
        {
            if (_cloudSprite != null) return _cloudSprite;
            const int size = 32;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "Map_Fog",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float n = 0.6f * Noise(x / 8f, y / 8f) + 0.4f * Noise(x / 3f + 17f, y / 3f + 5f);
                    byte v = (byte)(200 + 55 * n);
                    pixels[y * size + x] = new Color32(v, v, v, 255);
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            _cloudSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
            _cloudSprite.name = "Map_Fog";
            return _cloudSprite;
        }

        // Smooth value noise from a fixed integer hash (view only; no simulation randomness)
        private static float Noise(float x, float y)
        {
            int xi = Mathf.FloorToInt(x), yi = Mathf.FloorToInt(y);
            float fx = x - xi, fy = y - yi;
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);
            float a = Hash(xi, yi), b = Hash(xi + 1, yi), c = Hash(xi, yi + 1), d = Hash(xi + 1, yi + 1);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }

        private static float Hash(int x, int y)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263);
                h = (h ^ (h >> 13)) * 1274126177u;
                return (h ^ (h >> 16)) / (float)uint.MaxValue;
            }
        }
    }
}
