using System.Collections.Generic;
using Simpiens.Diagnostics;
using Simpiens.Narrative;
using UnityEngine;
using ZLogger;

namespace Simpiens.Simulation.Economy
{
    /// <summary>
    /// Pure C# implementation of ISupplyService.
    /// Dormant nodes are kept in a list sorted by (ReadyTick, GUID) so regrowth order is deterministic and never
    /// depends on dictionary enumeration. Steady-state ticking allocates nothing.
    /// </summary>
    public class SupplyService : ISupplyService
    {
        public const string CaravanSinkName = "SupplyCaravan";
        public const string ProvisionalExportFaucetName = "ProvisionalCaravanExport";

        private readonly struct DormantEntry
        {
            public readonly GUID ResourceId;
            public readonly uint ReadyTick;

            public DormantEntry(GUID resourceId, uint readyTick)
            {
                ResourceId = resourceId;
                ReadyTick = readyTick;
            }
        }

        private readonly SupplyTuning _tuning;
        private readonly List<DormantEntry> _dormant = new List<DormantEntry>(32);
        private readonly List<TradeStallData> _stallScratch = new List<TradeStallData>(StallScratchCapacity);

        private const int StallScratchCapacity = 8;

        public SupplyTuning Tuning => _tuning;
        public int DormantCount => _dormant.Count;
        public int RegrowthCount { get; private set; }
        public int RestockCount { get; private set; }

        /// <summary>Provisional salvage export visits (see SupplyTuning.ProvisionalOreExportPrice).</summary>
        public int ExportCount { get; private set; }

        public SupplyService() : this(SupplyTuning.Default)
        {
        }

        public SupplyService(SupplyTuning tuning)
        {
            _tuning = tuning;
        }

        public void RegisterDormant(GUID resourceId, uint readyTick)
        {
            // Replace any stale entry for the same node
            for (int i = 0; i < _dormant.Count; i++)
            {
                if (_dormant[i].ResourceId == resourceId)
                {
                    _dormant.RemoveAt(i);
                    break;
                }
            }

            var entry = new DormantEntry(resourceId, readyTick);
            int index = _dormant.Count;
            while (index > 0 && Precedes(entry, _dormant[index - 1]))
            {
                index--;
            }
            _dormant.Insert(index, entry);
        }

        public void Tick(uint currentTick, ISupplyHost host)
        {
            if (host == null) return;

            TickRegrowth(currentTick, host);

            if (_tuning.RestockIntervalTicks > 0 && currentTick > 0 && currentTick % _tuning.RestockIntervalTicks == 0)
            {
                TickCaravan(currentTick, host);
            }
        }

        public void Reset()
        {
            _dormant.Clear();
            _stallScratch.Clear();
            RegrowthCount = 0;
            RestockCount = 0;
            ExportCount = 0;
        }

        private void TickRegrowth(uint currentTick, ISupplyHost host)
        {
            while (_dormant.Count > 0 && _dormant[0].ReadyTick <= currentTick)
            {
                var entry = _dormant[0];
                _dormant.RemoveAt(0);

                if (!host.TryGetResource(entry.ResourceId, out var resource) || resource == null || !resource.IsDormant)
                {
                    continue;
                }

                resource.RemainingYield = resource.MaxYield > 0 ? resource.MaxYield : 1;
                resource.IsDormant = false;
                host.ReactivateResource(entry.ResourceId);
                RegrowthCount++;
                SimLog.Supply.ZLogDebug($"Regrew {SimLog.Agent(entry.ResourceId)} ({resource.YieldItem}) to {resource.RemainingYield} yield; {_dormant.Count} still dormant");

                host.ChronicleService?.RecordEvent(new ChronicleEvent(
                    ChronicleEventType.ResourceRegrown,
                    entry.ResourceId,
                    default,
                    0,
                    currentTick,
                    ChronicleIntensity.Trivial,
                    (byte)resource.YieldItem));
            }
        }

