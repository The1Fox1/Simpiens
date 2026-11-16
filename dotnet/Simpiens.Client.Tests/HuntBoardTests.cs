using System.Linq;
using Simpiens.Core.Session;
using Simpiens.Core.Session.Layouts;
using Simpiens.Hosting;
using Simpiens.Presentation.Dashboard;
using Simpiens.Simulation;
using Simpiens.Simulation.Economy;
using Xunit;

namespace Simpiens.Client.Tests
{
    /// <summary>
    /// A hunt on the bounty board (Epic 10 Part 3, K6.3), the model on the real outpost: the Hunt kind offers the charted lairs
    /// that hold a creature (none while none is charted), posts a hunt on the chosen one, and its card says whose lair it is
    /// and how many have joined.
    /// </summary>
    public class HuntBoardTests
    {
        [Fact]
        public void TheBoardPostsAHuntOnACharteredLair()
        {
            using var sim = LocalSimulation.Create(SimulationSettings.DefaultSeed);
            sim.Respawn(OutpostSession.SessionId);
            sim.Step(20);
            var model = new DashboardModel(DashboardContext.From(sim.Services));
            var board = model.Bounties;
            board.Open();
            board.SetType(BountyType.Hunt);
            Assert.True(board.TargetsLairs);
            Assert.Equal("Lair:", board.TargetLabel);
            Assert.False(board.CanPost); // no lair charted yet
            Assert.Equal(BountyBoardModel.NoLairText, board.Feedback.Text);

            // By the crawlers' rule: the Overgrowth charted and a resident held by the warren until it is charted
            sim.Sectors.SetExplored(Floor1Layout.OvergrowthSectorId, true);
            var watcher = sim.Host.CrawlerStates.Last(s => !s.IsDrifter);
            for (int t = 0; t < 200 && sim.Simulation.Creatures.ChartedMask == 0UL; t++)
            {
                watcher.Position = new Vec2(20f, 14f);
                sim.Step(1);
            }
            board.Refresh();
            Assert.True(board.CanPost);
            Assert.Contains("The Matriarch and the burrow rats of Overgrowth", board.SectorChoiceLabels); // her warren (K10.2a, K10.5d)
            board.SelectedSectorIndex = board.SectorChoiceLabels.ToList().IndexOf("The Matriarch and the burrow rats of Overgrowth");
            board.Reward = 60;
            board.Post();
            sim.Step(1);
            var hunt = Assert.Single(sim.Simulation.Hunts);
            board.Refresh();
            var row = board.Contracts.Single(r => r.BountyId == hunt.BountyId);
            Assert.Contains("Hunt a Lair", row.Title);
            Assert.Contains("Hunt the Matriarch and the burrow rats of Overgrowth · 0 of 3 hunters", row.Details);
        }
    }
}
