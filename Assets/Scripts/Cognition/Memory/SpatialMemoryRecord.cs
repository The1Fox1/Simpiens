using UnityEngine;
using Simpiens.Simulation.Economy;
using Simpiens.Simulation.Spatial;

namespace Simpiens.Cognition.Memory
{
    public struct SpatialMemoryRecord
    {
        public EntityId EntityId;
        public EntityType Type;
        public Vector2Int LastKnownLocation;
        public uint LastSeenTick;

        /// <summary>Yield item of a remembered resource node (None for non-resources and legacy records).</summary>
        public ItemType ResourceKind;

        public SpatialMemoryRecord(EntityId entityId, EntityType type, Vector2Int lastKnownLocation, uint lastSeenTick)
            : this(entityId, type, lastKnownLocation, lastSeenTick, ItemType.None)
        {
        }

        public SpatialMemoryRecord(EntityId entityId, EntityType type, Vector2Int lastKnownLocation, uint lastSeenTick, ItemType resourceKind)
        {
            ResourceKind = resourceKind;
            EntityId = entityId;
            Type = type;
            LastKnownLocation = lastKnownLocation;
            LastSeenTick = lastSeenTick;
        }
    }
}
