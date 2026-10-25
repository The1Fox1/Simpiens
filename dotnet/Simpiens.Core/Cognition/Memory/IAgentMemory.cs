using System.Collections.Generic;
using Simpiens.Cognition.Social;

namespace Simpiens.Cognition.Memory
{
    /// <summary>
    /// Read-only abstraction for an agent's spatial and relational knowledge.
    /// Implemented by both the live AgentMemory and immutable AgentMemorySnapshot.
    /// </summary>
    public interface IAgentMemory
    {
        Dictionary<EntityId, SpatialMemoryRecord> SpatialMemoryMap { get; }
        Dictionary<EntityId, AffinityRecord> AffinityMap { get; }

        bool IsBlacklisted(EntityId entityId, uint currentTick);
        AffinityRecord GetAffinity(EntityId peerId, uint currentTick);
        float CalculateSubmissionScore(EntityId peerId, uint currentTick);
        float CalculateAltruismScore(EntityId peerId, uint currentTick, float selfHunger, bool isTargetCollapsed);

        bool IsTrusted(EntityId peerId, uint currentTick, sbyte threshold = 20);
        bool IsDistrusted(EntityId peerId, uint currentTick, sbyte threshold = -20);
        bool IsFeared(EntityId peerId, uint currentTick, sbyte threshold = 40);
        bool IsWarm(EntityId peerId, uint currentTick, sbyte threshold = 20);
        bool IsRespected(EntityId peerId, uint currentTick, sbyte threshold = 30);

        bool TryGetSpatialRecord(EntityId entityId, out SpatialMemoryRecord record);

        /// <summary>Tick this agent last stood in the sector (0 = never), for systematic searching (Epic MAP Phase 3).</summary>
        uint LastSectorVisit(byte sectorId);
    }
}
