using System.Collections.Generic;
using System.Diagnostics;
using Simpiens.Core.Session;
using Simpiens.Hosting;
using Simpiens.Simulation;
using Simpiens.Simulation.Economy;
using Xunit;

namespace Simpiens.Core.Tests
{
    /// <summary>
    /// Measurement only (Epic MAP Phase 3; Unity's PopulationScalingProbe, Epic CLIENT M2.8): the outpost with 12, 16 and
    /// 24 crawlers, stepped as fast as the machine allows. Prints ticks a second and checks integrity (the ledger
    /// balances). Unity also sampled cognition latency in flight; headless every decision lands on the next tick
    /// (CognitionScheduler), so there is no latency to sample. Run it with
    /// <c>dotnet dotnet/Simpiens.Core.Tests/bin/Release/net10.0/Simpiens.Core.Tests.dll -method "*PopulationScaling*" -explicit only -showLiveOutput</c>.
    /// </summary>
    public class PopulationScalingProbe
    {
        [Theory(Explicit = true)]
        [InlineData(12)]
        [InlineData(16)]
        [InlineData(24)]
        public void MeasurePopulationScaling(int crawlers)
        {
            const long WarmupTicks = 200, MeasuredTicks = 20000;
            using var sim = LocalSimulation.Create(SimulationSettings.DefaultSeed);
            sim.Host.Respawn(new OutpostSession(crawlers));
            sim.Step(WarmupTicks);

            var watch = Stopwatch.StartNew();
            sim.Step(MeasuredTicks);
            watch.Stop();

            uint stallGold = 0, crawlerGold = 0;
            foreach (var stall in sim.Simulation.ActiveStalls) stallGold += stall.TreasuryGold;
            foreach (var state in sim.Host.CrawlerStates) crawlerGold += (uint)state.Inventory.Gold;
            bool balanced = sim.Simulation.EconomyLedger.ValidateConservation(stallGold, crawlerGold, out int discrepancy);
            TestContext.Current.TestOutputHelper?.WriteLine($"{crawlers} crawlers: {MeasuredTicks / watch.Elapsed.TotalSeconds:F0} ticks/s ({MeasuredTicks} ticks in {watch.Elapsed.TotalSeconds:F1} s); ledger balanced {balanced}");
            Assert.True(balanced, $"The ledger is off by {discrepancy}g.");
        }

        /// <summary>
        /// Measurement only (Epic 10 G5.5): an expedition's cost. The default seed with four residents signed on and geared
        /// (injected), the same 800 ticks with the party sent to the Drowned Gallery and without; five runs each, alternating.
        /// <c>-method "*MeasureAnExpeditionsCost*" -explicit only -showLiveOutput</c>.
        /// </summary>
        [Fact(Explicit = true)]
        public void MeasureAnExpeditionsCost()
        {
            const int Runs = 5;
            const long Window = 800;
            double sentSeconds = 0, homeSeconds = 0;
            for (int run = 0; run < Runs * 2; run++)
            {
                bool send = run % 2 == 0;
                using var sim = LocalSimulation.Create(SimulationSettings.DefaultSeed);
                sim.Respawn(OutpostSession.SessionId);
                sim.Step(300);
                var host = (Simpiens.Simulation.Guild.IGuildHost)sim.Simulation;
                var guild = sim.Simulation.Guild;
                var members = new List<EntityId>(4);
                for (int i = 0; members.Count < 4; i++)
                {
                    var crawler = sim.Host.CrawlerStates[i];
                    if (crawler.IsDrifter) continue;
                    crawler.Memory.SetAffinity(Simpiens.Simulation.WorldRegistry.PlayerEntityId, new Simpiens.Cognition.Social.AffinityRecord(60, 80, 0, 0, 1, 300));
                    guild.Recruit(host, crawler.AgentId, Simpiens.Simulation.Guild.GuildRules.ReservationWage(crawler, sim.Simulation.Economy.Guild), 300);
                    crawler.Inventory.TryEquip(new ItemInstance(1, ItemType.HazardGear, 1, 30, durability: 500));
                    members.Add(crawler.AgentId);
                }
                if (send)
                {
                    var commands = (Simpiens.Intervention.IPlayerInterventionService)sim.Services.GetService(typeof(Simpiens.Intervention.IPlayerInterventionService));
                    commands.QueueInfluence(Simpiens.Intervention.InfluenceEvent.CreateFormParty("Probe", new Simpiens.Intervention.FormPartyCommand(1, members.ToArray())));
                    commands.QueueInfluence(Simpiens.Intervention.InfluenceEvent.CreateDispatchExpedition("Probe",
                        new Simpiens.Intervention.DispatchExpeditionCommand(1, Simpiens.Core.Session.Layouts.Floor1Layout.DrownedGallerySectorId, 4,
                            Simpiens.Simulation.Guild.RetreatRule.Steady)));
                }
                var watch = Stopwatch.StartNew();
                sim.Step(Window);
                watch.Stop();
                if (send) sentSeconds += watch.Elapsed.TotalSeconds; else homeSeconds += watch.Elapsed.TotalSeconds;
            }
            TestContext.Current.TestOutputHelper?.WriteLine($"{Window} ticks: party sent {Runs * Window / sentSeconds:F0} ticks/s, kept home {Runs * Window / homeSeconds:F0} ticks/s ({(sentSeconds / homeSeconds - 1) * 100:+0;-0}% time)");
        }
    }
}
