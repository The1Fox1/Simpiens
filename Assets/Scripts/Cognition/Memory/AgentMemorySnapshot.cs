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
        private readonly Dictionary<GUID, SpatialMemoryRecord> _spatialMap;
        private readonly Dictionary<GUID, uint> _blacklistedEntities;
        private readonly Dictionary<GUID, AffinityRecord> _affinityMap;

        public Dictionary<GUID, SpatialMemoryRecord> SpatialMemoryMap => _spatialMap;
        public Dictionary<GUID, AffinityRecord> AffinityMap => _affinityMap;

        public AgentMemorySnapshot(int initialSpatialCapacity = 64, int initialAffinityCapacity = 16)
        {
            _spatialMap = new Dictionary<GUID, SpatialMemoryRecord>(initialSpatialCapacity);
            _blacklistedEntities = new Dictionary<GUID, uint>(16);
            _affinityMap = new Dictionary<GUID, AffinityRecord>(initialAffinityCapacity);
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
        }

        public bool IsBlacklisted(GUID entityId, uint currentTick)
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
        public AffinityRecord GetAffinity(GUID peerId, uint currentTick)
        {
            if (_affinityMap.TryGetValue(peerId, out var record))
            {
                return record.RegressTowardNeutral(currentTick);
            }
            return AffinityRecord.Neutral;
        }

        public float CalculateSubmissionScore(GUID peerId, uint currentTick)
        {
            var affinity = GetAffinity(peerId, currentTick);
            return affinity.Fear + (affinity.Respect * 0.8f);
        }

        public float CalculateAltruismScore(GUID peerId, uint currentTick, float selfHunger, bool isTargetCollapsed)
        {
            var affinity = GetAffinity(peerId, currentTick);
            float collapseBonus = isTargetCollapsed ? 30f : 0f;
            return (affinity.Warmth * 0.5f) + (affinity.Trust * 0.3f) - (selfHunger * 0.4f) + collapseBonus;
        }

        public bool IsTrusted(GUID peerId, uint currentTick, sbyte threshold = 20) => GetAffinity(peerId, currentTick).Trust >= threshold;
        public bool IsDistrusted(GUID peerId, uint currentTick, sbyte threshold = -20) => GetAffinity(peerId, currentTick).Trust <= threshold;
        public bool IsFeared(GUID peerId, uint currentTick, sbyte threshold = 40) => GetAffinity(peerId, currentTick).Fear >= threshold;
        public bool IsWarm(GUID peerId, uint currentTick, sbyte threshold = 20) => GetAffinity(peerId, currentTick).Warmth >= threshold;
        public bool IsRespected(GUID peerId, uint currentTick, sbyte threshold = 30) => GetAffinity(peerId, currentTick).Respect >= threshold;

        public bool TryGetSpatialRecord(GUID entityId, out SpatialMemoryRecord record)
        {
            return _spatialMap.TryGetValue(entityId, out record);
        }
    }
}
