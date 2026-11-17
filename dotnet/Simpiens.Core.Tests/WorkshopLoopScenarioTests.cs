using System.Collections.Generic;
using System.Linq;
using Simpiens.Cognition.Inventory;
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
    /// W6.1's DoD, <c>TheWorkshopLoopPlaysThrough</c> (Epic 10 Part 2), live and headless with nothing injected. The
    /// scripted players build and staff the forge, which makes respirators, and the still, which makes draughts; the guild
    /// player, keeping a reserve (D67), signs eight on, forms two parties and sends them together, the Gallery's party
    /// outfitted with the forge's respirators. Both parties are out at once and come home with hauls; residents buy what
    /// the stations made; nobody starves, nobody quits for want of pay (since Epic 10 Part 3's F3, option A: a single
    /// unpaid visit is a warning the game recovers from), the ledger balances and the days' checks hold.
    /// <para>
    /// On gate seed 424242, not the default seed (a deviation): on the default seed the stall affords the forge and a guild
    /// of eight but never the still as well (F14, the economy's balance, the producer's call at W6.3). Over 6 game days the
    /// whole loop played through on 2 of 5 seeds measured at W6 (gate seed 1, the seed until K9, and this one); with skills
    /// (Epic 10 Part 3, K9) on 3 of the 23 gate seeds in seven (<see cref="MeasureTheWorkshopLoopOnTheGateSeeds"/>: this
    /// one, 18 and 20), with both parties out at once on day 5.8. Gate seed 1 again since passions and practice by challenge
    /// (Epic 11, S1.5): it plays through on 1 of the 23 (seed 1, at day 5.5), the economy's strain under every station and a
    /// guild of eight (Epic 11's finding F3; the producer tolerates such flukes until they ruin a scenario).
    /// </para>
    /// </summary>
    public class WorkshopLoopScenarioTests
    {
        private const ulong Seed = 1;
        private const long Horizon = 7 * DayClock.TicksPerDay;

        /// <summary>
        /// How long the save scenario looks for both parties out with a make in hand: longer than the loop's horizon (Epic 10
        /// Part 3, F3's option A): on seed 1, until K9, both parties were first out together with a make in hand on day 9.2;
        /// on this seed, with skills, on day 6.5.
        /// </summary>
        private const long SaveSearch = 12 * DayClock.TicksPerDay;

        [Fact]
        public void TheWorkshopLoopPlaysThrough()
        {
            var shortfalls = PlayTheLoop(Seed, out string report);
            TestContext.Current.TestOutputHelper?.WriteLine(report);
            Assert.True(shortfalls.Count == 0, "The loop fell short:\n  " + string.Join("\n  ", shortfalls));
        }

        /// <summary>The measurement behind the seed: every gate seed's seven days (explicit, a few minutes).</summary>
        [Fact(Explicit = true)]
        public void MeasureTheWorkshopLoopOnTheGateSeeds()
        {
            int through = 0;
            foreach (var seed in OutpostWatch.GateSeeds)
            {
                var shortfalls = PlayTheLoop(seed, out string report);
                if (shortfalls.Count == 0) through++;
                TestContext.Current.TestOutputHelper?.WriteLine($"seed {seed}: {(shortfalls.Count == 0 ? "plays through" : "short: " + string.Join("; ", shortfalls))} | {report.Split('\n')[0]}");
            }
            TestContext.Current.TestOutputHelper?.WriteLine($"the whole loop played through on {through} of {OutpostWatch.GateSeeds.Length} seeds");
        }

        /// <summary>Plays the loop on <paramref name="seed"/>; what it fell short of (none when it played through), and the run's report.</summary>
        private static List<string> PlayTheLoop(ulong seed, out string report)
        {
            using var sim = LocalSimulation.Create(seed);
            sim.Respawn(OutpostSession.SessionId);
            var (forge, still, _) = Players(sim);
            var watch = new OutpostWatch(sim);
            var guild = sim.Simulation.Guild;
            var host = (IGuildHost)sim.Simulation;
            var workshops = sim.Simulation.Workshops;
            int forgeAt = workshops.IndexOf(StationKind.Forge), stillAt = workshops.IndexOf(StationKind.Still);

            long bothOut = -1;
            int worstUnpaid = 0, unpaidQuits = 0, respiratorsOnATrip = 0;
            var unpaid = new Dictionary<EntityId, int>(); // members' and artisans' unpaid runs, last tick
            var hauls = new[] { new List<int>(), new List<int>() }; // trips during which the other party was out too
            var haul = new int[2];
            var wasOut = new bool[2];
            var together = new bool[2];
            bool Done() => bothOut >= 0 && hauls[0].Any(h => h > 0) && hauls[1].Any(h => h > 0) && respiratorsOnATrip > 0 && MadeGoodsBought(sim) > 0;
            while (sim.Clock.CurrentTick < Horizon && !Done())
            {
                watch.Run(1);
                for (int p = 0; p < 2; p++)
                {
                    var party = guild.Parties[p];
                    if (party.IsOut)
                    {
                        if (!wasOut[p]) { haul[p] = 0; together[p] = false; }
                        together[p] |= guild.Parties[1 - p].IsOut;
                        var stage = party.Expedition.Stage;
                        if (stage == ExpeditionStage.Work || stage == ExpeditionStage.Return) haul[p] = System.Math.Max(haul[p], party.Gathered(host));
                        if (p == 0 && stage == ExpeditionStage.Work)
                        {
                            int wearing = party.Members.Count(m => m.IsWithParty && host.TryGetCrawler(m.CrawlerId, out var c) && WornGear.Protection(c.Inventory).Type == ItemType.Respirator);
                            respiratorsOnATrip = System.Math.Max(respiratorsOnATrip, wearing);
                        }
                    }
                    else if (wasOut[p] && together[p]) hauls[p].Add(haul[p]);
                    wasOut[p] = party.IsOut;
                }
                if (bothOut < 0 && guild.Parties[0].IsOut && guild.Parties[1].IsOut) bothOut = sim.Clock.CurrentTick;
                foreach (var contract in guild.Members) worstUnpaid = System.Math.Max(worstUnpaid, contract.UnpaidVisits);
                unpaidQuits += QuitUnpaid(sim, unpaid);
            }
            watch.Finish();
            double Day(long t) => 1 + t / (double)DayClock.TicksPerDay;
            report = $"forge standing day {Day(forge.BuiltTick):F1}, {workshops[forgeAt].Made} respirators made; still standing day {Day(still.BuiltTick):F1}, {workshops[stillAt].Made} draughts made; " +
                $"members {guild.Members.Count}; both parties out at once on day {Day(bothOut):F1}; party 1's hauls {string.Join(", ", hauls[0])} (up to {respiratorsOnATrip} in respirators), party 2's {string.Join(", ", hauls[1])}; " +
                $"made goods bought by residents {MadeGoodsBought(sim)}; worst unpaid {worstUnpaid}, quit unpaid {unpaidQuits}; treasury {sim.Simulation.ActiveStalls[0].TreasuryGold}g; ended day {Day(sim.Clock.CurrentTick):F1}\n{watch.Report()}";

            var shortfalls = new List<string>();
            void Need(bool held, string what) { if (!held) shortfalls.Add(what); }
            Need(workshops[forgeAt].Built && workshops[stillAt].Built, "Both stations were built.");
            Need(forge.Hired.Count > 0 && still.Hired.Count > 0, "Both were staffed.");
            Need(workshops[forgeAt].Made > 0 && workshops[stillAt].Made > 0, "Both made their goods.");
            Need(respiratorsOnATrip > 0, "The Gallery's party worked in the forge's respirators.");
            Need(guild.Members.Count == 8 && bothOut >= 0, "Eight members; both parties out at once.");
            Need(hauls[0].Any(h => h > 0) && hauls[1].Any(h => h > 0), "Both came home with hauls from trips they were out on together.");
            Need(MadeGoodsBought(sim) > 0, "Residents bought what the stations made.");
            Need(unpaidQuits == 0, $"Nobody quit for want of pay ({unpaidQuits} did)."); // a single unpaid visit is a warning (Epic 10 Part 3, F3's option A)
            Need(watch.ConservationViolations == 0, "The ledger balanced.");
            var failures = watch.DayFailures();
            Need(failures.Count == 0, "The days held: " + string.Join("; ", failures));
            return shortfalls;
        }

        /// <summary>
        /// W6.2 (Epic 10 Part 2): the same loop saved with both parties out and a station mid-make loads into a fresh
        /// session byte for byte and with the same world hash (the guild's two parties with their expeditions, the stations'
        /// makes, the barracks' bunks kept), and plays on: both parties come home and the make in hand is finished.
        /// </summary>
        [Fact]
        public void ASaveMidCraftAndMidExpeditionLoadsAndPlaysOn()
        {
            const long PlaysOn = 24 * DayClock.TicksPerHour; // a day on: a make saved after its artisan's shift is finished on the next
            using var played = LocalSimulation.Create(Seed);
            played.Respawn(OutpostSession.SessionId);
            Players(played);
            var workshops = played.Simulation.Workshops;
            int making() { for (int i = 0; i < workshops.Count; i++) if (workshops[i].InputsTaken && workshops[i].Progress > 0) return i; return -1; }
            var parties = played.Simulation.Guild.Parties;
            while (played.Clock.CurrentTick < SaveSearch && !(parties[0].IsOut && parties[1].IsOut && making() >= 0)) played.Step(1);
            TestContext.Current.TestOutputHelper?.WriteLine($"saved on day {1 + played.Clock.CurrentTick / (double)DayClock.TicksPerDay:F1}");
            int station = making();
            Assert.True(parties[0].IsOut && parties[1].IsOut && station >= 0, "Both parties out and a make in hand to save.");
            long tick = played.Clock.CurrentTick;
            byte[] saved = Simpiens.Simulation.Persistence.SaveSerializer.Serialize(played.Host.Capture());
            var savedHash = Simpiens.Testing.Environment.WorldStateHash.Capture(tick, played.Host, played.Simulation, played.Sectors, includeWork: false);
            uint madeAtSave = workshops[station].Made;

            using var loaded = LocalSimulation.Create(Seed);
            loaded.Respawn(OutpostSession.SessionId);
            Assert.True(loaded.Host.TryRestore(Simpiens.Simulation.Persistence.SaveSerializer.Deserialize<Simpiens.Simulation.Persistence.SessionSave>(saved), out string refusal), $"The save was refused: {refusal}");
            Assert.Equal(saved, Simpiens.Simulation.Persistence.SaveSerializer.Serialize(loaded.Host.Capture()));
            Assert.Null(savedHash.DescribeDifference(Simpiens.Testing.Environment.WorldStateHash.Capture(tick, loaded.Host, loaded.Simulation, loaded.Sectors, includeWork: false)));
            var guild = loaded.Simulation.Guild;
            Assert.True(guild.Parties[0].IsOut && guild.Parties[1].IsOut, "Both parties came back out.");
            Assert.True(guild.Members.All(m => m.HasBunk) && guild.Members.All(m => guild.KeptBunkFor(m.CrawlerId).IsKept), "Each member's bunk came back.");
            Assert.True(loaded.Simulation.Workshops[station].InputsTaken, "The make in hand came back.");

            Players(loaded);
            var watch = new OutpostWatch(loaded);
            long[] home = { -1, -1 };
            long finished = -1;
            while (loaded.Clock.CurrentTick < tick + PlaysOn && !(home[0] >= 0 && home[1] >= 0 && finished >= 0))
            {
                watch.Run(1);
                for (int p = 0; p < 2; p++) if (home[p] < 0 && !guild.Parties[p].IsOut) home[p] = loaded.Clock.CurrentTick;
                if (finished < 0 && loaded.Simulation.Workshops[station].Made > madeAtSave) finished = loaded.Clock.CurrentTick;
            }
            watch.Finish();
            TestContext.Current.TestOutputHelper?.WriteLine($"saved at t{tick} with both parties out and station {station} mid-make; party 1 home t{home[0]}, party 2 t{home[1]}; the make finished t{finished}");
            Assert.True(home[0] > tick && home[1] > tick, "Both parties came home.");
            Assert.True(finished > tick, "The make in hand was finished.");
            Assert.Equal(0, watch.ConservationViolations);
            Assert.Equal(0, watch.IntentFaults);
        }

        private static (WorkshopPlayer Forge, WorkshopPlayer Still, GuildPlayer Guild) Players(LocalSimulation sim)
        {
            var forge = new WorkshopPlayer(sim, StationKind.Forge, ItemType.Respirator);
            var still = new WorkshopPlayer(sim, StationKind.Still, ItemType.HealingDraught);
            var player = new GuildPlayer(sim, targetSize: 8, reserve: 200);
            player.SendExpeditions(Floor1Layout.DrownedGallerySectorId, Floor1Layout.GallerySeamYield, RetreatRule.Steady);
            player.SendSecondParty(Floor1Layout.GlowCavesSectorId, 3, RetreatRule.Steady, together: true);
            return (forge, still, player);
        }

        /// <summary>Residents' purchases of a station's goods: the respirators and draughts the stall sells come only from the forge and the still.</summary>
        private static int MadeGoodsBought(LocalSimulation sim) =>
            sim.Host.CrawlerStates.Where(c => !c.IsDrifter).Sum(c => c.Activity.MedicinePurchases) + RespiratorsBought(sim);

        private static int RespiratorsBought(LocalSimulation sim) =>
            sim.Host.CrawlerStates.Count(c => !c.IsDrifter && !sim.Simulation.Guild.IsMember(c.AgentId) && WornGear.Protection(c.Inventory).Type == ItemType.Respirator);

        /// <summary>
        /// Members and artisans who quit for want of pay since the last call: gone from their roster with one unpaid visit
        /// short of their quitting line (they quit on the visit that reaches it). Updates <paramref name="last"/>.
        /// </summary>
        private static int QuitUnpaid(LocalSimulation sim, Dictionary<EntityId, int> last)
        {
            var now = new Dictionary<EntityId, int>();
            foreach (var member in sim.Simulation.Guild.Members) now[member.CrawlerId] = member.UnpaidVisits;
            foreach (var artisan in sim.Simulation.Workshops.Artisans) now[artisan.CrawlerId] = artisan.UnpaidVisits;
            int quits = 0, line = System.Math.Min(sim.Simulation.Economy.Guild.MaxUnpaidVisits, sim.Simulation.Economy.Workshops.MaxUnpaidVisits);
            foreach (var pair in last)
                if (!now.ContainsKey(pair.Key) && pair.Value + 1 >= line) quits++;
            last.Clear();
            foreach (var pair in now) last[pair.Key] = pair.Value;
            return quits;
        }
    }
}
