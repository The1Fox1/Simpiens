using System.Collections.Generic;
using System.Linq;
using Simpiens.Core.Session;
using Simpiens.Core.Session.Layouts;
using Simpiens.Hosting;
using Simpiens.Simulation;
using Simpiens.Simulation.Guild;
using Xunit;

namespace Simpiens.Core.Tests
{
    /// <summary>
    /// W5's DoD, <c>TwoPartiesOutAtOnce</c> (Epic 10 Part 2), live and headless with nothing injected, on the default seed.
    /// The scripted guild player keeps eight members and forms two parties of four (D65), one for the Drowned Gallery's
    /// seam and one for the Glow Caves, outfitted from the stall's stock and sent together (D66). Both are out at once,
    /// each works its own sector and comes back with a haul; every wage is paid, the lodging fee taken from it (D63); every
    /// member sleeps in the bunk the barracks keep for it (D62), not in the common bunks. The ledger balances and the days'
    /// checks hold.
    /// </summary>
    public class GuildhallScenarioTests
    {
        private const long Horizon = 6 * DayClock.TicksPerDay;

        [Fact]
        public void TwoPartiesOutAtOnce()
        {
            using var sim = LocalSimulation.Create(SimulationSettings.DefaultSeed);
            sim.Respawn(OutpostSession.SessionId);
            var player = new GuildPlayer(sim, targetSize: 8);
            player.SendExpeditions(Floor1Layout.DrownedGallerySectorId, Floor1Layout.GallerySeamYield, RetreatRule.Steady);
            player.SendSecondParty(Floor1Layout.GlowCavesSectorId, 3, RetreatRule.Steady, together: true);
            var watch = new OutpostWatch(sim);
            var guild = sim.Simulation.Guild;
            var host = (IGuildHost)sim.Simulation;
            var manager = (SimulationManager)sim.Simulation;
            var barracks = new HashSet<EntityId>(guild.Barracks.Select(b => b.Id));

            long full = -1, bothOut = -1;
            int worstUnpaid = 0;
            var trips = new[] { new List<(int Haul, bool Back)>(), new List<(int Haul, bool Back)>() };
            var haul = new int[2];
            var back = new bool[2];
            var wasOut = new bool[2];
            var sectorsWorked = new[] { new HashSet<int>(), new HashSet<int>() };
            var sleptInOwnBunk = new HashSet<EntityId>();
            long ownBunkTicks = 0, commonBunkTicks = 0;
            while (sim.Clock.CurrentTick < Horizon && !(bothOut >= 0 && trips[0].Any(t => t.Haul > 0) && trips[1].Any(t => t.Haul > 0) && sleptInOwnBunk.Count >= 8))
            {
                watch.Run(1);
                if (full < 0 && guild.Members.Count >= 8) full = sim.Clock.CurrentTick;
                for (int p = 0; p < 2; p++)
                {
                    var party = guild.Parties[p];
                    if (party.IsOut)
                    {
                        if (!wasOut[p]) haul[p] = 0;
                        var stage = party.Expedition.Stage;
                        if (stage == ExpeditionStage.Work)
                        {
                            // The nodes its members claimed (the work starts as the first member arrives; the rest are on their way)
                            foreach (var member in party.Members)
                            {
                                if (member.IsWithParty && member.NodeId != default) sectorsWorked[p].Add(host.GetSectorForPosition(member.NodePosition));
                            }
                        }
                        if (stage == ExpeditionStage.Work || stage == ExpeditionStage.Return) haul[p] = System.Math.Max(haul[p], party.Gathered(host));
                        back[p] = party.Expedition.TurnedBack;
                    }
                    else if (wasOut[p]) trips[p].Add((haul[p], back[p]));
                    wasOut[p] = party.IsOut;
                }
                if (bothOut < 0 && guild.Parties[0].IsOut && guild.Parties[1].IsOut) bothOut = sim.Clock.CurrentTick;
                foreach (var contract in guild.Members) worstUnpaid = System.Math.Max(worstUnpaid, contract.UnpaidVisits);
                foreach (var occupied in manager.BunkOccupants)
                {
                    if (!guild.IsMember(occupied.Value)) continue;
                    if (!barracks.Contains(occupied.Key))
                    {
                        commonBunkTicks++;
                        continue;
                    }
                    ownBunkTicks++;
                    if (guild.KeptBunkFor(occupied.Value).BunkId == occupied.Key) sleptInOwnBunk.Add(occupied.Value);
                }
            }
            watch.Finish();
            var flows = sim.Simulation.EconomyLedger.Flows;
            double Day(long t) => 1 + t / (double)DayClock.TicksPerDay;
            string Trips(int p) => string.Join(", ", trips[p].Select(t => $"{t.Haul}{(t.Back ? " back" : string.Empty)}"));
            TestContext.Current.TestOutputHelper?.WriteLine($"eight members on day {Day(full):F1}; both parties out at once on day {Day(bothOut):F1}; party 1's trips (the Gallery): {Trips(0)}; party 2's (the Glow Caves): {Trips(1)}; " +
                $"wages {flows.Wages} ({flows.WageGold}g), worst unpaid {worstUnpaid}; members slept in their own bunks {sleptInOwnBunk.Count} of 8, member bunk-hours in the barracks {ownBunkTicks / (double)DayClock.TicksPerHour:F0}, common {commonBunkTicks / (double)DayClock.TicksPerHour:F0}; ended day {Day(sim.Clock.CurrentTick):F1}\n{watch.Report()}");

            Assert.True(full >= 0 && guild.Members.Count == 8, "The guild kept eight members.");
            Assert.True(guild.Parties[0].Members.Count == 4 && guild.Parties[1].Members.Count == 4, "In two parties of four.");
            Assert.True(bothOut >= 0, "Both parties were out at once.");
            Assert.Equal(new[] { (int)Floor1Layout.DrownedGallerySectorId }, sectorsWorked[0].ToArray());
            Assert.Equal(new[] { (int)Floor1Layout.GlowCavesSectorId }, sectorsWorked[1].ToArray());
            Assert.True(trips[0].Any(t => t.Haul > 0) && trips[1].Any(t => t.Haul > 0), "Both came back with a haul.");
            Assert.True(flows.Wages > 0 && worstUnpaid == 0, "Every wage was paid.");
            Assert.Equal(8, sleptInOwnBunk.Count); // every member slept in the bunk kept for it
            Assert.True(commonBunkTicks * 10 < ownBunkTicks, "Members sleep in the barracks, not the common bunks.");
            Assert.Equal(0, watch.ConservationViolations);
            var failures = watch.DayFailures();
            Assert.True(failures.Count == 0, "The days failed:\n  " + string.Join("\n  ", failures));
        }
    }
}
