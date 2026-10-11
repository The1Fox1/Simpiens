using System;
using Simpiens.Cognition;
using Simpiens.Simulation;
using Simpiens.Simulation.Economy;
using UnityEngine;
using VContainer.Unity;
using Debug = UnityEngine.Debug;

namespace Simpiens.Testing.Economy
{
    /// <summary>
    /// Milestone 1 closure, Phase 3: the read-only economy counters the observation soak relies on.
    /// Counters must move exactly with the gold they describe, agree with the ledger totals, reset cleanly,
    /// and cost nothing on the hot path.
    /// </summary>
    public class EconomyCountersValidator : IStartable
    {
        public void Start()
        {
            RunAllValidationTests();
        }

        public void RunAllValidationTests()
        {
            Debug.Log("[EconomyCountersValidator] Beginning economy counter validation...");

            ValidateTransferCounters();
            ValidateNamedBreakdownMatchesTotals();
            ValidateResetClearsCounters();
            ValidateCountersAreZeroAlloc();

            Debug.Log("<color=green>[EconomyCountersValidator] ALL 4 TESTS PASSED SUCCESSFULLY!</color>");
        }

        private void ValidateTransferCounters()
        {
            var ledger = new EconomyLedger();
            var registry = CreateRegistryWithAgent(out var agentId, gold: 10);
            var stallId = new GUID(7u, 7u, 7u, 7u);
            var stall = new TradeStallData(stallId, Vector2.zero, treasuryGold: 100);

            Assert(ledger.TryExecuteStallPurchase(stallId, ref stall, agentId, registry, 5), "Purchase succeeded.");
            Assert(!ledger.TryExecuteStallPurchase(stallId, ref stall, agentId, registry, 500), "Unaffordable purchase refused.");
            Assert(ledger.TryExecuteStallSale(stallId, ref stall, agentId, registry, 7), "Sale succeeded.");

            Assert(ledger.TryLockEscrow(stallId, 1, 20, ref stall), "Escrow 1 locked.");
            Assert(ledger.TryReleaseEscrowToClaimant(1, agentId, registry, out _), "Escrow 1 released.");
            Assert(ledger.TryLockEscrow(stallId, 2, 15, ref stall), "Escrow 2 locked.");
            Assert(ledger.TryRefundEscrow(2, stallId, ref stall, out _), "Escrow 2 refunded.");

            var f = ledger.Flows;
            Assert(f.Purchases == 1 && f.PurchaseGold == 5, $"Purchases {f.Purchases}/{f.PurchaseGold}g (refused purchase must not count).");
            Assert(f.Sales == 1 && f.SaleGold == 7, $"Sales {f.Sales}/{f.SaleGold}g.");
            Assert(f.EscrowLockedGold == 35 && f.EscrowReleasedGold == 20 && f.EscrowRefundedGold == 15,
                $"Escrow locked {f.EscrowLockedGold} / released {f.EscrowReleasedGold} / refunded {f.EscrowRefundedGold}.");

            Debug.Log("[PASS] Test 1: Purchase, sale and escrow counters move exactly with successful transfers.");
        }

        private void ValidateNamedBreakdownMatchesTotals()
        {
            var ledger = new EconomyLedger();
            ledger.RecordFaucet(100, "InitialSeed");
            ledger.RecordFaucet(50, SupplyService.ProvisionalExportFaucetName);
            ledger.RecordFaucet(25, "InitialSeed");
            ledger.RecordSink(30, SupplyService.CaravanSinkName);
            ledger.RecordSink(12, SupplyService.CaravanSinkName);

            uint faucetSum = ledger.GetFaucetTotal("InitialSeed") + ledger.GetFaucetTotal(SupplyService.ProvisionalExportFaucetName);
            Assert(ledger.GetFaucetTotal("InitialSeed") == 125, "InitialSeed accumulates across records.");
            Assert(faucetSum == ledger.TotalFaucetedGold, $"Faucet breakdown {faucetSum}g must equal total {ledger.TotalFaucetedGold}g.");
            Assert(ledger.GetSinkTotal(SupplyService.CaravanSinkName) == ledger.TotalSinkedGold, "Sink breakdown must equal total.");
            Assert(ledger.GetFaucetTotal("NeverRecorded") == 0 && ledger.GetSinkTotal(null) == 0, "Unknown names read as 0.");

            Debug.Log("[PASS] Test 2: Per-name faucet/sink breakdown sums to the ledger totals.");
        }

