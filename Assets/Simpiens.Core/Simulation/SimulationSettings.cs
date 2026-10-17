using System;

namespace Simpiens.Simulation
{
    /// <summary>
    /// Per-simulation configuration that must be identical for two runs to be reproducible (ROADMAP §1A.1).
    /// The seed drives <see cref="SimRandom"/> and <see cref="EntityIdAllocator"/>.
    /// Configure with SIMPIENS_SEED or the -simseed command-line argument; otherwise <see cref="DefaultSeed"/>.
    /// </summary>
    public sealed class SimulationSettings
    {
        public const ulong DefaultSeed = 20261009UL;

        public ulong Seed { get; }

        public SimulationSettings(ulong seed = DefaultSeed)
        {
            Seed = seed;
        }

        public static SimulationSettings FromEnvironment()
        {
            ulong seed = DefaultSeed;
            if (TryParse(global::System.Environment.GetEnvironmentVariable("SIMPIENS_SEED"), out ulong envSeed)) seed = envSeed;

            string[] args = global::System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == "-simseed" && TryParse(args[i + 1], out ulong argSeed)) seed = argSeed;
            }
            return new SimulationSettings(seed);
        }

        private static bool TryParse(string text, out ulong value)
        {
            value = 0;
            return !string.IsNullOrWhiteSpace(text) && ulong.TryParse(text.Trim(), out value);
        }
    }
}
