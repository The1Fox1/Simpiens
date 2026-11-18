using System.Linq;
using Gum.Wireframe;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Simpiens.Cognition.Contracts;
using Simpiens.Core.Session;
using Simpiens.Simulation;
using Simpiens.Simulation.Economy;
using Simpiens.Simulation.Items;
using Simpiens.Simulation.Skills;
using Simpiens.Testing.Environment;
using Xunit;

namespace Simpiens.Client.Tests
{
    /// <summary>
    /// Teachers on screen (Epic 11, S3.5, D9–D11): the station window builds the guildhall's drill yard and says who drilled
    /// there today, with no artisan or order section; the merchant terminal lists every primer with its price; the dossier says
    /// who first taught each skill. The drill and the teacher are set by hand on the live outpost; Build leaves as a command.
    /// </summary>
    public class TeachersOnScreenTests
    {
        private const int Width = 1280, Height = 720;

        [Fact]
        public void TheStationWindowBuildsTheYardAndSaysWhoDrilledToday()
        {
            var ui = Start();
            var game = ui.Game;
            var dashboard = game.Play.Dashboard;
            var sim = (SimulationManager)game.Session.Simulation.Simulation;
            int yard = sim.Workshops.IndexOf(StationKind.DrillYard);
            ui.Click(Centre(dashboard.Header.WorkshopButton));
            var window = dashboard.Stations;
            ui.Click(Centre(window.Tab(yard)));
            Assert.Equal("Drill yard", window.Tab(yard).Text);
            Assert.Equal("The Drill Yard · An empty plot · 200g to build", window.StatusText);
            ui.Click(Centre(window.BuildButton));
            ui.Ticks(1);
            ui.Frame();
            Assert.True(sim.Workshops[yard].Built, "Build's button built the yard.");
            Assert.Equal("WHO DRILLS", window.ArtisanHeaderText);
            Assert.False(window.ShowsOrders, "The yard makes nothing: no order section.");
            Assert.StartsWith("No artisan: guild members drill here", window.CandidatesNoteText);
            Assert.Equal("The Drill Yard · Standing · nobody has drilled today", window.StatusText);

            // A resident drills its hour there (by hand: at the yard, its intent run as the tick runs it)
            var node = game.Session.Simulation.Registry.ActiveEntities.First(e => e.Agent != null && !e.Agent.IsDrifter);
            node.SetPosition(sim.Map.Stations[yard].Position); // placed by hand: a fixture
            var state = new ActiveIntentState { Intent = new DrillIntent(node.Id, yard, null), CurrentWaypointIndex = 0, LastSampledPosition = node.Position };
            Assert.Equal(IntentResult.Success, sim.RunToCompletion(node, state, DayClock.TicksPerHour + 5));
            ui.Frame();
            Assert.Equal("The Drill Yard · Standing · drilled today: " + game.Session.Identity.GetFriendlyName(node.Id), window.StatusText);
            Assert.True(dashboard.CloseWindow());
        }

        [Fact]
        public void TheMerchantTerminalListsThePrimers()
        {
            var ui = Start();
            var dashboard = ui.Game.Play.Dashboard;
            ui.Click(Centre(dashboard.Header.MerchantButton));
            foreach (var primer in ItemCatalog.InCategory(ItemCategory.Manual))
            {
                var slider = dashboard.Merchant.PriceSlider(primer);
                Assert.NotNull(slider);
                Assert.Equal(25, (int)System.Math.Round(slider.Value));
            }
        }

        [Fact]
        public void TheDossierSaysWhoTaughtASkill()
        {
            var ui = Start();
            var game = ui.Game;
            var residents = game.Session.Simulation.Host.CrawlerStates.Where(s => !s.IsDrifter).ToList();
            var learner = residents[0];
            var teacher = residents[1];
            var traits = learner.Traits;
            traits.RecordTeacher(Skill.Mining, teacher.AgentId);
            learner.Traits = traits;
            game.Session.DashboardModel.SelectCrawler(learner.AgentId);
            ui.Frame();
            Assert.Equal("Mining taught by " + game.Session.Identity.GetFriendlyName(teacher.AgentId), game.Play.Dashboard.Panels.SkillNotesShown);
        }

        private static UiDriver Start()
            => UiDriver.Start(new ClientOptions
            {
                ConfigureServices = services =>
                {
                    services.RemoveAll<ISessionDefinition>();
                    services.AddSingleton<ISessionDefinition>(new OutpostSession(OutpostSession.DefaultCrawlerCount, EconomyTuning.Default.WithStallTreasury(1000)));
                },
            }, width: Width, height: Height);

        private static Vec2 Centre(GraphicalUiElement control)
            => new Vec2(control.AbsoluteLeft + control.AbsoluteWidth * 0.5f, control.AbsoluteTop + control.AbsoluteHeight * 0.5f);

        private static Vec2 Centre(Gum.Forms.Controls.FrameworkElement control) => Centre(control.Visual);
    }
}
