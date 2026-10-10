using System.Collections.Generic;
using UnityEngine;
using Simpiens.Simulation.Spatial;
using Simpiens.Cognition.Social;

namespace Simpiens.Cognition.Memory
{
    public class AgentMemory : IAgentMemory
    {
        public const uint DefaultDecayTicks = 1000;
        public const uint ResourceDecayTicks = 1500;
        public const uint PawnDecayTicks = 400;
        public const uint AnchorDecayTicks = 5000;

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
        private readonly Dictionary<GUID, AffinityRecord> _affinityMap;

        private readonly AgentMemorySnapshot[] _snapshots;
        private int _snapshotIndex;

        public Dictionary<GUID, AffinityRecord> AffinityMap => _affinityMap;
        public IReadOnlyDictionary<GUID, uint> BlacklistedEntities => _blacklistedEntities;
        public Dictionary<GUID, uint> BlacklistMap => _blacklistedEntities;

        public AgentMemory(int ledgerCapacity = 50, int initialMapCapacity = 64)
        {
            EventLedger = new MemoryLedger(ledgerCapacity);
            SpatialMemoryMap = new Dictionary<GUID, SpatialMemoryRecord>(initialMapCapacity);
            _blacklistedEntities = new Dictionary<GUID, uint>();
            _lastGossipWithAgent = new Dictionary<GUID, uint>();
            _affinityMap = new Dictionary<GUID, AffinityRecord>(16);

            _snapshots = new AgentMemorySnapshot[2]
            {
                new AgentMemorySnapshot(initialMapCapacity, 16),
                new AgentMemorySnapshot(initialMapCapacity, 16)
            };
            _snapshotIndex = 0;
        }

