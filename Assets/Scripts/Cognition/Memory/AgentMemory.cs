using System.Collections.Generic;
using UnityEngine;
using Simpiens.Simulation.Spatial;

namespace Simpiens.Cognition.Memory
{
    public class AgentMemory
    {
        public const uint DefaultDecayTicks = 1000;
        public const uint ResourceDecayTicks = 1500;
        public const uint PawnDecayTicks = 400;

        public MemoryLedger EventLedger { get; }
        public Dictionary<GUID, SpatialMemoryRecord> SpatialMemoryMap { get; }

        [System.ThreadStatic]
        private static List<EntitySnapshot> _queryResults;

        [System.ThreadStatic]
        private static List<GUID> _keysToRemove;

        [System.ThreadStatic]
        private static List<MemoryEvent> _gossipEventBuffer;

        private readonly Dictionary<GUID, uint> _blacklistedEntities;
        private readonly Dictionary<GUID, uint> _lastGossipWithAgent;

        public AgentMemory(int ledgerCapacity = 50, int initialMapCapacity = 64)
        {
            EventLedger = new MemoryLedger(ledgerCapacity);
            SpatialMemoryMap = new Dictionary<GUID, SpatialMemoryRecord>(initialMapCapacity);
            _blacklistedEntities = new Dictionary<GUID, uint>();
            _lastGossipWithAgent = new Dictionary<GUID, uint>();
        }

        public static uint GetDecayThreshold(EntityType type)
        {
            switch (type)
            {
                case EntityType.Pawn: return PawnDecayTicks;
                case EntityType.Resource: return ResourceDecayTicks;
                default: return DefaultDecayTicks;
            }
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

                // 1. Line-of-sight pruning: Can we see its last known location?
                float dist = Vector2.Distance(queryPos, new Vector2(record.LastKnownLocation.x, record.LastKnownLocation.y));
                if (dist <= visionRadius)
                {
                    // The location is in vision, but the entity is not. It has been destroyed or moved away.
                    _keysToRemove.Add(kvp.Key);
                    continue;
                }

                // 2. Temporal decay: If unobserved for longer than its retention threshold, it fades.
                uint decayThreshold = GetDecayThreshold(record.Type);
                if (currentTick >= record.LastSeenTick && (currentTick - record.LastSeenTick) > decayThreshold)
                {
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

        public bool CanGossipWith(GUID peerId, uint currentTick, uint cooldownTicks = 300)
        {
            if (_lastGossipWithAgent.TryGetValue(peerId, out var lastTick))
            {
                return currentTick >= lastTick + cooldownTicks;
            }
            return true;
        }

        public bool TryGossip(AgentMemory peerMemory, GUID peerId, GUID myId, uint currentTick, uint cooldownTicks = 300)
        {
            if (peerMemory == null) return false;

            if (!CanGossipWith(peerId, currentTick, cooldownTicks)) return false;

            // Mutual spatial knowledge exchange
            // Share this agent's spatial memory to peer
            foreach (var kvp in SpatialMemoryMap)
            {
                var myRecord = kvp.Value;
                if (peerMemory.IsBlacklisted(myRecord.EntityId, currentTick)) continue;

                if (peerMemory.SpatialMemoryMap.TryGetValue(myRecord.EntityId, out var peerRecord))
                {
                    if (myRecord.LastSeenTick > peerRecord.LastSeenTick)
                    {
                        peerMemory.SpatialMemoryMap[myRecord.EntityId] = myRecord;
                    }
                }
                else
                {
                    peerMemory.SpatialMemoryMap[myRecord.EntityId] = myRecord;
                }
            }

            // Share peer's spatial memory to this agent
            foreach (var kvp in peerMemory.SpatialMemoryMap)
            {
                var peerRecord = kvp.Value;
                if (IsBlacklisted(peerRecord.EntityId, currentTick)) continue;

                if (SpatialMemoryMap.TryGetValue(peerRecord.EntityId, out var myRecord))
                {
                    if (peerRecord.LastSeenTick > myRecord.LastSeenTick)
                    {
                        SpatialMemoryMap[peerRecord.EntityId] = peerRecord;
                    }
                }
                else
                {
                    SpatialMemoryMap[peerRecord.EntityId] = peerRecord;
                }
            }

            // Record gossip events in ledgers
            EventLedger.AddEvent(new MemoryEvent(MemoryEventType.GossipShared, peerId, Vector2Int.zero, currentTick));
            peerMemory.EventLedger.AddEvent(new MemoryEvent(MemoryEventType.GossipReceived, myId, Vector2Int.zero, currentTick));

            // Set cooldowns
            _lastGossipWithAgent[peerId] = currentTick;
            peerMemory._lastGossipWithAgent[myId] = currentTick;

            return true;
        }
    }
}
