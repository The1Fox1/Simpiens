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

        /// <summary>
        /// Drifters open the economy (Epic 10 G1). On every soak seed they leave gold behind (their purses exceed what they
        /// take away), and over the soak's seeds a slightly greedy player ends the 30 minutes with more gold than the same
        /// player in a closed outpost. Per seed, the two runs differ by more than the drifters' gold alone (a different run
        /// of a chaotic day), so the treasury comparison is summed. Before drifters, greed only shrank the trade that fills
        /// the caravan's quota (docs/economy-model.md §7).
        /// </summary>
        [Fact]
        public void AKeenPlayerDoesBetterWithDrifters()
        {
            long open = 0, closed = 0;
            foreach (var seed in EconomySoak.DefaultSeeds)
            {
                var withDrifters = EconomySoak.Run(seed, "keen");
                var withoutDrifters = EconomySoak.Run(seed, "keen", drifters: false);
                TestContext.Current.TestOutputHelper?.WriteLine($"with drifters: {withDrifters.Summary()} (drifters brought {withDrifters.DrifterPurses}g, took {withDrifters.DrifterDepartures}g)\nclosed: {withoutDrifters.Summary()}");
                Assert.Empty(withDrifters.Failures());
                Assert.Empty(withoutDrifters.Failures());
                Assert.True(withDrifters.DrifterPurses > withDrifters.DrifterDepartures, $"seed {seed}: drifters brought {withDrifters.DrifterPurses}g and took {withDrifters.DrifterDepartures}g away");
                Assert.Equal(0u, withoutDrifters.DrifterPurses);
                open += withDrifters.Last.Treasury;
                closed += withoutDrifters.Last.Treasury;
            }
            Assert.True(open > closed, $"keen over the soak seeds: {open}g with drifters, {closed}g closed");
        }
    }
}
