using UnityEngine;
using Simpiens.Simulation.Spatial;

namespace Simpiens.Cognition.Memory
{
    public struct SpatialMemoryRecord
    {
        public GUID EntityId;
        public EntityType Type;
        public Vector2Int LastKnownLocation;
        public uint LastSeenTick;

        public SpatialMemoryRecord(GUID entityId, EntityType type, Vector2Int lastKnownLocation, uint lastSeenTick)
        {
            EntityId = entityId;
            Type = type;
            LastKnownLocation = lastKnownLocation;
            LastSeenTick = lastSeenTick;
        }
    }
}
