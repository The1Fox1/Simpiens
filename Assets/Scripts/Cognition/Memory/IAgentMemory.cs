using System.Collections.Generic;
using UnityEngine;
using Simpiens.Cognition.Social;

namespace Simpiens.Cognition.Memory
{
    /// <summary>
    /// Read-only abstraction for an agent's spatial and relational knowledge.
    /// Implemented by both the live AgentMemory and immutable AgentMemorySnapshot.
    /// </summary>
    public interface IAgentMemory
    {
        Dictionary<GUID, SpatialMemoryRecord> SpatialMemoryMap { get; }
        Dictionary<GUID, AffinityRecord> AffinityMap { get; }

        bool IsBlacklisted(GUID entityId, uint currentTick);
        AffinityRecord GetAffinity(GUID peerId, uint currentTick);
        float CalculateSubmissionScore(GUID peerId, uint currentTick);
        float CalculateAltruismScore(GUID peerId, uint currentTick, float selfHunger, bool isTargetCollapsed);

        bool IsTrusted(GUID peerId, uint currentTick, sbyte threshold = 20);
        bool IsDistrusted(GUID peerId, uint currentTick, sbyte threshold = -20);
        bool IsFeared(GUID peerId, uint currentTick, sbyte threshold = 40);
        bool IsWarm(GUID peerId, uint currentTick, sbyte threshold = 20);
        bool IsRespected(GUID peerId, uint currentTick, sbyte threshold = 30);

        bool TryGetSpatialRecord(GUID entityId, out SpatialMemoryRecord record);

        /// <summary>Tick this agent last stood in the sector (0 = never), for systematic searching (Epic MAP Phase 3).</summary>
        uint LastSectorVisit(byte sectorId);
    }
}