        private void TickCaravan(uint currentTick, ISupplyHost host)
        {
            var stalls = host.ActiveStalls;
            if (stalls == null || stalls.Count == 0) return;

            // Copy first: UpdateStall rebuilds the host's active stall buffer. Sort by id for a stable order.
            _stallScratch.Clear();
            for (int i = 0; i < stalls.Count; i++)
            {
                var stall = stalls[i];
                int index = _stallScratch.Count;
                while (index > 0 && stall.StallEntityId.CompareTo(_stallScratch[index - 1].StallEntityId) < 0)
                {
                    index--;
                }
                _stallScratch.Insert(index, stall);
            }

            for (int i = 0; i < _stallScratch.Count; i++)
            {
                var stall = ExportSurplusSalvage(_stallScratch[i], currentTick, host);
                if (stall.RationStock >= _tuning.RationStockCap) continue;

                uint units = (uint)(_tuning.RationStockCap - stall.RationStock);
                uint cost = units * _tuning.WholesaleRationCost;

                // The caravan only delivers what the stall can pay for in full
                if (stall.TreasuryGold < cost)
                {
                    SimLog.Supply.ZLogWarning($"Supply caravan skipped {SimLog.Agent(stall.StallEntityId)}: treasury {stall.TreasuryGold}g < {cost}g for {units} rations (stock {stall.RationStock})");
                    continue;
                }

                host.UpdateStall(stall.WithTreasury(stall.TreasuryGold - cost).WithRationStock(_tuning.RationStockCap));
                host.EconomyLedger?.RecordSink(cost, CaravanSinkName);
                RestockCount++;
                SimLog.Supply.ZLogInformation($"Supply caravan restocked {SimLog.Agent(stall.StallEntityId)} with {units} rations for {cost}g (treasury now {stall.TreasuryGold - cost}g)");

                host.ChronicleService?.RecordEvent(new ChronicleEvent(
                    ChronicleEventType.SupplyCaravanArrived,
                    stall.StallEntityId,
                    default,
                    0,
                    currentTick,
                    ChronicleIntensity.Trivial,
                    units));
            }
        }

        /// <summary>
        /// PROVISIONAL outlet (see SupplyTuning.ProvisionalOreExportPrice): the caravan buys salvage stock above the
        /// reserve before restocking, so salvage the stall bought from crawlers can flow back out as gold.
        /// </summary>
        private TradeStallData ExportSurplusSalvage(TradeStallData stall, uint currentTick, ISupplyHost host)
        {
            ushort reserve = _tuning.ProvisionalExportReserve;
            uint oreUnits = _tuning.ProvisionalOreExportPrice > 0 && stall.OreStock > reserve ? (uint)(stall.OreStock - reserve) : 0u;
            uint herbUnits = _tuning.ProvisionalHerbExportPrice > 0 && stall.HerbStock > reserve ? (uint)(stall.HerbStock - reserve) : 0u;
            if (oreUnits == 0 && herbUnits == 0) return stall;

            uint revenue = oreUnits * _tuning.ProvisionalOreExportPrice + herbUnits * _tuning.ProvisionalHerbExportPrice;
            var updated = stall
                .WithOreStock((ushort)(stall.OreStock - oreUnits))
                .WithHerbStock((ushort)(stall.HerbStock - herbUnits))
                .WithTreasury(stall.TreasuryGold + revenue);
            host.UpdateStall(updated);
            host.EconomyLedger?.RecordFaucet(revenue, ProvisionalExportFaucetName);
            ExportCount++;
            SimLog.Supply.ZLogInformation($"Supply caravan bought {oreUnits} ore + {herbUnits} herbs from {SimLog.Agent(stall.StallEntityId)} for {revenue}g (provisional export; treasury now {updated.TreasuryGold}g)");
            return updated;
        }

        private static bool Precedes(in DormantEntry a, in DormantEntry b)
        {
            if (a.ReadyTick != b.ReadyTick) return a.ReadyTick < b.ReadyTick;
            return a.ResourceId.CompareTo(b.ResourceId) < 0;
        }
    }
}
