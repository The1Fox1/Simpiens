using System.Collections.Generic;
using System.Linq;
using Simpiens.Core.Session;
using Simpiens.Core.Session.Layouts;
using Simpiens.Hosting;
using Simpiens.Simulation;
using Simpiens.Simulation.Economy;
using Simpiens.Simulation.Guild;
using Simpiens.Simulation.Items;
using Xunit;

namespace Simpiens.Core.Tests
{
    /// <summary>
    /// W2's DoD, <c>ADraughtKeepsAPartyWorking</c> (Epic 10 Part 2), live and headless with nothing injected: on the default
    /// seed, twice, the scripted players build and staff the still, which makes draughts from the stall's herbs, and the
    /// guild, once it holds eight, sends four bare members to the Drowned Gallery under Steady. The two runs are the same
    /// until the dispatch: once each member carries two draughts, once none. Without, the party turns back; with, its
    /// members drink at the treat line and it works on until the seam is spent, longer, its haul at least as big, nobody
    /// downed. The ledger balances and the days' checks hold on the run with draughts.
    /// </summary>
    public class DraughtScenarioTests
    {
        private const long Horizon = 6 * DayClock.TicksPerDay;
        private const int Stockpile = 8;

        private sealed class Trip
        {
            public long Dispatched = -1, Work;
            public int Haul, Carried, Drunk, Downed, MadeAtStill, ConservationViolations;
            public bool TurnedBack, Ended;
            public List<string> DayFailures = new List<string>();
            public string Report = string.Empty;
        }

        private static Trip Run(byte draughts, bool watchTheDay)
        {
            using var sim = LocalSimulation.Create(SimulationSettings.DefaultSeed);
            sim.Respawn(OutpostSession.SessionId);
            var still = new WorkshopPlayer(sim, StationKind.Still, ItemType.HealingDraught);
            var player = new GuildPlayer(sim, targetSize: 4);
            player.SendExpeditions(Floor1Layout.DrownedGallerySectorId, Floor1Layout.GallerySeamYield, RetreatRule.Steady, outfit: false, draughts: draughts, stockpile: Stockpile);
            var watch = watchTheDay ? new OutpostWatch(sim) : null;
            var guild = sim.Simulation.Guild;
            var host = (IGuildHost)sim.Simulation;
            var trip = new Trip();
            var downed = new HashSet<EntityId>();
            var last = ExpeditionStage.Muster;
            long workStart = 0;
            while (sim.Clock.CurrentTick < Horizon && !trip.Ended)
            {
                if (watch != null) watch.Run(1); else sim.Step(1);
                var expedition = guild.Expedition;
                if (expedition.IsActive)
                {
                    if (trip.Dispatched < 0) trip.Dispatched = sim.Clock.CurrentTick;
                    if (expedition.Stage == ExpeditionStage.March && last == ExpeditionStage.Muster)
                        trip.Carried = guild.Party.Where(m => m.IsWithParty).Sum(m => host.TryGetCrawler(m.CrawlerId, out var c) ? c.Inventory.GetItemCount(ItemType.HealingDraught) : 0);
                    if (expedition.Stage == ExpeditionStage.Work && last != ExpeditionStage.Work) workStart = sim.Clock.CurrentTick;
                    if (expedition.Stage == ExpeditionStage.Return && last == ExpeditionStage.Work) trip.Work = sim.Clock.CurrentTick - workStart;
                    if (expedition.Stage == ExpeditionStage.Work || expedition.Stage == ExpeditionStage.Return) trip.Haul = System.Math.Max(trip.Haul, guild.Gathered(host));
                    foreach (var member in guild.Party) if (member.Status == ExpeditionMemberStatus.Downed) downed.Add(member.CrawlerId);
                    trip.TurnedBack = expedition.TurnedBack;
                    last = expedition.Stage;
                }
                else if (trip.Dispatched >= 0) trip.Ended = true;
            }
            trip.Downed = downed.Count;
            trip.Drunk = sim.Host.CrawlerStates.Sum(s => s.Activity.DraughtsDrunk);
            trip.MadeAtStill = (int)sim.Simulation.Workshops[sim.Simulation.Workshops.IndexOf(StationKind.Still)].Made;
            if (watch != null)
            {
                watch.Finish();
                trip.ConservationViolations = watch.ConservationViolations;
                trip.DayFailures = watch.DayFailures();
                trip.Report = watch.Report();
            }
            return trip;
        }

        [Fact]
        public void ADraughtKeepsAPartyWorking()
        {
            var without = Run(draughts: 0, watchTheDay: false);
            var with = Run(draughts: 2, watchTheDay: true);
            string Describe(Trip t) => $"sent t{t.Dispatched}, carried {t.Carried}, worked {t.Work} ticks, haul {t.Haul}{(t.TurnedBack ? ", turned back" : "")}, downed {t.Downed}, drunk {t.Drunk}; the still made {t.MadeAtStill}";
            TestContext.Current.TestOutputHelper?.WriteLine($"without: {Describe(without)}\nwith:    {Describe(with)}\n{with.Report}");

            Assert.True(without.Ended && with.Ended, "Both parties went and came home.");
            Assert.Equal(without.Dispatched, with.Dispatched); // the same until the dispatch: the guild waited for the same stockpile
            Assert.True(with.MadeAtStill >= Stockpile, "The still made the draughts from the stall's herbs.");
            Assert.Equal(0, without.Carried);
            Assert.Equal(2 * 4, with.Carried);
            Assert.True(without.TurnedBack, "Without draughts, the retreat rule turned the bare party back.");
            Assert.False(with.TurnedBack, "With draughts, the party worked on.");
            Assert.True(with.Drunk > 0, "Members drank at the treat line.");
            Assert.True(with.Work > without.Work, "It worked longer.");
            Assert.True(with.Haul >= without.Haul && with.Haul == Floor1Layout.GallerySeamYield, "It brought the whole seam home, at least as much.");
            Assert.Equal(0, without.Downed);
            Assert.Equal(0, with.Downed);
            Assert.Equal(0, with.ConservationViolations);
            Assert.True(with.DayFailures.Count == 0, "The days failed:\n  " + string.Join("\n  ", with.DayFailures));
        }
    }
}
