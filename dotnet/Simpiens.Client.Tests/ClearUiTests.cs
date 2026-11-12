using System.Linq;
using Gum.Forms.Controls;
using Simpiens.Cognition;
using Simpiens.Cognition.Social;
using Simpiens.Core.Session;
using Simpiens.Core.Session.Layouts;
using Simpiens.Hosting;
using Simpiens.Intervention;
using Simpiens.Presentation.Dashboard;
using Simpiens.Simulation;
using Simpiens.Simulation.Economy;
using Simpiens.Simulation.Guild;
using Xunit;

namespace Simpiens.Client.Tests
{
    /// <summary>
    /// The Clear order on screen (Epic 10 Part 3, K7.3), on the real outpost with the warren charted and four signed on by
    /// hand: the party builder offers the charted lair after the hazard sectors, arms from stock and sends a clear with no
    /// goal; the tracker follows the fight (the lair's living, the party's health) and the fallen with their carrier; and a
    /// clear's tracker on the map looks as recorded.
    /// </summary>
    public class ClearUiTests
    {
        private const int Width = 1280, Height = 720;

        // By hand: the warren charted by the crawlers' own rule, four residents signed on and formed as party 1
        private static AgentState[] Prepare(LocalSimulation sim)
        {
            sim.Sectors.SetExplored(Floor1Layout.OvergrowthSectorId, true);
            var watcher = sim.Host.CrawlerStates.Last(s => !s.IsDrifter);
            var home = watcher.Position;
            for (int t = 0; t < 200 && sim.Simulation.Creatures.ChartedMask == 0UL; t++)
            {
                watcher.Position = new Vec2(20f, 14f);
                sim.Step(1);
            }
            watcher.Position = home;
            Assert.NotEqual(0UL, sim.Simulation.Creatures.ChartedMask);
            var host = (IGuildHost)sim.Simulation;
            var guild = sim.Simulation.Guild;
            var members = sim.Host.CrawlerStates.Where(s => !s.IsDrifter).Take(4).ToArray();
            foreach (var m in members)
            {
                m.Memory.SetAffinity(WorldRegistry.PlayerEntityId, new AffinityRecord(60, 80, 0, 0, 1, 0));
                Assert.Equal(GuildRules.Check.Ok, guild.Recruit(host, m.AgentId, sim.Simulation.Economy.Guild.MaxWage, (uint)sim.Clock.CurrentTick));
            }
            Assert.Equal(GuildRules.Check.Ok, guild.FormParty(host, new FormPartyCommand(1, members.Select(m => m.AgentId).ToArray()), (uint)sim.Clock.CurrentTick));
            var sm = (SimulationManager)sim.Simulation;
            sm.UpdateStall(sm.ActiveStalls[0].WithStock(ItemType.IronBlade, 4)); // by hand: blades in stock
            return members;
        }

        private static ExpeditionModel Builder(DashboardModel model)
        {
            model.OpenGuild();
            var expedition = model.Guild.Expedition;
            expedition.Refresh();
            for (int i = 0; i < expedition.Targets.Count && !expedition.IsClear; i++) expedition.NextTarget();
            return expedition;
        }

        [Fact]
        public void TheBuilderOffersACharteredLairToClearAndArmsFromStock()
        {
            using var sim = LocalSimulation.Create(SimulationSettings.DefaultSeed);
            sim.Respawn(OutpostSession.SessionId);
            sim.Step(20);
            var members = Prepare(sim);
            var model = new DashboardModel(DashboardContext.From(sim.Services));
            var expedition = Builder(model);
            Assert.Contains(expedition.Targets, t => t.Lair && t.Name == "Clear the burrow rats");
            Assert.True(expedition.IsClear);
            Assert.Equal("Clear the burrow rats", expedition.TargetName);
            Assert.StartsWith("Arm: ", expedition.OutfitText);
            Assert.Contains("iron blade", expedition.OutfitText);
            expedition.ToggleOutfit(); // the back half with bows (Epic 10 Part 3, K8.2c); none in stock this early
            Assert.Equal("Arm: iron blade · no bows in stock", expedition.OutfitText);
            Assert.Equal(2, expedition.Shooters);
            expedition.ToggleOutfit();
            Assert.Equal("No arming", expedition.OutfitText);
            Assert.Equal(0, expedition.Shooters);
            expedition.ToggleOutfit();
            Assert.True(expedition.CanDispatch, expedition.DispatchText);
            Assert.Equal("Send party 1, 4 strong, to clear the burrow rats of Overgrowth.", expedition.DispatchText);

            expedition.Dispatch();
            sim.Step(1);
            var party = sim.Simulation.Guild.Parties[0];
            Assert.True(party.IsOut && party.Expedition.IsClear);
            Assert.All(members, m => Assert.True(Vitality.IsArmed(m.Inventory), "Each was given a blade from stock."));
        }

