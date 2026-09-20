using UnityEngine;

namespace Simpiens.Cognition.Memory
{
    public enum MemoryEventType : byte
    {
        Attacked,
        ResourceSpotted,
        AgentSpotted,
        GossipReceived,
        GossipShared
    }

    public readonly struct MemoryEvent
    {
        public readonly MemoryEventType EventType;
        public readonly GUID SubjectId;
        public readonly Vector2Int Location;
        public readonly uint SimulationTick;

        public MemoryEvent(MemoryEventType eventType, GUID subjectId, Vector2Int location, uint simulationTick)
        {
            EventType = eventType;
            SubjectId = subjectId;
            Location = location;
            SimulationTick = simulationTick;
        }
    }
}
