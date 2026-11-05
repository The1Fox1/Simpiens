using System.Linq;
using Gum.Wireframe;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Simpiens.Cognition.Social;
using Simpiens.Core.Session;
using Simpiens.Presentation;
using Simpiens.Simulation;
using Simpiens.Simulation.Economy;
using Simpiens.Simulation.Items;
using Simpiens.Simulation.Workshops;
using Xunit;

namespace Simpiens.Client.Tests
{
    /// <summary>
    /// The station window in the real client (Epic 10 Part 2, W1.6a.2): the header's Workshop button opens it; Build,
    /// Hire, a shift's toggle with Set, and Order send their commands; a click on a station on the map opens its window.
    /// A resident's trust is set by hand, as the model's tests do.
    /// </summary>
    public class StationWindowTests
    {
        private const int Width = 1280, Height = 720;

        private static Vec2 Centre(GraphicalUiElement control)
            => new Vec2(control.AbsoluteLeft + control.AbsoluteWidth * 0.5f, control.AbsoluteTop + control.AbsoluteHeight * 0.5f);

        private static Vec2 Centre(Gum.Forms.Controls.FrameworkElement control) => Centre(control.Visual);

        private static UiDriver Start()
            => UiDriver.Start(new ClientOptions
            {
                ConfigureServices = services =>
                {
                    services.RemoveAll<ISessionDefinition>();
                    services.AddSingleton<ISessionDefinition>(new OutpostSession(OutpostSession.DefaultCrawlerCount, EconomyTuning.Default.WithStallTreasury(1000)));
                },
            }, width: Width, height: Height);

        [Fact]
        public void TheHeadersButtonOpensItAndItsButtonsBuildHireSetShiftsAndOrder()
        {
            var ui = Start();
            var game = ui.Game;
            var dashboard = game.Play.Dashboard;
            var workshops = game.Session.Simulation.Simulation.Workshops;
            int forge = workshops.IndexOf(StationKind.Forge);

            ui.Click(Centre(dashboard.Header.WorkshopButton));
            var window = dashboard.Stations;
            Assert.True(window.Window.IsShown, "The header's Workshop button opens the station window.");
            Assert.Equal(forge, game.Session.DashboardModel.Stations.Index);
            Assert.True(window.ShowsBuild);
            Assert.Equal("The Forge · An empty plot · 300g to build", window.StatusText);

            ui.Click(Centre(window.BuildButton));
            ui.Ticks(1);
            ui.Frame();
            Assert.True(workshops[forge].Built, "Build's button built the forge.");
            Assert.False(window.ShowsBuild);

            // A willing resident (by hand), hired by its card's button, on the day shift
            var smith = game.Session.Simulation.Host.CrawlerStates.First(s => !s.IsDrifter && s.Personality.RiskTolerancePercent < WorkshopTuning.Default.MaxRiskTolerancePercent);
            smith.Memory.SetAffinity(WorldRegistry.PlayerEntityId, new AffinityRecord(warmth: 60, trust: 80, fear: 0, respect: 40, interactionCount: 1, lastInteractionTick: 0));
            ui.Frame();
            Assert.True(window.CandidatesShown >= 1);
            ui.Click(Centre(window.HireButton(0)));
            ui.Ticks(1);
            ui.Frame();
            Assert.True(workshops.IsArtisan(smith.AgentId), "Hire's button hired it.");
            Assert.True(window.ShowsArtisan);
            Assert.Equal(game.Session.DashboardModel.Stations.ArtisanName, window.ArtisanNameText);

            // Its evening too: three blocks in a row, a warning, then Set
            ui.Click(Centre(window.Block(3)));
            Assert.StartsWith("12 hours a day", window.ShiftsNoteText);
            ui.Click(Centre(window.ApplyButton));
            ui.Ticks(1);
            ui.Frame();
            Assert.True(workshops.TryGetContract(smith.AgentId, out var contract) && contract.Shifts == 0b001110, "Set's button sent the shifts.");

            ui.Click(Centre(window.MoreCountButton(0)));
            ui.Click(Centre(window.OrderButton(0)));
            ui.Ticks(1);
            ui.Frame();
            Assert.True(workshops[forge].HasOrder && workshops[forge].Remaining == 2, "Order's button ordered two pickaxes.");
            Assert.StartsWith("Making pickaxe: 2 to go", window.OrderText);
        }

        [Fact]
        public void AClickOnAStationOnTheMapOpensItsWindow()
        {
            var ui = Start();
            var game = ui.Game;
            var workshops = game.Session.Simulation.Simulation.Workshops;
            int still = workshops.IndexOf(StationKind.Still);
            var at = workshops.DefinitionAt(still).Position;
            game.Play.Dashboard.TogglePanels(); // the game starts on the dashboard: Tab to the map
            game.World.Rig.ZoomAt(at, 0.3f);
            game.World.Rig.Pan(at - game.World.Rig.Position);
            ui.Frame();
            ui.Click(game.World.View.WorldToScreen(at));
            Assert.True(game.Session.DashboardModel.Stations.IsOpen, "A click on the still's plot opens the station window.");
            Assert.Equal(still, game.Session.DashboardModel.Stations.Index);
            Assert.StartsWith("The Alchemy Still", game.Play.Dashboard.Stations.StatusText);
        }
    }
}
