using System.Collections.Generic;
using UnityEngine;
using Simpiens.Cognition.Social;

namespace Simpiens.Cognition.Memory
{
    /// <summary>
    /// A preallocated, zero-allocation read-only snapshot of an agent's memory.
    /// Handed to background cognitive evaluators to isolate them from main-thread mutations.
    /// </summary>
    public class AgentMemorySnapshot : IAgentMemory
    {
        private readonly Dictionary<EntityId, SpatialMemoryRecord> _spatialMap;
        private readonly Dictionary<EntityId, uint> _blacklistedEntities;
        private readonly Dictionary<EntityId, AffinityRecord> _affinityMap;

        public Dictionary<EntityId, SpatialMemoryRecord> SpatialMemoryMap => _spatialMap;
        public Dictionary<EntityId, AffinityRecord> AffinityMap => _affinityMap;

        public AgentMemorySnapshot(int initialSpatialCapacity = 64, int initialAffinityCapacity = 16)
        {
            _spatialMap = new Dictionary<EntityId, SpatialMemoryRecord>(initialSpatialCapacity);
            _blacklistedEntities = new Dictionary<EntityId, uint>(16);
            _affinityMap = new Dictionary<EntityId, AffinityRecord>(initialAffinityCapacity);
        }

        /// <summary>
        /// Populates this snapshot buffer from live AgentMemory.
        /// Retains dictionary capacity to guarantee strictly zero GC heap allocations.
        /// Must be called on the main simulation thread.
        /// </summary>
        public void PopulateFrom(AgentMemory source, uint currentTick)
        {
            _spatialMap.Clear();
            _blacklistedEntities.Clear();
            _affinityMap.Clear();
            System.Array.Clear(_sectorLastVisit, 0, _sectorLastVisit.Length);

            if (source == null) return;

            foreach (var kvp in source.SpatialMemoryMap)
            {
                _spatialMap[kvp.Key] = kvp.Value;
            }

            foreach (var kvp in source.BlacklistMap)
            {
                _blacklistedEntities[kvp.Key] = kvp.Value;
            }

            foreach (var kvp in source.AffinityMap)
            {
                _affinityMap[kvp.Key] = kvp.Value;
            }

            for (int i = 0; i < _sectorLastVisit.Length; i++)
            {
                _sectorLastVisit[i] = source.LastSectorVisit((byte)i);
            }
        }

        private readonly uint[] _sectorLastVisit = new uint[64];

        public uint LastSectorVisit(byte sectorId) => sectorId < _sectorLastVisit.Length ? _sectorLastVisit[sectorId] : 0u;

        public bool IsBlacklisted(EntityId entityId, uint currentTick)
        {
            if (_blacklistedEntities.TryGetValue(entityId, out var untilTick))
            {
                return currentTick < untilTick;
            }
            return false;
        }

        /// <summary>
        /// Side-effect-free affinity calculation. Returns the temporally regressed affinity
        /// without mutating the internal dictionary, making queries safe for concurrent readers.
        /// </summary>
        public AffinityRecord GetAffinity(EntityId peerId, uint currentTick)
        {
            if (_affinityMap.TryGetValue(peerId, out var record))
            {
                return record.RegressTowardNeutral(currentTick);
            }
            return AffinityRecord.Neutral;
        }

        public float CalculateSubmissionScore(EntityId peerId, uint currentTick)
        {
            var affinity = GetAffinity(peerId, currentTick);
            return affinity.Fear + (affinity.Respect * 0.8f);
        }

        public float CalculateAltruismScore(EntityId peerId, uint currentTick, float selfHunger, bool isTargetCollapsed)
        {
            var affinity = GetAffinity(peerId, currentTick);
            float collapseBonus = isTargetCollapsed ? 30f : 0f;
            return (affinity.Warmth * 0.5f) + (affinity.Trust * 0.3f) - (selfHunger * 0.4f) + collapseBonus;
        }

        public bool IsTrusted(EntityId peerId, uint currentTick, sbyte threshold = 20) => GetAffinity(peerId, currentTick).Trust >= threshold;
        public bool IsDistrusted(EntityId peerId, uint currentTick, sbyte threshold = -20) => GetAffinity(peerId, currentTick).Trust <= threshold;
        public bool IsFeared(EntityId peerId, uint currentTick, sbyte threshold = 40) => GetAffinity(peerId, currentTick).Fear >= threshold;
        public bool IsWarm(EntityId peerId, uint currentTick, sbyte threshold = 20) => GetAffinity(peerId, currentTick).Warmth >= threshold;
        public bool IsRespected(EntityId peerId, uint currentTick, sbyte threshold = 30) => GetAffinity(peerId, currentTick).Respect >= threshold;

        public bool TryGetSpatialRecord(EntityId entityId, out SpatialMemoryRecord record)
        {
            return _spatialMap.TryGetValue(entityId, out record);
        }
    }
}
