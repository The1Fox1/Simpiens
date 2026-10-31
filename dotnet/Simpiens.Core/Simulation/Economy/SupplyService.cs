using System.Collections.Generic;
using Simpiens.Diagnostics;
using Simpiens.Narrative;
using Simpiens.Simulation.Persistence;
using ZLogger;

using Simpiens.Simulation.Items;

namespace Simpiens.Simulation.Economy
{
    /// <summary>
    /// Pure C# implementation of ISupplyService.
    /// Dormant nodes are kept in a list sorted by (ReadyTick, EntityId) so regrowth order is deterministic and never
    /// depends on dictionary enumeration. Steady-state ticking allocates nothing.
    /// </summary>
    public class SupplyService : ISupplyService
    {
        public const string CaravanSinkName = "SupplyCaravan";
        public const string SalvageSalesFaucetName = "CaravanSalvageSales";
        public const string UpkeepSinkName = "OutpostUpkeep";
        public const string GearSinkName = "GearWholesale";

        private readonly struct DormantEntry
        {
            public readonly EntityId ResourceId;
            public readonly uint ReadyTick;

            public DormantEntry(EntityId resourceId, uint readyTick)
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

        public void RegisterDormant(EntityId resourceId, uint readyTick)
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

        public void RestoreFrom(SupplySave save)
        {
            if (save == null) throw new System.ArgumentNullException(nameof(save));

            Reset();
            if (save.Dormant != null)
            {
                // Saved in queue order (ready tick, then id): append as it is
                for (int i = 0; i < save.Dormant.Length; i++) _dormant.Add(new DormantEntry(save.Dormant[i].ResourceId, save.Dormant[i].ReadyTick));
            }
            var c = save.Counters;
            RegrowthCount = c.Regrowths;
            RestockCount = c.Restocks;
            SalvageSaleVisits = c.SalvageSaleVisits;
            UpkeepPaidCount = c.UpkeepPaid;
            UpkeepShortCount = c.UpkeepShort;
            PartialRestockCount = c.PartialRestocks;
            SkippedRestockCount = c.SkippedRestocks;
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
                var stall = BuyExports(_stallScratch[i], host);
                stall = PayUpkeep(stall, currentTick, host);
                // What the caravan supplies: food first, then the guild's wages (Epic 10 G4, D3), then the rest, each in id order
                stall = RestockSupplies(stall, currentTick, host, food: true);
                stall = host.PayGuildWages(stall, currentTick);
                RestockSupplies(stall, currentTick, host, food: false);
            }
        }

        private TradeStallData RestockSupplies(TradeStallData stall, uint currentTick, ISupplyHost host, bool food)
        {
            var all = ItemCatalog.All;
            for (int i = 0; i < all.Count; i++)
            {
                var item = all[i].Id;
                var terms = _tuning.Terms(item);
                if (terms.Role != CaravanRole.Supplies || ItemCatalog.Is(item, ItemCategory.Food) != food || !stall.Trades(item)) continue;
                stall = food ? RestockFood(stall, item, terms, currentTick, host) : RestockGoods(stall, item, terms, host);
            }
            return stall;
        }

