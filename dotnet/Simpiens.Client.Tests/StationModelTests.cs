using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Simpiens.Cognition.Social;
using Simpiens.Core.Session;
using Simpiens.Hosting;
using Simpiens.Presentation.Dashboard;
using Simpiens.Simulation;
using Simpiens.Simulation.Economy;
using Simpiens.Simulation.Items;
using Simpiens.Simulation.Workshops;
using Xunit;

namespace Simpiens.Client.Tests
{
    /// <summary>
    /// The station window as data (Epic 10 Part 2, W1.6a.2), on the real outpost: it shows a station's building, its
    /// artisan or who would take the work, its shifts and its order in the rules' words, and every action leaves as a
    /// typed command the simulation applies on the next tick. Mechanism tests: a resident's trust is set by hand where
    /// the test needs a willing one.
    /// </summary>
    public class StationModelTests
    {
        private static LocalSimulation Outpost(uint treasury = 1000)
        {
            var sim = LocalSimulation.Create(SimulationSettings.DefaultSeed, services =>
            {
                services.RemoveAll<ISessionDefinition>();
                services.AddSingleton<ISessionDefinition>(new OutpostSession(OutpostSession.DefaultCrawlerCount, EconomyTuning.Default.WithStallTreasury(treasury)));
            });
            sim.Respawn(OutpostSession.SessionId);
            return sim;
        }

        private static void Step(LocalSimulation sim, DashboardModel model, long ticks = 1)
        {
            sim.Step(ticks);
            model.Refresh();
        }

        /// <summary>A cautious resident who trusts the player, by hand.</summary>
        private static Simpiens.Cognition.AgentState Willing(LocalSimulation sim)
        {
            var tuning = WorkshopTuning.Default;
            var smith = sim.Host.CrawlerStates.First(s => !s.IsDrifter && s.Personality.RiskTolerancePercent < tuning.MaxRiskTolerancePercent);
            smith.Memory.SetAffinity(WorldRegistry.PlayerEntityId, new AffinityRecord(warmth: 60, trust: 80, fear: 0, respect: 40, interactionCount: 1, lastInteractionTick: 0));
            return smith;
        }

        [Fact]
        public void OneWindowAtATimeAndAClickOpensTheStationClicked()
        {
            using var sim = Outpost();
            var model = new DashboardModel(DashboardContext.From(sim.Services));
            int still = sim.Simulation.Workshops.IndexOf(StationKind.Still);
            model.OpenGuild();
            model.OpenStation(still);
            Assert.True(model.Stations.IsOpen && !model.Guild.IsOpen, "Opening the station window closes the guild's.");
            Assert.Equal(still, model.Stations.Index);
            Assert.Equal("The Alchemy Still", model.Stations.Title);
            model.OpenMerchant();
            Assert.False(model.Stations.IsOpen);
            model.ToggleStations();
            Assert.True(model.Stations.IsOpen && model.Stations.Index == still, "The header's button opens the station shown last.");
            Assert.True(model.CloseWindow() && !model.Stations.IsOpen, "Esc closes it.");
            Assert.False(model.CloseWindow());
            Assert.Equal("Forge", model.Stations.TabName(sim.Simulation.Workshops.IndexOf(StationKind.Forge)));
            Assert.Equal("Still", model.Stations.TabName(still));
        }

        [Fact]
        public void BuildingSaysWhatItCostsAndWhyNotThenBuilds()
        {
            using (var poor = Outpost(treasury: 250))
            {
                var model = new DashboardModel(DashboardContext.From(poor.Services));
                model.OpenStation(poor.Simulation.Workshops.IndexOf(StationKind.Forge));
                var forge = model.Stations;
                Assert.False(forge.Built);
                Assert.Equal("An empty plot · 300g to build", forge.Status);
                Assert.False(forge.CanBuild, "The stall opens with less than a forge costs.");
                Assert.Equal("The stall can't pay the 300g to build the forge.", forge.BuildNote);
            }

            using var sim = Outpost();
            var dashboard = new DashboardModel(DashboardContext.From(sim.Services));
            dashboard.OpenStation(sim.Simulation.Workshops.IndexOf(StationKind.Forge));
            var station = dashboard.Stations;
            Assert.True(station.CanBuild);
            Assert.Equal("Build for 300g", station.BuildLabel);
            station.Build();
            Step(sim, dashboard);
            Assert.True(station.Built && sim.Simulation.Workshops[station.Index].Built, "Build's command built the forge.");
            Assert.Equal("Standing · 0 made", station.Status);
            Assert.False(station.CanBuild);
            Assert.False(station.HasArtisan);
        }

