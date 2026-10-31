using System.Runtime.InteropServices;
using MemoryPack;
using Simpiens.Simulation.Economy;

namespace Simpiens.Simulation.Persistence
{
    /// <summary>The stalls, every bounty and the ledger in a save, written and read by the simulation manager.</summary>
    [MemoryPackable]
    public sealed partial class EconomySave
    {
        /// <summary>By stall id.</summary>
        public TradeStallData[] Stalls { get; set; }

        /// <summary>By bounty id, finished ones included: the next posted bounty's id counts them.</summary>
        public BountyContract[] Bounties { get; set; }

        public LedgerSave Ledger { get; set; }
    }

    /// <summary>The ledger in a save, written and read by <see cref="EconomyLedger"/>. Named totals are in ordinal name order.</summary>
    [MemoryPackable]
    public sealed partial class LedgerSave
    {
        public LedgerTotals Totals { get; set; }

        /// <summary>By bounty id.</summary>
        public EscrowRecord[] Escrows { get; set; }

        public string[] FaucetNames { get; set; }
        public uint[] FaucetTotals { get; set; }
        public string[] SinkNames { get; set; }
        public uint[] SinkTotals { get; set; }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct LedgerTotals
    {
        public uint Escrow;
        public uint Fauceted;
        public uint Sunk;
        public uint Purchases;
        public uint PurchaseGold;
        public uint Sales;
        public uint SaleGold;
        public uint EscrowLocked;
        public uint EscrowReleased;
        public uint EscrowRefunded;
        public uint Fees;      // the guild's signing fees and wages (Epic 10 G4, format 8)
        public uint FeeGold;
        public uint Wages;
        public uint WageGold;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct EscrowRecord
    {
        public uint Amount;
        public ushort BountyId;
        private ushort _pad;

        public EscrowRecord(ushort bountyId, uint amount)
        {
            Amount = amount;
            BountyId = bountyId;
            _pad = 0;
        }
    }
}
