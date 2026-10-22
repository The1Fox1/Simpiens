using System.Linq;
using Gum.Forms.Controls;
using Gum.Wireframe;
using Simpiens.Core.Session.Layouts;
using Simpiens.Presentation;
using Simpiens.Presentation.Dashboard;
using Simpiens.Simulation;
using Simpiens.Simulation.Economy;
using Xunit;

namespace Simpiens.Client.Tests
{
    /// <summary>
    /// The bounty board (Epic CLIENT M2.5), with Unity's PlayerScoutScenario ported as the player's Definition of Done:
    /// on the real outpost, through the UI only, with nothing injected.
    /// </summary>
    public class BountyBoardTests
    {
        private const int Width = 1280, Height = 720;

        /// <summary>The Overgrowth is charted within this many ticks of the post (Unity's bound).</summary>
        private const long ChartDeadlineTicks = 520;
        /// <summary>Its fog is gone within this much view time once charted (Unity's bound; the fade is 1.5 s).</summary>
        private const float LiftSeconds = 2f;

        private static Vec2 Centre(FrameworkElement control)
            => new Vec2(control.AbsoluteLeft + control.ActualWidth * 0.5f, control.AbsoluteTop + control.ActualHeight * 0.5f);

        /// <summary>Opens a drop-down with a click and clicks the item that reads <paramref name="label"/>.</summary>
        private static void Choose(UiDriver ui, ComboBox box, string label)
        {
            int index = box.Items.Cast<object>().Select(o => o?.ToString()).ToList().IndexOf(label);
            Assert.True(index >= 0, $"'{label}' is not offered.");
            ui.Click(Centre(box));
            var list = (box.Visual as Gum.Forms.DefaultVisuals.V3.ComboBoxVisual)?.ListBoxInstance is InteractiveGue gue ? gue.FormsControlAsObject as ListBox : null;
            Assert.NotNull(list);
            ui.Click(Centre(list.ListBoxItems[index]));
            Assert.Equal(index, box.SelectedIndex);
        }

        private static UiDriver BoardOverTheMap()
        {
            var ui = UiDriver.Start(width: Width, height: Height);
            ui.Game.Play.Dashboard.TogglePanels();
            ui.Frame();
            ui.Click(Centre(ui.Game.Play.Dashboard.Header.BountiesButton));
            Assert.True(ui.Game.Play.Dashboard.Bounties.Window.IsShown);
            return ui;
        }

        private static ushort FindScout(ISimulationManager sim, int sectorId)
            => sim.ActiveBounties.Where(b => b.Type == BountyType.ScoutSector && b.TargetSectorId == sectorId).Select(b => b.BountyId).FirstOrDefault();

