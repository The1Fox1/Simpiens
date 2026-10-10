using System;
using System.Collections.Generic;
using Simpiens.Simulation;
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
            SimLog.Economy.ZLogDebug($"Ledger: locked {amount}g escrow for bounty #{bountyId} from {SimLog.Agent(stallId)} (total escrow {TotalEscrowGold}g)");
            return true;
        }

        public bool TryReleaseEscrowToClaimant(ushort bountyId, GUID claimantId, IAgentRegistry agentRegistry, out uint releasedAmount)
        {
            if (_bountyEscrows.TryGetValue(bountyId, out releasedAmount) && releasedAmount > 0)
            {
                _bountyEscrows.Remove(bountyId);
                TotalEscrowGold -= releasedAmount;
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

            if (agent.Inventory == null || agent.Inventory.Gold < totalCost)
            {
                return false;
            }

            agent.Inventory.Gold -= (int)totalCost;
            stall = stall.WithTreasury(stall.TreasuryGold + totalCost);
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

            stall = stall.WithTreasury(stall.TreasuryGold - totalPayout);
            agent.Inventory?.AddGold((int)totalPayout);
            return true;
        }

        public void RecordFaucet(uint amount, string faucetName)
        {
            TotalFaucetedGold += amount;
            SimLog.Economy.ZLogDebug($"Ledger: faucet {faucetName} +{amount}g (total fauceted {TotalFaucetedGold}g)");
        }

        public void RecordSink(uint amount, string sinkName)
        {
            TotalSinkedGold += amount;
            SimLog.Economy.ZLogDebug($"Ledger: sink {sinkName} -{amount}g (total sunk {TotalSinkedGold}g)");
        }

        public void Reset()
        {
            _bountyEscrows.Clear();
            TotalEscrowGold = 0;
            TotalFaucetedGold = 0;
            TotalSinkedGold = 0;
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
