using System;
using Simpiens.Simulation;
using UnityEngine;

namespace Simpiens.Simulation.Economy
{
    /// <summary>
    /// Authoritative double-entry ledger tracking circulating gold,
    /// bounty escrow allocations, and named faucets/sinks.
    /// Enforces the conservation invariant: TotalCirculatingGold = Faucets - Sinks.
    /// </summary>
    public interface IEconomyLedger
    {
        uint TotalEscrowGold { get; }
        uint TotalFaucetedGold { get; }
        uint TotalSinkedGold { get; }

        bool HasEscrow(ushort bountyId);
        uint GetEscrowAmount(ushort bountyId);

        bool TryLockEscrow(GUID stallId, ushort bountyId, uint amount, ref TradeStallData stall);
        bool TryReleaseEscrowToClaimant(ushort bountyId, GUID claimantId, IAgentRegistry agentRegistry, out uint releasedAmount);
        bool TryRefundEscrow(ushort bountyId, GUID stallId, ref TradeStallData stall, out uint refundedAmount);

        bool TryExecuteStallPurchase(GUID stallId, ref TradeStallData stall, GUID buyerAgentId, IAgentRegistry agentRegistry, uint totalCost);
        bool TryExecuteStallSale(GUID stallId, ref TradeStallData stall, GUID sellerAgentId, IAgentRegistry agentRegistry, uint totalPayout);

        void RecordFaucet(uint amount, string faucetName);
        void RecordSink(uint amount, string sinkName);

        bool ValidateConservation(uint currentTotalStallGold, uint currentTotalAgentGold, out int discrepancy);

        /// <summary>Cumulative conserved transfers (purchases, sales, escrow) since the last Reset. Observation only.</summary>
        EconomyFlowTotals Flows { get; }

        /// <summary>Gold recorded under one faucet name since the last Reset (0 if never recorded).</summary>
        uint GetFaucetTotal(string faucetName);

        /// <summary>Gold recorded under one sink name since the last Reset (0 if never recorded).</summary>
        uint GetSinkTotal(string sinkName);

        /// <summary>Clears all escrows and faucet/sink totals (session respawn).</summary>
        void Reset();
    }
}
