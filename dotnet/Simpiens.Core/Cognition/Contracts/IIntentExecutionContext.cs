using System.Collections.Generic;
using Simpiens.Simulation;
using Simpiens.Simulation.Economy;
using Simpiens.Narrative;
using Simpiens.Entities;
using Simpiens.Cognition.Pathfinding;

namespace Simpiens.Cognition.Contracts
{
    /// <summary>
    /// Context passed to IIntentHandler instances to interact with simulation state,
    /// physical movement, registries, and the economy ledger without tight coupling to SimulationManager.
    /// </summary>
    public interface IIntentExecutionContext
    {
        uint CurrentTick { get; }
        float FixedTickDeltaTime { get; }
        EntityId PlayerEntityId { get; }
        IWorldRegistry WorldRegistry { get; }
        INarrativeChronicleService ChronicleService { get; }
        IEconomyLedger EconomyLedger { get; }

        /// <summary>The session's economy tuning (never null; EconomyTuning.Default until a session applies one).</summary>
        EconomyTuning Economy { get; }

        IReadOnlyDictionary<EntityId, ActiveIntentState> ActiveIntents { get; }

        IntentResult ExecutePathMovement(WorldEntity node, PathResponse path, ActiveIntentState state);
        IntentResult ExecutePanicMovement(WorldEntity node, PathResponse path, ref int currentIndex);

        /// <summary>Moves a pawn only if the move stays clear of static walls and (by default) other pawns; returns false (no move) otherwise.</summary>
        bool TryDisplacePawn(WorldEntity node, Vec2 candidate, bool respectPawns = true);
        void CheckGossipOpportunity(WorldEntity node);
        int GetSectorForPosition(Vec2 position);

        /// <summary>The loaded map (immutable): its sectors, lairs and points; null before one is loaded.</summary>
        Simpiens.Simulation.World.MapDefinition Map { get; }

        /// <summary>The creature with <paramref name="id"/>, alive (Epic 10 Part 3, K3.3: a fight's quarry).</summary>
        bool TryGetCreature(EntityId id, out Simpiens.Simulation.Creatures.CreatureState creature);

        /// <summary>A crawler in a fight asks to swing at a creature this tick; the creatures' phase decides it (K3.3, D6).</summary>
        void SwingAtCreature(WorldEntity crawler, EntityId creature);

        /// <summary>Danger for the fallen (Epic 10 Part 3, K5.1): a hazard sector, or the reach of a lair with a living creature.</summary>
        bool InDanger(Vec2 position);

        /// <summary>The living creature of lair <paramref name="lairIndex"/> nearest <paramref name="from"/> (Epic 10 Part 3, K6.2: a hunter's quarry); false when the lair is empty.</summary>
        bool TryGetNearestLairCreature(int lairIndex, Vec2 from, out Simpiens.Simulation.Creatures.CreatureState creature);

        /// <summary>A claim on a hunt joins its party while it has room (Epic 10 Part 3, K6.1); true when the crawler is in it.</summary>
        bool TryJoinHunt(ushort bountyId, EntityId crawler);

        /// <summary>True while the pile lies on the ground (Epic 10 Part 3, K4.3).</summary>
        bool PileExists(EntityId pileId);

        /// <summary>A crawler picks over a pile by the loot rule (K4.3d): the units taken; -1 when the pile is gone or not yet its to take.</summary>
        int TakeFromPile(WorldEntity taker, EntityId pileId);

        bool TryGetResource(EntityId resourceId, out ResourceData resource);
        void RemoveResource(EntityId resourceId);

        /// <summary>
        /// Called when a harvest empties a node. Regrowable nodes (RegrowTicks &gt; 0) go dormant and are handed to
        /// the supply service; others are removed permanently. The simulation owns the node view, never the handler.
        /// </summary>
        void DepleteResource(EntityId resourceId);

        /// <summary>Marks a facility sector explored (e.g. on Scout bounty delivery), lifting its UI shroud.</summary>
        void MarkSectorExplored(int sectorId);

        /// <summary>
        /// Publishes a scout's survey of a sector it just reported on to every agent. Returns the landmark count.
        /// </summary>
        int PublishSurvey(EntityId scoutId, int sectorId);

        /// <summary>Claims a free bunk for an agent standing at it (one sleeper per bunk). Main thread.</summary>
        bool TryClaimBunk(EntityId bunkId, EntityId agentId, Vec2 agentPosition);

        /// <summary>
        /// Moves the agent one tick toward the bunk's exact position, ignoring pawn clearance (the bunk is free; stepping
        /// onto it is the point). Success once on the bunk's exact position, so the sleeper lies in it; PathBlocked when a
        /// wall is in the way; TargetMissing when unknown or taken by someone else.
        /// </summary>
        IntentResult StepOntoBunk(WorldEntity node, EntityId bunkId);

        /// <summary>Frees the bunk if this agent holds it.</summary>
        void ReleaseBunk(EntityId bunkId, EntityId agentId);

        /// <summary>
        /// A crawler walks out by the Road Gate (Epic 10 G1): the gold it carries leaves the economy as the
        /// "DrifterDepartures" sink, a bounty it claimed goes back on the board (its escrow stays), its bunk is freed,
        /// and it is marked as gone; the session host removes it at the end of the tick.
        /// </summary>
        void DepartOutpost(WorldEntity node);

        /// <summary>Counts a completed bunk sleep (observed by scenarios).</summary>
        void RecordBunkSleep(EntityId agentId);

        /// <summary>Counts one harvested unit (observed by the economy soak).</summary>
        void RecordHarvest(Simpiens.Simulation.Economy.ItemType item);

        bool TryGetStall(EntityId stallId, out TradeStallData stall);
        void UpdateStall(TradeStallData stall);
        IReadOnlyList<TradeStallData> ActiveStalls { get; }

        bool TryGetBounty(ushort bountyId, out BountyContract bounty);
        void UpdateBounty(BountyContract bounty);

        /// <summary>One tick of an artisan's work at the station at <paramref name="stationIndex"/> (Epic 10 Part 2, W1.3).</summary>
        Simpiens.Simulation.Workshops.CraftStep WorkStation(int stationIndex);
    }
}