        private void ValidateResetClearsCounters()
        {
            var ledger = new EconomyLedger();
            var registry = CreateRegistryWithAgent(out var agentId, gold: 10);
            var stallId = new GUID(8u, 8u, 8u, 8u);
            var stall = new TradeStallData(stallId, Vector2.zero, treasuryGold: 100);
            ledger.RecordFaucet(100, "InitialSeed");
            ledger.RecordSink(10, SupplyService.CaravanSinkName);
            ledger.TryExecuteStallPurchase(stallId, ref stall, agentId, registry, 5);
            ledger.TryLockEscrow(stallId, 3, 10, ref stall);

            ledger.Reset();

            var f = ledger.Flows;
            Assert(f.Purchases == 0 && f.PurchaseGold == 0 && f.Sales == 0 && f.SaleGold == 0, "Transfer counters reset.");
            Assert(f.EscrowLockedGold == 0 && f.EscrowReleasedGold == 0 && f.EscrowRefundedGold == 0, "Escrow counters reset.");
            Assert(ledger.GetFaucetTotal("InitialSeed") == 0 && ledger.GetSinkTotal(SupplyService.CaravanSinkName) == 0, "Named totals reset.");

            Debug.Log("[PASS] Test 3: Reset clears every counter.");
        }

        private void ValidateCountersAreZeroAlloc()
        {
            var ledger = new EconomyLedger();
            var registry = CreateRegistryWithAgent(out var agentId, gold: 1000000);
            var stallId = new GUID(9u, 9u, 9u, 9u);
            var stall = new TradeStallData(stallId, Vector2.zero, treasuryGold: 1000000);

            // Warm up: the first record under a name inserts its dictionary entry
            ledger.RecordFaucet(1, SupplyService.ProvisionalExportFaucetName);
            ledger.RecordSink(1, SupplyService.CaravanSinkName);
            ledger.TryExecuteStallPurchase(stallId, ref stall, agentId, registry, 1);
            ledger.TryExecuteStallSale(stallId, ref stall, agentId, registry, 1);
            var warm = ledger.Flows;

            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 1000; i++)
            {
                ledger.RecordFaucet(1, SupplyService.ProvisionalExportFaucetName);
                ledger.RecordSink(1, SupplyService.CaravanSinkName);
                ledger.TryExecuteStallPurchase(stallId, ref stall, agentId, registry, 1);
                ledger.TryExecuteStallSale(stallId, ref stall, agentId, registry, 1);
                warm = ledger.Flows;
            }
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert(allocated == 0, $"Counter updates allocated {allocated} bytes across 1,000 iterations.");
            Assert(warm.Purchases == 1001, $"Purchases counted {warm.Purchases} instead of 1001.");

            Debug.Log("[PASS] Test 4: Counter updates are allocation-free.");
        }

        private static AgentRegistry CreateRegistryWithAgent(out GUID agentId, int gold)
        {
            var registry = new AgentRegistry();
            var agent = new AgentState();
            agent.AgentId = new GUID(1u, 2u, 3u, (uint)gold);
            agent.Inventory.Gold = gold;
            registry.RegisterAgent(agent);
            agentId = agent.AgentId;
            return registry;
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition)
            {
                throw new Exception($"[ASSERTION FAILED] {message}");
            }
        }
    }
}