        /// <summary>
        /// The caravan tops the stall's food up to the cap, as far as the treasury pays (review §3.3 D6: a poor stall still
        /// gets some food); food restocks are counted and told in the chronicle.
        /// </summary>
        private TradeStallData RestockFood(TradeStallData stall, ItemType item, in CaravanTerms terms, uint currentTick, ISupplyHost host)
        {
            int stock = stall.StockOf(item);
            if (stock >= terms.Limit) return stall;

            uint missing = (uint)(terms.Limit - stock);
            uint affordable = terms.Price > 0 ? stall.TreasuryGold / terms.Price : missing;
            uint units = missing < affordable ? missing : affordable;

            if (units == 0)
            {
                SkippedRestockCount++;
                SimLog.Supply.ZLogWarning($"Supply caravan skipped {SimLog.Agent(stall.StallEntityId)}: treasury {stall.TreasuryGold}g can't pay {terms.Price}g for one {ItemCatalog.NameOf(item)} (stock {stock})");
                return stall;
            }

            uint cost = units * terms.Price;
            var restocked = stall.WithTreasury(stall.TreasuryGold - cost).WithStock(item, (ushort)(stock + units));
            host.UpdateStall(restocked);
            host.EconomyLedger?.RecordSink(cost, terms.FlowName);
            if (units < missing) PartialRestockCount++;
            RestockCount++;
            SimLog.Supply.ZLogInformation($"Supply caravan restocked {SimLog.Agent(stall.StallEntityId)} with {units} {ItemCatalog.NameOf(item)} for {cost}g (treasury now {stall.TreasuryGold - cost}g)");

            host.ChronicleService?.RecordEvent(new ChronicleEvent(
                ChronicleEventType.SupplyCaravanArrived,
                stall.StallEntityId,
                default,
                0,
                currentTick,
                ChronicleIntensity.Trivial,
                units));
            return restocked;
        }

        /// <summary>
        /// After the food, the caravan tops up the other goods it supplies (hazard gear since Epic 10 G3) to their caps at
        /// the wholesale price, as far as the treasury pays, under each one's ledger flow. A poor stall gets what it can pay for.
        /// </summary>
        private TradeStallData RestockGoods(TradeStallData stall, ItemType item, in CaravanTerms terms, ISupplyHost host)
        {
            int stock = stall.StockOf(item);
            if (terms.Limit == 0 || stock >= terms.Limit) return stall;
            uint missing = (uint)(terms.Limit - stock);
            uint affordable = terms.Price > 0 ? stall.TreasuryGold / terms.Price : missing;
            uint units = missing < affordable ? missing : affordable;
            if (units == 0) return stall;

            uint cost = units * terms.Price;
            var restocked = stall.WithTreasury(stall.TreasuryGold - cost).WithStock(item, (ushort)(stock + units));
            host.UpdateStall(restocked);
            if (cost > 0) host.EconomyLedger?.RecordSink(cost, terms.FlowName);
            SimLog.Supply.ZLogInformation($"Supply caravan brought {SimLog.Agent(stall.StallEntityId)} {units} {ItemCatalog.NameOf(item)} for {cost}g (stock {stock + units}, treasury now {stall.TreasuryGold - cost}g)");
            return restocked;
        }

        /// <summary>
        /// The caravan's market (review §3.3 D1): it buys what it exports from the stall's stock, up to each quota at the
        /// market price, before restocking, so salvage the stall bought from crawlers flows back out as gold. The quotas
        /// bound the faucet: at most SupplyTuning.MaxSalvagePaymentPerVisit per stall per visit.
        /// </summary>
        private TradeStallData BuyExports(TradeStallData stall, ISupplyHost host)
        {
            uint revenue = 0;
            uint unitsBought = 0;
            var updated = stall;
            var all = ItemCatalog.All;
            for (int i = 0; i < all.Count; i++)
            {
                var item = all[i].Id;
                var terms = _tuning.Terms(item);
                if (terms.Role != CaravanRole.Exports || terms.Price == 0) continue;
                int stock = updated.StockOf(item);
                uint units = (uint)System.Math.Min(stock, (int)terms.Limit);
                if (units == 0) continue;
                updated = updated.WithStock(item, (ushort)(stock - units));
                revenue += units * terms.Price;
                unitsBought += units;
            }
            if (unitsBought == 0) return stall;

            updated = updated.WithTreasury(stall.TreasuryGold + revenue);
            host.UpdateStall(updated);
            host.EconomyLedger?.RecordFaucet(revenue, SalvageSalesFaucetName);
            SalvageSaleVisits++;
            SimLog.Supply.ZLogInformation($"Supply caravan bought {unitsBought} units of salvage from {SimLog.Agent(stall.StallEntityId)} for {revenue}g (treasury now {updated.TreasuryGold}g)");
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
