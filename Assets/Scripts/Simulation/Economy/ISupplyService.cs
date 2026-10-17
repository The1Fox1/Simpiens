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
        bool TryGetResource(EntityId resourceId, out ResourceData resource);

        /// <summary>Re-registers a regrown node with the world so agents can perceive and harvest it again.</summary>
        void ReactivateResource(EntityId resourceId);

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

        /// <summary>Replaces the tuning (the session's EconomyTuning, applied on every respawn).</summary>
        void Configure(in SupplyTuning tuning);

        int DormantCount { get; }

        /// <summary>Cumulative number of nodes regrown since the last Reset.</summary>
        int RegrowthCount { get; }

        /// <summary>Cumulative number of stall restocks since the last Reset.</summary>
        int RestockCount { get; }

        /// <summary>Caravan visits that bought salvage from a stall, since the last reset.</summary>
        int SalvageSaleVisits { get; }

        /// <summary>Visits where a stall paid its upkeep in full, since the last reset.</summary>
        int UpkeepPaidCount { get; }

        /// <summary>Visits where a stall couldn't pay its upkeep in full, since the last reset.</summary>
        int UpkeepShortCount { get; }

        /// <summary>Caravan restocks that delivered fewer rations than the cap (the stall couldn't pay for all), since the last reset.</summary>
        int PartialRestockCount { get; }

        /// <summary>Caravan restocks skipped because the stall couldn't pay, since the last Reset.</summary>
        int SkippedRestockCount { get; }

        /// <summary>Schedules a depleted node to regrow at readyTick.</summary>
        void RegisterDormant(EntityId resourceId, uint readyTick);

        void Tick(uint currentTick, ISupplyHost host);

        void Reset();

        /// <summary>Writes the regrowth queue, in its order, and the counters into a save (Milestone 1 save/load).</summary>
        void CaptureTo(Simpiens.Simulation.Persistence.SupplySave save);

        /// <summary>Replaces the regrowth queue and the counters with a save's (a load).</summary>
        void RestoreFrom(Simpiens.Simulation.Persistence.SupplySave save);
    }
}
