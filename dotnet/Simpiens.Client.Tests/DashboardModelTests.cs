using System;
using System.Collections.Generic;
using System.Linq;
using Simpiens.Core.Session;
using Simpiens.Core.Session.Layouts;
using Simpiens.Hosting;
using Simpiens.Intervention;
using Simpiens.Narrative;
using Simpiens.Presentation;
using Simpiens.Presentation.Dashboard;
using Simpiens.Simulation;
using Simpiens.Simulation.Economy;
using Simpiens.Simulation.Spatial;
using Simpiens.Simulation.World;
using Simpiens.Testing.World;
using Xunit;

namespace Simpiens.Client.Tests
{
    /// <summary>The zone service, ported with Unity's FacilityZoneServiceValidator checks (Epic CLIENT M2.1).</summary>
    public class FacilityZoneServiceTests
    {
        [Fact]
        public void EachDevMapPenIsItsOwnSectorAndEverythingElseIsTransit()
        {
            var zones = new FacilityZoneService(TestWorlds.DevMapSectors());
            var centres = new[] { (-8f, 0f), (-4f, 0f), (0f, 0f), (4f, 0f), (8f, 0f), (-8f, -9f), (-4f, -9f), (0f, -9f), (4f, -9f), (8f, -9f) };
            for (int pen = 1; pen <= 10; pen++)
            {
                var (x, y) = centres[pen - 1];
                Assert.Equal(pen, zones.GetZoneForPosition(new Vec2(x, y)).ZoneId);
            }
            Assert.Equal(0, zones.GetZoneForPosition(new Vec2(0f, 15f)).ZoneId);
            Assert.Equal(0, zones.GetZoneById(999).ZoneId);
            Assert.False(zones.TryGetZone(999, out _));
        }

        [Fact]
        public void QueriesWithoutARegistryAreEmptyAndQueriesWithOneAllocateNothing()
        {
            var zones = new FacilityZoneService(TestWorlds.DevMapSectors());
            Assert.Equal(0, zones.GetOccupancyCount(4, null));
            zones.GetAgentsInZone(4, null, null);
            var buffer = new List<WorldEntity> { null };
            zones.GetAgentsInZone(4, null, buffer);
            Assert.Empty(buffer);

            using var sim = HeadlessSimulation.Create(SimulationSettings.DefaultSeed);
            sim.Respawn(OutpostSession.SessionId);
            var outpost = new FacilityZoneService(sim.Sectors);
            int gate = Floor1Layout.RoadGateSectorId;
            outpost.GetOccupancyCount(gate, sim.Registry); // warm up
            outpost.GetAgentsInZone(gate, sim.Registry, buffer);
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 1000; i++)
            {
                outpost.GetOccupancyCount(gate, sim.Registry);
                outpost.GetAgentsInZone(gate, sim.Registry, buffer);
            }
            Assert.Equal(0L, GC.GetAllocatedBytesForCurrentThread() - before);
            Assert.Equal(12, buffer.Count); // the whole outpost starts at the Road Gate
        }

