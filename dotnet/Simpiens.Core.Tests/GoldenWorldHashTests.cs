using System.Collections.Generic;
using System.Linq;
using Simpiens.Core.Session;
using Simpiens.Testing.Environment;
using Xunit;

namespace Simpiens.Core.Tests
{
    /// <summary>
    /// Epic CORE's Phase 1 gate: the simulation on .NET 10 (CoreCLR) reproduces, sample for sample, the golden world
    /// hashes Unity (Mono) recorded in Baselines/golden-world-hash.txt. Every seed in the file is checked.
    /// </summary>
    public class GoldenWorldHashTests
    {
        public static IEnumerable<object[]> BaselineSeeds() =>
            GoldenWorldHash.Read(Repo.GoldenBaseline).Keys.OrderBy(s => s).Select(s => new object[] { s });

        [Theory]
        [MemberData(nameof(BaselineSeeds))]
        public void HeadlessRunReproducesUnitysGoldenHashes(ulong seed)
        {
            var baseline = GoldenWorldHash.Read(Repo.GoldenBaseline)[seed];
            var samples = HeadlessRun.GoldenSamples(seed);

            Assert.Equal(GoldenWorldHash.HorizonTicks / GoldenWorldHash.SampleEveryTicks + 1, samples.Count);
            string difference = GoldenWorldHash.Compare(baseline, OutpostSession.SessionId, samples, out long tick);
            Assert.True(difference == null, $"Seed {seed}: first difference from Unity's golden hashes at t{tick}: {difference}");
        }

        [Fact]
        public void TheBaselineFileHoldsTheThreeGateSeeds()
        {
            var seeds = GoldenWorldHash.Read(Repo.GoldenBaseline).Keys;
            Assert.Contains(20261009UL, seeds); // the default seed: every Unity full suite checks it too
            Assert.Contains(20261010UL, seeds);
            Assert.Contains(424242UL, seeds);
        }
    }

    /// <summary>Same seed, same world: two headless runs in one process are identical at every sample.</summary>
    public class DeterminismTests
    {
        [Fact]
        public void SameSeedTwiceGivesTheSameWorld()
        {
            var first = HeadlessRun.GoldenSamples(Simpiens.Simulation.SimulationSettings.DefaultSeed);
            var second = HeadlessRun.GoldenSamples(Simpiens.Simulation.SimulationSettings.DefaultSeed);
            Assert.Equal(first.Count, second.Count);
            for (int i = 0; i < first.Count; i++)
            {
                Assert.True(first[i].Total == second[i].Total, $"t{first[i].Tick}: {first[i].DescribeDifference(second[i])}");
            }
        }

        [Fact]
        public void DifferentSeedsGiveDifferentWorlds()
        {
            var a = HeadlessRun.GoldenSamples(20261009UL);
            var b = HeadlessRun.GoldenSamples(424242UL);
            Assert.NotEqual(a[a.Count - 1].Total, b[b.Count - 1].Total);
        }
    }
}
