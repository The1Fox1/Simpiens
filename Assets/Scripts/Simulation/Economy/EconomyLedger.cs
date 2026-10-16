using System;
using System.Collections.Generic;
using Simpiens.Simulation;
using Simpiens.Simulation.Persistence;
using Simpiens.Diagnostics;
using UnityEngine;
using ZLogger;

namespace Simpiens.Simulation.Economy
{
    /// <summary>
    /// Pure C# authoritative double-entry ledger implementing IEconomyLedger.
    /// Eliminates gold minting bugs by enforcing that only locked escrow can be refunded.
    /// </summary>
    public class EconomyLedger : IEconomyLedger
    {
        private readonly Dictionary<ushort, uint> _bountyEscrows = new Dictionary<ushort, uint>(32);

        // Observation counters (economy soak). Written only where gold already moves; never read by the simulation.
        private readonly Dictionary<string, uint> _faucetTotals = new Dictionary<string, uint>(8);
        private readonly Dictionary<string, uint> _sinkTotals = new Dictionary<string, uint>(8);
        private uint _purchases, _purchaseGold, _sales, _saleGold;
        private uint _escrowLockedGold, _escrowReleasedGold, _escrowRefundedGold;

        public EconomyFlowTotals Flows => new EconomyFlowTotals(
            _purchases, _purchaseGold, _sales, _saleGold, _escrowLockedGold, _escrowReleasedGold, _escrowRefundedGold);

        public uint GetFaucetTotal(string faucetName) =>
            faucetName != null && _faucetTotals.TryGetValue(faucetName, out var total) ? total : 0u;

        public uint GetSinkTotal(string sinkName) =>
            sinkName != null && _sinkTotals.TryGetValue(sinkName, out var total) ? total : 0u;

        public uint TotalEscrowGold { get; private set; }
        public uint TotalFaucetedGold { get; private set; }
        public uint TotalSinkedGold { get; private set; }

        public bool HasEscrow(ushort bountyId)
        {
            return _bountyEscrows.ContainsKey(bountyId);
        }

        public uint GetEscrowAmount(ushort bountyId)
        {
            return _bountyEscrows.TryGetValue(bountyId, out var amount) ? amount : 0;
        }

        public bool TryLockEscrow(GUID stallId, ushort bountyId, uint amount, ref TradeStallData stall)
        {
            if (stall.TreasuryGold < amount)
            {
                SimLog.Economy.ZLogDebug($"Ledger: escrow lock for bounty #{bountyId} refused ({amount}g > treasury {stall.TreasuryGold}g)");
                return false;
            }

            stall = stall.WithTreasury(stall.TreasuryGold - amount);
            _bountyEscrows[bountyId] = amount;
            TotalEscrowGold += amount;
            _escrowLockedGold += amount;
            SimLog.Economy.ZLogDebug($"Ledger: locked {amount}g escrow for bounty #{bountyId} from {SimLog.Agent(stallId)} (total escrow {TotalEscrowGold}g)");
            return true;
        }

        public bool TryReleaseEscrowToClaimant(ushort bountyId, GUID claimantId, IAgentRegistry agentRegistry, out uint releasedAmount)
        {
            if (_bountyEscrows.TryGetValue(bountyId, out releasedAmount) && releasedAmount > 0)
            {
                _bountyEscrows.Remove(bountyId);
                TotalEscrowGold -= releasedAmount;
                _escrowReleasedGold += releasedAmount;
                SimLog.Economy.ZLogDebug($"Ledger: released {releasedAmount}g escrow of bounty #{bountyId} to {SimLog.Agent(claimantId)}");

                if (agentRegistry != null && agentRegistry.TryGetAgent(claimantId, out var agent))
                {
                    agent.Inventory?.AddGold((int)releasedAmount);
                }

                return true;
            }

            releasedAmount = 0;
            return false;
        }

        public bool TryRefundEscrow(ushort bountyId, GUID stallId, ref TradeStallData stall, out uint refundedAmount)
        {
            if (_bountyEscrows.TryGetValue(bountyId, out refundedAmount) && refundedAmount > 0)
            {
                _bountyEscrows.Remove(bountyId);
                TotalEscrowGold -= refundedAmount;
                _escrowRefundedGold += refundedAmount;

                stall = stall.WithTreasury(stall.TreasuryGold + refundedAmount);
                SimLog.Economy.ZLogDebug($"Ledger: refunded {refundedAmount}g escrow of bounty #{bountyId} to {SimLog.Agent(stallId)}");
                return true;
            }

            refundedAmount = 0;
            return false;
        }

        public bool TryExecuteStallPurchase(GUID stallId, ref TradeStallData stall, GUID buyerAgentId, IAgentRegistry agentRegistry, uint totalCost)
        {
            if (agentRegistry == null || !agentRegistry.TryGetAgent(buyerAgentId, out var agent))
            {
                return false;
            }
            return TryExecuteStallPurchase(stallId, ref stall, agent.Inventory, totalCost);
        }

