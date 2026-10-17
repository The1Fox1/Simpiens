using System.Collections.Generic;
using Simpiens.Entities;
using Simpiens.Simulation;
using Simpiens.Simulation.Spatial;
using Simpiens.Simulation.World;
using UnityEngine;
using VContainer;

namespace Simpiens.UI.Zones
{
    /// <summary>
    /// UI adapter over the simulation-owned <see cref="ISectorRegistry"/>: presents the loaded map's sectors as
    /// <see cref="FacilityZone"/>s and adds occupancy queries for the dashboard. Holds no sector data of its own.
    /// Zero GC allocations in hot paths (the zone list is rebuilt only when a new map is loaded).
    /// </summary>
    public class FacilityZoneService : IFacilityZoneService
    {
        private readonly ISectorRegistry _sectors;
        private readonly List<FacilityZone> _zones = new List<FacilityZone>(16);
        private MapDefinition _cachedMap;
        private bool _hasCache;

        [Inject]
        public FacilityZoneService(ISectorRegistry sectors = null)
        {
            _sectors = sectors ?? new SectorRegistry();
        }

        public IReadOnlyList<FacilityZone> GetAllZones()
        {
            RefreshZones();
            return _zones;
        }

        public FacilityZone GetZoneById(int zoneId)
        {
            if (_sectors.TryGetSector(zoneId, out var sector)) return ToZone(sector);
            return ToZone(_sectors.Fallback);
        }

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

        public FacilityZone GetZoneForPosition(Vector2 worldPos)
        {
            return ToZone(_sectors.GetSectorAt(worldPos.ToSim()));
        }

        public int GetOccupancyCount(int zoneId, IWorldRegistry registry)
        {
            if (registry == null) return 0;

            var zone = GetZoneById(zoneId);
            var nodes = registry.ActiveNodes;
            int total = nodes.Count;
            int occupancy = 0;

            for (int i = 0; i < total; i++)
            {
                var node = nodes[i];
                if (node != null && node.Type == EntityType.Pawn)
                {
                    if (zone.Contains(node.Position.ToUnity()))
                    {
                        occupancy++;
                    }
                }
            }

            return occupancy;
        }

        public void GetAgentsInZone(int zoneId, IWorldRegistry registry, List<NodeController> resultsBuffer)
        {
            if (resultsBuffer == null) return;
            resultsBuffer.Clear();

            if (registry == null) return;

            var zone = GetZoneById(zoneId);
            var nodes = registry.ActiveNodes;
            int total = nodes.Count;

            for (int i = 0; i < total; i++)
            {
                var node = nodes[i];
                if (node != null && node.Type == EntityType.Pawn)
                {
                    if (zone.Contains(node.Position.ToUnity()))
                    {
                        resultsBuffer.Add(node);
                    }
                }
            }
        }

        public bool IsSectorExplored(int zoneId)
        {
            return _sectors.IsExplored(zoneId);
        }

        public void SetSectorExplored(int zoneId, bool isExplored)
        {
            _sectors.SetExplored(zoneId, isExplored);
        }

        public void SetAllSectorsExplored(bool isExplored)
        {
            _sectors.SetAllExplored(isExplored);
        }

        private FacilityZone ToZone(in SectorDefinition sector)
        {
            return new FacilityZone(sector.Id, sector.Name, sector.Description, sector.Bounds.ToUnity(), sector.Anchor.ToUnity(), sector.AnchorRadius, _sectors.IsExplored(sector.Id));
        }

        private void RefreshZones()
        {
            var map = _sectors.Map;
            if (!_hasCache || !ReferenceEquals(map, _cachedMap))
            {
                _zones.Clear();
                var all = _sectors.All;
                for (int i = 0; i < all.Count; i++)
                {
                    _zones.Add(ToZone(all[i]));
                }
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
