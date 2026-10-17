using System.Collections.Generic;
using UnityEngine;
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

        IntentResult ExecutePathMovement(NodeController node, PathResponse path, ActiveIntentState state);
        IntentResult ExecutePanicMovement(NodeController node, PathResponse path, ref int currentIndex);

        /// <summary>Moves a pawn only if the move stays clear of static walls and (by default) other pawns; returns false (no move) otherwise.</summary>
        bool TryDisplacePawn(NodeController node, Vector2 candidate, bool respectPawns = true);
        void CheckGossipOpportunity(NodeController node);
        int GetSectorForPosition(Vector2 position);

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
        bool TryClaimBunk(EntityId bunkId, EntityId agentId, UnityEngine.Vector2 agentPosition);

        /// <summary>
        /// Moves the agent one tick toward the bunk's exact position, ignoring pawn clearance (the bunk is free; stepping
        /// onto it is the point). Success once on the bunk's exact position, so the sleeper lies in it; PathBlocked when a
        /// wall is in the way; TargetMissing when unknown or taken by someone else.
        /// </summary>
        IntentResult StepOntoBunk(Simpiens.Entities.NodeController node, EntityId bunkId);

        /// <summary>Frees the bunk if this agent holds it.</summary>
        void ReleaseBunk(EntityId bunkId, EntityId agentId);

        /// <summary>Counts a completed bunk sleep (observed by scenarios).</summary>
        void RecordBunkSleep(EntityId agentId);

        /// <summary>Counts one harvested unit (observed by the economy soak).</summary>
        void RecordHarvest(Simpiens.Simulation.Economy.ItemType item);

        bool TryGetStall(EntityId stallId, out TradeStallData stall);
        void UpdateStall(TradeStallData stall);
        IReadOnlyList<TradeStallData> ActiveStalls { get; }

        bool TryGetBounty(ushort bountyId, out BountyContract bounty);
        void UpdateBounty(BountyContract bounty);
    }
}