        [Fact]
        public void TheTrackerFollowsTheFightAndTheFallen()
        {
            using var sim = LocalSimulation.Create(SimulationSettings.DefaultSeed);
            sim.Respawn(OutpostSession.SessionId);
            sim.Step(20);
            var members = Prepare(sim);
            var model = new DashboardModel(DashboardContext.From(sim.Services));
            var expedition = Builder(model);
            expedition.Dispatch();
            sim.Step(1);
            var party = sim.Simulation.Guild.Parties[0];
            var heart = sim.Simulation.Creatures.Lairs[party.Expedition.LairIndex].Definition.Position;
            for (int t = 0; t < 400 && party.Expedition.Stage != ExpeditionStage.Fight; t++)
            {
                var at = party.Expedition.Stage == ExpeditionStage.Muster ? sim.Simulation.ActiveStalls[0].Position : heart + new Vec2(-1.5f, -1f);
                for (int i = 0; i < members.Length; i++) { members[i].Position = at + new Vec2(i * 0.5f, 0f); members[i].Hunger = 0f; } // by hand: mustered, then at the lair
                sim.Step(1);
            }
            Assert.Equal(ExpeditionStage.Fight, party.Expedition.Stage);
            sim.Step(1);
            expedition.Refresh();
            var tracker = expedition.Trackers[0];
            Assert.True(tracker.IsClear);
            Assert.Equal("Clear: The burrow rats of Overgrowth · Steady", tracker.Title);
            Assert.Equal("Fighting the burrow rats", tracker.StageText);
            Assert.Matches(@"^Burrow rats: \d of \d left · party health \d+$", tracker.FightText);
            Assert.Equal("Picked up 0 hides", tracker.HaulText);

            var fallen = members[1];
            fallen.Health = 0f;
            fallen.IsDowned = true; // by hand: downed in the lair
            sim.Step(2); // it collapses; the guild sends the healthiest mate
            expedition.Refresh();
            Assert.Equal("Fallen, bleeding", tracker.Members[1].Status);
            int carrier = System.Array.FindIndex(members, m => m.AgentId == party.Carrier);
            Assert.True(carrier >= 0 && carrier != 1, "The guild sent a mate.");
            Assert.StartsWith("Going for ", tracker.Members[carrier].Status);
        }

        [Fact]
        public void AClearsTrackerLooksAsRecorded()
        {
            var ui = UiDriver.Start(width: Width, height: Height);
            var sim = ui.Game.Session.Simulation;
            var members = Prepare(sim);
            var model = ui.Game.Session.DashboardModel;
            var expedition = Builder(model);
            expedition.Dispatch();
            ui.Ticks(1);
            var party = sim.Simulation.Guild.Parties[0];
            for (int t = 0; t < 2000 && party.Expedition.Stage != ExpeditionStage.Fight; t++) ui.Ticks(1); // they muster and march there themselves
            Assert.Equal(ExpeditionStage.Fight, party.Expedition.Stage);
            ui.Ticks(60);
            var dashboard = ui.Game.Play.Dashboard;
            dashboard.CloseWindow();
            dashboard.TogglePanels(); // the map
            ui.Frame();
            Assert.True(dashboard.Tracker.IsShown);
            Assert.StartsWith("Burrow rats: ", dashboard.Tracker.FightText);
            RenderTests.CompareWithReference("render-outpost-clear", FrameCapture.Capture(ui.Game, Width, Height), Width, Height);
        }
    }
}