        public bool TryExecuteStallPurchase(GUID stallId, ref TradeStallData stall, Simpiens.Cognition.Inventory.AgentInventory buyer, uint totalCost)
        {
            if (buyer == null || buyer.Gold < totalCost)
            {
                return false;
            }

            buyer.Gold -= (int)totalCost;
            stall = stall.WithTreasury(stall.TreasuryGold + totalCost);
            _purchases++;
            _purchaseGold += totalCost;
            return true;
        }

        public bool TryExecuteStallSale(GUID stallId, ref TradeStallData stall, GUID sellerAgentId, IAgentRegistry agentRegistry, uint totalPayout)
        {
            if (stall.TreasuryGold < totalPayout)
            {
                return false;
            }

            if (agentRegistry == null || !agentRegistry.TryGetAgent(sellerAgentId, out var agent))
            {
                return false;
            }
            return TryExecuteStallSale(stallId, ref stall, agent.Inventory, totalPayout);
        }

        public bool TryExecuteStallSale(GUID stallId, ref TradeStallData stall, Simpiens.Cognition.Inventory.AgentInventory seller, uint totalPayout)
        {
            if (seller == null || stall.TreasuryGold < totalPayout)
            {
                return false;
            }

            stall = stall.WithTreasury(stall.TreasuryGold - totalPayout);
            seller.AddGold((int)totalPayout);
            _sales++;
            _saleGold += totalPayout;
            return true;
        }

        public void RecordFaucet(uint amount, string faucetName)
        {
            TotalFaucetedGold += amount;
            AddNamed(_faucetTotals, faucetName, amount);
            SimLog.Economy.ZLogDebug($"Ledger: faucet {faucetName} +{amount}g (total fauceted {TotalFaucetedGold}g)");
        }

        public void RecordSink(uint amount, string sinkName)
        {
            TotalSinkedGold += amount;
            AddNamed(_sinkTotals, sinkName, amount);
            SimLog.Economy.ZLogDebug($"Ledger: sink {sinkName} -{amount}g (total sunk {TotalSinkedGold}g)");
        }

        public void Reset()
        {
            _bountyEscrows.Clear();
            TotalEscrowGold = 0;
            TotalFaucetedGold = 0;
            TotalSinkedGold = 0;
            _faucetTotals.Clear();
            _sinkTotals.Clear();
            _purchases = _purchaseGold = _sales = _saleGold = 0;
            _escrowLockedGold = _escrowReleasedGold = _escrowRefundedGold = 0;
        }

        public void CaptureTo(LedgerSave save)
        {
            if (save == null) throw new ArgumentNullException(nameof(save));

            save.Totals = new LedgerTotals
            {
                Escrow = TotalEscrowGold,
                Fauceted = TotalFaucetedGold,
                Sunk = TotalSinkedGold,
                Purchases = _purchases,
                PurchaseGold = _purchaseGold,
                Sales = _sales,
                SaleGold = _saleGold,
                EscrowLocked = _escrowLockedGold,
                EscrowReleased = _escrowReleasedGold,
                EscrowRefunded = _escrowRefundedGold
            };

            var escrows = new EscrowRecord[_bountyEscrows.Count];
            int n = 0;
            foreach (var kvp in _bountyEscrows) escrows[n++] = new EscrowRecord(kvp.Key, kvp.Value);
            Array.Sort(escrows, (a, b) => a.BountyId.CompareTo(b.BountyId));
            save.Escrows = escrows;

            CaptureNamed(_faucetTotals, out var faucetNames, out var faucetTotals);
            save.FaucetNames = faucetNames;
            save.FaucetTotals = faucetTotals;
            CaptureNamed(_sinkTotals, out var sinkNames, out var sinkTotals);
            save.SinkNames = sinkNames;
            save.SinkTotals = sinkTotals;
        }

        private static void CaptureNamed(Dictionary<string, uint> totals, out string[] names, out uint[] amounts)
        {
            names = new string[totals.Count];
            int n = 0;
            foreach (var kvp in totals) names[n++] = kvp.Key;
            Array.Sort(names, StringComparer.Ordinal);
            amounts = new uint[names.Length];
            for (int i = 0; i < names.Length; i++) amounts[i] = totals[names[i]];
        }

        private static void AddNamed(Dictionary<string, uint> totals, string name, uint amount)
        {
            string key = name ?? string.Empty;
            totals.TryGetValue(key, out var current);
            totals[key] = current + amount;
        }

        public bool ValidateConservation(uint currentTotalStallGold, uint currentTotalAgentGold, out int discrepancy)
        {
            uint actualTotal = currentTotalStallGold + currentTotalAgentGold + TotalEscrowGold;
            long expectedTotal = (long)TotalFaucetedGold - (long)TotalSinkedGold;
            discrepancy = (int)(actualTotal - expectedTotal);
            if (discrepancy != 0)
            {
                SimLog.Economy.ZLogError($"Ledger conservation violated by {discrepancy}g: stalls {currentTotalStallGold} + agents {currentTotalAgentGold} + escrow {TotalEscrowGold} != faucets {TotalFaucetedGold} - sinks {TotalSinkedGold}");
            }
            return discrepancy == 0;
        }
    }
}
