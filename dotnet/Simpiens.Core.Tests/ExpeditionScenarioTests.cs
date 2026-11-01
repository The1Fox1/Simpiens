using System.Collections.Generic;
using Simpiens.Cognition.Groups;
using Simpiens.Core.Session;
using Simpiens.Core.Session.Layouts;
using Simpiens.Hosting;
using Simpiens.Simulation;
using Simpiens.Simulation.Economy;
using Simpiens.Simulation.Guild;
using Xunit;

namespace Simpiens.Core.Tests
{
    /// <summary>
    /// Epic 10 G5's Definition of Done (epic §3), live and headless: on the real outpost with nothing injected, a scripted
    /// player that acts only through commands recruits four, outfits them from the stall's stock and sends them to the
    /// Drowned Gallery, where nobody goes alone. They muster, march, work the seam and come back with glowstone; the
    /// caravan buys it at its premium, each member is paid a share, nobody is downed and the days' checks hold. And G6's
    /// first: the whole loop, recruiting to the second haul, plays through.
    /// </summary>
    public class ExpeditionScenarioTests
    {
        private const long HalfAnHour = 18000;

        [Fact]
        public void AGearedPartyBringsBackGlowstone()
        {
            using var sim = LocalSimulation.Create(SimulationSettings.DefaultSeed);
            sim.Respawn(OutpostSession.SessionId);
            var player = new GuildPlayer(sim, targetSize: 4);
            player.SendExpeditions(Floor1Layout.DrownedGallerySectorId, Floor1Layout.GallerySeamYield, RetreatRule.Steady);
            var watch = new OutpostWatch(sim);
            var guild = sim.Simulation.Guild;
            var ledger = sim.Simulation.EconomyLedger;

            // The first expedition, from dispatch to its end: its stages, whether it turned back, anyone downed
            var stages = new List<ExpeditionStage>();
            bool turnedBack = false, downed = false, allWent = true;
            var haulItem = ItemType.None;
            long ended = -1;
            int stockAtDelivery = 0;
            uint caravanAtDelivery = 0;
            while (sim.Clock.CurrentTick < HalfAnHour)
            {
                watch.Run(1);
                var expedition = guild.Expedition;
                if (ended >= 0 || (!expedition.IsActive && stages.Count == 0)) continue;
                if (expedition.IsActive)
                {
                    haulItem = expedition.HaulItem;
                    if (stages.Count == 0 || stages[stages.Count - 1] != expedition.Stage) stages.Add(expedition.Stage);
                    turnedBack |= expedition.TurnedBack;
                    for (int i = 0; i < guild.Party.Count; i++)
                    {
                        downed |= guild.Party[i].Status == ExpeditionMemberStatus.Downed;
                        if (expedition.Stage != ExpeditionStage.Muster) allWent &= guild.Party[i].IsWithParty;
                    }
                    continue;
                }
                ended = sim.Clock.CurrentTick;
                stockAtDelivery = sim.Simulation.ActiveStalls[0].StockOf(haulItem);
                caravanAtDelivery = ledger.GetFaucetTotal("CaravanSalvageSales");
            }
            watch.Finish();
            TestContext.Current.TestOutputHelper?.WriteLine($"recruited {player.Recruited.Count}, gear given {player.GearGiven}, sent at t{player.FirstDispatchTick}, " +
                $"stages {string.Join(" > ", stages)}, ended at t{ended}; shares {ledger.Flows.Shares} ({ledger.Flows.ShareGold}g)\n" + watch.Report());

            Assert.Equal(4, guild.Members.Count);
            Assert.True(player.GearGiven >= 4, "The player outfitted the four from the stall's stock.");
            Assert.True(player.FirstDispatchTick >= 0 && ended > player.FirstDispatchTick, "An expedition was sent and came home within half an hour.");
            Assert.Equal(new[] { ExpeditionStage.Muster, ExpeditionStage.March, ExpeditionStage.Work, ExpeditionStage.Return }, stages);
            Assert.True(allWent, "All four set out and came home together.");
            Assert.False(turnedBack, "Geared, the party didn't turn back.");
            Assert.False(downed, "Nobody was downed.");
            Assert.Equal(4u, ledger.Flows.Shares); // each member its share, paid on delivery
            uint value = sim.Simulation.Economy.Supply.Terms(haulItem).Price;
            Assert.Equal(Floor1Layout.GallerySeamYield * value * sim.Simulation.Economy.Guild.Expedition.SharePercent / 100u, ledger.Flows.ShareGold);
            Assert.True(sim.Simulation.ActiveStalls[0].StockOf(haulItem) < stockAtDelivery && ledger.GetFaucetTotal("CaravanSalvageSales") > caravanAtDelivery,
                "The caravan bought the haul at its premium.");
            Assert.Equal(0, watch.AloneInTheChamber);
            var failures = watch.DayFailures();
            Assert.True(failures.Count == 0, "The days failed:\n  " + string.Join("\n  ", failures));
        }

