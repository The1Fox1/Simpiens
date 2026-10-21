using System;
using System.Collections.Generic;
using Simpiens.Simulation.World;

namespace Simpiens.Presentation
{
    /// <summary>
    /// Fog of war on the map (Epic CLIENT M1.3): a port of Unity's <c>ShroudView</c>, rule for rule.
    /// <list type="bullet">
    /// <item><description>Each uncharted sector's interior is covered by an opaque cloud, under the crawlers and over its props (producer decision D1 = A). "UNCHARTED" sits under the room's name. The walls and the name stay visible.</description></item>
    /// <item><description>When a scout's report charts the sector, its fog fades out over <see cref="FadeSeconds"/> of view time and is removed.</description></item>
    /// </list>
    /// A view: it reads which sectors a frame captured as charted (<see cref="WorldViewFrame.IsCharted(int)"/>), the same
    /// state cognition sees, and owns none.
    /// </summary>
    public sealed class FogOfWar
    {
        public const float FadeSeconds = 1.5f;
        public const float FogAlpha = 0.97f;
        public const string UnchartedText = "UNCHARTED";

        private static readonly Rgba FogColor = new Rgba(0.07f, 0.08f, 0.11f, FogAlpha);
        private static readonly Rgba UnchartedColor = new Rgba(0.75f, 0.77f, 0.82f, 1f);

        /// <summary>One sector's fog: covering (<see cref="FadeStart"/> unset) or fading since <see cref="FadeStart"/>.</summary>
        public sealed class Overlay
        {
            public int SectorId { get; internal set; }
            /// <summary>The cloud's rectangle: the sector's bounds inset by the walls' half-thickness.</summary>
            public RectF Area { get; internal set; }
            /// <summary>Where "UNCHARTED" hangs: on the room's name (the layout puts it on the next line), or the sector's centre.</summary>
            public Vec2 LabelAt { get; internal set; }
            /// <summary>The view time the fade began, or null while the sector is uncharted.</summary>
            public float? FadeStart { get; internal set; }
        }

        private readonly List<Overlay> _overlays = new List<Overlay>(8);
        private MapDefinition _map;
        private ulong _lastMask;

        /// <summary>Overlays still on the map (covering or fading).</summary>
        public IReadOnlyList<Overlay> Overlays => _overlays;

        /// <summary>True while the sector's fog is fully in place (not fading).</summary>
        public bool IsCovered(int sectorId)
        {
            var overlay = Find(sectorId);
            return overlay != null && overlay.FadeStart == null;
        }

        /// <summary>True while any of the sector's fog is on the map, fading included (Unity's <c>ShroudView.HasOverlay</c>).</summary>
        public bool HasOverlay(int sectorId) => Find(sectorId) != null;

        /// <summary>Rebuilds the overlays for a newly loaded map: every sector the frame doesn't show charted is covered.</summary>
        public void Load(MapDefinition map, WorldViewFrame frame)
        {
            _overlays.Clear();
            _map = map;
            _lastMask = frame != null ? frame.ChartedMask : 0UL;
            if (map == null) return;
            for (int i = 0; i < map.Sectors.Count; i++)
            {
                var sector = map.Sectors[i];
                bool charted = frame != null ? frame.IsCharted(sector) : sector.StartsExplored;
                if (!charted) _overlays.Add(CreateOverlay(map, sector));
            }
        }

        /// <summary>
        /// Brings the fog up to date: a newly charted sector starts to fade, a finished fade is removed. Loads the map
        /// first if it changed (a respawn, a session switch, a load).
        /// </summary>
        public void Update(MapDefinition map, WorldViewFrame frame, float viewSeconds)
        {
            if (!ReferenceEquals(map, _map)) Load(map, frame);
            if (frame == null || _overlays.Count == 0) return;

            ulong mask = frame.ChartedMask;
            if (mask != _lastMask)
            {
                // A sector can only be un-charted by a respawn or a load, which reload the map; this is a scout's report
                _lastMask = mask;
                for (int i = 0; i < _overlays.Count; i++)
                {
                    if (_overlays[i].FadeStart == null && frame.IsCharted(_overlays[i].SectorId)) _overlays[i].FadeStart = viewSeconds;
                }
            }

            for (int i = _overlays.Count - 1; i >= 0; i--)
            {
                var start = _overlays[i].FadeStart;
                if (start != null && viewSeconds - start.Value >= FadeSeconds) _overlays.RemoveAt(i);
            }
        }

