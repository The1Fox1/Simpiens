using System;
using System.Linq;
using Simpiens.Cognition.Planning;
using Simpiens.Cognition.Social;
using Simpiens.Core.Session;
using Simpiens.Hosting;
using Simpiens.Presentation.Dashboard;
using Simpiens.Simulation;
using Simpiens.Simulation.Spatial;
using Xunit;

namespace Simpiens.Client.Tests
{
    /// <summary>What the dashboard says to the player, and the rules behind it (review U5, S7, S8).</summary>
    public class PlayerTextTests
    {
        [Fact]
        public void EveryGoalAndActionThePlannerHasHasADisplayName()
        {
            var planning = typeof(GoapGoal).Assembly.GetTypes().Where(t => !t.IsAbstract && (typeof(GoapGoal).IsAssignableFrom(t) || typeof(GoapAction).IsAssignableFrom(t))).ToList();
            Assert.NotEmpty(planning);
            var unnamed = planning.Where(t => !PlanText.IsNamed(t)).Select(t => t.Name).ToList();
            Assert.True(unnamed.Count == 0, "Name these in PlanText: " + string.Join(", ", unnamed));

            Assert.Equal("(None)", PlanText.Goal(null));
            Assert.Equal("(Idle)", PlanText.Action(null));
            Assert.Equal("Travel to stall", PlanText.Words("TravelToStallAction")); // a type the table doesn't know yet
            Assert.Equal("Earn income", PlanText.Words("EarnIncomeGoal"));
        }

        private static LocalSimulation Outpost(long ticks)
        {
            var sim = LocalSimulation.Create(SimulationSettings.DefaultSeed);
            sim.Respawn(OutpostSession.SessionId);
            sim.Step(ticks);
            return sim;
        }

        [Fact]
        public void EachCrawlerIsCountedInOneSectorTheOneTheMapGives()
        {
            using var sim = Outpost(300);
            var zones = DashboardContext.From(sim.Services).Zones;
            int crawlers = sim.Registry.ActiveEntities.Count(e => e.Type == EntityType.Pawn);
            int counted = sim.Sectors.All.Sum(s => zones.GetOccupancyCount(s.Id, sim.Registry)) + zones.GetOccupancyCount(sim.Sectors.Fallback.Id, sim.Registry);
            Assert.Equal(crawlers, counted);

            var residents = new System.Collections.Generic.List<WorldEntity>();
            foreach (var crawler in sim.Registry.ActiveEntities.Where(e => e.Type == EntityType.Pawn))
            {
                int sector = sim.Sectors.GetSectorAt(crawler.Position).Id;
                zones.GetAgentsInZone(sector, sim.Registry, residents);
                Assert.Contains(crawler, residents);
            }
        }

        [Fact]
        public void AffinitiesReadWarmestFirstAndAnAbsentPeerIsUnknown()
        {
            using var sim = Outpost(0);
            var crawlers = sim.Registry.ActiveEntities.Where(e => e.Type == EntityType.Pawn).ToList();
            var subject = crawlers[0];
            var memory = (Simpiens.Cognition.Memory.AgentMemory)subject.Agent.Memory;
            memory.AffinityMap.Clear();
            memory.SetAffinity(crawlers[1].Id, new AffinityRecord(-20, 0, 0, 0, 1, 0));
            memory.SetAffinity(crawlers[2].Id, new AffinityRecord(40, 0, 0, 0, 2, 0));
            memory.SetAffinity(new EntityId(9u, 9u, 9u, 9u), new AffinityRecord(10, 0, 0, 0, 1, 0)); // gone from the world

            var model = new DashboardModel(DashboardContext.From(sim.Services));
            model.SelectCrawler(subject.Id);
            var lines = model.Dossier.Affinities;
            Assert.Equal(3, lines.Count);
            Assert.Contains("W:+40", lines[0]);
            Assert.StartsWith(DossierModel.UnknownName + ": W:+10", lines[1]);
            Assert.Contains("W:-20", lines[2]);
        }

        [Fact]
        public void ADriftersDossierSaysWhenItMovesOn()
        {
            Assert.Equal(string.Empty, DossierModel.Stay(0, 100));
            Assert.Equal("drifter, leaves in 2 min", DossierModel.Stay(1300, 200));
            Assert.Equal("drifter, leaves in 1 min", DossierModel.Stay(1300, 1000));
            Assert.Equal("drifter, leaving", DossierModel.Stay(1300, 1300));

            using var sim = Outpost(0);
            while (sim.Clock.CurrentTick < 4000 && !sim.Host.CrawlerStates.Any(s => s.IsDrifter)) sim.Step(1);
            var drifter = sim.Host.CrawlerStates.First(s => s.IsDrifter);
            var model = new DashboardModel(DashboardContext.From(sim.Services));
            model.SelectCrawler(drifter.AgentId);
            Assert.StartsWith("drifter, leaves in", model.Dossier.Residency);
            Assert.EndsWith(" · " + model.Dossier.Residency, model.Dossier.Name);

            model.SelectCrawler(sim.Host.CrawlerStates.First(s => !s.IsDrifter).AgentId);
            Assert.Equal(string.Empty, model.Dossier.Residency);
        }

        [Fact]
        public void TheMerchantNamesItsStallByTheRoomItStandsIn()
        {
            using var sim = Outpost(0);
            var model = new DashboardModel(DashboardContext.From(sim.Services));
            model.OpenMerchant();
            Assert.NotEqual(default, model.Merchant.StallId);
            Assert.True(model.Context.TryGetStall(model.Merchant.StallId, out var stall));
            Assert.Equal("Outpost Stall @ " + sim.Sectors.GetSectorAt(stall.Position).Name, model.Merchant.StallStatus);
            Assert.False(model.Context.TryGetStall(new EntityId(1u, 2u, 3u, 4u), out _));
        }
    }
}
