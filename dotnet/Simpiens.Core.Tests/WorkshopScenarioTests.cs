using System.Linq;
using Simpiens.Core.Session;
using Simpiens.Hosting;
using Simpiens.Simulation;
using Simpiens.Simulation.Economy;
using Simpiens.Simulation.Items;
using Simpiens.Simulation.Workshops;
using Xunit;

namespace Simpiens.Core.Tests
{
    /// <summary>
    /// Epic 10 Part 2's forge, live and headless (W1.5a, the DoD <c>AForgeIsBuiltAndWorked</c>): on the real outpost with
    /// nothing injected, a scripted player that acts only through commands builds the forge once the stall can pay, hires
    /// a resident who qualifies, gives it the day shift and orders pickaxes; the artisan works its shifts, and pickaxes
    /// made from the stall's ore go into the stall's stock. The construction is a sink the ledger names, the gold
    /// balances every tick, and the days' checks hold.
    /// </summary>
    public class WorkshopScenarioTests
    {
        private const long ThreeDays = 3 * DayClock.TicksPerDay;

        [Fact]
        public void AForgeIsBuiltAndWorked()
        {
            using var sim = LocalSimulation.Create(SimulationSettings.DefaultSeed);
            sim.Respawn(OutpostSession.SessionId);
            var player = new WorkshopPlayer(sim, StationKind.Forge, ItemType.ToolPickaxe);
            var watch = new OutpostWatch(sim);
            var workshops = sim.Simulation.Workshops;
            int forge = workshops.IndexOf(StationKind.Forge);
            var stall = sim.Simulation.ActiveStalls[0].StallEntityId;
            int oreTaken = 0, lastOre = sim.Simulation.ActiveStalls[0].StockOf(ItemType.IronOre);
            bool lastTaken = false;
            while (sim.Clock.CurrentTick < ThreeDays && sim.Simulation.ActiveStalls[0].StockOf(ItemType.ToolPickaxe) < 2)
            {
                watch.Run(1);
                bool taken = workshops[forge].InputsTaken;
                if (taken && !lastTaken) oreTaken++;
                lastTaken = taken;
            }
            watch.Finish();
            var ledger = sim.Simulation.EconomyLedger;
            int stocked = sim.Simulation.ActiveStalls[0].StockOf(ItemType.ToolPickaxe);
            TestContext.Current.TestOutputHelper?.WriteLine($"built t{player.BuiltTick}, hired t{player.FirstHireTick} ({player.Hired.Count}), first order t{player.FirstOrderTick}; " +
                $"made {workshops[forge].Made}, stocked {stocked} by t{sim.Clock.CurrentTick} (day {DayClock.Day(sim.Clock.CurrentTick)}); makes begun {oreTaken}\n" + watch.Report());

            Assert.True(player.BuiltTick >= 0 && workshops[forge].Built, "The player built the forge.");
            Assert.True(player.FirstHireTick > player.BuiltTick && workshops.ArtisanAt(forge) != default, "It hired an artisan who qualified, after the forge stood.");
            Assert.True(workshops.TryGetContract(workshops.ArtisanAt(forge), out var contract) && contract.Shifts == ShiftBlocks.DayShift, "The artisan works the day shift.");
            Assert.True(stocked >= 2 && workshops[forge].Made >= 2, $"Two pickaxes made and stocked within three days (made {workshops[forge].Made}, stocked {stocked}).");
            Assert.Equal(sim.Simulation.Economy.Workshops.ConstructionCost, ledger.GetSinkTotal(WorkshopService.ConstructionSinkName));
            Assert.Equal(0, watch.ConservationViolations);
            var failures = watch.DayFailures();
            Assert.True(failures.Count == 0, "The days failed:\n  " + string.Join("\n  ", failures));
        }
    }
}
