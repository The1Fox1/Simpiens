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
        GroupSchism,
        Grieved // Epic 10 Part 3, K5.5: a friend died; the event's other is the dead
    }

    public readonly struct MemoryEvent
    {
        public readonly MemoryEventType EventType;
        public readonly EntityId SubjectId;
        public readonly GridPoint Location;
        public readonly uint SimulationTick;

        public MemoryEvent(MemoryEventType eventType, EntityId subjectId, GridPoint location, uint simulationTick)
        {
            EventType = eventType;
            SubjectId = subjectId;
            Location = location;
            SimulationTick = simulationTick;
        }
    }
}