        /// <summary>Adds the clouds to <paramref name="list"/>, in the layer and at the art scale of <paramref name="projection"/>.</summary>
        public void Draw(DrawList list, float viewSeconds, IViewProjection projection)
        {
            int layer = projection.LayerOf(DrawRole.Fog);
            float artPixelsPerUnit = projection.ArtPixelsPerUnit;
            for (int i = 0; i < _overlays.Count; i++)
            {
                var overlay = _overlays[i];
                float t = overlay.FadeStart == null ? 0f : SimMath.Clamp01((viewSeconds - overlay.FadeStart.Value) / FadeSeconds);
                var area = overlay.Area;
                var fog = new Rgba(FogColor.R, FogColor.G, FogColor.B, FogAlpha * (1f - t));
                list.Add(layer, DrawRole.Fog, 0f, FogCloud.Sprite.Index, area.Center.X, area.Center.Y, area.Width * artPixelsPerUnit, area.Height * artPixelsPerUnit, Rgba32.From(fog), 0f, DrawSheet.Fog);
            }
        }

        /// <summary>Adds each overlay's "UNCHARTED", fading with its cloud, under the room's name.</summary>
        public void AddLabels(List<WorldLabel> labels, float viewSeconds)
        {
            for (int i = 0; i < _overlays.Count; i++)
            {
                var overlay = _overlays[i];
                float t = overlay.FadeStart == null ? 0f : SimMath.Clamp01((viewSeconds - overlay.FadeStart.Value) / FadeSeconds);
                labels.Add(new WorldLabel(UnchartedText, overlay.LabelAt, LabelKind.Uncharted, Rgba32.From(new Rgba(UnchartedColor.R, UnchartedColor.G, UnchartedColor.B, 1f - t))));
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

        private static Overlay CreateOverlay(MapDefinition map, in SectorDefinition sector)
        {
            // Sector bounds meet on wall centre lines: inset by the wall's half-thickness so the room's walls show
            float inset = MapDefinitionBuilder.WallHalfExtent;
            var b = sector.Bounds;
            var area = new RectF(b.Center.X - SimMath.Max(0.1f, b.Width - 2f * inset) * 0.5f, b.Center.Y - SimMath.Max(0.1f, b.Height - 2f * inset) * 0.5f,
                SimMath.Max(0.1f, b.Width - 2f * inset), SimMath.Max(0.1f, b.Height - 2f * inset));

            // "UNCHARTED" under the room's name (the map label inside the sector), or at its centre
            var at = b.Center;
            for (int i = 0; i < map.Labels.Count; i++)
            {
                if (sector.Contains(map.Labels[i].Position)) { at = map.Labels[i].Position; break; }
            }
            return new Overlay { SectorId = sector.Id, Area = area, LabelAt = at };
        }
    }

    /// <summary>
    /// The fog's soft grey cloud: <c>ShroudView</c>'s 32x32 value noise, stretched over each sector and sampled smoothly
    /// (its own sheet). Tinted dark when drawn.
    /// </summary>
    public static class FogCloud
    {
        public const int Size = 32;

        public static PixelSprite Sprite { get; } = Build();

        private static PixelSprite Build()
        {
            var pixels = new Rgba32[Size * Size];
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    float n = 0.6f * Noise(x / 8f, y / 8f) + 0.4f * Noise(x / 3f + 17f, y / 3f + 5f);
                    byte v = (byte)(200 + 55 * n);
                    // Unity filled rows from the bottom; rows here are from the top
                    pixels[(Size - 1 - y) * Size + x] = new Rgba32(v, v, v, 255);
                }
            }
            return new PixelSprite(0, "Map_Fog", Size, Size, pixels);
        }

        // Smooth value noise from a fixed integer hash (view only; no simulation randomness)
        private static float Noise(float x, float y)
        {
            int xi = SimMath.FloorToInt(x), yi = SimMath.FloorToInt(y);
            float fx = x - xi, fy = y - yi;
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);
            float a = Hash(xi, yi), b = Hash(xi + 1, yi), c = Hash(xi, yi + 1), d = Hash(xi + 1, yi + 1);
            return Lerp(Lerp(a, b, fx), Lerp(c, d, fx), fy);
        }

        private static float Lerp(float a, float b, float t) => a + (b - a) * t;

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
