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
    /// Epic 10 Part 2's forge, live and headless (W1's DoD, <c>AForgeTurnsOreIntoPickaxes</c>; W1.5a's
    /// <c>AForgeIsBuiltAndWorked</c> folded in): on the real outpost with nothing injected, a scripted player that acts only
    /// through commands builds the forge once the stall can pay, hires a resident who qualifies, gives it the day shift and
    /// orders pickaxes; the artisan works its shifts and makes them from the stall's ore; a resident who mines buys one
    /// and mines faster with it. The construction is a sink the ledger names, the gold balances every tick, and the days'
    /// checks hold.
    /// </summary>
    public class WorkshopScenarioTests
    {
        private const long Horizon = 4 * DayClock.TicksPerDay;

        [Fact]
        public void AForgeTurnsOreIntoPickaxes()
        {
            using var sim = LocalSimulation.Create(SimulationSettings.DefaultSeed);
            sim.Respawn(OutpostSession.SessionId);
            var player = new WorkshopPlayer(sim, StationKind.Forge, ItemType.ToolPickaxe);
            var watch = new OutpostWatch(sim);
            var workshops = sim.Simulation.Workshops;
            int forge = workshops.IndexOf(StationKind.Forge);
            int makesBegun = 0;
            bool lastTaken = false;
            long firstPurchase = -1, firstMinedWithOne = -1;
            while (sim.Clock.CurrentTick < Horizon && (workshops[forge].Made < 2 || firstMinedWithOne < 0))
            {
                watch.Run(1);
                bool taken = workshops[forge].InputsTaken; // a make takes its three ore from the stall's stock as it begins
                if (taken && !lastTaken) makesBegun++;
                lastTaken = taken;
                if (firstPurchase < 0 && sim.Host.CrawlerStates.Any(s => s.Activity.ToolPurchases > 0)) firstPurchase = sim.Clock.CurrentTick;
                if (firstMinedWithOne < 0 && sim.Host.CrawlerStates.Any(s => s.Activity.ToolMinedUnits > 0)) firstMinedWithOne = sim.Clock.CurrentTick;
            }
            watch.Finish();
            var ledger = sim.Simulation.EconomyLedger;
            var buyers = sim.Host.CrawlerStates.Where(s => s.Activity.ToolPurchases > 0).ToList();
            TestContext.Current.TestOutputHelper?.WriteLine($"built t{player.BuiltTick}, hired t{player.FirstHireTick} ({player.Hired.Count}), first order t{player.FirstOrderTick}; " +
                $"made {workshops[forge].Made} (makes begun {makesBegun}); first bought t{firstPurchase}, first mined with one t{firstMinedWithOne}; {buyers.Count} buyers; " +
                $"ended t{sim.Clock.CurrentTick} (day {DayClock.Day(sim.Clock.CurrentTick)})\n" + watch.Report());

            Assert.True(player.BuiltTick >= 0 && workshops[forge].Built, "The player built the forge.");
            Assert.True(player.FirstHireTick > player.BuiltTick && workshops.ArtisanAt(forge) != default, "It hired an artisan who qualified, after the forge stood.");
            Assert.True(workshops.TryGetContract(workshops.ArtisanAt(forge), out var contract) && contract.Shifts == ShiftBlocks.DayShift, "The artisan works the day shift.");
            Assert.True(workshops[forge].Made >= 2 && makesBegun >= workshops[forge].Made, $"Two pickaxes made, each from the stall's ore (made {workshops[forge].Made}, makes begun {makesBegun}).");
            Assert.True(buyers.Count >= 1 && buyers.All(b => !b.IsDrifter), "A resident bought one.");
            Assert.True(firstMinedWithOne >= 0, "A buyer mined with its pickaxe, faster (ToolValidator times it).");
            Assert.Equal(sim.Simulation.Economy.Workshops.ConstructionCost, ledger.GetSinkTotal(WorkshopService.ConstructionSinkName));
            Assert.Equal(0, watch.ConservationViolations);
            var failures = watch.DayFailures();
            Assert.True(failures.Count == 0, "The days failed:\n  " + string.Join("\n  ", failures));
        }
    }
}
