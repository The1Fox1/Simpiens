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
        /// <summary>On the 24-hour day the first resident reaches the guild's standing in 0.4 to 1.8 game days (Epic 10 Part 2, T4.2b).</summary>
        private const long QualifiesWithin = 2 * Simpiens.Simulation.DayClock.TicksPerDay;

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
            Assert.Equal("Guild 1/8", dashboard.Header.GuildButton.Text);
            Assert.Contains("Guild: member, 5g a visit", dashboard.Panels.GroupsShown);
            Assert.Equal(string.Empty, dashboard.Panels.RecruitShown);
            Assert.False(dashboard.Panels.RecruitButton.IsEnabled, "A member can't be recruited again.");

            ui.Click(Centre(dashboard.Header.GuildButton));
            var roster = dashboard.Guild;
            Assert.True(roster.Window.IsShown);
            Assert.Equal(1, roster.CardsShown);
            Assert.Equal(game.Session.Identity.GetFriendlyName(recruit), roster.CardName(0));
            Assert.StartsWith("Trust ", roster.CardStanding(0));
            Assert.StartsWith("1 of 8 members", roster.StatusText);

            roster.WageSlider(0).Value = 9;
            ui.Click(Centre(roster.ApplyButton));
            ui.Ticks(1);
            Assert.True(guild.TryGetContract(recruit, out var contract) && contract.Wage == 9, "Apply sent the new wage.");

            ui.Click(Centre(roster.DismissButton(0)));
            ui.Ticks(1);
            Assert.False(guild.IsMember(recruit), "Dismiss let the member go.");
            Assert.Equal(0, roster.CardsShown);
            Assert.Equal("Guild 0/8", dashboard.Header.GuildButton.Text);

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
            // Into party 1 from its card (Epic 10 Part 2, W5, D65): a FormParty command, applied on the next tick
            Assert.Equal(Simpiens.Client.Ui.GuildWindow.PartyText(0), roster.PartyButton(0).Text);
            ui.Click(Centre(roster.PartyButton(0)));
            ui.Ticks(1);
            ui.Frame();
            Assert.Equal(Simpiens.Client.Ui.GuildWindow.PartyText(1), roster.PartyButton(0).Text);
            ui.Click(Centre(roster.ExpeditionsTab)); // the builder's tab (Epic 10 Part 2, W5.1)
            Assert.False(roster.Card(0).Visible && roster.Card(0).Parent.Visible, "The Expeditions tab hides the members' cards.");
            Assert.Equal("Party 1 · 1", roster.PartySelectButton.Text);
            Assert.Equal("The Drowned Gallery", roster.TargetButton.Text);
            Assert.Equal("Send party 1, 1 strong, to The Drowned Gallery for 4.", roster.DispatchText);
            Assert.True(roster.DispatchButton.IsEnabled && !roster.RecallButton.IsEnabled);

            ui.Click(Centre(roster.RuleButton));
            Assert.Equal("BOLD", roster.RuleButton.Text);

            // Draughts each (Epic 10 Part 2, W2.4): none, one, two and round; the line says when stock is short
            Assert.Equal("No draughts", roster.DraughtsButton.Text);
            ui.Click(Centre(roster.DraughtsButton));
            ui.Click(Centre(roster.DraughtsButton));
            Assert.Equal("2 draughts each", roster.DraughtsButton.Text);
            Assert.Equal("Send party 1, 1 strong, to The Drowned Gallery for 4. 0 of 2 draughts in stock.", roster.DispatchText);
            var protection = Simpiens.Simulation.Items.ItemCategory.Protection;
            bool inStock = sim.ActiveStalls[0].SellStockOf(Simpiens.Simulation.Economy.ItemType.HazardGear) > 0;
            ui.Click(Centre(roster.DispatchButton));
            ui.Ticks(1);
            ui.Frame();
            Assert.True(guild.HasExpedition && guild.Parties[0].Expedition.Rule == Simpiens.Simulation.Guild.RetreatRule.Bold, "Dispatch sent the party under Bold.");
            Assert.Equal(2, guild.Parties[0].Expedition.Draughts); // the order carries them; the muster gives what stock allows
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
            Assert.True(!guild.HasExpedition || guild.Parties[0].Expedition.TurnedBack, "Recall ends a muster, or turns a march back.");
        }

        /// <summary>
        /// A guild of eight (Epic 10 Part 2, W5.1, D61): eight cards on the Members tab, and the Expeditions tab, each fit
        /// a 1280×720 window. The standing that lets eight sign on at once is set by hand: this checks the layout, not play.
        /// </summary>
        [Fact]
        public void EightMembersAndTheExpeditionsTabFitTheWindow()
        {
            var ui = UiDriver.Start(width: Width, height: Height);
            var game = ui.Game;
            var sim = game.Session.Simulation;
            var guild = sim.Simulation.Guild;
            var host = (Simpiens.Simulation.Guild.IGuildHost)sim.Simulation;
            var commands = (Simpiens.Intervention.IPlayerInterventionService)sim.Services.GetService(typeof(Simpiens.Intervention.IPlayerInterventionService));
            const ushort wage = 20;
            foreach (var crawler in sim.Host.CrawlerStates.Where(s => !s.IsDrifter))
            {
                crawler.Memory.SetAffinity(Simpiens.Simulation.WorldRegistry.PlayerEntityId, new Simpiens.Cognition.Social.AffinityRecord(warmth: 60, trust: 80, fear: 0, respect: 40, interactionCount: 1, lastInteractionTick: 0));
            }
            foreach (var crawler in sim.Host.CrawlerStates.Where(s => !s.IsDrifter).Take(8))
            {
                Assert.Equal(Simpiens.Simulation.Guild.GuildRules.Check.Ok, guild.CanRecruit(host, crawler.AgentId, wage, (uint)sim.Clock.CurrentTick));
                commands.QueueInfluence(Simpiens.Intervention.InfluenceEvent.CreateRecruit("test", crawler.AgentId, wage));
            }
            ui.Ticks(1);
            Assert.Equal(8, guild.Members.Count);

            var dashboard = game.Play.Dashboard;
            ui.Click(Centre(dashboard.Header.GuildButton));
            ui.Frame();
            var roster = dashboard.Guild;
            Assert.Equal(8, roster.CardsShown);
            Assert.StartsWith("8 of 8 members", roster.StatusText);
            float frameBottom = roster.Window.Frame.AbsoluteTop + roster.Window.Frame.AbsoluteHeight;
            float lastCard = roster.Card(7).AbsoluteTop + roster.Card(7).AbsoluteHeight;
            Assert.True(lastCard < frameBottom && frameBottom <= Height, $"The eighth card ends at {lastCard} and the window at {frameBottom}, within the screen's {Height} rows.");
            Assert.True(roster.Window.Frame.AbsoluteTop >= dashboard.Header.GuildButton.AbsoluteTop + dashboard.Header.GuildButton.ActualHeight, "The window starts below the header.");

            // Two parties of four from the cards (Epic 10 Part 2, W5, D65): each click joins the first party with room
            for (int i = 0; i < 8; i++)
            {
                ui.Click(Centre(roster.PartyButton(i)));
                ui.Ticks(1);
            }
            ui.Frame();
            for (int i = 0; i < 8; i++) Assert.Equal(Simpiens.Client.Ui.GuildWindow.PartyText(i < 4 ? (byte)1 : (byte)2), roster.PartyButton(i).Text);
            Assert.True(guild.Parties[0].Members.Count == 4 && guild.Parties[1].Members.Count == 4, "Two parties of four.");
            byte[] eight = FrameCapture.Capture(ui.Game, Width, Height);

            ui.Click(Centre(roster.ExpeditionsTab));
            ui.Frame();
            frameBottom = roster.Window.Frame.AbsoluteTop + roster.Window.Frame.AbsoluteHeight;
            Assert.True(roster.RecallButton.AbsoluteTop + roster.RecallButton.ActualHeight < frameBottom && frameBottom <= Height, "The Expeditions tab fits too.");
            Assert.Equal("Send party 1, 4 strong, to The Drowned Gallery for 4.", roster.DispatchText);
            ui.Click(Centre(roster.PartySelectButton));
            Assert.Equal("Party 2 · 4", roster.PartySelectButton.Text);
            RenderTests.CompareWithReference("render-outpost-guild-eight", eight, Width, Height);
        }

        [Fact]
        public void TheExpeditionTrackerLooksAsRecorded()
        {
            var (ui, _) = RecruitFromTheDossier();
            var dashboard = ui.Game.Play.Dashboard;
            ui.Click(Centre(dashboard.Header.GuildButton));
            ui.Click(Centre(dashboard.Guild.PartyButton(0)));
            ui.Ticks(1);
            ui.Click(Centre(dashboard.Guild.ExpeditionsTab));
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
