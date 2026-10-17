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

        bool TryLockEscrow(EntityId stallId, ushort bountyId, uint amount, ref TradeStallData stall);
        bool TryReleaseEscrowToClaimant(ushort bountyId, EntityId claimantId, IAgentRegistry agentRegistry, out uint releasedAmount);
        bool TryRefundEscrow(ushort bountyId, EntityId stallId, ref TradeStallData stall, out uint refundedAmount);

        bool TryExecuteStallPurchase(EntityId stallId, ref TradeStallData stall, EntityId buyerAgentId, IAgentRegistry agentRegistry, uint totalCost);
        bool TryExecuteStallSale(EntityId stallId, ref TradeStallData stall, EntityId sellerAgentId, IAgentRegistry agentRegistry, uint totalPayout);

        /// <summary>Stall purchase against an inventory the caller already holds (intent handlers). Agent → stall.</summary>
        bool TryExecuteStallPurchase(EntityId stallId, ref TradeStallData stall, Simpiens.Cognition.Inventory.AgentInventory buyer, uint totalCost);

        /// <summary>Stall sale against an inventory the caller already holds (intent handlers). Stall → agent.</summary>
        bool TryExecuteStallSale(EntityId stallId, ref TradeStallData stall, Simpiens.Cognition.Inventory.AgentInventory seller, uint totalPayout);

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

        /// <summary>Writes every total, escrow and named faucet and sink into a save (Milestone 1 save/load).</summary>
        void CaptureTo(Simpiens.Simulation.Persistence.LedgerSave save);

        /// <summary>Replaces every total, escrow and named faucet and sink with a save's (a load). No InitialSeed is recorded.</summary>
        void RestoreFrom(Simpiens.Simulation.Persistence.LedgerSave save);
    }
}