        public static uint GetDecayThreshold(EntityType type)
        {
            switch (type)
            {
                case EntityType.Pawn: return PawnDecayTicks;
                case EntityType.Resource: return ResourceDecayTicks;
                case EntityType.Anchor: return AnchorDecayTicks;
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

                // Line-of-sight check: agents cannot perceive through walls
                if (snapshot != null && !snapshot.HasLineOfSight(queryPos, entity.Position))
                {
                    continue;
                }

                Vector2Int entityPosInt = new Vector2Int(Mathf.RoundToInt(entity.Position.x), Mathf.RoundToInt(entity.Position.y));

                if (SpatialMemoryMap.TryGetValue(entity.Id, out var record))
                {
                    // Update existing record
                    record.LastKnownLocation = entityPosInt;
                    record.LastSeenTick = currentTick;
                    record.ResourceKind = entity.ResourceKind;
                    SpatialMemoryMap[entity.Id] = record;
                }
                else
                {
                    // Create new record
                    SpatialMemoryMap[entity.Id] = new SpatialMemoryRecord(entity.Id, entity.Type, entityPosInt, currentTick, entity.ResourceKind);
                    
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
                    // If obstructed by a wall, the agent cannot verify whether the entity is gone
                    if (snapshot != null && !snapshot.HasLineOfSight(queryPos, new Vector2(record.LastKnownLocation.x, record.LastKnownLocation.y)))
                    {
                        continue;
                    }

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

        public AffinityRecord GetAffinity(GUID peerId, uint currentTick)
        {
            if (_affinityMap.TryGetValue(peerId, out var record))
            {
                return record.RegressTowardNeutral(currentTick);
            }
            return AffinityRecord.Neutral;
        }

        public IAgentMemory GetSnapshot(uint currentTick)
        {
            _snapshotIndex = 1 - _snapshotIndex;
            var snap = _snapshots[_snapshotIndex];
            snap.PopulateFrom(this, currentTick);
            return snap;
        }

        public bool TryGetSpatialRecord(GUID entityId, out SpatialMemoryRecord record)
        {
            return SpatialMemoryMap.TryGetValue(entityId, out record);
        }

        public void PruneStaleResourceMemories(uint currentTick)
        {
            if (_keysToRemove == null)
            {
                _keysToRemove = new List<GUID>(16);
            }
            _keysToRemove.Clear();

            foreach (var kvp in SpatialMemoryMap)
            {
                if (kvp.Value.Type == EntityType.Resource)
                {
                    _keysToRemove.Add(kvp.Key);
                }
            }

            for (int i = 0; i < _keysToRemove.Count; i++)
            {
                SpatialMemoryMap.Remove(_keysToRemove[i]);
            }
        }

        public void RecordInteraction(GUID peerId, sbyte deltaWarmth, sbyte deltaTrust, sbyte deltaFear, sbyte deltaRespect, uint currentTick)
        {
            var current = GetAffinity(peerId, currentTick);
            _affinityMap[peerId] = current.WithInteraction(deltaWarmth, deltaTrust, deltaFear, deltaRespect, currentTick);
        }

        public void SetAffinity(GUID peerId, AffinityRecord record)
        {
            _affinityMap[peerId] = record;
        }

        public void RecordEvent(MemoryEventType type, GUID subjectId, Vector2Int location, uint simulationTick)
        {
            EventLedger.AddEvent(new MemoryEvent(type, subjectId, location, simulationTick));
        }

        public bool IsTrusted(GUID peerId, uint currentTick, sbyte threshold = 20) => GetAffinity(peerId, currentTick).Trust >= threshold;
        public bool IsDistrusted(GUID peerId, uint currentTick, sbyte threshold = -20) => GetAffinity(peerId, currentTick).Trust <= threshold;
        public bool IsFeared(GUID peerId, uint currentTick, sbyte threshold = 40) => GetAffinity(peerId, currentTick).Fear >= threshold;
        public bool IsWarm(GUID peerId, uint currentTick, sbyte threshold = 20) => GetAffinity(peerId, currentTick).Warmth >= threshold;
        public bool IsRespected(GUID peerId, uint currentTick, sbyte threshold = 30) => GetAffinity(peerId, currentTick).Respect >= threshold;

        /// <summary>
        /// Calculates social submission score toward a peer: Fear + (Respect * 0.8).
        /// High submission indicates willingness to yield contested resources to this peer.
        /// </summary>
        public float CalculateSubmissionScore(GUID peerId, uint currentTick)
        {
            var affinity = GetAffinity(peerId, currentTick);
            return affinity.Fear + (affinity.Respect * 0.8f);
        }

        /// <summary>
        /// Calculates altruism utility score toward a distressed peer:
        /// (Warmth * 0.5) + (Trust * 0.3) - (SelfHunger * 0.4) + (IsTargetCollapsed ? 30 : 0).
        /// Scores >= 25 indicate willingness to share food with this peer.
        /// </summary>
        public float CalculateAltruismScore(GUID peerId, uint currentTick, float selfHunger, bool isTargetCollapsed)
        {
            var affinity = GetAffinity(peerId, currentTick);
            float collapseBonus = isTargetCollapsed ? 30f : 0f;
            return (affinity.Warmth * 0.5f) + (affinity.Trust * 0.3f) - (selfHunger * 0.4f) + collapseBonus;
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

            // Relational pre-filter: Distrusted or terrified agents refuse to converse
            var myAffinity = GetAffinity(peerId, currentTick);
            var peerAffinity = peerMemory.GetAffinity(myId, currentTick);
            if (myAffinity.Trust < -20 || myAffinity.Fear > 60 || peerAffinity.Trust < -20 || peerAffinity.Fear > 60)
            {
                return false;
            }

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

            // Record relational bonding across the 4 axes:
            // Mutual knowledge sharing builds warmth (+5), trust (+10), respect (+5), and relieves fear (-5)
            RecordInteraction(peerId, deltaWarmth: 5, deltaTrust: 10, deltaFear: -5, deltaRespect: 5, currentTick);
            peerMemory.RecordInteraction(myId, deltaWarmth: 5, deltaTrust: 10, deltaFear: -5, deltaRespect: 5, currentTick);

            // Set cooldowns
            _lastGossipWithAgent[peerId] = currentTick;
            peerMemory._lastGossipWithAgent[myId] = currentTick;

            return true;
        }
    }
}
