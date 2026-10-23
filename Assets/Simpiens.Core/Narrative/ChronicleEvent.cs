using System;
namespace Simpiens.Narrative
{
    /// <summary>
    /// Categorizes narrative story beats recorded in the facility chronicle.
    /// Distinguishes player-facing story events from internal cognitive memory logs.
    /// </summary>
    public enum ChronicleEventType : byte
    {
        CampfireGossip = 0,
        CampfireCompanionship = 1,
        AltruisticFoodShare = 2,
        ResourceYielded = 3,
        ResourceClaimed = 4,
        DirectiveIssued = 5,
        DirectiveComplied = 6,
        TantrumStarted = 7,
        ExhaustionCollapsed = 8,
        StarvationPanic = 9,
        GroupFormed = 10,
        LeadershipAssumed = 11,
        LeadershipChallenged = 12,
        GroupSchism = 13,
        TradeTransaction = 14,
        BountyPosted = 15,
        BountyClaimed = 16,
        BountyFulfilled = 17,
        BountyExpired = 18,
        SupplyCaravanArrived = 19,
        ResourceRegrown = 20,
        UpkeepUnpaid = 21,
        DrifterArrived = 22,
        DrifterLeft = 23,
        CrawlerDowned = 24,
        CrawlerRecovered = 25
    }

    /// <summary>
    /// Narrative weight of an event for filtering and future LLM enrichment prioritization.
    /// </summary>
    public enum ChronicleIntensity : byte
    {
        Trivial = 0,
        Notable = 1,
        Dramatic = 2,
        Crisis = 3
    }

    /// <summary>
    /// Immutable record representing a humanized story event in the facility.
    /// Engineered as a zero-allocation readonly struct to sit in circular ring buffers.
    /// </summary>
    public readonly struct ChronicleEvent : IEquatable<ChronicleEvent>
    {
        public readonly ulong EventId;
        public readonly ChronicleEventType EventType;
        public readonly EntityId ActorAId;
        public readonly EntityId ActorBId;
        public readonly int SectorId;
        public readonly uint SimulationTick;
        public readonly ChronicleIntensity Intensity;
        public readonly float ExtraData;

        public ChronicleEvent(
            ChronicleEventType eventType,
            EntityId actorAId,
            EntityId actorBId,
            int sectorId,
            uint simulationTick,
            ChronicleIntensity intensity = ChronicleIntensity.Notable,
            float extraData = 0f,
            ulong eventId = 0)
        {
            EventId = eventId;
            EventType = eventType;
            ActorAId = actorAId;
            ActorBId = actorBId;
            SectorId = sectorId;
            SimulationTick = simulationTick;
            Intensity = intensity;
            ExtraData = extraData;
        }

        public ChronicleEvent WithEventId(ulong eventId)
        {
            return new ChronicleEvent(
                EventType,
                ActorAId,
                ActorBId,
                SectorId,
                SimulationTick,
                Intensity,
                ExtraData,
                eventId);
        }

        public bool Equals(ChronicleEvent other)
        {
            return EventId == other.EventId &&
                   EventType == other.EventType &&
                   ActorAId.Equals(other.ActorAId) &&
                   ActorBId.Equals(other.ActorBId) &&
                   SectorId == other.SectorId &&
                   SimulationTick == other.SimulationTick &&
                   Intensity == other.Intensity &&
                   SimMath.Approximately(ExtraData, other.ExtraData);
        }

        public override bool Equals(object obj)
        {
            return obj is ChronicleEvent other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = EventId.GetHashCode();
                hash = (hash * 397) ^ (int)EventType;
                hash = (hash * 397) ^ ActorAId.GetHashCode();
                hash = (hash * 397) ^ ActorBId.GetHashCode();
                hash = (hash * 397) ^ SectorId;
                hash = (hash * 397) ^ (int)SimulationTick;
                hash = (hash * 397) ^ (int)Intensity;
                return hash;
            }
        }
    }
}
