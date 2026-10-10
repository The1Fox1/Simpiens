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
        GUID PlayerEntityId { get; }
        IWorldRegistry WorldRegistry { get; }
        INarrativeChronicleService ChronicleService { get; }
        IEconomyLedger EconomyLedger { get; }

        IReadOnlyDictionary<GUID, ActiveIntentState> ActiveIntents { get; }

        IntentResult ExecutePathMovement(NodeController node, PathResponse path, ActiveIntentState state);
        IntentResult ExecutePanicMovement(NodeController node, PathResponse path, ref int currentIndex);

        /// <summary>Moves a pawn only if the move stays clear of static walls and (by default) other pawns; returns false (no move) otherwise.</summary>
        bool TryDisplacePawn(NodeController node, Vector2 candidate, bool respectPawns = true);
        void CheckGossipOpportunity(NodeController node);
        int GetSectorForPosition(Vector2 position);

        bool TryGetResource(GUID resourceId, out ResourceData resource);
        void RemoveResource(GUID resourceId);

        /// <summary>
        /// Called when a harvest empties a node. Regrowable nodes (RegrowTicks &gt; 0) go dormant and are handed to
        /// the supply service; others are removed permanently. The simulation owns the node view, never the handler.
        /// </summary>
        void DepleteResource(GUID resourceId);

        /// <summary>Marks a facility sector explored (e.g. on Scout bounty delivery), lifting its UI shroud.</summary>
        void MarkSectorExplored(int sectorId);

        bool TryGetStall(GUID stallId, out TradeStallData stall);
        void UpdateStall(TradeStallData stall);
        IReadOnlyList<TradeStallData> ActiveStalls { get; }

        bool TryGetBounty(ushort bountyId, out BountyContract bounty);
        void UpdateBounty(BountyContract bounty);
    }
}
