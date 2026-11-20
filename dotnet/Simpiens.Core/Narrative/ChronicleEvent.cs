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
        CrawlerRecovered = 25,
        GearWornOut = 26, // Epic 10 G3
        GuildJoined = 27, // Epic 10 G4
        GuildUnpaid = 28,
        GuildQuit = 29,
        GuildDismissed = 30,
        ExpeditionSent = 31, // Epic 10 G5: ActorA is the party's first member; the extra is the party's size, or the haul
        ExpeditionTurnedBack = 32,
        ExpeditionReturned = 33,
        ExpeditionDowned = 34,
        StationBuilt = 35, // Epic 10 Part 2: ActorB is the player; the extra is the station's kind
        ArtisanHired = 36, // the extra is the wage; the sector is the station's
        ArtisanUnpaid = 37, // the extra is the station's kind, for this and the next two
        ArtisanQuit = 38,
        ArtisanDismissed = 39,
        ArtisanBrokeDown = 40, // W1.3, D16: overworked on its shift; the extra is the station's kind
        ToolWornOut = 41, // W1.4, D37: a tool used to its end broke; the extra is the item
        CreatureSlain = 42, // Epic 10 Part 3, K3.3: ActorA slew the creature ActorB; the extra is its kind
        CrawlerDied = 43, // K5.4a: ActorA died; ActorB is what downed it (a creature) or none; the extra is its DeathCause x 256 + the cause's detail (the kind)
        CrawlerRescued = 44, // K5.3: ActorA rescued ActorB; the extra is 1 for a carry out, 2 for a medicine given
        NewcomerArrived = 45, // K5.6: ActorA settled at the outpost, 2 days after a resident's death
        SkillBandReached = 46, // K9.2: ActorA's practice reached a band; the extra is the skill x 256 + the band
        GateBreached = 47, // K10.4: ActorA slew the gate's keeper ActorB and the descent gate is breached; the extra is the keeper's kind
        RaiseAsked = 48, // Epic 11, S2.2 (D8): ActorA, a member or artisan, reached a band and asks the player (ActorB) for a raise; the extra is the wage it asks
        FirstLesson = 49, // Epic 11, S3.1 (D9): ActorA was first taught a skill by ActorB, its mentor; the extra is the skill
        TechniqueChosen = 50, // Epic 11, S4.2 (D15): ActorA reached a band and took up a technique by its nature; the extra is the technique
        TechniqueSponsored = 51, // Epic 11, S4.6 (D16): ActorA took up a technique for the patron's (ActorB's) gold; the extra is the technique
        CallingEarned = 52, // Epic 11, S5.3 (D21): ActorA's best skill and its technique there earned it a calling; the extra is the calling
        EpithetEarned = 53 // Epic 11, S6.1 (D27): ActorA earned an epithet; ActorB is the crawler it names, if any; the extra is the epithet x 256 + the kind it names
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
