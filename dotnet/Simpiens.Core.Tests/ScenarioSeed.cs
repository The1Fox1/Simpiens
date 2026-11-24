namespace Simpiens.Core.Tests
{
    /// <summary>
    /// A live scenario's seed (Epic LIFE-1 plan 2, L2.6): its own, or <c>SIMPIENS_SCENARIO_SEED</c>'s while re-picking it.
    /// A deliberate behaviour change moves the outpost's history, and a scenario whose moment no longer comes on its seed is
    /// re-picked (AGENTS.md, Definition of Done): run it alone (<c>--filter</c>) over candidate seeds from its measurement
    /// with the variable set, then write the seed that passes into the scenario, with the reason. Unset, nothing changes.
    /// </summary>
    internal static class ScenarioSeed
    {
        public static ulong Or(ulong seed) => ulong.TryParse(System.Environment.GetEnvironmentVariable("SIMPIENS_SCENARIO_SEED"), out ulong s) ? s : seed;
    }
}
