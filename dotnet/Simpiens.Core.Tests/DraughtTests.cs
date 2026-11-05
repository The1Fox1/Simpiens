using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Simpiens.Cognition.Contracts;
using Simpiens.Cognition.Social;
using Simpiens.Core.Session;
using Simpiens.Hosting;
using Simpiens.Intervention;
using Simpiens.Simulation;
using Simpiens.Simulation.Economy;
using Simpiens.Simulation.Items;
using Simpiens.Simulation.Workshops;
using Xunit;

namespace Simpiens.Core.Tests
{
    /// <summary>
    /// The draught (Epic 10 Part 2, W2.1, D42), on the real outpost: the still, built and staffed as the forge is, turns
    /// two of the stall's herbs into a draught in its stock; a hurt crawler carrying one drinks it under the treat line and
    /// heals 80, herbs first when it carries both. Mechanism tests: the hire's trust and the hurt are set by hand.
    /// </summary>
    public class DraughtTests
    {
        [Fact]
        public void TheStillTurnsTwoHerbsIntoADraught()
        {
            var economy = EconomyTuning.Default.WithStallTreasury(1000);
            using var sim = LocalSimulation.Create(SimulationSettings.DefaultSeed, services =>
            {
                services.RemoveAll<ISessionDefinition>();
                services.AddSingleton<ISessionDefinition>(new OutpostSession(OutpostSession.DefaultCrawlerCount, economy));
            });
            sim.Respawn(OutpostSession.SessionId);
            var workshops = sim.Simulation.Workshops;
            int still = workshops.IndexOf(StationKind.Still);
            var commands = (IPlayerInterventionService)sim.Services.GetService(typeof(IPlayerInterventionService));
            var brewer = sim.Host.CrawlerStates.First(s => !s.IsDrifter && s.Personality.RiskTolerancePercent < economy.Workshops.MaxRiskTolerancePercent);
            brewer.Memory.SetAffinity(WorldRegistry.PlayerEntityId, new AffinityRecord(warmth: 60, trust: 80, fear: 0, respect: 40, interactionCount: 1, lastInteractionTick: 0));
            commands.QueueInfluence(InfluenceEvent.CreateBuildStation("test", (byte)still));
            commands.QueueInfluence(InfluenceEvent.CreateHireArtisan("test", brewer.AgentId, (byte)still, 10));
            commands.QueueInfluence(InfluenceEvent.CreateOrderStation("test", (byte)still, ItemType.HealingDraught, 1));
            commands.QueueInfluence(InfluenceEvent.CreateSetShifts("test", brewer.AgentId, ShiftBlocks.All));
            sim.Step(1);
            int herbs = sim.Simulation.ActiveStalls[0].StockOf(ItemType.MedicinalHerb);
            Assert.True(herbs >= 2, "The stall opens with herbs.");

            for (long t = 0; t < 4 * DayClock.TicksPerHour && workshops[still].Made == 0; t++) sim.Step(1);
            var stall = sim.Simulation.ActiveStalls[0];
            TestContext.Current.TestOutputHelper?.WriteLine($"made {workshops[still].Made} at t{sim.Clock.CurrentTick}; herbs {herbs} -> {stall.StockOf(ItemType.MedicinalHerb)}");
            Assert.Equal(1u, workshops[still].Made);
            Assert.Equal(1, stall.StockOf(ItemType.HealingDraught));
            Assert.Equal(0, sim.Simulation.IntentFaults);
        }

        [Fact]
        public void AHurtCrawlerDrinksItsDraughtAndHealsHerbsFirst()
        {
            using var sim = LocalSimulation.Create(SimulationSettings.DefaultSeed);
            sim.Respawn(OutpostSession.SessionId);
            var crawler = sim.Host.CrawlerStates.First(s => !s.IsDrifter);
            var node = sim.Registry.GetEntity(crawler.AgentId);
            crawler.Inventory.TryAddItem(new ItemInstance(7, ItemType.HealingDraught, 1, 20), out _);
            crawler.Inventory.TryAddItem(new ItemInstance(3, ItemType.MedicinalHerb, 1, 6), out _);
            crawler.Health = 15f; // by hand: hurt
            var sm = (SimulationManager)sim.Simulation;

            var state = new ActiveIntentState { Intent = new TreatWoundsIntent(crawler.AgentId), LastSampledPosition = node.Position };
            Assert.Equal(IntentResult.Success, Simpiens.Testing.Environment.HeadlessIntents.RunToCompletion(sm, node, state));
            Assert.Equal(45f, crawler.Health, 3); // the herb first: 30
            Assert.Equal(0, crawler.Inventory.GetItemCount(ItemType.MedicinalHerb));
            Assert.Equal(1, crawler.Inventory.GetItemCount(ItemType.HealingDraught));

            state = new ActiveIntentState { Intent = new TreatWoundsIntent(crawler.AgentId), LastSampledPosition = node.Position };
            Assert.Equal(IntentResult.Success, Simpiens.Testing.Environment.HeadlessIntents.RunToCompletion(sm, node, state));
            Assert.Equal(100f, crawler.Health, 3); // then the draught: 80, to the most a crawler has
            Assert.Equal(0, crawler.Inventory.GetItemCount(ItemType.HealingDraught));
            Assert.Equal(1, crawler.Activity.DraughtsDrunk);
            Assert.Equal(1, crawler.Activity.HerbsEaten);
        }
    }
}
