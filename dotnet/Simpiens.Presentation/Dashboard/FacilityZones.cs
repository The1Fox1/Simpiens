using System.Collections.Generic;
using Simpiens.Simulation;
using Simpiens.Simulation.Spatial;
using Simpiens.Simulation.World;

namespace Simpiens.Presentation.Dashboard
{
    /// <summary>A sector as the dashboard shows it (Unity's <c>FacilityZone</c>).</summary>
    public readonly struct FacilityZone
    {
        public readonly int ZoneId;
        public readonly string Name;
        public readonly string Description;
        public readonly RectF Bounds;
        public readonly Vec2 AnchorPosition;
        public readonly float AnchorRadius;
        public readonly bool IsExplored;

        public FacilityZone(int zoneId, string name, string description, RectF bounds, Vec2 anchorPosition = default, float anchorRadius = 0f, bool isExplored = true)
        {
            ZoneId = zoneId;
            Name = name;
            Description = description;
            Bounds = bounds;
            AnchorPosition = anchorPosition;
            AnchorRadius = anchorRadius;
            IsExplored = isExplored;
        }

        public FacilityZone WithExplored(bool explored) => new FacilityZone(ZoneId, Name, Description, Bounds, AnchorPosition, AnchorRadius, explored);

        public bool Contains(Vec2 position)
        {
            if (AnchorRadius > 0f && Vec2.Distance(position, AnchorPosition) <= AnchorRadius) return true;
            return Bounds.Contains(position);
        }
    }

    /// <summary>
    /// The loaded map's sectors for the dashboard, with occupancy queries (Epic CLIENT M2.1, a port of Unity's
    /// <c>FacilityZoneService</c>). An adapter over the simulation's <see cref="ISectorRegistry"/>: it holds no sector
    /// data and writes nothing. The queries allocate nothing; the zone list is rebuilt only when a new map loads.
    /// </summary>
    public sealed class FacilityZoneService
    {
        private readonly ISectorRegistry _sectors;
        private readonly List<FacilityZone> _zones = new List<FacilityZone>(16);
        private MapDefinition _cachedMap;
        private bool _hasCache;

        public FacilityZoneService(ISectorRegistry sectors = null) => _sectors = sectors ?? new SectorRegistry();

        public IReadOnlyList<FacilityZone> GetAllZones()
        {
            RefreshZones();
            return _zones;
        }

        /// <summary>The sector, or the map's fallback (transit, id 0) for an id the map doesn't have.</summary>
        public FacilityZone GetZoneById(int zoneId) => _sectors.TryGetSector(zoneId, out var sector) ? ToZone(sector) : ToZone(_sectors.Fallback);

        public bool TryGetZone(int zoneId, out FacilityZone zone)
        {
            if (_sectors.TryGetSector(zoneId, out var sector))
            {
                zone = ToZone(sector);
                return true;
            }
            zone = default;
            return false;
        }

        public FacilityZone GetZoneForPosition(Vec2 position) => ToZone(_sectors.GetSectorAt(position));

        public int GetOccupancyCount(int zoneId, IWorldRegistry registry)
        {
            if (registry == null) return 0;
            var zone = GetZoneById(zoneId);
            var nodes = registry.ActiveEntities;
            int occupancy = 0;
            for (int i = 0; i < nodes.Count; i++)
            {
                var node = nodes[i];
                if (node != null && node.Type == EntityType.Pawn && zone.Contains(node.Position)) occupancy++;
            }
            return occupancy;
        }

        public void GetAgentsInZone(int zoneId, IWorldRegistry registry, List<WorldEntity> results)
        {
            if (results == null) return;
            results.Clear();
            if (registry == null) return;
            var zone = GetZoneById(zoneId);
            var nodes = registry.ActiveEntities;
            for (int i = 0; i < nodes.Count; i++)
            {
                var node = nodes[i];
                if (node != null && node.Type == EntityType.Pawn && zone.Contains(node.Position)) results.Add(node);
            }
        }

        public bool IsSectorExplored(int zoneId) => _sectors.IsExplored(zoneId);

        private FacilityZone ToZone(in SectorDefinition sector)
            => new FacilityZone(sector.Id, sector.Name, sector.Description, sector.Bounds, sector.Anchor, sector.AnchorRadius, _sectors.IsExplored(sector.Id));

        private void RefreshZones()
        {
            var map = _sectors.Map;
            if (!_hasCache || !ReferenceEquals(map, _cachedMap))
            {
                _zones.Clear();
                var all = _sectors.All;
                for (int i = 0; i < all.Count; i++) _zones.Add(ToZone(all[i]));
                _cachedMap = map;
                _hasCache = true;
                return;
            }

            // Same map: only exploration can have changed
            for (int i = 0; i < _zones.Count; i++)
            {
                bool explored = _sectors.IsExplored(_zones[i].ZoneId);
                if (explored != _zones[i].IsExplored) _zones[i] = _zones[i].WithExplored(explored);
            }
        }
    }
}
