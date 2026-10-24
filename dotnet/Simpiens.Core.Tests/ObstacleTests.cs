using Simpiens.Core.Session;
using Simpiens.Core.Session.Layouts;
using Simpiens.Hosting;
using Simpiens.Simulation;
using Xunit;

namespace Simpiens.Core.Tests
{
    /// <summary>
    /// Walls dropped into doors the crawlers are streaming through (Epic CLIENT M2.8; Unity's DynamicChaosScenario): the
    /// unstuck reflexes recover with at most two transient jitters, nobody clips into a wall or leaves the map. First the
    /// Hub - Bunkhouse door, then the Ore Quarry - Hub door, as the crawlers walk out of the Road Gate. Unity's timeline
    /// at 2.5x, in ticks: the first wall at 25, the second 25 ticks after it, then 100 more.
    /// </summary>
    public class ObstacleTests
    {
        // Spots across a doorway (offsets along the door from its centre, half a cell either side of the wall line)
        private static readonly Vec2[] DoorSpots =
        {
            new Vec2(-0.5f, 0.5f), new Vec2(0.5f, 0.5f), new Vec2(-1.5f, 0.5f), new Vec2(1.5f, 0.5f),
            new Vec2(-0.5f, -0.5f), new Vec2(0.5f, -0.5f), new Vec2(-1.5f, -0.5f), new Vec2(1.5f, -0.5f)
        };

        /// <summary>
        /// An injected wall blocks the 3x3 cells around it, reaching 1.5 units from its centre along an axis; a pawn
        /// closer than this is inside the wall the moment it appears. Beyond it, the wall drops in front of the pawn.
        /// </summary>
        private const float PawnClearance = 2.0f;
        private const int MaxWaitTicks = 300;

        [Fact]
        public void CrawlersRecoverFromWallsDroppedIntoDoors()
        {
            using var sim = LocalSimulation.Create(SimulationSettings.DefaultSeed);
            sim.Respawn(OutpostSession.SessionId);
            var detector = AnomalyDetector.For(sim);
            var injector = EdgeCaseInjector.For(sim);
            void Run(int ticks) { for (int t = 0; t < ticks; t++) { sim.Step(1); detector.Observe(sim.Clock.CurrentTick); } }

            Run(25);
            Inject(sim, injector, Floor1Layout.HubBunkhouseDoor, verticalWall: false, Run);
            Run(25);
            Inject(sim, injector, Floor1Layout.Doors[3], verticalWall: true, Run); // Ore Quarry - Hub
            Run(100);

            TestContext.Current.TestOutputHelper?.WriteLine(detector.Summary());
            Assert.True(detector.JitterCount <= 2, $"Excessive jitter recovering from the walls: {detector.Summary()}; first {detector.First}");
            Assert.True(detector.PenetrationCount == 0, $"A crawler clipped into a wall: {detector.First}");
            Assert.True(detector.BoundaryBreachCount == 0, $"A crawler left the map: {detector.First}");
        }

        private static void Inject(LocalSimulation sim, EdgeCaseInjector injector, Vec2 door, bool verticalWall, System.Action<int> run)
        {
            for (int tick = 0; tick < MaxWaitTicks; tick++)
            {
                for (int s = 0; s < DoorSpots.Length; s++)
                {
                    var offset = DoorSpots[s];
                    var spot = door + (verticalWall ? new Vec2(offset.Y, offset.X) : offset);
                    if (!IsClearOfPawns(sim, spot)) continue;
                    injector.InjectWall(spot);
                    TestContext.Current.TestOutputHelper?.WriteLine($"wall at {spot} (door {door}) at t{sim.Clock.CurrentTick}");
                    return;
                }
                run(1);
            }
            Assert.Fail($"No spot in the door at {door} was clear of pawns for {MaxWaitTicks} ticks.");
        }

        private static bool IsClearOfPawns(LocalSimulation sim, Vec2 spot)
        {
            var crawlers = sim.Host.CrawlerStates;
            for (int i = 0; i < crawlers.Count; i++)
            {
                if (Vec2.Distance(crawlers[i].Position, spot) < PawnClearance) return false;
            }
            return true;
        }
    }
}
