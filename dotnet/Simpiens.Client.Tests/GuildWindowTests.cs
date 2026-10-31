using System.Linq;
using Gum.Forms.Controls;
using Xunit;

namespace Simpiens.Client.Tests
{
    /// <summary>
    /// The guild on screen (Epic 10 G4, D6), driven with the scripted pointer on the live outpost: the dossier says who
    /// would sign on and its Recruit sends the command; the header counts members; the roster window shows them, sets a
    /// wage and dismisses. Every action leaves as a typed command, applied on the next tick.
    /// </summary>
    public class GuildWindowTests
    {
        private const int Width = 1280, Height = 720;

        /// <summary>
        /// How long the outpost may take for a resident to qualify: the default seed's first one does within its first few
        /// minutes, at a tick that moves with every change of behaviour, so the tests wait for it rather than name it.
        /// </summary>
        private const long QualifiesWithin = 9000;

        private static Vec2 Centre(FrameworkElement control)
            => new Vec2(control.AbsoluteLeft + control.ActualWidth * 0.5f, control.AbsoluteTop + control.ActualHeight * 0.5f);

        private static (UiDriver Ui, EntityId Recruit) RecruitFromTheDossier()
        {
            var ui = UiDriver.Start(width: Width, height: Height);
            var model = ui.Game.Session.DashboardModel;
            var panels = ui.Game.Play.Dashboard.Panels;
            var crawlers = ui.Game.Session.Simulation.Host.CrawlerStates;
            for (long waited = 0; !crawlers.Any(s => model.Guild.CanRecruit(s.AgentId)); waited += 100)
            {
                Assert.True(waited < QualifiesWithin, $"Nobody would sign on within {QualifiesWithin} ticks.");
                ui.Ticks(100);
            }
            var candidate = crawlers.First(s => model.Guild.CanRecruit(s.AgentId)).AgentId;
            model.SelectCrawler(candidate);
            ui.Frame();
            Assert.StartsWith("Will join", panels.RecruitShown);
            Assert.True(panels.RecruitButton.IsEnabled, "Recruit is enabled for a crawler who would sign on.");
            ui.Click(Centre(panels.RecruitButton));
            ui.Ticks(1);
            return (ui, candidate);
        }

        [Fact]
        public void RecruitingFromTheDossierFillsTheRosterAndTheRosterSetsWagesAndDismisses()
        {
            var (ui, recruit) = RecruitFromTheDossier();
            var game = ui.Game;
            var guild = game.Session.Simulation.Simulation.Guild;
            var dashboard = game.Play.Dashboard;
            Assert.True(guild.IsMember(recruit), "The Recruit button's command signed the crawler on.");
            Assert.Equal("GUILD 1/4", dashboard.Header.GuildButton.Text);
            Assert.Contains("Guild: member, 5g a visit", dashboard.Panels.GroupsShown);
            Assert.Equal(string.Empty, dashboard.Panels.RecruitShown);
            Assert.False(dashboard.Panels.RecruitButton.IsEnabled, "A member can't be recruited again.");

            ui.Click(Centre(dashboard.Header.GuildButton));
            var roster = dashboard.Guild;
            Assert.True(roster.Window.IsShown);
            Assert.Equal(1, roster.CardsShown);
            Assert.Equal(game.Session.Identity.GetFriendlyName(recruit), roster.CardName(0));
            Assert.StartsWith("Trust ", roster.CardStanding(0));
            Assert.StartsWith("1 of 4 members", roster.StatusText);

            roster.WageSlider(0).Value = 9;
            ui.Click(Centre(roster.ApplyButton));
            ui.Ticks(1);
            Assert.True(guild.TryGetContract(recruit, out var contract) && contract.Wage == 9, "Apply sent the new wage.");

            ui.Click(Centre(roster.DismissButton(0)));
            ui.Ticks(1);
            Assert.False(guild.IsMember(recruit), "Dismiss let the member go.");
            Assert.Equal(0, roster.CardsShown);
            Assert.Equal("GUILD 0/4", dashboard.Header.GuildButton.Text);

            Assert.True(dashboard.CloseWindow(), "Esc closes the roster.");
            Assert.False(roster.Window.IsShown);
        }

