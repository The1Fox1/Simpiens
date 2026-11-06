using System.Collections.Generic;
using Simpiens.Cognition.Groups;
using Simpiens.Core.Session;
using Simpiens.Core.Session.Layouts;
using Simpiens.Hosting;
using Simpiens.Simulation;
using Simpiens.Simulation.Economy;
using Simpiens.Simulation.Guild;
using Simpiens.Simulation.Persistence;
using Simpiens.Testing.Environment;
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
        /// <summary>The soak's horizon: on the 24-hour day four residents qualify for the guild in about two days (Epic 10 Part 2, T4.2b).</summary>
        private const long ThreeDays = 3 * DayClock.TicksPerDay;

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
            uint caravanAtDelivery = 0, sharesAtDelivery = 0, shareGoldAtDelivery = 0;
            while (sim.Clock.CurrentTick < ThreeDays)
            {
                watch.Run(1);
                var expedition = guild.Parties[0].Expedition;
                if (ended >= 0 || (!expedition.IsActive && stages.Count == 0)) continue;
                if (expedition.IsActive)
                {
                    haulItem = expedition.HaulItem;
                    if (stages.Count == 0 || stages[stages.Count - 1] != expedition.Stage) stages.Add(expedition.Stage);
                    turnedBack |= expedition.TurnedBack;
                    for (int i = 0; i < guild.Parties[0].Members.Count; i++)
                    {
                        downed |= guild.Parties[0].Members[i].Status == ExpeditionMemberStatus.Downed;
                        if (expedition.Stage != ExpeditionStage.Muster) allWent &= guild.Parties[0].Members[i].IsWithParty;
                    }
                    continue;
                }
                ended = sim.Clock.CurrentTick;
                stockAtDelivery = sim.Simulation.ActiveStalls[0].StockOf(haulItem);
                caravanAtDelivery = ledger.GetFaucetTotal("CaravanSalvageSales");
                sharesAtDelivery = ledger.Flows.Shares;
                shareGoldAtDelivery = ledger.Flows.ShareGold;
            }
            watch.Finish();
            TestContext.Current.TestOutputHelper?.WriteLine($"recruited {player.Recruited.Count}, gear given {player.GearGiven}, sent at t{player.FirstDispatchTick}, " +
                $"stages {string.Join(" > ", stages)}, ended at t{ended}; shares {ledger.Flows.Shares} ({ledger.Flows.ShareGold}g)\n" + watch.Report());

            Assert.Equal(4, guild.Members.Count);
            Assert.True(player.GearGiven >= 4, "The player outfitted the four from the stall's stock.");
            Assert.True(player.FirstDispatchTick >= 0 && ended > player.FirstDispatchTick, "An expedition was sent and came home within three days.");
            Assert.Equal(new[] { ExpeditionStage.Muster, ExpeditionStage.March, ExpeditionStage.Work, ExpeditionStage.Return }, stages);
            Assert.True(allWent, "All four set out and came home together.");
            Assert.False(turnedBack, "Geared, the party didn't turn back.");
            Assert.False(downed, "Nobody was downed.");
            Assert.Equal(4u, sharesAtDelivery); // each member its share, paid on delivery
            uint value = sim.Simulation.Economy.Supply.Terms(haulItem).Price;
            Assert.Equal(Floor1Layout.GallerySeamYield * value * sim.Simulation.Economy.Guild.Expedition.SharePercent / 100u, shareGoldAtDelivery);
            Assert.True(sim.Simulation.ActiveStalls[0].StockOf(haulItem) < stockAtDelivery && ledger.GetFaucetTotal("CaravanSalvageSales") > caravanAtDelivery,
                "The caravan bought the haul at its premium.");
            Assert.Equal(0, watch.AloneInTheChamber);
            var failures = watch.DayFailures();
            Assert.True(failures.Count == 0, "The days failed:\n  " + string.Join("\n  ", failures));
        }

        /// <summary>
        /// Epic 10 G6.1: the whole loop plays through on the default seed, nothing injected. Playing fair prices, the
        /// scripted player recruits as crawlers qualify and pays every wage; once four have signed it outfits them and
        /// sends them whenever the seam is back; the haul is sold and the loop goes round again. Three game days on the
        /// 24-hour day (Epic 10 Part 2, T5): the guild fills on day 2 and the hauls come home at about t21050 and t27670.
        /// </summary>
        [Fact]
        public void TheGuildLoopPlaysThrough()
        {
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
            while (sim.Clock.CurrentTick < ThreeDays)
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
            for (int i = 0; i < guild.Members.Count; i++) visits += (uint)(ThreeDays / interval - guild.Members[i].JoinTick / interval);
            Assert.Equal(visits, ledger.Flows.Wages); // every visit's wage, for every member
            Assert.True(hauls >= 2, $"At least two expeditions came home with glowstone ({hauls}).");
            Assert.Equal(0, leftSettlement);
            Assert.Equal(0, watch.AloneInTheChamber);
            var failures = watch.DayFailures(); // trust up for 8 or more of 12, nobody starving, the ledger balanced, the rest of the day
            Assert.True(failures.Count == 0, "The days failed:\n  " + string.Join("\n  ", failures));
        }

        /// <summary>
        /// Epic 10 G6.2: the game saved while the party works the seam loads into a fresh session byte for byte, with the
        /// same world hash (the groups and the guild included), and plays on: the party comes home with its haul, the
        /// wages are paid, the ledger balances and no intent handler throws. The scripted player drives both sessions, as a
        /// player would play on after loading.
        /// </summary>
        [Fact]
        public void ASaveMidExpeditionLoadsAndPlaysOn()
        {
            const long PlaysOn = 8 * DayClock.TicksPerHour; // home from the seam, through a caravan visit
            using var played = LocalSimulation.Create(SimulationSettings.DefaultSeed);
            played.Respawn(OutpostSession.SessionId);
            new GuildPlayer(played, targetSize: 4).SendExpeditions(Floor1Layout.DrownedGallerySectorId, Floor1Layout.GallerySeamYield, RetreatRule.Steady);
            while (played.Simulation.Guild.Parties[0].Expedition.Stage != ExpeditionStage.Work && played.Clock.CurrentTick < ThreeDays) played.Step(1);
            Assert.Equal(ExpeditionStage.Work, played.Simulation.Guild.Parties[0].Expedition.Stage);
            long tick = played.Clock.CurrentTick;
            byte[] saved = SaveSerializer.Serialize(played.Host.Capture());
            var savedHash = WorldStateHash.Capture(tick, played.Host, played.Simulation, played.Sectors, includeWork: false);
            var expedition = played.Simulation.Guild.Parties[0].Expedition;

            // A fresh session, loaded
            using var loaded = LocalSimulation.Create(SimulationSettings.DefaultSeed);
            loaded.Respawn(OutpostSession.SessionId);
            Assert.True(loaded.Host.TryRestore(SaveSerializer.Deserialize<SessionSave>(saved), out string refusal), $"The save was refused: {refusal}");
            Assert.Equal(tick, loaded.Clock.CurrentTick);
            Assert.Equal(saved, SaveSerializer.Serialize(loaded.Host.Capture()));
            Assert.Null(savedHash.DescribeDifference(WorldStateHash.Capture(tick, loaded.Host, loaded.Simulation, loaded.Sectors, includeWork: false)));
            var guild = loaded.Simulation.Guild;
            Assert.True(guild.HasExpedition && guild.Parties[0].Expedition.Stage == ExpeditionStage.Work && guild.Parties[0].Expedition.StageTick == expedition.StageTick);
            Assert.True(loaded.Simulation.Groups.TryGetGroup(expedition.PartyGroupId, out var party) && party.ParentGroupId == guild.GroupId
                && party.ActiveDirective.Type == Simpiens.Cognition.Groups.DirectiveType.Expedition, "The party, its guild and its order came back.");

            // It plays on
            new GuildPlayer(loaded, targetSize: 4).SendExpeditions(Floor1Layout.DrownedGallerySectorId, Floor1Layout.GallerySeamYield, RetreatRule.Steady);
            var watch = new OutpostWatch(loaded);
            var ledger = loaded.Simulation.EconomyLedger;
            uint shares = ledger.Flows.Shares, wages = ledger.Flows.Wages;
            long home = -1;
            while (loaded.Clock.CurrentTick < tick + PlaysOn)
            {
                watch.Run(1);
                if (home < 0 && !guild.HasExpedition) home = loaded.Clock.CurrentTick;
            }
            TestContext.Current.TestOutputHelper?.WriteLine($"saved at t{tick} at work; home at t{home}; shares {ledger.Flows.Shares - shares}, wages {ledger.Flows.Wages - wages}");

            Assert.True(home > tick, "The party came home.");
            Assert.True(ledger.Flows.Shares > shares, "It came home with its haul, and the shares were paid.");
            uint interval = loaded.Simulation.Economy.Supply.RestockIntervalTicks;
            uint visits = (uint)((tick + PlaysOn) / interval - tick / interval);
            Assert.Equal(visits * (uint)guild.Members.Count, ledger.Flows.Wages - wages);
            for (int i = 0; i < guild.Members.Count; i++) Assert.Equal(0, guild.Members[i].UnpaidVisits);
            Assert.Equal(0, watch.ConservationViolations);
            Assert.Equal(0, watch.IntentFaults);
        }
    }
}
