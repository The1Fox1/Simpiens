using System.Collections.Generic;
using Simpiens.Entities;
using Simpiens.Simulation;
using UnityEngine;

namespace Simpiens.UI.Zones
{
    /// <summary>
    /// Service interface for querying facility sectors, zone boundaries, and agent occupancy.
    /// Provides zero-allocation spatial grouping over the continuous 2D simulation world.
    /// </summary>
    public interface IFacilityZoneService
    {
        FacilityZone GetZoneForPosition(Vector2 worldPos);
        FacilityZone GetZoneById(int zoneId);
        bool TryGetZone(int zoneId, out FacilityZone zone);
        IReadOnlyList<FacilityZone> GetAllZones();
        int GetOccupancyCount(int zoneId, IWorldRegistry registry);
        void GetAgentsInZone(int zoneId, IWorldRegistry registry, List<WorldEntity> resultsBuffer);
        bool IsSectorExplored(int zoneId);
        void SetSectorExplored(int zoneId, bool isExplored);
        void SetAllSectorsExplored(bool isExplored);
    }
}
