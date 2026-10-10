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

        /// <summary>
        /// PROVISIONAL (not an economy design decision; review §3.3 is deferred by the producer, 2026-10-09).
        /// Gold per unit the caravan pays for stall salvage above <see cref="ProvisionalExportReserve"/>; enters as the
        /// "ProvisionalCaravanExport" faucet. Without an outlet the stall buys salvage it can never resell and the treasury
        /// drains until the loop stalls. 0 disables the export.
        /// </summary>
        public readonly ushort ProvisionalOreExportPrice;

        /// <summary>PROVISIONAL: export price per Medicinal Herb unit (see <see cref="ProvisionalOreExportPrice"/>).</summary>
        public readonly ushort ProvisionalHerbExportPrice;

        /// <summary>PROVISIONAL: salvage units of each type the caravan leaves in the stall's stock.</summary>
        public readonly ushort ProvisionalExportReserve;

        public SupplyTuning(uint restockIntervalTicks, ushort rationStockCap, ushort wholesaleRationCost)
            : this(restockIntervalTicks, rationStockCap, wholesaleRationCost, 0, 0, 0)
        {
        }

        public SupplyTuning(uint restockIntervalTicks, ushort rationStockCap, ushort wholesaleRationCost,
            ushort provisionalOreExportPrice, ushort provisionalHerbExportPrice, ushort provisionalExportReserve)
        {
            RestockIntervalTicks = restockIntervalTicks;
            RationStockCap = rationStockCap;
            WholesaleRationCost = wholesaleRationCost;
            ProvisionalOreExportPrice = provisionalOreExportPrice;
            ProvisionalHerbExportPrice = provisionalHerbExportPrice;
            ProvisionalExportReserve = provisionalExportReserve;
        }

        // Placeholder export prices sit ~20% above the seeded stall buy prices (ore 20g, herb 25g)
        public static SupplyTuning Default => new SupplyTuning(restockIntervalTicks: 600, rationStockCap: 50, wholesaleRationCost: 2,
            provisionalOreExportPrice: 24, provisionalHerbExportPrice: 30, provisionalExportReserve: 10);
    }
}
