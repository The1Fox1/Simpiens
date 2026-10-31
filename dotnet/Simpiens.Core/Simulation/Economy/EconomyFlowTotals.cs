namespace Simpiens.Simulation.Economy
{
    /// <summary>
    /// Read-only snapshot of cumulative conserved gold transfers since the ledger's last Reset.
    /// Observation only (economy soak, diagnostics); nothing in the simulation reads these to make decisions.
    /// </summary>
    public readonly struct EconomyFlowTotals
    {
        /// <summary>Successful agent purchases at a stall (agent → stall).</summary>
        public readonly uint Purchases;
        public readonly uint PurchaseGold;

        /// <summary>Successful agent sales to a stall (stall → agent).</summary>
        public readonly uint Sales;
        public readonly uint SaleGold;

        /// <summary>Bounty escrow locked (stall → escrow), released to claimants (escrow → agent) and refunded (escrow → stall).</summary>
        public readonly uint EscrowLockedGold;
        public readonly uint EscrowReleasedGold;
        public readonly uint EscrowRefundedGold;

        /// <summary>The guild's signing fees and wages paid from the stall to crawlers (Epic 10 G4).</summary>
        public readonly uint Fees;
        public readonly uint FeeGold;
        public readonly uint Wages;
        public readonly uint WageGold;

        public EconomyFlowTotals(uint purchases, uint purchaseGold, uint sales, uint saleGold,
            uint escrowLockedGold, uint escrowReleasedGold, uint escrowRefundedGold,
            uint fees = 0, uint feeGold = 0, uint wages = 0, uint wageGold = 0)
        {
            Fees = fees;
            FeeGold = feeGold;
            Wages = wages;
            WageGold = wageGold;
            Purchases = purchases;
            PurchaseGold = purchaseGold;
            Sales = sales;
            SaleGold = saleGold;
            EscrowLockedGold = escrowLockedGold;
            EscrowReleasedGold = escrowReleasedGold;
            EscrowRefundedGold = escrowRefundedGold;
        }
    }
}
