using System.Collections.Generic;
using UnityEngine;
using Simpiens.Simulation.Spatial;

namespace Simpiens.Cognition.Memory
{
    public class AgentMemory
    {
        public MemoryLedger EventLedger { get; }
        public Dictionary<GUID, SpatialMemoryRecord> SpatialMemoryMap { get; }

        [System.ThreadStatic]
        private static List<EntitySnapshot> _queryResults;

        [System.ThreadStatic]
        private static List<GUID> _keysToRemove;

        private readonly Dictionary<GUID, uint> _blacklistedEntities;

        public AgentMemory(int ledgerCapacity = 50, int initialMapCapacity = 64)
        {
            EventLedger = new MemoryLedger(ledgerCapacity);
            SpatialMemoryMap = new Dictionary<GUID, SpatialMemoryRecord>(initialMapCapacity);
            _blacklistedEntities = new Dictionary<GUID, uint>();
        }

        public void BlacklistEntity(GUID entityId, uint untilTick)
        {
            _blacklistedEntities[entityId] = untilTick;
        }

        public bool IsBlacklisted(GUID entityId, uint currentTick)
        {
            if (_blacklistedEntities.TryGetValue(entityId, out var untilTick))
            {
                return currentTick < untilTick;
            }
            return false;
        }

        public void RemoveMemory(GUID entityId)
        {
            SpatialMemoryMap.Remove(entityId);
            _blacklistedEntities.Remove(entityId);
        }

        public void UpdateMemory(SharedWorldSnapshot snapshot, Vector2Int currentPos, int visionRadius, uint currentTick)
        {
            if (_queryResults == null)
            {
                _queryResults = new List<EntitySnapshot>(64);
            }
            _queryResults.Clear();

            Vector2 queryPos = new Vector2(currentPos.x, currentPos.y);
            
            // Query the global snapshot strictly within the agent's visionRadius
            // This populates our thread-local _queryResults without allocating memory
            snapshot.GetEntitiesInRange(queryPos, visionRadius, _queryResults);

            int count = _queryResults.Count;
            for (int i = 0; i < count; i++)
            {
                var entity = _queryResults[i];
                Vector2Int entityPosInt = new Vector2Int(Mathf.RoundToInt(entity.Position.x), Mathf.RoundToInt(entity.Position.y));

                if (SpatialMemoryMap.TryGetValue(entity.Id, out var record))
                {
                    // Update existing record
                    record.LastKnownLocation = entityPosInt;
                    record.LastSeenTick = currentTick;
                    SpatialMemoryMap[entity.Id] = record;
                }
                else
                {
                    // Create new record
                    SpatialMemoryMap[entity.Id] = new SpatialMemoryRecord(entity.Id, entity.Type, entityPosInt, currentTick);
                    
                    // Add event to the ledger for spotting new entities
                    MemoryEventType eventType = entity.Type == EntityType.Resource ? MemoryEventType.ResourceSpotted : MemoryEventType.AgentSpotted;
                    EventLedger.AddEvent(new MemoryEvent(eventType, entity.Id, entityPosInt, currentTick));
                }
            }

            // Prune memory: if we remember something at a location we can currently see, but we didn't just see it, it must be gone.
            if (_keysToRemove == null)
            {
                _keysToRemove = new List<GUID>(16);
            }
            _keysToRemove.Clear();

            foreach (var kvp in SpatialMemoryMap)
            {
                var record = kvp.Value;
                
                // If we just saw it this tick, it's still there
                if (record.LastSeenTick == currentTick) continue;

                // We didn't see it. Can we see its last known location?
                float dist = Vector2.Distance(queryPos, new Vector2(record.LastKnownLocation.x, record.LastKnownLocation.y));
                if (dist <= visionRadius)
                {
                    // The location is in vision, but the entity is not. It has been destroyed or moved away.
                    _keysToRemove.Add(kvp.Key);
                }
            }

            for (int i = 0; i < _keysToRemove.Count; i++)
            {
                SpatialMemoryMap.Remove(_keysToRemove[i]);
            }

            // Clean up expired blacklists
            _keysToRemove.Clear();
            foreach (var kvp in _blacklistedEntities)
            {
                if (currentTick >= kvp.Value)
                {
                    _keysToRemove.Add(kvp.Key);
                }
            }

            for (int i = 0; i < _keysToRemove.Count; i++)
            {
                _blacklistedEntities.Remove(_keysToRemove[i]);
            }
        }
    }
}
