using System.Collections.Generic;
using System.Linq;
using Simpiens.Cognition;
using Simpiens.Cognition.Contracts;
using Simpiens.Core.Session;
using Simpiens.Core.Session.Layouts;
using Simpiens.Hosting;
using Simpiens.Simulation;
using Simpiens.Simulation.Economy;
using Simpiens.Simulation.Items;
using Simpiens.Simulation.Life;
using Simpiens.Simulation.Persistence;
using Simpiens.Testing.Environment;
using Xunit;

namespace Simpiens.Core.Tests
{
    /// <summary>
    /// Epic LIFE-1 plan 2's Definition of Done, live on the real outpost with nothing injected (the tavern player acts only
    /// through commands): three game days, since the tavern is first kept on the second (a resident must trust the patron
    /// enough to keep it).
    /// <list type="bullet">
    /// <item><b><c>AnEveningAtTheTavern</c></b> (L2, D24): the tavern built and kept; it serves on two days at least; half the
    /// residents spend an hour or more at a venue other than the hearth; the days hold (nobody starves; the ledger balances,
    /// with its takings, wholesale and the keeper's wage); a save on the third day loads with the tavern standing and kept.</item>
    /// <item><b><c>AWantComesTrue</c></b> (L1's wants, D20): wants arise, and some come true at a venue, leaving their thoughts.</item>
    /// </list>
    /// </summary>
    public class AnEveningAtTheTavernTests
    {
        private const long Horizon = 3 * DayClock.TicksPerDay;

        internal sealed class Run
        {
            public OutpostWatch Watch;
            public TavernPlayer Player;
            public long BuiltTick = -1, KeptTick = -1;
            public readonly HashSet<int> ServedDays = new HashSet<int>();
            public int Residents, AtVenuesAnHour;
            public double VenueHoursPerResidentDay;
            public int WantsArisen, WantsFulfilled, WantsMissed;
            public uint Wholesale, ArtisanWages, Takings, EveningTakings;

            public override string ToString() =>
                $"built t{BuiltTick}, kept t{KeptTick}, served {Player.Served} ({Player.Ales} ales, {Player.Suppers} suppers) for {Takings}g ({EveningTakings}g in the evening) on days [{string.Join(",", ServedDays.OrderBy(d => d))}]; "
                + $"{AtVenuesAnHour} of {Residents} an hour at a venue, {VenueHoursPerResidentDay:F2} h a resident-day; wants arisen {WantsArisen}, come true {WantsFulfilled}, missed {WantsMissed}; "
                + $"wholesale {Wholesale}g, artisans' wages {ArtisanWages}g";
        }

        internal static Run Play(ulong seed, long horizon, out LocalSimulation keep)
        {
            var sim = LocalSimulation.Create(seed);
            sim.Respawn(OutpostSession.SessionId);
            var run = new Run { Player = new TavernPlayer(sim, reserve: EconomySoak.TavernReserve), Watch = new OutpostWatch(sim) };
            sim.Simulation.Served += (crawler, item, price) =>
            {
                run.ServedDays.Add((int)DayClock.Day(sim.Clock.CurrentTick));
                run.Takings += price;
                int hour = DayClock.HourOfDay(sim.Clock.CurrentTick);
                if (hour >= 18 || hour < 2) run.EveningTakings += price; // the evening, 18:00 to 02:00 (plan 3's D.2)
            };
            var map = sim.Host.Current.Map;
            int hearth = map.Venues.ToList().FindIndex(v => v.Key == Floor1Layout.HearthVenue);
            var atVenues = new Dictionary<EntityId, long>();
            var wantsHeld = new Dictionary<EntityId, ushort[]>();
            var fulfilledThoughts = WantTable.All.Select(w => w.Fulfilled).ToHashSet();
            var missedThoughts = WantTable.All.Where(w => w.Expired != Thought.None).Select(w => w.Expired).ToHashSet();
            while (sim.Clock.CurrentTick < horizon)
            {
                run.Watch.Run(1);
                long now = sim.Clock.CurrentTick;
                int tavern = sim.Simulation.Workshops.IndexOf(StationKind.Tavern);
                if (run.BuiltTick < 0 && sim.Simulation.Workshops[tavern].Built) run.BuiltTick = now;
                if (run.KeptTick < 0 && (sim.Simulation.Workshops.KeptMask((Simpiens.Simulation.Workshops.IWorkshopHost)sim.Simulation, (uint)now) & (1UL << tavern)) != 0) run.KeptTick = now;
                foreach (var crawler in sim.Host.CrawlerStates)
                {
                    if (crawler.IsDrifter) continue;
                    if (crawler.CurrentIntent is RecreateIntent recreate && recreate.Begun && recreate.VenueIndex != hearth)
                        atVenues[crawler.AgentId] = (atVenues.TryGetValue(crawler.AgentId, out long n) ? n : 0) + 1;
                    // Wants arisen: a slot's want newly held
                    if (!wantsHeld.TryGetValue(crawler.AgentId, out var held)) wantsHeld[crawler.AgentId] = held = new ushort[CrawlerLife.WantCapacity];
                    for (int w = 0; w < CrawlerLife.WantCapacity; w++)
                    {
                        ushort want = crawler.Life.WantAt(w).Want;
                        if (want != 0 && held[w] != want) run.WantsArisen++;
                        held[w] = want;
                    }
                }
            }
            run.Watch.Finish();
            var residents = sim.Host.CrawlerStates.Where(c => !c.IsDrifter).ToList();
            run.Residents = residents.Count;
            run.AtVenuesAnHour = residents.Count(c => atVenues.TryGetValue(c.AgentId, out long t) && t >= DayClock.TicksPerHour);
            run.VenueHoursPerResidentDay = atVenues.Values.Sum() / (double)DayClock.TicksPerHour / (residents.Count * (horizon / (double)DayClock.TicksPerDay));
            for (int t = 0; t < run.Watch.ThoughtsThought.Length; t++)
            {
                if (fulfilledThoughts.Contains((Thought)t)) run.WantsFulfilled += run.Watch.ThoughtsThought[t];
                if (missedThoughts.Contains((Thought)t)) run.WantsMissed += run.Watch.ThoughtsThought[t];
            }
            run.Wholesale = sim.Simulation.EconomyLedger.GetSinkTotal(SupplyService.TavernSinkName);
            run.ArtisanWages = sim.Simulation.EconomyLedger.Flows.ArtisanWageGold;
            keep = sim;
            return run;
        }

