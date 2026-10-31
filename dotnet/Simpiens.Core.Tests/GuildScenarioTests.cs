using System.Collections.Generic;
using System.Linq;
using Simpiens.Cognition.Groups;
using Simpiens.Core.Session;
using Simpiens.Hosting;
using Simpiens.Intervention;
using Simpiens.Narrative;
using Simpiens.Simulation;
using Simpiens.Simulation.Economy;
using Xunit;

namespace Simpiens.Core.Tests
{
    /// <summary>
    /// Epic 10 G4's Definition of Done (epic §3), live and headless: on the real outpost with nothing injected, a scripted
    /// player that acts only through commands sees a trusted crawler sign on within the day and stay while paid; and,
    /// when the stall can no longer pay, a member goes unpaid and quits, keeping its settlement.
    /// </summary>
    public class GuildScenarioTests
    {
        private const long Day = 9000;

        /// <summary>Playing fair prices only, a resident signs on within the day; the fee and every visit's wage are paid; it keeps its settlement and its trust; the day holds.</summary>
        [Fact]
        public void TheTrustedSignOnAndThePaidStay()
        {
            using var sim = LocalSimulation.Create(SimulationSettings.DefaultSeed);
            sim.Respawn(OutpostSession.SessionId);
            var player = new GuildPlayer(sim, targetSize: 1);
            var watch = new OutpostWatch(sim);
            var guild = sim.Simulation.Guild;
            var groups = sim.Simulation.Groups;

            // Run until the first recruit has signed, noting the settlement it had just before
            byte settlementBefore = 0;
            while (guild.Members.Count == 0 && sim.Clock.CurrentTick < Day)
            {
                if (player.Recruited.Count > 0 && groups.TryGetMembership(player.Recruited[0], GroupType.Settlement, out var s)) settlementBefore = s.GroupId;
                watch.Run(1);
            }
            Assert.True(guild.Members.Count == 1, $"Nobody signed on within the day (asked at t{player.FirstRecruitTick}).");
            var contract = guild.Members[0];
            uint joined = contract.JoinTick;
            var ledger = sim.Simulation.EconomyLedger;
            Assert.Equal(1u, ledger.Flows.Fees);
            Assert.Equal((uint)sim.Simulation.Economy.Guild.SigningFee, ledger.Flows.FeeGold);
            if (settlementBefore != 0) Assert.True(groups.TryGetMembership(contract.CrawlerId, settlementBefore, out _), "The recruit kept its settlement (D1).");

            watch.Run(Day - sim.Clock.CurrentTick);
            watch.Finish();
            TestContext.Current.TestOutputHelper?.WriteLine($"Signed at t{joined} ({sim.Host.CrawlerNames[IndexOf(sim, contract.CrawlerId)]}); wages paid {ledger.Flows.Wages}\n" + watch.Report());

            uint interval = sim.Simulation.Economy.Supply.RestockIntervalTicks;
            uint visitsSince = (uint)(Day / interval - joined / interval);
            Assert.True(guild.IsMember(contract.CrawlerId), "The paid member stayed.");
            Assert.Equal(visitsSince, ledger.Flows.Wages);
            Assert.True(guild.TryGetContract(contract.CrawlerId, out var after) && after.UnpaidVisits == 0, "Never unpaid.");
            Assert.True(GuildRules(sim, contract.CrawlerId) >= sim.Simulation.Economy.Guild.QuitTrust, "Its trust stayed at the floor or above.");
            var failures = watch.DayFailures();
            Assert.True(failures.Count == 0, "The day failed:\n  " + string.Join("\n  ", failures));
        }

        /// <summary>Recruited at fair prices, then the stall goes broke under generous prices: the member goes unpaid and quits within two visits, keeping its settlement; the chronicle tells it; the ledger balances.</summary>
        [Fact]
        public void TheUnpaidQuit()
        {
            using var sim = LocalSimulation.Create(SimulationSettings.DefaultSeed);
            sim.Respawn(OutpostSession.SessionId);
            var player = new GuildPlayer(sim, targetSize: 1);
            var watch = new OutpostWatch(sim);
            var guild = sim.Simulation.Guild;
            var groups = sim.Simulation.Groups;

            while (guild.Members.Count == 0 && sim.Clock.CurrentTick < Day) watch.Run(1);
            Assert.True(guild.Members.Count == 1, "Nobody signed on within the day at fair prices.");
            player.Stop();
            var member = guild.Members[0].CrawlerId;
            bool hadSettlement = groups.TryGetMembership(member, GroupType.Settlement, out var settlement);

            // The soak's generous prices empty the treasury (economy model §7)
            player.SetPrices(EconomySoak.Resolve("generous", sim.Simulation.Economy).Prices);
            long unpaidAt = -1;
            for (long end = sim.Clock.CurrentTick + 2 * Day; guild.IsMember(member) && sim.Clock.CurrentTick < end;)
            {
                watch.Run(1);
                if (unpaidAt < 0 && guild.TryGetContract(member, out var c) && c.UnpaidVisits > 0) unpaidAt = sim.Clock.CurrentTick;
            }
            TestContext.Current.TestOutputHelper?.WriteLine($"Unpaid first at t{unpaidAt}; quit by t{sim.Clock.CurrentTick}; treasury {sim.Simulation.ActiveStalls[0].TreasuryGold}g");

            Assert.True(unpaidAt > 0, "The member was never unpaid: the stall didn't go broke.");
            Assert.False(guild.IsMember(member), "The unpaid member didn't quit.");
            uint interval = sim.Simulation.Economy.Supply.RestockIntervalTicks;
            Assert.True(sim.Clock.CurrentTick - unpaidAt <= interval * (sim.Simulation.Economy.Guild.MaxUnpaidVisits - 1) + 1, "It quit within two unpaid visits.");
            var told = new List<ChronicleEvent>();
            sim.Chronicle.GetActorEvents(member, told);
            Assert.Contains(told, e => e.EventType == ChronicleEventType.GuildQuit);
            Assert.Contains(told, e => e.EventType == ChronicleEventType.GuildUnpaid);
            if (hadSettlement && groups.TryGetGroup(settlement.GroupId, out _)) Assert.True(groups.TryGetMembership(member, settlement.GroupId, out _), "It kept its settlement (D1).");
            Assert.Equal(0, watch.ConservationViolations);
        }

        private static int GuildRules(LocalSimulation sim, EntityId crawlerId)
        {
            var state = sim.Host.CrawlerStates.First(s => s.AgentId == crawlerId);
            return Simpiens.Simulation.Guild.GuildRules.Standing(state, (uint)sim.Clock.CurrentTick).Trust;
        }

        private static int IndexOf(LocalSimulation sim, EntityId crawlerId)
        {
            for (int i = 0; i < sim.Host.CrawlerStates.Count; i++) if (sim.Host.CrawlerStates[i].AgentId == crawlerId) return i;
            return -1;
        }
    }
}
