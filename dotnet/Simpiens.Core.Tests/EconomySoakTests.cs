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
        /// The 24-hour day's soak (Epic 10 Part 2, T4.4, D28): each profile on the soak's seeds for three game days on the
        /// day's economy stays in the day's bands, proposed to the producer (<see cref="EconomySoak.Pace.Day"/>).
        /// </summary>
        [Theory]
        [MemberData(nameof(Runs))]
        public void OnTheDayEachPlayerStaysInItsPaceBand(string profile, ulong seed)
        {
            var result = EconomySoak.Run(seed, profile, pace: EconomySoak.Pace.Day);
            TestContext.Current.TestOutputHelper?.WriteLine(result.Summary());
            var failures = result.Failures();
            Assert.True(failures.Count == 0, $"day, {profile} seed {seed}: " + string.Join("; ", failures));
        }

        /// <summary>
        /// On the 24-hour day drifters work (T4.4): they sell salvage, so they take away more gold than they bring (G1's
        /// check below turns over), and a keen player's stall ends richer with them than in a closed outpost on every soak
        /// seed. Measured over seeds 1-15 and 20261009: ahead on all 16, 17,254g against 11,985g, where G1's day left the
        /// comparison in the noise (F4).
        /// </summary>
        [Fact]
        public void OnTheDayDriftersLeaveAKeenPlayersStallRicher()
        {
            foreach (var seed in EconomySoak.DefaultSeeds)
            {
                var withDrifters = EconomySoak.Run(seed, "keen", pace: EconomySoak.Pace.Day);
                var withoutDrifters = EconomySoak.Run(seed, "keen", drifters: false, pace: EconomySoak.Pace.Day);
                TestContext.Current.TestOutputHelper?.WriteLine($"with drifters: {withDrifters.Summary()} (drifters brought {withDrifters.DrifterPurses}g, took {withDrifters.DrifterDepartures}g)\nclosed: {withoutDrifters.Summary()}");
                Assert.Empty(withDrifters.Failures());
                Assert.Empty(withoutDrifters.Failures());
                Assert.True(withDrifters.Last.Treasury > withoutDrifters.Last.Treasury,
                    $"seed {seed}: the stall ended at {withDrifters.Last.Treasury}g with drifters, {withoutDrifters.Last.Treasury}g without");
            }
        }

        /// <summary>
        /// A profile's prices are shares of the economy's fair ones (Epic 10 Part 2, T4.4): on today's economy exactly the
        /// prices it set before, and on the 24-hour day's, the same shares of its cheaper salvage.
        /// </summary>
        [Theory]
        [InlineData("fair", "Ration 15g, IronOre 20g, MedicinalHerb 25g, HazardGear 30g", "Ration 15g, IronOre 5g, MedicinalHerb 6g, HazardGear 30g")]
        [InlineData("greedy", "Ration 30g, IronOre 10g, MedicinalHerb 12g, HazardGear 60g", "Ration 30g, IronOre 2g, MedicinalHerb 3g, HazardGear 60g")]
        [InlineData("generous", "Ration 5g, IronOre 35g, MedicinalHerb 40g, HazardGear 20g", "Ration 5g, IronOre 9g, MedicinalHerb 10g, HazardGear 20g")]
        [InlineData("keen", "Ration 20g, IronOre 18g, MedicinalHerb 22g, HazardGear 35g", "Ration 20g, IronOre 4g, MedicinalHerb 5g, HazardGear 35g")]
        public void EachProfilePricesAShareOfTheFairPrices(string profile, string today, string day)
        {
            Assert.Equal(today, EconomySoak.Resolve(profile, Simpiens.Simulation.Economy.EconomyTuning.Default).PriceText());
            Assert.Equal(day, EconomySoak.Resolve(profile, EconomySoak.Pace.Day.Economy).PriceText());
        }

        /// <summary>
        /// Drifters open the economy (Epic 10 G1): for a slightly greedy player, on every soak seed they leave gold behind
        /// (their purses exceed what they take away), and both runs, open and closed, stay sound. G1 also asserted that the
        /// keen player's treasury, summed over the soak seeds, ends higher than in a closed outpost; since glowstone (G2)
        /// that comparison is within the noise of a chaotic day (measured 2026-10-25 over seeds 1-15: 19,355g against
        /// 19,249g, ahead on 8 of 15), so it is reported, not asserted (Epic 10 finding F4).
        /// </summary>
        [Fact]
        public void DriftersLeaveGoldBehindForAKeenPlayer()
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
            TestContext.Current.TestOutputHelper?.WriteLine($"keen over the soak seeds: {open}g with drifters, {closed}g closed (reported, not asserted: F4)");
        }
    }
}
