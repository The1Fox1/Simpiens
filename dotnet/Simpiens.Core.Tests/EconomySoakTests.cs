using System.Collections.Generic;
using Simpiens.Hosting;
using Simpiens.Simulation.Economy;
using Xunit;

namespace Simpiens.Core.Tests
{
    /// <summary>
    /// The economy soak headless (Epic CORE C10): each player profile on the soak's seeds for three game days stays in its
    /// pace band (docs/economy-model.md §7; the 24-hour day's horizon and bands, Epic 10 Part 2, T4.4). A headless soak
    /// takes under a second, so it runs with every `dotnet test`.
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
        /// Drifters open the economy (Epic 10 G1). On the 24-hour day they work (Epic 10 Part 2, F11): they sell salvage, so
        /// they take away more gold than they bring, where on G1's day they left it behind. But a keen (slightly greedy)
        /// player's stall ends richer with them than in a closed outpost on every soak seed: measured at T4.4 over seeds 1-15
        /// and 20261009, ahead on all 16 (17,254g against 11,985g), a comparison G1's day left in the noise (Epic 10 F4).
        /// </summary>
        [Fact]
        public void DriftersLeaveAKeenPlayersStallRicher()
        {
            foreach (var seed in EconomySoak.DefaultSeeds)
            {
                var withDrifters = EconomySoak.Run(seed, "keen");
                var withoutDrifters = EconomySoak.Run(seed, "keen", drifters: false);
                TestContext.Current.TestOutputHelper?.WriteLine($"with drifters: {withDrifters.Summary()} (drifters brought {withDrifters.DrifterPurses}g, took {withDrifters.DrifterDepartures}g)\nclosed: {withoutDrifters.Summary()}");
                Assert.Empty(withDrifters.Failures());
                Assert.Empty(withoutDrifters.Failures());
                Assert.Equal(0u, withoutDrifters.DrifterPurses);
                Assert.True(withDrifters.Last.Treasury > withoutDrifters.Last.Treasury,
                    $"seed {seed}: the stall ended at {withDrifters.Last.Treasury}g with drifters, {withoutDrifters.Last.Treasury}g without");
            }
        }

        /// <summary>
        /// A profile's prices are shares of the economy's fair ones (Epic 10 Part 2, T4.4), so each means the same on any
        /// economy: on Part 1's they gave exactly its fixed prices (greedy rations 30g, ore 10g, herbs 12g, gear 60g).
        /// </summary>
        [Theory]
        [InlineData("fair", "Ration 15g, IronOre 5g, MedicinalHerb 6g, HazardGear 30g")]
        [InlineData("greedy", "Ration 30g, IronOre 2g, MedicinalHerb 3g, HazardGear 60g")]
        [InlineData("generous", "Ration 5g, IronOre 9g, MedicinalHerb 10g, HazardGear 20g")]
        [InlineData("keen", "Ration 20g, IronOre 4g, MedicinalHerb 5g, HazardGear 35g")]
        public void EachProfilePricesAShareOfTheFairPrices(string profile, string prices)
        {
            Assert.Equal(prices, EconomySoak.Resolve(profile, EconomyTuning.Default).PriceText());
        }
    }
}
