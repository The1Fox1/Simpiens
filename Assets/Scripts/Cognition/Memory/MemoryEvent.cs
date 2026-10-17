using UnityEngine;

namespace Simpiens.Cognition.Memory
{
    public enum MemoryEventType : byte
    {
        Attacked,
        ResourceSpotted,
        AgentSpotted,
        GossipReceived,
        GossipShared,
        ResourceYielded,
        ResourceClaimed,
        ContentionFaced,
        AidGiven,
        AidReceived,
        CampfireCompanionship,
        JoinedGroup,
        LeftGroup,
        Promoted,
        Demoted,
        DirectiveReceived,
        DirectiveComplied,
        SubordinateDefied,
        DesertedGroup,
        GroupFormed,
        LeadershipAssumed,
        DominanceChallenged,
        GroupSchism
    }

    public readonly struct MemoryEvent
    {
        public readonly MemoryEventType EventType;
        public readonly EntityId SubjectId;
        public readonly Vector2Int Location;
        public readonly uint SimulationTick;

        public MemoryEvent(MemoryEventType eventType, EntityId subjectId, Vector2Int location, uint simulationTick)
        {
            EventType = eventType;
            SubjectId = subjectId;
            Location = location;
            SimulationTick = simulationTick;
        }
    }
}
