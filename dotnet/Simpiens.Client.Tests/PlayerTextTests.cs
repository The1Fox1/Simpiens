using System;
using System.Linq;
using Simpiens.Cognition.Planning;
using Simpiens.Cognition.Social;
using Simpiens.Core.Session;
using Simpiens.Hosting;
using Simpiens.Presentation.Dashboard;
using Simpiens.Simulation;
using Simpiens.Simulation.Economy;
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
        public void TheMerchantPricesGlowstoneAndTheDossierShowsHealth()
        {
            using var sim = Outpost(0);
            var model = new DashboardModel(DashboardContext.From(sim.Services));
            model.OpenMerchant();
            Assert.Equal(45, model.Merchant.GlowstonePrice);
            Assert.Contains("0 Glowstone", model.Merchant.Stock);
            model.Merchant.GlowstonePrice = 38;
            model.Merchant.Apply();
            sim.Step(1);
            Assert.True(model.Context.TryGetStall(model.Merchant.StallId, out var stall));
            Assert.Equal(38, stall.GlowstoneBuyPrice);
            Assert.Equal(20, stall.OreBuyPrice); // the others keep their prices

            var crawler = sim.Host.CrawlerStates[0];
            crawler.Health = 30f;
            crawler.IsDowned = true;
            model.SelectCrawler(crawler.AgentId);
            Assert.Equal(30f, model.Dossier.Health);
            Assert.Equal(DossierModel.DownedBadge, model.Dossier.Badge);
        }

        /// <summary>Epic 10 G3: the terminal prices hazard gear and shows its stock; the dossier shows worn gear and its wear.</summary>
        [Fact]
        public void TheMerchantPricesGearAndTheDossierShowsItsWear()
        {
            using var sim = Outpost(0);
            var model = new DashboardModel(DashboardContext.From(sim.Services));
            model.OpenMerchant();
            Assert.Equal(30, model.Merchant.GearPrice);
            Assert.Contains("3 Gear", model.Merchant.Stock);
            model.Merchant.GearPrice = 26;
            model.Merchant.Apply();
            sim.Step(1);
            Assert.True(model.Context.TryGetStall(model.Merchant.StallId, out var stall));
            Assert.Equal(26, stall.HazardGearSellPrice);
            Assert.Equal(45, stall.GlowstoneBuyPrice); // the others keep their prices

            var crawler = sim.Host.CrawlerStates[0];
            model.SelectCrawler(crawler.AgentId);
            Assert.Equal(DossierModel.NoGear, model.Dossier.Gear);
            ushort life = Simpiens.Simulation.Items.ItemCatalog.Get(ItemType.HazardGear).Equipment.Durability;
            crawler.Inventory.TryEquip(new ItemInstance(1, ItemType.HazardGear, 1, 30, durability: (ushort)(life * 64 / 100)));
            model.SelectCrawler(crawler.AgentId);
            Assert.Equal("Hazard Gear, 64% left", model.Dossier.Gear); // by its catalog name
            Assert.Equal("Hazard Gear, 1% left", DossierModel.GearText(new ItemInstance(1, ItemType.HazardGear, 1, 30, durability: 1)));
        }

        [Fact]
        public void AHazardSectorReadsAsDangerous()
        {
            var map = Simpiens.Core.Session.Layouts.Floor1Layout.Definition;
            map.TryGetSector(Simpiens.Core.Session.Layouts.Floor1Layout.SumpSectorId, out var sump);
            var sumpTint = Simpiens.Presentation.MapTileLayer.FloorTint(map, sump);
            Assert.True(sumpTint.R > sumpTint.B && sumpTint.R >= sumpTint.G, $"The Sump's floor {sumpTint} is not reddish.");

            // Its danger is written under its name; no other room has a note
            using var sim = Outpost(0);
            var scene = new Simpiens.Presentation.WorldScene();
            scene.Build(sim.Host.Current.Map, TestFrames.Of(sim), 0f);
            var notes = Enumerable.Range(0, scene.Labels.Count).Select(i => scene.Labels[i]).Where(l => l.Kind == Simpiens.Presentation.LabelKind.Hazard).ToList();
            Assert.Equal(2, notes.Count);
            Assert.Contains(notes, l => l.Text == "FOUL AIR" && l.SectorId == Simpiens.Core.Session.Layouts.Floor1Layout.SumpSectorId);
            Assert.Contains(notes, l => l.Text == "FALLING ROCK" && l.SectorId == Simpiens.Core.Session.Layouts.Floor1Layout.GlowCavesSectorId);
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