        [Fact]
        public void ThePlayerScoutsTheOvergrowthFromTheMapAndItsFogLifts()
        {
            var ui = BoardOverTheMap();
            var game = ui.Game;
            var sectors = game.Session.Simulation.Sectors;
            var sim = game.Session.Simulation.Simulation;
            var board = game.Play.Dashboard.Bounties;
            int overgrowth = Floor1Layout.OvergrowthSectorId;

            Choose(ui, board.TypeBox, nameof(BountyType.ScoutSector));
            Assert.False(board.ItemFieldsShown, "Scout has no item or quantity.");
            var uncharted = sectors.All.Where(s => !sectors.IsExplored(s.Id)).Select(s => (int)s.Id).OrderBy(i => i).ToArray();
            Assert.Equal(uncharted, game.Session.DashboardModel.Bounties.SectorChoiceIds.OrderBy(i => i).ToArray());
            Choose(ui, board.SectorBox, "Overgrowth");
            Assert.Equal("Uncharted", game.Session.DashboardModel.Bounties.SectorState);
            int reward = (int)board.RewardSlider.Value;
            Assert.Equal(BountyBoardModel.DefaultReward, reward);
            ui.Click(Centre(board.PostButton));
            Assert.StartsWith("Posted Scout Sector contract", board.FeedbackText);
            ui.Click(Centre(board.Window.CloseButton));
            Assert.False(board.Window.IsShown);

            long start = game.Session.Simulation.Clock.CurrentTick;
            ushort bountyId = 0;
            long claimed = -1;
            while (game.Session.Simulation.Clock.CurrentTick - start < ChartDeadlineTicks && !sectors.IsExplored(overgrowth))
            {
                ui.Ticks(1);
                if (bountyId == 0) bountyId = FindScout(sim, overgrowth);
                if (bountyId != 0 && claimed < 0 && sim.ActiveBounties.Any(b => b.BountyId == bountyId && b.Status != BountyStatus.Open)) claimed = game.Session.Simulation.Clock.CurrentTick - start;
            }
            long charted = game.Session.Simulation.Clock.CurrentTick - start;
            Assert.True(bountyId != 0, "The posted Scout Overgrowth bounty reached the simulation.");
            Assert.True(sectors.IsExplored(overgrowth), $"The Overgrowth was not charted within {ChartDeadlineTicks} ticks (claimed at {claimed}).");

            // Its fog fades on the map in view time (tick-based here: 0.1 s a tick) and is then gone
            float liftStart = game.Session.ViewSeconds;
            Assert.True(game.World.Scene.Fog.HasOverlay(overgrowth) && !game.World.Scene.Fog.IsCovered(overgrowth), "The fog is fading on the frame after the chart.");
            while (game.World.Scene.Fog.HasOverlay(overgrowth) && game.Session.ViewSeconds - liftStart <= LiftSeconds * 3f) ui.Ticks(1);
            float lifted = game.Session.ViewSeconds - liftStart;
            Assert.False(game.World.Scene.Fog.HasOverlay(overgrowth), "The Overgrowth's fog never lifted.");
            Assert.True(lifted <= LiftSeconds, $"The fog took {lifted:F2} s to lift (limit {LiftSeconds} s).");
            TestContext.Current.TestOutputHelper?.WriteLine($"posted Scout Overgrowth for {reward}g; claimed@{claimed}, charted@{charted}, fog gone after {lifted:F2}s");

            // Once the seeded scout charts the Deep Seam too, Scout has nothing left to offer
            for (int i = 0; i < 150 && !sectors.IsExplored(Floor1Layout.DeepSeamSectorId); i++) ui.Ticks(20);
            Assert.True(sectors.IsExplored(Floor1Layout.DeepSeamSectorId), "The Deep Seam was never charted.");
            ui.Click(Centre(game.Play.Dashboard.Header.BountiesButton));
            Assert.Equal(1, board.TypeBox.SelectedIndex);
            Assert.Empty(game.Session.DashboardModel.Bounties.SectorChoiceIds);
            Assert.False(board.PostButton.IsEnabled, "Post is disabled when there is nothing to scout.");
            Assert.Equal(BountyBoardModel.AllChartedText, board.FeedbackText);
        }

        [Fact]
        public void AGatherContractIsPostedAndCancelledFromItsCard()
        {
            var ui = BoardOverTheMap();
            var game = ui.Game;
            var board = game.Play.Dashboard.Bounties;
            int before = game.Session.Simulation.Simulation.ActiveBounties.Count;

            Assert.True(board.ItemFieldsShown);
            Choose(ui, board.SectorBox, "Ore Quarry");
            Choose(ui, board.ItemBox, nameof(ItemType.IronOre));
            ui.Click(Centre(board.PostButton));
            ui.Ticks(1);
            Assert.Equal(before + 1, game.Session.Simulation.Simulation.ActiveBounties.Count);
            var posted = game.Session.Simulation.Simulation.ActiveBounties.Last();
            Assert.Equal((BountyType.GatherResource, ItemType.IronOre, Floor1Layout.OreQuarrySectorId), (posted.Type, posted.TargetItemType, (byte)posted.TargetSectorId));

            var card = board.Cards.Take(board.ShownCards).Single(c => c.BountyId == posted.BountyId);
            Assert.StartsWith($"#{posted.BountyId} - Gather Salvage", card.Title);
            ui.Click(Centre(card.CancelButton));
            Assert.StartsWith($"Dispatched cancellation for contract #{posted.BountyId}", board.FeedbackText);
            ui.Ticks(1);
            Assert.DoesNotContain(game.Session.Simulation.Simulation.ActiveBounties, b => b.BountyId == posted.BountyId);
            Assert.True(game.Session.Simulation.Simulation is SimulationManager manager && manager.TryGetBounty(posted.BountyId, out var cancelled) && cancelled.Status == BountyStatus.Cancelled);
            Assert.DoesNotContain(board.Cards.Take(board.ShownCards), c => c.BountyId == posted.BountyId);
        }

        [Fact]
        public void TheBountyBoardLooksAsRecorded()
        {
            var game = Offscreen.Start(new ClientOptions { TicksPerUpdate = 100 }, frames: 3, Width, Height);
            game.Play.Dashboard.TogglePanels();
            game.Session.Simulation.Clock.SetTimeMultiplier(1f);
            game.Session.DashboardModel.OpenBounties();
            game.Play.Dashboard.Refresh();
            game.RunOneFrame();
            RenderTests.CompareWithReference("render-outpost-bounties-t300", FrameCapture.Capture(game, Width, Height), Width, Height);
        }
    }
}
