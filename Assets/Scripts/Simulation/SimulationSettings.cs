using System;

namespace Simpiens.Simulation
{
    /// <summary>
    /// Per-simulation configuration that must be identical for two runs to be reproducible (ROADMAP §1A.1).
    /// The seed drives <see cref="SimRandom"/> and <see cref="EntityIdAllocator"/>.
    /// Configure with SIMPIENS_SEED or the -simseed command-line argument; otherwise <see cref="DefaultSeed"/>.
    /// SIMPIENS_PROVISIONAL_EXPORT=0 / -simprovisional-export 0 disables the provisional caravan export (economy A/B soaks).
    /// </summary>
    public sealed class SimulationSettings
    {
        public const ulong DefaultSeed = 20261009UL;

        public ulong Seed { get; }

        /// <summary>PROVISIONAL economy stopgap switch (see SupplyTuning.ProvisionalOreExportPrice). Default on.</summary>
        public bool ProvisionalExportEnabled { get; }

        public SimulationSettings(ulong seed = DefaultSeed, bool provisionalExportEnabled = true)
        {
            Seed = seed;
            ProvisionalExportEnabled = provisionalExportEnabled;
        }

        public static SimulationSettings FromEnvironment()
        {
            ulong seed = DefaultSeed;
            bool export = true;
            if (TryParse(global::System.Environment.GetEnvironmentVariable("SIMPIENS_SEED"), out ulong envSeed)) seed = envSeed;
            if (TryParse(global::System.Environment.GetEnvironmentVariable("SIMPIENS_PROVISIONAL_EXPORT"), out ulong envExport)) export = envExport != 0;

            string[] args = global::System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == "-simseed" && TryParse(args[i + 1], out ulong argSeed)) seed = argSeed;
                if (args[i] == "-simprovisional-export" && TryParse(args[i + 1], out ulong argExport)) export = argExport != 0;
            }
            return new SimulationSettings(seed, export);
        }

        private static bool TryParse(string text, out ulong value)
        {
            value = 0;
            return !string.IsNullOrWhiteSpace(text) && ulong.TryParse(text.Trim(), out value);
        }
    }
}
