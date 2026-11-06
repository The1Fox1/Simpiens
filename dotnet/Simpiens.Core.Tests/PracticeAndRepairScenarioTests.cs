using System.Linq;
using Simpiens.Core.Session;
using Simpiens.Core.Session.Layouts;
using Simpiens.Hosting;
using Simpiens.Simulation;
using Simpiens.Simulation.Economy;
using Simpiens.Simulation.Guild;
using Simpiens.Simulation.Items;
using Simpiens.Simulation.Workshops;
using Xunit;

namespace Simpiens.Core.Tests
{
    /// <summary>
    /// W4's DoD (Epic 10 Part 2), live and headless with nothing injected, on the default seed:
    /// <c>PracticeMakesBetterGear</c>, the forge's smith grows practised and its later pickaxes, made Fine or better, last
    /// longer in the hands of the residents who buy them; <c>TheForgeMendsWornGear</c>, the guild's worn respirators wait on
    /// the stall's shelf and the forge mends them for half their inputs, without the glowstone a new one takes.
    /// </summary>
    public class PracticeAndRepairScenarioTests
    {
        [Fact]
        public void PracticeMakesBetterGear()
        {
            using var sim = LocalSimulation.Create(SimulationSettings.DefaultSeed);
            sim.Respawn(OutpostSession.SessionId);
            var forgePlayer = new WorkshopPlayer(sim, StationKind.Forge, ItemType.ToolPickaxe);
            var watch = new OutpostWatch(sim);
            var workshops = sim.Simulation.Workshops;
            int forge = workshops.IndexOf(StationKind.Forge);
            ItemInstance first = default, better = default;
            long fineAt = -1, firstAt = -1, betterAt = -1;
            var holding = new System.Collections.Generic.HashSet<EntityId>();
            while (sim.Clock.CurrentTick < 12 * DayClock.TicksPerDay && betterAt < 0)
            {
                watch.Run(1);
                var smith = workshops.ArtisanAt(forge);
                if (fineAt < 0 && smith != default && workshops.TryGetContract(smith, out var contract) && WorkshopService.GradeOf(contract, sim.Simulation.Economy.Workshops) >= Grades.Fine) fineAt = sim.Clock.CurrentTick;
                foreach (var crawler in sim.Host.CrawlerStates)
                {
                    var tool = crawler.Inventory.GetEquipped(EquipmentSlot.Tool);
                    if (tool.Type != ItemType.ToolPickaxe) { holding.Remove(crawler.AgentId); continue; }
                    if (!holding.Add(crawler.AgentId)) continue; // a pickaxe newly taken up
                    if (firstAt < 0) { first = tool; firstAt = sim.Clock.CurrentTick; }
                    if (tool.Quality >= Grades.Fine && betterAt < 0) { better = tool; betterAt = sim.Clock.CurrentTick; }
                }
            }
            watch.Finish();
            TestContext.Current.TestOutputHelper?.WriteLine($"forge built t{forgePlayer.BuiltTick}; first pickaxe taken up t{firstAt} ({Grades.NameOf(first.Quality)}, {first.Durability}); the smith Fine at t{fineAt} (day {DayClock.Day(fineAt)}); a {Grades.NameOf(better.Quality)} one taken up t{betterAt} (day {DayClock.Day(betterAt)}), {better.Durability}; made {workshops[forge].Made}\n{watch.Report()}");

            Assert.True(firstAt >= 0 && first.Quality == Grades.Standard && first.Durability == 40, "The first pickaxe a resident took up was Standard, 40 units.");
            Assert.True(fineAt > firstAt, "The smith grew practised: Fine after ten makes.");
            Assert.True(betterAt > fineAt && better.Quality >= Grades.Fine, "A later pickaxe was made Fine or better.");
            Assert.True(better.Durability > first.Durability, $"It lasts longer ({better.Durability} units against {first.Durability}).");
            Assert.Equal(0, watch.ConservationViolations);
            var failures = watch.DayFailures();
            Assert.True(failures.Count == 0, "The days failed:\n  " + string.Join("\n  ", failures));
        }

        [Fact]
        public void TheForgeMendsWornGear()
        {
            using var sim = LocalSimulation.Create(SimulationSettings.DefaultSeed);
            sim.Respawn(OutpostSession.SessionId);
            var forgePlayer = new WorkshopPlayer(sim, StationKind.Forge, ItemType.Respirator);
            var guild = new GuildPlayer(sim, targetSize: 4);
            guild.SendExpeditions(Floor1Layout.DrownedGallerySectorId, Floor1Layout.GallerySeamYield, RetreatRule.Steady);
            var workshops = sim.Simulation.Workshops;
            int forge = workshops.IndexOf(StationKind.Forge);
            long firstWorn = -1, mendBegun = -1, mended = -1;
            int glowstoneBefore = 0, glowstoneAfter = 0, wornBefore = 0, wornAfter = 0;
            bool wasTaken = false;
            ushort remaining = 0;
            while (sim.Clock.CurrentTick < 10 * DayClock.TicksPerDay && mended < 0)
            {
                var before = sim.Simulation.ActiveStalls[0];
                sim.Step(1);
                var stall = sim.Simulation.ActiveStalls[0];
                var station = workshops[forge];
                if (firstWorn < 0 && stall.WornOf(ItemType.Respirator) > 0) firstWorn = sim.Clock.CurrentTick;
                bool taken = station.Repair && station.InputsTaken;
                if (taken && !wasTaken && mendBegun < 0)
                {
                    mendBegun = sim.Clock.CurrentTick;
                    glowstoneBefore = before.StockOf(ItemType.Glowstone);
                    glowstoneAfter = stall.StockOf(ItemType.Glowstone);
                    wornBefore = before.WornOf(ItemType.Respirator);
                    wornAfter = stall.WornOf(ItemType.Respirator);
                    remaining = station.Remaining;
                }
                if (mendBegun >= 0 && mended < 0 && (!station.Repair || station.Remaining < remaining)) mended = sim.Clock.CurrentTick;
                wasTaken = taken;
            }
            TestContext.Current.TestOutputHelper?.WriteLine($"respirators made {workshops[forge].Made}; first worn one on the shelf t{firstWorn} (day {DayClock.Day(firstWorn)}); a mend begun t{mendBegun}: worn {wornBefore} -> {wornAfter}, glowstone {glowstoneBefore} -> {glowstoneAfter}; mended t{mended}; mend orders {forgePlayer.Repairs}");

            Assert.True(firstWorn >= 0, "The guild handed a worn respirator back to the stall's shelf.");
            Assert.True(mendBegun > firstWorn, "The forge began to mend it.");
            Assert.Equal(wornBefore - 1, wornAfter);
            Assert.True(glowstoneAfter >= glowstoneBefore, "Mending took no glowstone (a new respirator takes one).");
            var recipe = RecipeBook.For(ItemType.Respirator);
            Assert.Equal(new[] { 0, 1 }, Enumerable.Range(0, recipe.InputCount).Select(i => Crafting.InputCount(recipe.GetInput(i), repair: true)).ToArray()); // half, rounded down: no glowstone, one herb
            Assert.True(mended > mendBegun, "The mended respirator went back into stock, full.");
            Assert.Equal(0, sim.Simulation.IntentFaults);
        }
    }
}
