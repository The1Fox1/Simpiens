using System.Collections.Generic;
using System.Linq;
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
    /// K4's DoD, <c>KillsFeedTheForge</c> (Epic 10 Part 3), live and headless on a gate seed with nothing injected: the scripted
    /// workshop player builds a forge and orders iron blades, and armour whenever the stall holds a kill's materials for it
    /// (a leather jerkin from two hides, a chitin plate from two chitin and an ore), through commands, as a player would.
    /// A bold resident buys a blade and slays a creature; its drop is picked up and sold to the stall; the forge makes armour
    /// from it; a resident buys it and wears it. The days hold, the ledger balances, no intent handler throws.
    /// </summary>
    public class KillsFeedTheForgeTests
    {
        private const long Horizon = 10 * DayClock.TicksPerDay;

        /// <summary>What a run saw: the kills, the units picked up, the materials the stall bought, the armour made and worn.</summary>
        private sealed class Run
        {
            public int Kills, PickedUp, MaterialsBought, ArmourMade;
            public long WornAt = -1;
            public EntityId Wearer;
            public ItemType Armour;
            public OutpostWatch Watch;
            public override string ToString() => $"kills {Kills}, picked up {PickedUp}, materials bought {MaterialsBought}, armour made {ArmourMade}, worn {(WornAt >= 0 ? $"at t{WornAt} ({Armour})" : "never")}";
        }

        private static readonly ItemType[] Armour = { ItemType.LeatherJerkin, ItemType.ChitinPlate };
        private static readonly ItemType[] Materials = { ItemType.Hide, ItemType.Chitin };

        private static Run Play(ulong seed, bool untilWorn)
        {
            using var sim = LocalSimulation.Create(seed);
            sim.Respawn(OutpostSession.SessionId);
            var player = new WorkshopPlayer(sim, StationKind.Forge, ItemType.IronBlade).Also(ItemType.LeatherJerkin).Also(ItemType.ChitinPlate);
            var watch = new OutpostWatch(sim);
            var run = new Run { Watch = watch };
            int lastMaterials = 0, lastArmour = 0;
            while (sim.Clock.CurrentTick < Horizon && (!untilWorn || run.WornAt < 0))
            {
                watch.Run(1);
                var stall = sim.Simulation.ActiveStalls[0];
                int materials = Materials.Sum(m => stall.StockOf(m));
                if (materials > lastMaterials) run.MaterialsBought += materials - lastMaterials;
                lastMaterials = materials;
                int armour = Armour.Sum(a => stall.SellStockOf(a));
                if (armour > lastArmour) run.ArmourMade += armour - lastArmour;
                lastArmour = armour;
                if (run.WornAt >= 0) continue;
                foreach (var crawler in sim.Host.CrawlerStates)
                {
                    var body = crawler.Inventory.GetEquipped(EquipmentSlot.Body).Type;
                    if (crawler.IsDrifter || System.Array.IndexOf(Armour, body) < 0) continue;
                    run.WornAt = sim.Clock.CurrentTick;
                    run.Wearer = crawler.AgentId;
                    run.Armour = body;
                    break;
                }
            }
            watch.Finish();
            run.Kills = watch.Kills;
            run.PickedUp = sim.Simulation.Piles.UnitsTaken;
            return run;
        }

        /// <summary>
        /// Gate seed 15: K4.4 measured armour from a kill worn on 5 of the 23 gate seeds in ten days; with skills (K9) on 2, this
        /// one first at t74143 (a chitin plate) and 424242. Noise, not skills: on seeds 21 to 80 it was worn on 18 with skills
        /// and 19 without.
        /// </summary>
        private const ulong Seed = 15;

        [Fact]
        public void AKillsMaterialsBecomeArmourAResidentWears()
        {
            var run = Play(Seed, untilWorn: true);
            var watch = run.Watch;
            TestContext.Current.TestOutputHelper?.WriteLine($"{run}\n{watch.CreatureSummary()}\n{watch.Report()}");

            Assert.True(run.Kills > 0, "A creature was slain.");
            Assert.True(run.PickedUp > 0, "Its drop was picked up.");
            Assert.True(run.MaterialsBought > 0, "The stall bought its materials.");
            Assert.True(run.ArmourMade > 0, "The forge made armour from them.");
            Assert.True(run.WornAt >= 0, $"A resident wears it within {Horizon} ticks.");
            Assert.Equal(0, watch.ConservationViolations);
            Assert.Equal(0, watch.IntentFaults);
            var failures = watch.DayFailures();
            Assert.True(failures.Count == 0, "The days failed:\n  " + string.Join("\n  ", failures));
        }

        /// <summary>The measurement behind the seed: every gate seed's ten days (explicit, a few minutes).</summary>
        [Fact(Explicit = true)]
        public void MeasureKillsFeedTheForgeOnTheGateSeeds()
        {
            int worn = 0;
            foreach (var seed in OutpostWatch.GateSeeds)
            {
                var run = Play(seed, untilWorn: false);
                if (run.WornAt >= 0) worn++;
                var failures = run.Watch.DayFailures();
                TestContext.Current.TestOutputHelper?.WriteLine($"seed {seed}: {run}; downed by creatures {run.Watch.DownedByCreatures}, deaths {run.Watch.Deaths}; days {(failures.Count == 0 ? "hold" : "FAIL: " + string.Join("; ", failures))}");
            }
            TestContext.Current.TestOutputHelper?.WriteLine($"armour from a kill worn on {worn} of {OutpostWatch.GateSeeds.Length} seeds");
        }
    }
}