        /// <summary>
        /// Epic 10 G6.1: the whole loop plays through on the default seed, nothing injected. Playing fair prices, the
        /// scripted player recruits as crawlers qualify and pays every wage; once four have signed it outfits them and
        /// sends them whenever the seam is back; the haul is sold and the loop goes round again. 45 minutes, not the plan's
        /// 30: the guild fills at t13604 and the seam regrows for 10 minutes after it is spent, so the second haul comes
        /// home at about t20900.
        /// </summary>
        [Fact]
        public void TheGuildLoopPlaysThrough()
        {
            const long ThreeQuartersOfAnHour = 27000;
            using var sim = LocalSimulation.Create(SimulationSettings.DefaultSeed);
            sim.Respawn(OutpostSession.SessionId);
            var player = new GuildPlayer(sim, targetSize: 4);
            player.SendExpeditions(Floor1Layout.DrownedGallerySectorId, Floor1Layout.GallerySeamYield, RetreatRule.Steady);
            var watch = new OutpostWatch(sim);
            var guild = sim.Simulation.Guild;
            var groups = sim.Simulation.Groups;
            var ledger = sim.Simulation.EconomyLedger;

            // Each member's settlement when it signed: recruiting, sending and disbanding a party never take it out (G4's D1)
            var settlements = new Dictionary<EntityId, byte>();
            int unpaid = 0, smallest = 0, hauls = 0, leftSettlement = 0;
            uint shares = 0;
            bool out_ = false;
            var homes = new List<long>();
            while (sim.Clock.CurrentTick < ThreeQuartersOfAnHour)
            {
                watch.Run(1);
                for (int i = 0; i < guild.Members.Count; i++)
                {
                    var contract = guild.Members[i];
                    if (!settlements.ContainsKey(contract.CrawlerId))
                    {
                        settlements[contract.CrawlerId] = groups.TryGetMembership(contract.CrawlerId, GroupType.Settlement, out var s) ? s.GroupId : (byte)0;
                    }
                    if (contract.UnpaidVisits > unpaid) unpaid = contract.UnpaidVisits;
                }
                if (settlements.Count > 0 && guild.Members.Count < settlements.Count) smallest = settlements.Count - guild.Members.Count;
                if (guild.HasExpedition) out_ = true;
                else if (out_)
                {
                    out_ = false;
                    if (ledger.Flows.Shares > shares) { hauls++; homes.Add(sim.Clock.CurrentTick); }
                    shares = ledger.Flows.Shares;
                    foreach (var kv in settlements)
                    {
                        if (kv.Value != 0 && groups.TryGetGroup(kv.Value, out _) && !groups.TryGetMembership(kv.Key, kv.Value, out _)) leftSettlement++;
                    }
                }
            }
            watch.Finish();
            TestContext.Current.TestOutputHelper?.WriteLine($"signed {settlements.Count}, sent {player.Dispatches}, home with glowstone at {string.Join(", ", homes)}; " +
                $"wages {ledger.Flows.Wages} ({ledger.Flows.WageGold}g), shares {ledger.Flows.Shares} ({ledger.Flows.ShareGold}g)\n" + watch.Report());

            Assert.Equal(4, guild.Members.Count);
            Assert.True(settlements.Count == 4 && smallest == 0, "Four signed and nobody quit.");
            Assert.Equal(0, unpaid);
            uint interval = sim.Simulation.Economy.Supply.RestockIntervalTicks;
            uint visits = 0;
            for (int i = 0; i < guild.Members.Count; i++) visits += (uint)(ThreeQuartersOfAnHour / interval - guild.Members[i].JoinTick / interval);
            Assert.Equal(visits, ledger.Flows.Wages); // every visit's wage, for every member
            Assert.True(hauls >= 2, $"At least two expeditions came home with glowstone ({hauls}).");
            Assert.Equal(0, leftSettlement);
            Assert.Equal(0, watch.AloneInTheChamber);
            var failures = watch.DayFailures(); // trust up for 8 or more of 12, nobody starving, the ledger balanced, the rest of the day
            Assert.True(failures.Count == 0, "The days failed:\n  " + string.Join("\n  ", failures));
        }
    }
}
