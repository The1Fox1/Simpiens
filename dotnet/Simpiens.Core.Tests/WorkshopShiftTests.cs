using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
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
    /// Shifts (Epic 10 Part 2, W1.3, D13), on the real outpost: an artisan on shift at a built, ordered forge walks there
    /// and works it, tick by tick, until the make is done; off shift it leaves the forge be. A mechanism test: the smith's
    /// trust and the stall's treasury are set by hand, and the player's moves are typed commands. The live scenario, with
    /// nothing injected, is W1.5a's.
    /// </summary>
    public class WorkshopShiftTests
    {
        [Fact]
        public void AnArtisanOnShiftWorksTheForgeAndLeavesItBeOffShift()
        {
            // The stall sells pickaxes, so a finished one is stocked rather than held (W1.4 gives the outpost its row)
            var economy = EconomyTuning.Default.WithStallTreasury(1000)
                .WithItemTerms(ItemType.ToolPickaxe, new ItemTerms(EconomyTuning.Default.BaseValue(ItemType.ToolPickaxe), StallRole.Sells, 30, 0));
            using var sim = LocalSimulation.Create(SimulationSettings.DefaultSeed, services =>
            {
                services.RemoveAll<ISessionDefinition>();
                services.AddSingleton<ISessionDefinition>(new OutpostSession(OutpostSession.DefaultCrawlerCount, economy));
            });
            sim.Respawn(OutpostSession.SessionId);
            var workshops = sim.Simulation.Workshops;
            int forge = workshops.IndexOf(StationKind.Forge);
            var place = workshops.DefinitionAt(forge);
            var commands = (IPlayerInterventionService)sim.Services.GetService(typeof(IPlayerInterventionService));

            // A cautious resident who trusts the player (by hand: this checks the work, not the hiring)
            var smith = sim.Host.CrawlerStates.First(s => !s.IsDrifter && s.Personality.RiskTolerancePercent < economy.Workshops.MaxRiskTolerancePercent);
            smith.Memory.SetAffinity(WorldRegistry.PlayerEntityId, new AffinityRecord(warmth: 60, trust: 80, fear: 0, respect: 40, interactionCount: 1, lastInteractionTick: 0));
            commands.QueueInfluence(InfluenceEvent.CreateBuildStation("test", (byte)forge));
            commands.QueueInfluence(InfluenceEvent.CreateHireArtisan("test", smith.AgentId, (byte)forge, 10));
            commands.QueueInfluence(InfluenceEvent.CreateOrderStation("test", (byte)forge, ItemType.ToolPickaxe, 1));
            commands.QueueInfluence(InfluenceEvent.CreateSetShifts("test", smith.AgentId, ShiftBlocks.All));
            sim.Step(1);
            Assert.True(workshops.TryGetContract(smith.AgentId, out var contract) && contract.Shifts == ShiftBlocks.All, "Hired, on every block.");
            Assert.True(workshops[forge].Built && workshops[forge].HasOrder, "The forge stands with an order for a pickaxe.");

            // On shift: it walks to the forge and works it until the pickaxe is made (two game hours of work)
            int pickaxes = sim.Simulation.ActiveStalls[0].StockOf(ItemType.ToolPickaxe);
            long arrived = -1;
            for (long t = 0; t < 6 * DayClock.TicksPerHour && workshops[forge].Made == 0; t++)
            {
                sim.Step(1);
                if (arrived < 0 && (smith.Position - place.Position).SqrMagnitude <= place.Radius * place.Radius) arrived = sim.Clock.CurrentTick;
            }
            TestContext.Current.TestOutputHelper?.WriteLine($"at the forge t{arrived}; made {workshops[forge].Made} at t{sim.Clock.CurrentTick}; energy {smith.Energy:F0}, hunger {smith.Hunger:F0}");
            Assert.True(arrived >= 0, "The smith walked to the forge.");
            Assert.Equal(1u, workshops[forge].Made);
            Assert.Equal(pickaxes + 1, sim.Simulation.ActiveStalls[0].StockOf(ItemType.ToolPickaxe));
            Assert.False(workshops[forge].HasOrder, "The order is done.");

            // Off shift: another order, and no blocks; the forge waits
            commands.QueueInfluence(InfluenceEvent.CreateOrderStation("test", (byte)forge, ItemType.ToolPickaxe, 1));
            commands.QueueInfluence(InfluenceEvent.CreateSetShifts("test", smith.AgentId, 0));
            sim.Step(1);
            Assert.False(workshops.OrderFor(smith.AgentId, (uint)sim.Clock.CurrentTick).IsActive, "Off shift, the smith has no work at the forge.");
            sim.Step(DayClock.TicksPerHour);
            Assert.True(workshops[forge].HasOrder && workshops[forge].Progress == 0 && !workshops[forge].InputsTaken, "Off shift nobody works the forge.");
            Assert.Equal(0, sim.Simulation.IntentFaults);
        }
    }
}