        /// <summary>The seed, from <see cref="MeasureTheTavernOnTheGateSeeds"/> (plan 2's D.1).</summary>
        private static readonly ulong Seed = ScenarioSeed.Or(5);

        [Fact]
        public void AnEveningAtTheTavern()
        {
            var run = Play(Seed, Horizon, out var sim);
            using (sim)
            {
                TestContext.Current.TestOutputHelper?.WriteLine($"{run}\n{run.Watch.Report()}");
                Assert.True(run.BuiltTick >= 0 && run.KeptTick >= 0, "The tavern was built and kept.");
                Assert.True(run.ServedDays.Count >= 2 && run.Player.Served >= 3, $"It served on two days at least ({run}).");
                Assert.Equal(run.Player.Takings, run.Takings);
                Assert.True(run.EveningTakings * 2 >= run.Takings, $"Most of its takings came in the evening, 18:00 to 02:00 (plan 3's D.2: {run.EveningTakings}g of {run.Takings}g).");
                Assert.True(run.AtVenuesAnHour * 2 >= run.Residents, $"Half the residents spent an hour at a venue other than the hearth ({run.AtVenuesAnHour} of {run.Residents}).");
                Assert.True(run.Wholesale > 0 && run.ArtisanWages > 0, "The caravan's wholesale for the menu and the keeper's wage are in the ledger.");
                Assert.Equal(0, run.Watch.ConservationViolations);
                Assert.Equal(0, run.Watch.IntentFaults);
                var failures = run.Watch.DayFailures();
                Assert.True(failures.Count == 0, "The days failed:\n  " + string.Join("\n  ", failures));

                // A save now loads with the tavern standing and its keeper's contract
                int tavern = sim.Simulation.Workshops.IndexOf(StationKind.Tavern);
                var keeper = sim.Simulation.Workshops.ArtisanAt(tavern);
                byte[] saved = SaveSerializer.Serialize(sim.Host.Capture());
                using var loaded = LocalSimulation.Create(Seed);
                loaded.Respawn(OutpostSession.SessionId);
                Assert.True(loaded.Host.TryRestore(SaveSerializer.Deserialize<SessionSave>(saved), out string refusal), $"The save was refused: {refusal}");
                Assert.Equal(saved, SaveSerializer.Serialize(loaded.Host.Capture()));
                Assert.True(loaded.Simulation.Workshops[tavern].Built && loaded.Simulation.Workshops.ArtisanAt(tavern) == keeper, "Loaded: the tavern stands, kept by the same keeper.");
            }
        }

        [Fact]
        public void AWantComesTrue()
        {
            var run = Play(Seed, Horizon, out var sim);
            using (sim)
            {
                TestContext.Current.TestOutputHelper?.WriteLine(run.ToString());
                Assert.True(run.WantsArisen > 0, "Wants arose.");
                Assert.True(run.WantsFulfilled > 0, $"Some came true at a venue, leaving their thoughts ({run}).");
                Assert.True(run.WantsFulfilled >= run.WantsMissed, $"No more missed than come true ({run.WantsFulfilled} against {run.WantsMissed}).");
            }
        }

        /// <summary>The measurement behind the seed: every gate seed's three days with the tavern player (explicit).</summary>
        [Fact(Explicit = true)]
        public void MeasureTheTavernOnTheGateSeeds()
        {
            var seeds = OutpostWatch.GateSeeds;
            var lines = new string[seeds.Length];
            System.Threading.Tasks.Parallel.For(0, seeds.Length, new System.Threading.Tasks.ParallelOptions { MaxDegreeOfParallelism = 8 }, i =>
            {
                var run = Play(seeds[i], Horizon, out var sim);
                using (sim) lines[i] = $"seed {seeds[i]}: {run}; days {(run.Watch.DayFailures().Count == 0 ? "hold" : "FAIL: " + string.Join("; ", run.Watch.DayFailures()))}";
            });
            foreach (var line in lines) TestContext.Current.TestOutputHelper?.WriteLine(line);
        }
    }
}
