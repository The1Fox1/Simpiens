using System.Collections.Generic;
using Simpiens.Diagnostics;
using Simpiens.Narrative;
using Simpiens.Simulation.Persistence;
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
        public const string SalvageSalesFaucetName = "CaravanSalvageSales";
        public const string UpkeepSinkName = "OutpostUpkeep";

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

        private SupplyTuning _tuning;
        private readonly List<DormantEntry> _dormant = new List<DormantEntry>(32);
        private readonly List<TradeStallData> _stallScratch = new List<TradeStallData>(StallScratchCapacity);

        private const int StallScratchCapacity = 8;

        public SupplyTuning Tuning => _tuning;
        public int DormantCount => _dormant.Count;
        public int RegrowthCount { get; private set; }
        public int RestockCount { get; private set; }

        /// <summary>Caravan visits that bought salvage from a stall.</summary>
        public int SalvageSaleVisits { get; private set; }

        /// <summary>Visits where a stall paid its upkeep in full.</summary>
        public int UpkeepPaidCount { get; private set; }

        /// <summary>Visits where a stall couldn't pay its upkeep in full (it went or stayed in arrears).</summary>
        public int UpkeepShortCount { get; private set; }

        /// <summary>Caravan restocks that delivered fewer rations than the cap because the stall couldn't pay for all.</summary>
        public int PartialRestockCount { get; private set; }

        /// <summary>Caravan restocks skipped because the stall couldn't pay.</summary>
        public int SkippedRestockCount { get; private set; }

        public SupplyService() : this(SupplyTuning.Default)
        {
        }

        public SupplyService(SupplyTuning tuning)
        {
            _tuning = tuning;
        }

        /// <summary>Replaces the tuning (the session's EconomyTuning, applied on every respawn).</summary>
        public void Configure(in SupplyTuning tuning)
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
            SalvageSaleVisits = 0;
            UpkeepPaidCount = 0;
            UpkeepShortCount = 0;
            PartialRestockCount = 0;
            SkippedRestockCount = 0;
        }

        public void CaptureTo(SupplySave save)
        {
            if (save == null) throw new System.ArgumentNullException(nameof(save));

            var dormant = new DormantRecord[_dormant.Count];
            for (int i = 0; i < _dormant.Count; i++)
            {
                dormant[i] = new DormantRecord { ResourceId = _dormant[i].ResourceId, ReadyTick = _dormant[i].ReadyTick };
            }
            save.Dormant = dormant;
            save.Counters = new SupplyCounters
            {
                Regrowths = RegrowthCount,
                Restocks = RestockCount,
                SalvageSaleVisits = SalvageSaleVisits,
                UpkeepPaid = UpkeepPaidCount,
                UpkeepShort = UpkeepShortCount,
                PartialRestocks = PartialRestockCount,
                SkippedRestocks = SkippedRestockCount
            };
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
                var stall = BuySalvage(_stallScratch[i], host);
                stall = PayUpkeep(stall, currentTick, host);
                if (stall.RationStock >= _tuning.RationStockCap) continue;

                uint missing = (uint)(_tuning.RationStockCap - stall.RationStock);
                uint affordable = _tuning.WholesaleRationCost > 0 ? stall.TreasuryGold / _tuning.WholesaleRationCost : missing;
                uint units = missing < affordable ? missing : affordable;

                // The caravan delivers what the stall can pay for (review §3.3 D6: a poor stall still gets some food)
                if (units == 0)
                {
                    SkippedRestockCount++;
                    SimLog.Supply.ZLogWarning($"Supply caravan skipped {SimLog.Agent(stall.StallEntityId)}: treasury {stall.TreasuryGold}g can't pay {_tuning.WholesaleRationCost}g for one ration (stock {stall.RationStock})");
                    continue;
                }

                uint cost = units * _tuning.WholesaleRationCost;
                host.UpdateStall(stall.WithTreasury(stall.TreasuryGold - cost).WithRationStock((ushort)(stall.RationStock + units)));
                host.EconomyLedger?.RecordSink(cost, CaravanSinkName);
                if (units < missing) PartialRestockCount++;
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
        /// The caravan's market (review §3.3 D1): it buys up to its quota of each salvage type from the stall's stock at
        /// the market price, before restocking, so salvage the stall bought from crawlers flows back out as gold. The
        /// quota bounds the faucet: at most SupplyTuning.MaxSalvagePaymentPerVisit per stall per visit.
        /// </summary>
        private TradeStallData BuySalvage(TradeStallData stall, ISupplyHost host)
        {
            uint oreUnits = _tuning.OreMarketPrice > 0 ? System.Math.Min(stall.OreStock, (uint)_tuning.OreQuota) : 0u;
            uint herbUnits = _tuning.HerbMarketPrice > 0 ? System.Math.Min(stall.HerbStock, (uint)_tuning.HerbQuota) : 0u;
            if (oreUnits == 0 && herbUnits == 0) return stall;

            uint revenue = oreUnits * _tuning.OreMarketPrice + herbUnits * _tuning.HerbMarketPrice;
            var updated = stall
                .WithOreStock((ushort)(stall.OreStock - oreUnits))
                .WithHerbStock((ushort)(stall.HerbStock - herbUnits))
                .WithTreasury(stall.TreasuryGold + revenue);
            host.UpdateStall(updated);
            host.EconomyLedger?.RecordFaucet(revenue, SalvageSalesFaucetName);
            SalvageSaleVisits++;
            SimLog.Supply.ZLogInformation($"Supply caravan bought {oreUnits} ore + {herbUnits} herbs from {SimLog.Agent(stall.StallEntityId)} for {revenue}g (treasury now {updated.TreasuryGold}g, ore {updated.OreStock}, herbs {updated.HerbStock} left)");
            return updated;
        }

        /// <summary>
        /// The outpost's running cost (review §3.3 D2), paid after the caravan's salvage purchase and before the restock.
        /// A stall that can't pay in full pays what it has (no debt) and is in arrears: it buys no salvage, but still sells
        /// rations, until a visit finds the upkeep paid in full.
        /// </summary>
        private TradeStallData PayUpkeep(TradeStallData stall, uint currentTick, ISupplyHost host)
        {
            uint due = _tuning.UpkeepPerVisit;
            if (due == 0)
            {
                if (!stall.InArrears) return stall;
                stall = stall.WithArrears(false);
                host.UpdateStall(stall);
                return stall;
            }

            uint paid = stall.TreasuryGold < due ? stall.TreasuryGold : due;
            bool inArrears = paid < due;
            var updated = stall.WithTreasury(stall.TreasuryGold - paid).WithArrears(inArrears);
            host.UpdateStall(updated);
            if (paid > 0) host.EconomyLedger?.RecordSink(paid, UpkeepSinkName);

            if (inArrears)
            {
                UpkeepShortCount++;
                if (!stall.InArrears)
                {
                    SimLog.Supply.ZLogWarning($"{SimLog.Agent(stall.StallEntityId)} paid {paid}g of {due}g upkeep and is in arrears: it buys no salvage until the upkeep is paid");
                    host.ChronicleService?.RecordEvent(new ChronicleEvent(
                        ChronicleEventType.UpkeepUnpaid,
                        stall.StallEntityId,
                        default,
                        0,
                        currentTick,
                        ChronicleIntensity.Notable,
                        due - paid));
                }
            }
            else
            {
                UpkeepPaidCount++;
                if (stall.InArrears) SimLog.Supply.ZLogInformation($"{SimLog.Agent(stall.StallEntityId)} paid its {due}g upkeep and is out of arrears (treasury now {updated.TreasuryGold}g)");
                else SimLog.Supply.ZLogDebug($"{SimLog.Agent(stall.StallEntityId)} paid {due}g upkeep (treasury now {updated.TreasuryGold}g)");
            }
            return updated;
        }

        private static bool Precedes(in DormantEntry a, in DormantEntry b)
        {
            if (a.ReadyTick != b.ReadyTick) return a.ReadyTick < b.ReadyTick;
            return a.ResourceId.CompareTo(b.ResourceId) < 0;
        }
    }
}