        [Fact]
        public void TheWillingAreListedAndHiredOnTheDayShiftThenTheirWorkIsSetAndTheyAreLetGo()
        {
            using var sim = Outpost();
            var dashboard = new DashboardModel(DashboardContext.From(sim.Services));
            var workshops = sim.Simulation.Workshops;
            int forge = workshops.IndexOf(StationKind.Forge);
            dashboard.OpenStation(forge);
            var station = dashboard.Stations;
            station.Build();
            Step(sim, dashboard);
            if (station.Candidates.Count == 0) Assert.StartsWith("Nobody yet.", station.CandidatesNote);

            var smith = Willing(sim);
            dashboard.Refresh();
            var row = station.Candidates.FirstOrDefault(c => c.CrawlerId == smith.AgentId);
            Assert.NotNull(row);
            Assert.True(station.Candidates.Count <= StationModel.MaxCandidates);
            Assert.Equal("Trust 80 · Warmth 60", row.Standing);
            Assert.Equal(WorkshopRules.ReservationWage(smith, WorkshopTuning.Default), row.Ask);
            Assert.Equal(smith.AgentId, station.Candidates[0].CrawlerId); // the most trusting first

            station.Hire(smith.AgentId);
            Step(sim, dashboard);
            Assert.True(workshops.TryGetContract(smith.AgentId, out var contract) && contract.StationIndex == forge, "Hire's command made it the forge's artisan.");
            Assert.Equal(ShiftBlocks.DayShift, contract.Shifts);
            Assert.True(station.HasArtisan && station.ArtisanId == smith.AgentId);
            Assert.Empty(station.Candidates);
            Assert.Equal(ShiftBlocks.DayShift, station.PendingShifts);
            Assert.Equal("8 hours a day", station.ShiftsNote);
            Assert.False(station.HasChanges);

            // Three blocks in a row reach the break's hours (D35): the window warns
            station.ToggleBlock(3);
            Assert.True(station.ShiftsTire);
            Assert.Equal("12 hours a day · that many in a row tires it: expect breaks", station.ShiftsNote);
            station.MoreWage();
            Assert.True(station.HasChanges);
            station.ApplyArtisan();
            Step(sim, dashboard);
            workshops.TryGetContract(smith.AgentId, out contract);
            Assert.Equal(0b001110, contract.Shifts);
            Assert.Equal(row.Ask + 1, contract.Wage);
            Assert.False(station.HasChanges, "The window shows what the contract now holds.");

            station.Dismiss();
            Step(sim, dashboard);
            Assert.False(workshops.IsArtisan(smith.AgentId), "Dismiss's command let it go.");
            Assert.False(station.HasArtisan);
        }

        [Fact]
        public void AnOrderIsPlacedAndShowsWhyItWaitsThenIsCancelled()
        {
            using var sim = Outpost();
            var dashboard = new DashboardModel(DashboardContext.From(sim.Services));
            var workshops = sim.Simulation.Workshops;
            int forge = workshops.IndexOf(StationKind.Forge);
            dashboard.OpenStation(forge);
            var station = dashboard.Stations;
            Assert.False(station.CanOrder, "Nothing is ordered before the forge stands.");
            station.Build();
            Step(sim, dashboard);

            // The forge's recipes in the order of what they make: the pickaxe (W1), the respirator and the hard hat (W3)
            Assert.Equal(new[] { ItemType.ToolPickaxe, ItemType.Respirator, ItemType.HardHat }, station.Recipes.Select(r => r.Item).ToArray());
            Assert.Equal("1 glowstone, 2 herbs · 2 h of work", station.Recipes[1].Recipe);
            var pickaxe = station.Recipes[0];
            Assert.Equal(ItemType.ToolPickaxe, pickaxe.Item);
            Assert.Equal("Pickaxe", pickaxe.Name);
            Assert.Equal("3 iron ore · 2 h of work", pickaxe.Recipe);
            Assert.Equal("No order.", station.OrderText);
            station.MoreCount(0);
            station.MoreCount(0);
            station.LessCount(0);
            Assert.Equal(2, pickaxe.PendingCount);
            station.Order(0);
            Step(sim, dashboard);
            Assert.True(workshops[forge].HasOrder && workshops[forge].Order == ItemType.ToolPickaxe && workshops[forge].Remaining == 2);
            Assert.Equal("Making pickaxe: 2 to go · 0%", station.OrderText);
            Assert.Equal("Nobody works the forge: hire an artisan.", station.OrderNote);
            Assert.Equal(2, pickaxe.PendingCount); // the count stays while the station is shown

            station.CancelOrder();
            Step(sim, dashboard);
            Assert.False(workshops[forge].HasOrder);
            Assert.Equal("No order.", station.OrderText);

            // The still's tab shows the still's recipe (W2's draught), and no order
            dashboard.Stations.Show(workshops.IndexOf(StationKind.Still));
            var draught = Assert.Single(station.Recipes);
            Assert.Equal(ItemType.HealingDraught, draught.Item);
            Assert.Equal("Healing draught", draught.Name);
            Assert.Equal("2 herbs · 1 h of work", draught.Recipe);
            Assert.Equal("No order.", station.OrderText);
        }

        [Fact]
        public void WithoutACommandQueueTheWindowSendsNothing()
        {
            using var sim = Outpost();
            var context = new DashboardContext(sim.Simulation, sim.Clock, sim.Registry, sim.Sectors);
            var model = new DashboardModel(context);
            model.OpenStation(0);
            Assert.False(model.Stations.CanBuild, "With no queue for commands, Build is off.");
            model.Stations.Build();
            sim.Step(1);
            Assert.False(sim.Simulation.Workshops[0].Built);
        }
    }
}
