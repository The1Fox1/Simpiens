using System.Diagnostics;
using Simpiens.Presentation;
using Xunit;

namespace Simpiens.Client.Tests
{
    /// <summary>
    /// The frame-time gate (review T3): the outpost in the game's view must stay far inside the 30 fps budget on Mesa's
    /// software renderer, which is slower than any GPU. The budget is generous (the frame takes about 1.6 ms on the
    /// development machine, and CI's runners are slower), so it catches a change that multiplies the frame's cost, not
    /// noise. <see cref="FrameTimeProbe"/> measures; this fails.
    /// </summary>
    public class FrameTimeGateTests
    {
        /// <summary>The mean frame, wall time with the GPU flushed, in milliseconds.</summary>
        public const double MeanBudget = 10.0;

        /// <summary>No frame's CPU time above the 30 fps budget, at the 99th percentile.</summary>
        public const double P99Budget = 33.0;

        [Fact]
        public void TheIsometricOutpostDrawsFarInsideTheFrameBudget()
        {
            var game = Offscreen.Start(new ClientOptions { View = ViewMode.Isometric, TicksPerUpdate = 1 }, frames: 0);
            game.RunOneFrame();
            game.Play.Dashboard.TogglePanels(); // the map with the header over it
            AssertInsideTheBudget(game, "isometric at 800x600");
        }

        /// <summary>Epic UI-Beta B4's gate: the map with the inspector's card, its portrait and bars, over it.</summary>
        [Fact]
        public void TheMapWithTheInspectorDrawsFarInsideTheFrameBudget()
        {
            var game = Offscreen.Start(new ClientOptions { View = ViewMode.Isometric, TicksPerUpdate = 1 }, frames: 0);
            game.RunOneFrame();
            game.Play.Dashboard.TogglePanels();
            var crawler = System.Linq.Enumerable.First(game.Session.Simulation.Registry.ActiveEntities, e => e.Type == Simpiens.Simulation.Spatial.EntityType.Pawn);
            game.Session.Selection.SelectCrawler(crawler.Id, follow: true);
            game.RunOneFrame();
            Assert.True(game.Play.Dashboard.Inspector.Root.Visible);
            AssertInsideTheBudget(game, "isometric at 800x600 with the inspector");
        }

        /// <summary>Epic UI-Beta B4's gate: the dashboard with faces on the residents' cards, the dossier's portrait and its affinities' faces.</summary>
        [Fact]
        public void TheDashboardWithFacesDrawsFarInsideTheFrameBudget()
        {
            var game = Offscreen.Start(new ClientOptions { View = ViewMode.Isometric, TicksPerUpdate = 1 }, frames: 0, width: 1280, height: 720);
            game.RunOneFrame();
            game.Session.DashboardModel.SelectSector(Simpiens.Core.Session.Layouts.Floor1Layout.RoadGateSectorId); // every crawler, at the start
            var crawler = System.Linq.Enumerable.First(game.Session.Simulation.Registry.ActiveEntities, e => e.Type == Simpiens.Simulation.Spatial.EntityType.Pawn);
            game.Session.DashboardModel.SelectCrawler(crawler.Id);
            game.RunOneFrame();
            Assert.True(game.Play.Dashboard.Panels.Portrait.Visible);
            AssertInsideTheBudget(game, "the dashboard at 1280x720 with faces");
        }

        private static void AssertInsideTheBudget(SimpiensGame game, string what)
        {
            for (int i = 0; i < 200; i++) game.RunOneFrame(); // warm up: the code compiled, a crowd on the move
            game.Timings.Clear();

            const int frames = 200;
            var watch = Stopwatch.StartNew();
            for (int i = 0; i < frames; i++)
            {
                game.RunOneFrame();
                game.GraphicsDevice.Flush();
            }
            watch.Stop();
            double mean = watch.Elapsed.TotalMilliseconds / frames;
            double p99 = game.Timings.FramePercentile(99);
            TestContext.Current.TestOutputHelper.WriteLine($"{what}: mean {mean:F2} ms (budget {MeanBudget}), CPU p99 {p99:F2} ms (budget {P99Budget})");

            Assert.True(mean <= MeanBudget, $"The mean frame took {mean:F2} ms, over the gate's {MeanBudget} ms (FrameTimeProbe shows where the time goes).");
            Assert.True(p99 <= P99Budget, $"The 99th percentile frame took {p99:F2} ms on the CPU, over the {P99Budget} ms budget.");
        }
    }
}
