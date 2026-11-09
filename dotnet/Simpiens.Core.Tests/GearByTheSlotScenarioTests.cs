using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using Simpiens.Cognition;
using Simpiens.Core.Session;
using Simpiens.Hosting;
using Simpiens.Simulation;
using Simpiens.Simulation.Economy;
using Simpiens.Simulation.Items;
using Xunit;

namespace Simpiens.Core.Tests
{
    /// <summary>
    /// K1's DoD, <c>GearByTheSlot</c> (Epic 10 Part 3), live and headless on the 23 gate seeds with nothing injected: the
    /// scripted workshop player builds a forge and orders hard hats, through commands, as a player would. A bold resident
    /// who works the Glow Caves' glowstone in hazard gear buys a hard hat for its bare head (K1.4: the best piece for a
    /// bare slot, by stats) and wears the two together (K1.3): on at least 15 of the 23 seeds within six days. K1.4
    /// measured 17; on the other six no venturer remembered the caves' glowstone (spent nodes regrow out of sight) when it
    /// had the gold. The ledger balances and no intent handler throws. The guild's and the workshops' scenarios hold
    /// beside it (ExpeditionTests, RightGearScenarioTests, WorkshopLoopScenarioTests).
    /// </summary>
    public class GearByTheSlotScenarioTests
    {
        private const long Horizon = 6 * DayClock.TicksPerDay;
        private const int AtLeast = 15;

        [Fact]
        public void AVenturerWearsAHardHatOverHazardGear()
        {
            var results = new ConcurrentDictionary<ulong, (long Tick, int Purchases, int Risk, int Violations, int Faults)>();
            Parallel.ForEach(OutpostWatch.GateSeeds, seed =>
            {
                using var sim = LocalSimulation.Create(seed);
                sim.Respawn(OutpostSession.SessionId);
                var forge = new WorkshopPlayer(sim, StationKind.Forge, ItemType.HardHat);
                var watch = new OutpostWatch(sim);
                AgentState wearer = null;
                while (sim.Clock.CurrentTick < Horizon && wearer == null)
                {
                    watch.Run(1);
                    wearer = sim.Host.CrawlerStates.FirstOrDefault(c => !c.IsDrifter
                        && c.Inventory.GetEquipped(EquipmentSlot.Head).Type == ItemType.HardHat
                        && c.Inventory.GetEquipped(EquipmentSlot.Body).Type == ItemType.HazardGear);
                }
                watch.Finish();
                results[seed] = (wearer != null ? sim.Clock.CurrentTick : -1, wearer?.Activity.GearPurchases ?? 0, wearer?.Personality.RiskTolerancePercent ?? 0,
                    watch.ConservationViolations, watch.IntentFaults);
            });

            foreach (ulong seed in OutpostWatch.GateSeeds)
            {
                var r = results[seed];
                TestContext.Current.TestOutputHelper?.WriteLine(r.Tick >= 0
                    ? $"seed {seed}: a hard hat over hazard gear at t{r.Tick} (day {DayClock.Day(r.Tick)}); risk {r.Risk}%, gear bought {r.Purchases}"
                    : $"seed {seed}: no hard hat over hazard gear in {Horizon} ticks");
                Assert.Equal(0, r.Violations);
                Assert.Equal(0, r.Faults);
                if (r.Tick < 0) continue;
                Assert.True(r.Risk >= Vitality.VentureRiskTolerancePercent, $"Seed {seed}: the wearer is a venturer (risk {r.Risk}%).");
                Assert.True(r.Purchases >= 2, $"Seed {seed}: it bought both pieces ({r.Purchases}).");
            }
            int seeds = OutpostWatch.GateSeeds.Count(s => results[s].Tick >= 0);
            Assert.True(seeds >= AtLeast, $"A venturer wore a hard hat over hazard gear on {seeds} of {OutpostWatch.GateSeeds.Length} gate seeds; at least {AtLeast} expected.");
        }
    }
}
