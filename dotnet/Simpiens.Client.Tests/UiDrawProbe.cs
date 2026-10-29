using System.Linq;
using RenderingLibrary.Graphics;
using Xunit;

namespace Simpiens.Client.Tests
{
    /// <summary>
    /// A measurement, not a gate (graphics V4.0): what the dashboard's draw costs, by cause. The dossier-panels frame of
    /// <see cref="FrameTimeProbe"/>, with Gum's own counters (sprite-batch begins, why each began, texture switches, the
    /// shape batch's begins), under Gum's default sibling ordering and under its batch-grouped ordering.
    /// Run: <c>-method "*UiDrawProbe*" -explicit only -showLiveOutput</c>.
    /// </summary>
    public class UiDrawProbe
    {
        [Theory(Explicit = true)]
        [InlineData(false)]
        [InlineData(true)]
        public void MeasureTheDashboardsDraw(bool grouped)
        {
            var previous = Renderer.SiblingOrdering;
            try
            {
                if (grouped) Renderer.SiblingOrdering = BatchKeyGroupedOrderer.Instance;
                var game = Offscreen.Start(new ClientOptions { TicksPerUpdate = 1 }, frames: 0, width: 1280, height: 720);
                game.Options.DashboardRefreshSeconds = 0f;
                for (int i = 0; i < 300; i++) game.RunOneFrame();
                var crawler = game.Session.Simulation.Registry.ActiveEntities.First(e => e.Type == Simpiens.Simulation.Spatial.EntityType.Pawn);
                game.Session.DashboardModel.SelectCrawler(crawler.Id);
                for (int i = 0; i < 20; i++) game.RunOneFrame();
                // Twice, reporting the second: the process's first measured frames run slow (about 5 ms against 3), whichever ordering runs first
                for (int pass = 0; pass < 2; pass++)
                {
                    game.Timings.Clear();
                    for (int i = 0; i < 300; i++)
                    {
                        game.RunOneFrame();
                        game.GraphicsDevice.Flush();
                    }
                }
                var summary = Gum.GumService.Default.Renderer.GetDrawStateSummary(); // the game's Gum, initialized by its first frames
                double ui = game.Timings.Mean(FramePhase.UiDraw);
                TestContext.Current.TestOutputHelper.WriteLine(
                    $"{(grouped ? "batch-grouped ordering" : "default ordering")}: UiDraw {ui:F3} ms; sprite-batch begins {summary.BeginCount} " +
                    $"(initial {summary.InitialBeginCount}, clip changes {summary.ClipChangeBeginCount}, state changes {summary.StateChangeBeginCount}), " +
                    $"texture switches {summary.TextureSetCount}, shape-batch begins {summary.ShapeBatchBeginCount}");
            }
            finally
            {
                Renderer.SiblingOrdering = previous;
            }
        }
    }
}
