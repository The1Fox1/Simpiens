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

        public AgentMemory(int ledgerCapacity = 50, int initialMapCapacity = 64)
        {
            EventLedger = new MemoryLedger(ledgerCapacity);
            SpatialMemoryMap = new Dictionary<GUID, SpatialMemoryRecord>(initialMapCapacity);
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
        }
    }
}
