using System.Linq;
using Simpiens.Core.Session;
using Simpiens.Core.Session.Layouts;
using Simpiens.Hosting;
using Simpiens.Simulation;
using Simpiens.Simulation.Economy;
using Xunit;

namespace Simpiens.Core.Tests
{
    /// <summary>
    /// The trade caravan on the map (Epic 10 Part 2, the stretch, D70): its place is a pure function of the clock and the
    /// supply schedule. It walks in from the road to reach the stall on each visit's tick, trades there a game hour, walks
    /// back out, and is away between visits; on the live outpost the supply visit happens with it at the stall.
    /// </summary>
    public class CaravanTests
    {
        private static readonly Vec2[] Route = Floor1Layout.Definition.CaravanRoute.ToArray();

        [Fact]
        public void ItArrivesOnTheVisitsTickTradesAndWalksBackOut()
        {
            uint interval = SupplyTuning.Default.RestockIntervalTicks;
            float length = Caravan.Length(Route);
            long walk = (long)System.Math.Ceiling(length / Caravan.UnitsPerTick);
            long visit = 2 * interval;

            Assert.Equal(CaravanStage.Away, Caravan.At(visit - walk - 1, interval, Route).Stage);
            var setOut = Caravan.At(visit - walk, interval, Route);
            Assert.True(setOut.Stage == CaravanStage.Arriving && setOut.Position == Route[0] && setOut.Progress == 0f, "It sets out from the road.");
            var close = Caravan.At(visit - 1, interval, Route);
            Assert.True(close.Stage == CaravanStage.Arriving && close.Progress > 0.95f, "A tick before the visit it is nearly there.");
            var trading = Caravan.At(visit, interval, Route);
            Assert.True(trading.Stage == CaravanStage.Trading && trading.Position == Route[Route.Length - 1], "On the visit's tick it is at the stall.");
            Assert.Equal(CaravanStage.Trading, Caravan.At(visit + Caravan.TradeTicks - 1, interval, Route).Stage);
            var leaving = Caravan.At(visit + Caravan.TradeTicks + walk / 2, interval, Route);
            Assert.True(leaving.Stage == CaravanStage.Leaving && leaving.Progress > 0.3f && leaving.Progress < 0.7f, "Halfway out, halfway along.");
            Assert.Equal(CaravanStage.Away, Caravan.At(visit + Caravan.TradeTicks + walk + 1, interval, Route).Stage);
            Assert.Equal(CaravanStage.Away, Caravan.At(visit + interval / 2, interval, Route).Stage);

            // No caravan comes for tick 0; the first walks in for the first visit
            Assert.Equal(CaravanStage.Away, Caravan.At(0, interval, Route).Stage);
            Assert.Equal(CaravanStage.Arriving, Caravan.At(interval - 1, interval, Route).Stage);

            // Its steps are small and steady: no jumps along the way, in or out
            for (long t = visit - walk; t < visit + Caravan.TradeTicks + walk; t++)
            {
                var a = Caravan.At(t, interval, Route);
                var b = Caravan.At(t + 1, interval, Route);
                if (a.IsOnTheMap && b.IsOnTheMap) Assert.True((b.Position - a.Position).Magnitude <= Caravan.UnitsPerTick + 1e-4f, $"A jump at t{t}.");
            }
        }

        [Fact]
        public void OnTheLiveOutpostTheVisitHappensWithTheCaravanAtTheStall()
        {
            using var sim = LocalSimulation.Create(SimulationSettings.DefaultSeed);
            sim.Respawn(OutpostSession.SessionId);
            var route = sim.Host.Current.Map.CaravanRoute;
            uint interval = sim.Simulation.Economy.Supply.RestockIntervalTicks;
            var supply = sim.Simulation.Supply;
            while (sim.Clock.CurrentTick < interval - 1) sim.Step(1);
            int before = supply.UpkeepPaidCount + supply.UpkeepShortCount; // every visit takes its upkeep, paid or short
            Assert.Equal(CaravanStage.Arriving, Caravan.At(sim.Clock.CurrentTick, interval, route).Stage);
            sim.Step(1);
            Assert.Equal(CaravanStage.Trading, Caravan.At(sim.Clock.CurrentTick, interval, route).Stage);
            Assert.True(supply.UpkeepPaidCount + supply.UpkeepShortCount > before, "The visit happened on the tick the caravan reached the stall.");
            Assert.True(sim.Simulation.ActiveStalls.Count > 0 && Vec2.Distance(route[route.Count - 1], sim.Simulation.ActiveStalls[0].Position) < 3f, "Its route ends at the stall.");
        }
    }
}
