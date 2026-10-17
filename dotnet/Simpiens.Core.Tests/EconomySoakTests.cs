using System.Collections.Generic;
using Simpiens.Hosting;
using Xunit;

namespace Simpiens.Core.Tests
{
    /// <summary>
    /// The economy soak headless (Epic CORE C10): each player profile on the soak's seeds for 30 sim-minutes stays in its
    /// pace band (docs/economy-model.md §7), as Unity's [Explicit] Scenario_EconomySoak_PaceBands asserts it. A headless
    /// soak takes a fraction of a second, so it runs with every `dotnet test`.
    /// </summary>
    public class EconomySoakTests
    {
        public static IEnumerable<object[]> Runs()
        {
            foreach (var profile in EconomySoak.Profiles)
                foreach (var seed in EconomySoak.DefaultSeeds)
                    yield return new object[] { profile, seed };
        }

        [Theory]
        [MemberData(nameof(Runs))]
        public void EachPlayerStaysInItsPaceBand(string profile, ulong seed)
        {
            var result = EconomySoak.Run(seed, profile);
            TestContext.Current.TestOutputHelper?.WriteLine(result.Summary());
            var failures = result.Failures();
            Assert.True(failures.Count == 0, $"{profile} seed {seed}: " + string.Join("; ", failures));
        }
    }
}
