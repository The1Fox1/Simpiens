using System.Collections.Generic;
using Simpiens.Narrative;
using UnityEngine;

namespace Simpiens.Simulation.Economy
{
    /// <summary>
    /// The authoritative simulation surface the supply service acts on. Implemented by SimulationManager
    /// so all mutation stays on the simulation thread (single writer).
    /// </summary>
    public interface ISupplyHost
    {
        bool TryGetResource(GUID resourceId, out ResourceData resource);

        /// <summary>Re-registers a regrown node with the world so agents can perceive and harvest it again.</summary>
        void ReactivateResource(GUID resourceId);

        IReadOnlyList<TradeStallData> ActiveStalls { get; }
        void UpdateStall(TradeStallData stall);
        IEconomyLedger EconomyLedger { get; }
        INarrativeChronicleService ChronicleService { get; }
    }

    /// <summary>
    /// Minimal supply faucets for the Milestone 1 loop: dormant resource regrowth and the stall supply caravan.
    /// Plain C#, no Unity API; ticked once per logical tick after intent execution.
    /// </summary>
    public interface ISupplyService
    {
        SupplyTuning Tuning { get; }

        int DormantCount { get; }

        /// <summary>Cumulative number of nodes regrown since the last Reset.</summary>
        int RegrowthCount { get; }

        /// <summary>Cumulative number of stall restocks since the last Reset.</summary>
        int RestockCount { get; }

        /// <summary>Provisional salvage export visits since the last reset.</summary>
        int ExportCount { get; }

        /// <summary>Caravan restocks skipped because the stall couldn't pay, since the last Reset.</summary>
        int SkippedRestockCount { get; }

        /// <summary>Schedules a depleted node to regrow at readyTick.</summary>
        void RegisterDormant(GUID resourceId, uint readyTick);

        void Tick(uint currentTick, ISupplyHost host);

        void Reset();
    }
}
