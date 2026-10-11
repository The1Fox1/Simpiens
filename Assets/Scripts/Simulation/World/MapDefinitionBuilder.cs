using System;
using System.Collections.Generic;
using Simpiens.Simulation.Economy;
using UnityEngine;

namespace Simpiens.Simulation.World
{
    /// <summary>
    /// Fluent authoring helper for <see cref="MapDefinition"/>. Build() checks the cheap structural invariants;
    /// connectivity and walkability are checked by the map validators against a rasterized grid.
    /// </summary>
    public sealed class MapDefinitionBuilder
    {
        /// <summary>
        /// Half thickness of a unit wall cell. Matches the legacy wall-node collider radius (0.55), so a wall run
        /// rasterizes to exactly the cells the old per-cell wall entities blocked.
        /// </summary>
        public const float WallHalfExtent = 0.55f;

        private readonly string _name;
        private Rect _playableBounds;
        private Rect _cameraFrame;
        private SectorDefinition _fallback = new SectorDefinition(0, "Transit / Open Yard", "Exterior grounds, open corridors, and transit routes.", new Rect(-50f, -50f, 100f, 100f));
        private readonly List<WallRect> _walls = new List<WallRect>();
        private readonly List<SectorDefinition> _sectors = new List<SectorDefinition>();
        private readonly List<ResourceNodeDefinition> _nodes = new List<ResourceNodeDefinition>();
        private readonly List<PoiDefinition> _pois = new List<PoiDefinition>();
        private readonly List<MapLabel> _labels = new List<MapLabel>();

        public MapDefinitionBuilder(string name, Rect playableBounds)
        {
            _name = name;
            _playableBounds = playableBounds;
            _cameraFrame = playableBounds;
        }

        public MapDefinitionBuilder CameraFrame(Rect frame)
        {
            _cameraFrame = frame;
            return this;
        }

        public MapDefinitionBuilder Fallback(string name, string description, bool startsExplored = true)
        {
            _fallback = new SectorDefinition(0, name, description, _fallback.Bounds, default, 0f, startsExplored);
            return this;
        }

        /// <summary>Solid wall rect in world units.</summary>
        public MapDefinitionBuilder Wall(Rect solid)
        {
            _walls.Add(new WallRect(solid));
            return this;
        }

        /// <summary>
        /// An axis-aligned run of unit wall cells centred from <paramref name="from"/> to <paramref name="to"/> (inclusive).
        /// </summary>
        public MapDefinitionBuilder WallRun(Vector2 from, Vector2 to)
        {
            if (!Mathf.Approximately(from.x, to.x) && !Mathf.Approximately(from.y, to.y))
            {
                throw new ArgumentException($"Wall run {from} -> {to} in map '{_name}' is not axis-aligned.");
            }
            float minX = Mathf.Min(from.x, to.x) - WallHalfExtent;
            float maxX = Mathf.Max(from.x, to.x) + WallHalfExtent;
            float minY = Mathf.Min(from.y, to.y) - WallHalfExtent;
            float maxY = Mathf.Max(from.y, to.y) + WallHalfExtent;
            _walls.Add(new WallRect(Rect.MinMaxRect(minX, minY, maxX, maxY)));
            return this;
        }

        public MapDefinitionBuilder Sector(byte id, string name, string description, Rect bounds, Vector2 anchor = default, float anchorRadius = 0f, bool startsExplored = true)
        {
            _sectors.Add(new SectorDefinition(id, name, description, bounds, anchor, anchorRadius, startsExplored));
            return this;
        }

        public MapDefinitionBuilder Node(string key, Vector2 position, ItemType item, int yield, uint regrowTicks)
        {
            _nodes.Add(new ResourceNodeDefinition(key, position, item, yield, regrowTicks));
            return this;
        }

        public MapDefinitionBuilder Poi(string key, PoiKind kind, Vector2 position, float radius = 0f)
        {
            _pois.Add(new PoiDefinition(key, kind, position, radius));
            return this;
        }

        public MapDefinitionBuilder Label(string text, Vector2 position, Color color)
        {
            _labels.Add(new MapLabel(text, position, color));
            return this;
        }

        public MapDefinition Build()
        {
            if (string.IsNullOrEmpty(_name)) throw new InvalidOperationException("Map definition needs a name.");
            if (_playableBounds.width <= 0f || _playableBounds.height <= 0f)
            {
                throw new InvalidOperationException($"Map '{_name}' has empty playable bounds {_playableBounds}.");
            }

            var ids = new HashSet<byte>();
            for (int i = 0; i < _sectors.Count; i++)
            {
                byte id = _sectors[i].Id;
                if (id == _fallback.Id) throw new InvalidOperationException($"Map '{_name}': sector id {id} is reserved for the fallback sector.");
                if (!ids.Add(id)) throw new InvalidOperationException($"Map '{_name}': duplicate sector id {id}.");
            }

            var keys = new HashSet<string>();
            for (int i = 0; i < _nodes.Count; i++)
            {
                RequireKey(keys, _nodes[i].Key);
                RequireInside(_nodes[i].Position, _nodes[i].Key);
            }
            for (int i = 0; i < _pois.Count; i++)
            {
                RequireKey(keys, _pois[i].Key);
                RequireInside(_pois[i].Position, _pois[i].Key);
            }

            return new MapDefinition(
                _name,
                _playableBounds,
                _cameraFrame,
                _walls.ToArray(),
                _sectors.ToArray(),
                _fallback,
                _nodes.ToArray(),
                _pois.ToArray(),
                _labels.ToArray());
        }

        private void RequireKey(HashSet<string> keys, string key)
        {
            if (string.IsNullOrEmpty(key)) throw new InvalidOperationException($"Map '{_name}': entry without a key.");
            if (!keys.Add(key)) throw new InvalidOperationException($"Map '{_name}': duplicate key '{key}'.");
        }

        private void RequireInside(Vector2 pos, string key)
        {
            if (!_playableBounds.Contains(pos)) throw new InvalidOperationException($"Map '{_name}': '{key}' at {pos} lies outside the playable bounds {_playableBounds}.");
        }
    }
}