        /// <summary>The party builder (Epic 10 G5): it starts on the chamber, sends the party with gear from stock, and the tracker follows it on the map until Recall.</summary>
        [Fact]
        public void TheRosterSendsAPartyAndTheTrackerFollowsIt()
        {
            var (ui, recruit) = RecruitFromTheDossier();
            var game = ui.Game;
            var sim = game.Session.Simulation.Simulation;
            var guild = sim.Guild;
            var dashboard = game.Play.Dashboard;
            ui.Click(Centre(dashboard.Header.GuildButton));
            var roster = dashboard.Guild;
            Assert.Equal(Simpiens.Client.Ui.GuildWindow.PartyText(true), roster.PartyButton(0).Text);
            Assert.Equal("The Drowned Gallery", roster.TargetButton.Text);
            Assert.Equal("Send 1 to The Drowned Gallery for 4.", roster.DispatchText);
            Assert.True(roster.DispatchButton.IsEnabled && !roster.RecallButton.IsEnabled);

            ui.Click(Centre(roster.RuleButton));
            Assert.Equal("BOLD", roster.RuleButton.Text);
            var protection = Simpiens.Simulation.Items.ItemCategory.Protection;
            bool inStock = sim.ActiveStalls[0].SellStockOf(Simpiens.Simulation.Economy.ItemType.HazardGear) > 0;
            ui.Click(Centre(roster.DispatchButton));
            ui.Ticks(1);
            ui.Frame();
            Assert.True(guild.HasExpedition && guild.Expedition.Rule == Simpiens.Simulation.Guild.RetreatRule.Bold, "Dispatch sent the party under Bold.");
            if (inStock) Assert.True(Simpiens.Cognition.Inventory.InventoryGroups.IsWearing(game.Session.Simulation.Host.CrawlerStates.First(s => s.AgentId == recruit).Inventory, protection), "Outfitted from stock.");
            Assert.False(roster.DispatchButton.IsEnabled);
            Assert.True(roster.RecallButton.IsEnabled);

            Assert.True(dashboard.CloseWindow());
            dashboard.TogglePanels(); // the map
            ui.Frame();
            Assert.True(dashboard.Tracker.IsShown, "The tracker shows on the map while the party is out.");
            Assert.Matches("^(Mustering at the stall|Marching to The Drowned Gallery) · ", dashboard.Tracker.StageText);

            dashboard.TogglePanels();
            ui.Click(Centre(dashboard.Header.GuildButton));
            ui.Click(Centre(roster.RecallButton));
            ui.Ticks(1);
            Assert.True(!guild.HasExpedition || guild.Expedition.TurnedBack, "Recall ends a muster, or turns a march back.");
        }

        [Fact]
        public void TheExpeditionTrackerLooksAsRecorded()
        {
            var (ui, _) = RecruitFromTheDossier();
            var dashboard = ui.Game.Play.Dashboard;
            ui.Click(Centre(dashboard.Header.GuildButton));
            ui.Click(Centre(dashboard.Guild.DispatchButton));
            ui.Ticks(100);
            dashboard.CloseWindow();
            dashboard.TogglePanels();
            ui.Frame();
            Assert.True(dashboard.Tracker.IsShown);
            RenderTests.CompareWithReference("render-outpost-expedition", FrameCapture.Capture(ui.Game, Width, Height), Width, Height);
        }

        [Fact]
        public void TheGuildRosterLooksAsRecorded()
        {
            var (ui, _) = RecruitFromTheDossier();
            ui.Click(Centre(ui.Game.Play.Dashboard.Header.GuildButton));
            ui.Frame();
            RenderTests.CompareWithReference("render-outpost-guild", FrameCapture.Capture(ui.Game, Width, Height), Width, Height);
        }
    }
}