        [Fact]
        public void ExplorationIsReadFromTheSimulation()
        {
            var sectors = TestWorlds.CommonsYardSectors();
            var zones = new FacilityZoneService(sectors);
            int market = CommonsYardFixture.OutpostMarketSectorId, ridge = CommonsYardFixture.OreRidgeSectorId;
            Assert.True(zones.IsSectorExplored(0) && zones.IsSectorExplored(market));
            Assert.False(zones.IsSectorExplored(ridge));
            Assert.False(zones.GetAllZones().First(z => z.ZoneId == ridge).IsExplored);

            sectors.SetExplored(ridge, true); // the simulation charts it; the dashboard only reads
            Assert.True(zones.IsSectorExplored(ridge));
            Assert.True(zones.GetAllZones().First(z => z.ZoneId == ridge).IsExplored);
        }
    }

    /// <summary>
    /// The dashboard model (Epic CLIENT M2.1), with Unity's BountyTerminalValidator checks ported to go through the model's
    /// typed commands, and the model on the live outpost.
    /// </summary>
    public class DashboardModelTests
    {
        /// <summary>A bare simulation manager with one stall, as Unity's validator built it.</summary>
        private static (SimulationManager sim, PlayerInterventionService queue, EntityId stall, DashboardModel model) BareOutpost(uint treasury = 250, ISectorRegistry sectors = null, NarrativeChronicleService chronicle = null)
        {
            var queue = new PlayerInterventionService();
            var sim = new SimulationManager(worldRegistry: null, spatialPartition: null, chronicleService: chronicle, sectors: sectors, interventionService: queue);
            var stall = new EntityId(9u, 9u, 9u, 9u);
            sim.RegisterTradeStall(new TradeStallData(stall, Vec2.Zero, 15, 50, 20, 10, 25, 10, treasuryGold: treasury, interactionRadius: 3f));
            var model = new DashboardModel(new DashboardContext(simulation: sim, sectors: sectors, intervention: queue));
            return (sim, queue, stall, model);
        }

        [Fact]
        public void ApplyingPricesChangesTheStallOnTheNextTick()
        {
            var (sim, _, stall, model) = BareOutpost();
            model.OpenMerchant();
            Assert.Equal((15, 20, 25), (model.Merchant.RationPrice, model.Merchant.OrePrice, model.Merchant.HerbPrice));
            model.Merchant.RationPrice = 25;
            model.Merchant.OrePrice = 35;
            model.Merchant.HerbPrice = 45;
            model.Merchant.Apply();
            Assert.Equal(FeedbackTone.Ok, model.Merchant.Feedback.Tone);
            Assert.True(sim.TryGetTradeStall(stall, out var before) && before.RationSellPrice == 15, "Nothing changes before the tick.");

            sim.Tick();
            Assert.True(sim.TryGetTradeStall(stall, out var after));
            Assert.Equal((25, 35, 45), ((int)after.RationSellPrice, (int)after.OreBuyPrice, (int)after.HerbBuyPrice));

            model.Merchant.RationPrice = 999; // the slider's range holds
            model.Merchant.Apply();
            sim.Tick();
            Assert.True(sim.TryGetTradeStall(stall, out var clamped) && clamped.RationSellPrice == MerchantModel.MaxPrice);
        }

        [Fact]
        public void PostingAGatherBountyEscrowsTheRewardAndCancellingRefundsIt()
        {
            var sectors = new SectorRegistry();
            sectors.Load(Floor1Layout.Definition);
            var chronicle = new NarrativeChronicleService(capacity: 32);
            var (sim, _, stall, model) = BareOutpost(250, sectors, chronicle);

            model.OpenBounties();
            model.Bounties.Type = BountyType.GatherResource;
            Assert.True(model.Bounties.SelectSector(Floor1LayoutSector("Ore Quarry")));
            model.Bounties.Item = ItemType.IronOre;
            model.Bounties.Quantity = 2;
            model.Bounties.Reward = 50;
            model.Bounties.Duration = 1500;
            model.Bounties.Post();
            Assert.Equal(FeedbackTone.Ok, model.Bounties.Feedback.Tone);
            sim.Tick();

            var bounty = Assert.Single(sim.ActiveBounties);
            Assert.Equal((BountyType.GatherResource, ItemType.IronOre, 2, 50, BountyStatus.Open), (bounty.Type, bounty.TargetItemType, (int)bounty.TargetQuantity, (int)bounty.RewardGold, bounty.Status));
            Assert.True(sim.TryGetTradeStall(stall, out var escrowed) && escrowed.TreasuryGold == 200);
            var events = new List<ChronicleEvent>();
            chronicle.GetAllRecentEvents(events, 5);
            Assert.Contains(events, e => e.EventType == ChronicleEventType.BountyPosted);

            model.Refresh();
            var row = Assert.Single(model.Bounties.Contracts);
            Assert.Equal($"#{bounty.BountyId} - Gather Salvage", row.Title);
            Assert.Equal("OPEN", row.StatusText);
            Assert.Equal("2x IronOre from Ore Quarry | Expires: " + bounty.ExpirationTick + " ticks", row.Details);
            Assert.Equal("50 Gold", row.Reward);
            Assert.True(row.CanCancel);
            Assert.Equal("1 ACTIVE", model.Bounties.CountBadge);

            model.Bounties.Cancel(row.BountyId);
            Assert.Equal(FeedbackTone.Notice, model.Bounties.Feedback.Tone);
            sim.Tick();
            Assert.Empty(sim.ActiveBounties);
            Assert.True(sim.TryGetBounty(row.BountyId, out var cancelled) && cancelled.Status == BountyStatus.Cancelled);
            Assert.True(sim.TryGetTradeStall(stall, out var refunded) && refunded.TreasuryGold == 250);
        }

        private static int Floor1LayoutSector(string name) => Floor1Layout.Definition.Sectors.First(s => s.Name == name).Id;

        [Fact]
        public void TheBoardRefusesWhatTheSimulationWouldAndSaysWhy()
        {
            var sectors = new SectorRegistry();
            sectors.Load(Floor1Layout.Definition);
            var (sim, _, stall, model) = BareOutpost(20, sectors);
            model.OpenBounties();

            // Scout offers exactly the uncharted rooms
            model.Bounties.Type = BountyType.ScoutSector;
            Assert.Equal(new[] { (int)Floor1Layout.DeepSeamSectorId, Floor1Layout.OvergrowthSectorId }, model.Bounties.SectorChoiceIds.OrderBy(i => i).ToArray());
            Assert.False(model.Bounties.SelectSector(Floor1Layout.HubSectorId), "A charted room is not offered to scout.");
            Assert.Equal("Uncharted", model.Bounties.SectorState);

            // Gather offers every sector, marking the uncharted ones
            model.Bounties.Type = BountyType.GatherResource;
            Assert.Equal(sectors.All.Count, model.Bounties.SectorChoiceIds.Count);
            int deep = model.Bounties.SectorChoiceIds.ToList().IndexOf(Floor1Layout.DeepSeamSectorId);
            Assert.EndsWith(BountyBoardModel.AwaitsChartSuffix, model.Bounties.SectorChoiceLabels[deep]);

            // A reward above the treasury is refused before anything is queued
            model.Bounties.SelectSector(Floor1Layout.HubSectorId);
            model.Bounties.Reward = 50;
            model.Bounties.Post();
            Assert.Equal(FeedbackTone.Error, model.Bounties.Feedback.Tone);
            Assert.Contains("Insufficient treasury gold (20g available, need 50g)", model.Bounties.Feedback.Text);
            sim.Tick();
            Assert.Empty(sim.ActiveBounties);

            // Once every room is charted, Scout has nothing left to offer
            sectors.SetAllExplored(true);
            model.Bounties.Type = BountyType.ScoutSector;
            Assert.False(model.Bounties.CanPost);
            Assert.Equal(BountyBoardModel.AllChartedText, model.Bounties.Feedback.Text);
            model.Bounties.Post();
            Assert.Equal(BountyBoardModel.AllChartedText, model.Bounties.Feedback.Text);
            Assert.True(sim.TryGetTradeStall(stall, out var untouched) && untouched.TreasuryGold == 20);
        }

        [Fact]
        public void WithNothingBehindItTheModelDoesNothingAndNeverThrows()
        {
            var model = new DashboardModel(null);
            model.Refresh();
            model.ToggleMerchant();
            model.Merchant.Apply();
            model.ToggleBounties();
            model.Bounties.Post();
            model.Bounties.Cancel(999);
            model.SelectSector(1);
            model.SelectCrawler(new EntityId(1u, 2u, 3u, 4u));
            model.TogglePanels();
            model.SessionChanged();
            Assert.False(model.Dossier.HasCrawler);
            Assert.Equal("No intervention queue available.", model.Merchant.Feedback.Text);
        }

        [Fact]
        public void OnTheOutpostTheModelShowsWhatTheSimulationHolds()
        {
            using var sim = HeadlessSimulation.Create(SimulationSettings.DefaultSeed);
            sim.Respawn(OutpostSession.SessionId);
            using var frames = new WorldViewFrames();
            frames.Follow(sim.Host, sim.Registry, sim.Sectors, sim.Clock);
            var selection = new WorldSelection(frames, sim.Sectors, (IAgentIdentityService)sim.Services.GetService(typeof(IAgentIdentityService)));
            var model = new DashboardModel(DashboardContext.From(sim.Services), selection);
            model.Refresh();

            Assert.True(model.Context.TryGetStall(out var stall));
            Assert.Equal($"{stall.TreasuryGold} GOLD", model.Header.Treasury);
            Assert.Equal($"{stall.RationStock} RATIONS", model.Header.Rations);
            Assert.Equal("TICK: 00000", model.Header.Tick);
            Assert.Equal($"{sim.Simulation.ActiveBounties.Count} BOUNTIES", model.Header.Bounties);

            // Every sector is listed; the two frontier rooms are shrouded; the whole crew is at the Road Gate
            Assert.Equal(sim.Sectors.All.Count, model.Sectors.Sectors.Count);
            Assert.Equal("SHROUDED", model.Sectors.Sectors.First(r => r.SectorId == Floor1Layout.DeepSeamSectorId).Badge);
            Assert.Equal("12", model.Sectors.Sectors.First(r => r.SectorId == Floor1Layout.RoadGateSectorId).Badge);
            Assert.NotEqual(0, model.Sectors.SelectedSectorId);

            model.SelectSector(Floor1Layout.RoadGateSectorId);
            Assert.Equal("ROAD GATE", model.Sectors.Title);
            Assert.Equal(12, model.Sectors.Residents.Count);
            Assert.Equal(Floor1Layout.RoadGateSectorId, selection.SelectedSector);

            // A resident picked here shows in the dossier and is followed on the map
            var resident = model.Sectors.Residents[0];
            model.SelectCrawler(resident.CrawlerId);
            Assert.Equal(resident.CrawlerId, selection.SelectedCrawler);
            Assert.True(selection.Following);
            Assert.True(model.Dossier.HasCrawler);
            var agent = sim.Registry.GetEntity(resident.CrawlerId).Agent;
            Assert.Equal(agent.Hunger, model.Dossier.Hunger);
            Assert.Contains("[", model.Dossier.Name);
            Assert.True(model.Sectors.Residents.Single(r => r.Selected).CrawlerId == resident.CrawlerId);

            // A click on the map moves the dashboard to the crawler's room
            sim.Step(300);
            var walker = sim.Registry.ActiveEntities.First(e => e.Type == EntityType.Pawn && sim.Sectors.GetSectorAt(e.Position).Id != Floor1Layout.RoadGateSectorId);
            selection.ClickAt(walker.Position, walker.Id); // the crawler drawn there (picking: SelectionTests)
            Assert.Equal(sim.Sectors.GetSectorAt(walker.Position).Id, model.Sectors.SelectedSectorId);
            Assert.Equal(walker.Id, model.Dossier.CrawlerId);

            // A respawn drops the selection and closes the windows
            model.OpenBounties();
            sim.Respawn(OutpostSession.SessionId);
            selection.Clear();
            model.SessionChanged();
            Assert.False(model.Bounties.IsOpen);
            Assert.False(model.Dossier.HasCrawler);
            Assert.Equal("TICK: 00000", model.Header.Tick);
        }

        [Fact]
        public void CalmFromTheDossierCalmsTheCrawlerOnTheNextTick()
        {
            using var sim = HeadlessSimulation.Create(SimulationSettings.DefaultSeed);
            sim.Respawn(OutpostSession.SessionId);
            var model = new DashboardModel(DashboardContext.From(sim.Services));
            var crawler = sim.Registry.ActiveEntities.First(e => e.Type == EntityType.Pawn);
            model.SelectCrawler(crawler.Id);
            Assert.True(model.Dossier.CanCalm);

            crawler.Agent.Frustration = 90f; // the precondition, set by hand: this checks the button's path, not how frustration arises
            model.Dossier.Calm();
            Assert.Equal(90f, crawler.Agent.Frustration);
            sim.Step(1);
            Assert.Equal(0f, crawler.Agent.Frustration);

            Assert.False(new DashboardModel(null).Dossier.CanCalm);
        }

        [Fact]
        public void TheDossierNamesThePlayerAmongACrawlersAffinities()
        {
            using var sim = HeadlessSimulation.Create(SimulationSettings.DefaultSeed);
            sim.Respawn(OutpostSession.SessionId);
            sim.Step(600); // trades build feelings toward the outpost
            var trader = sim.Registry.ActiveEntities.First(e => e.Type == EntityType.Pawn && e.Agent.Memory?.AffinityMap.ContainsKey(WorldRegistry.PlayerEntityId) == true);
            var model = new DashboardModel(DashboardContext.From(sim.Services));
            model.SelectCrawler(trader.Id);
            Assert.Contains(model.Dossier.Affinities, line => line.StartsWith(DossierModel.PlayerName + ": W:"));
        }

        [Fact]
        public void ResidentsSayWhatTheyAreDoingAndEventsReadAsTheChronicle()
        {
            using var sim = HeadlessSimulation.Create(SimulationSettings.DefaultSeed);
            sim.Respawn(OutpostSession.SessionId);
            sim.Step(1500);
            var model = new DashboardModel(DashboardContext.From(sim.Services));
            // The busiest room: its residents' words and its events
            int busiest = sim.Sectors.All.Select(s => s.Id).OrderByDescending(id => model.Context.Zones.GetOccupancyCount(id, sim.Registry)).First();
            model.SelectSector(busiest);
            Assert.NotEmpty(model.Sectors.Residents);
            Assert.All(model.Sectors.Residents, r => Assert.False(string.IsNullOrEmpty(r.Activity)));
            var anyEvents = sim.Sectors.All.Select(s => s.Id).FirstOrDefault(id => { model.SelectSector(id); return model.Sectors.Events.Count > 0; });
            Assert.NotEqual(0, anyEvents);
            Assert.All(model.Sectors.Events, e => Assert.Matches(@"^\S.* \S", e));
            Assert.True(model.Sectors.Events.Count <= SectorPanelModel.MaxEvents);
        }
    }
}
