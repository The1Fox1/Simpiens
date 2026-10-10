namespace Simpiens.Simulation.Economy
{
    /// <summary>
    /// Tunables for the minimal supply faucets that keep the Milestone 1 loop running.
    /// All durations are in logical simulation ticks (10 Hz). Faucet/sink retuning is owned by §3.3.
    /// </summary>
    public readonly struct SupplyTuning
    {
        /// <summary>Ticks between supply caravan visits (600 = 60 s).</summary>
        public readonly uint RestockIntervalTicks;

        /// <summary>Caravan tops each stall's ration stock up to this cap.</summary>
        public readonly ushort RationStockCap;

        /// <summary>Gold per ration the stall pays the caravan; leaves the economy as the "SupplyCaravan" sink.</summary>
        public readonly ushort WholesaleRationCost;

        public SupplyTuning(uint restockIntervalTicks, ushort rationStockCap, ushort wholesaleRationCost)
        {
            RestockIntervalTicks = restockIntervalTicks;
            RationStockCap = rationStockCap;
            WholesaleRationCost = wholesaleRationCost;
        }

        public static SupplyTuning Default => new SupplyTuning(restockIntervalTicks: 600, rationStockCap: 50, wholesaleRationCost: 2);
    }
}
