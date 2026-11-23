using System.Collections.Generic;
using System.Linq;
using Simpiens.Core.Session;
using Simpiens.Hosting;
using Simpiens.Narrative;
using Simpiens.Simulation;
using Simpiens.Simulation.Items;
using Simpiens.Simulation.Persistence;
using Simpiens.Simulation.Workshops;
using Xunit;

namespace Simpiens.Core.Tests
{
    /// <summary>
    /// S8's Definition of Done (Epic 11, S8.6): a masterpiece has a maker, on the live outpost with nothing injected. The soak's
    /// <c>hunting</c> players build the forge and hire its best hand, arm the guild from the stall and post hunts, acting only
    /// through commands.
    /// <list type="bullet">
    /// <item><b>Inspired:</b> the forge's smith, a Journeyman at its work, makes a masterpiece, as the chronicle tells it.</item>
    /// <item><b>Worn:</b> a resident wears it, bought or given, its owner recorded.</item>
    /// <item><b>Remembered:</b> a save and a load keep its record, its mark on its wearer, its owners and its maker's name.</item>
    /// </list>
    /// </summary>
    public class AMasterpieceHasAMakerTests
    {
        private sealed class Run
        {
            public long MadeAt = -1, WornAt = -1;
            public ushort Masterpiece;
            public EntityId Maker, Wearer;
            public bool MakerAtTheForge, MakerAJourneyman;
            public string Told = "-", Name = "-";
            public int Made;
            public OutpostWatch Watch;
            public bool All => WornAt >= 0;
            public override string ToString() =>
                $"made {(MadeAt >= 0 ? $"t{MadeAt} (day {1 + MadeAt / DayClock.TicksPerDay}): {Told}" : "never")} ({Made} in all); worn {(WornAt >= 0 ? "t" + WornAt : "never")}";
        }

        private static Run Play(LocalSimulation sim, long horizon, bool untilAll)
        {
            EconomySoak.AttachPlayers(sim, "hunting");
            var watch = new OutpostWatch(sim);
            var run = new Run { Watch = watch };
            var sm = (SimulationManager)sim.Simulation;
            var formatter = new TemplateNarrativeFormatter(sim.Identity, sim.Sectors);
            var told = new List<ChronicleEvent>(64);
            long seen = -1;
            while (sim.Clock.CurrentTick < horizon && !(untilAll && run.All))
            {
                watch.Run(1);
                told.Clear();
                sim.Chronicle.GetAllRecentEvents(told, 64);
                long newest = seen;
                foreach (var e in told)
                {
                    if (e.SimulationTick <= seen) continue; // each tick's events once
                    if (e.SimulationTick > newest) newest = e.SimulationTick;
                    if (e.EventType != ChronicleEventType.MasterpieceMade || run.MadeAt >= 0) continue;
                    run.MadeAt = e.SimulationTick;
                    run.Maker = e.ActorAId;
                    run.Told = formatter.FormatEvent(e);
                    run.Masterpiece = (ushort)sm.Masterpieces.Count; // the first, told the tick it was made
                    var maker = sim.Host.CrawlerStates.First(c => c.AgentId == run.Maker);
                    int forge = sm.Workshops.IndexOf(StationKind.Forge);
                    run.MakerAtTheForge = sm.Workshops.ArtisanAt(forge) == run.Maker;
                    run.MakerAJourneyman = WorkshopService.GradeOf(maker, StationKind.Forge) >= Grades.Masterwork;
                }
                seen = newest;
                if (run.Masterpiece != 0 && run.WornAt < 0)
                {
                    foreach (var crawler in sim.Host.CrawlerStates)
                    {
                        if (crawler.HasLeft || crawler.IsDrifter || !Wears(crawler, run.Masterpiece)) continue;
                        run.WornAt = sim.Clock.CurrentTick;
                        run.Wearer = crawler.AgentId;
                        break;
                    }
                }
            }
            run.Made = sm.Masterpieces.Count;
            if (run.Masterpiece != 0 && sm.Masterpieces.TryGet(run.Masterpiece, out var record)) run.Name = MasterpieceNames.NameOf(record, id => sim.Identity.GetFriendlyName(id));
            watch.Finish();
            return run;
        }

        private static bool Wears(Simpiens.Cognition.AgentState crawler, ushort masterpiece)
        {
            var slots = EquipmentSlots.All;
            for (int s = 0; s < slots.Count; s++) if (crawler.Inventory.GetEquipped(slots[s].Id).Masterpiece == masterpiece) return true;
            return false;
        }

        /// <summary>The horizon: fourteen game days.</summary>
        private const long Horizon = 14 * DayClock.TicksPerDay;

        /// <summary>
        /// Gate seed 424242, the default (S8.4, `docs/skills-model.md` §5.14): the forge's smith makes a masterpiece on day 5 and a
        /// resident wears it within the two weeks; <see cref="MeasureAMasterpieceHasAMakerOnTheGateSeeds"/> gives the rest.
        /// </summary>
        private const ulong Seed = 424242;

        [Fact]
        public void AMasterpieceHasAMaker()
        {
            using var sim = LocalSimulation.Create(Seed);
            sim.Respawn(OutpostSession.SessionId);
            var run = Play(sim, Horizon, untilAll: true);
            var watch = run.Watch;
            TestContext.Current.TestOutputHelper?.WriteLine($"{run}; {run.Name}, worn by {sim.Identity.GetFriendlyName(run.Wearer)}\n{watch.Report()}");

            // Inspired: the forge's smith, a Journeyman, as the chronicle tells it
            Assert.True(run.MadeAt >= 0, "The forge's smith made a masterpiece, as the chronicle told it.");
            Assert.Contains("inspired, made a masterpiece at the forge", run.Told);
            Assert.True(run.MakerAtTheForge && run.MakerAJourneyman, "Its maker is the forge's hand, a Journeyman at its work.");
            var sm = (SimulationManager)sim.Simulation;
            Assert.True(sm.Masterpieces.TryGet(run.Masterpiece, out var record) && record.Maker == run.Maker && record.MadeAt == run.MadeAt, "Its record names its maker and when.");

            // Worn: a resident wears it, its owner recorded
            Assert.True(run.WornAt >= run.MadeAt, "A resident wears it.");
            Assert.True(record.OwnerCount >= 1 && record.LastOwner == run.Wearer && record.Place == MasterpiecePlace.World, "Its wearer is its owner on record.");
            Assert.Equal(0, watch.ConservationViolations);
            Assert.Equal(0, watch.IntentFaults);
            var failures = watch.DayFailures();
            Assert.True(failures.Count == 0, "The days failed:\n  " + string.Join("\n  ", failures));

            // Remembered: a save, played past, loaded back
            string maker = sim.Identity.GetFriendlyName(run.Maker);
            byte[] saved = SaveSerializer.Serialize(sim.Host.Capture());
            sim.Step(DayClock.TicksPerHour);
            Assert.True(sim.Host.TryRestore(SaveSerializer.Deserialize<SessionSave>(saved), out string refusal), refusal);
            sm = (SimulationManager)sim.Simulation;
            var wearer = sim.Host.CrawlerStates.Single(c => c.AgentId == run.Wearer);
            Assert.True(Wears(wearer, run.Masterpiece), "Its wearer wears it after the load, marked.");
            Assert.True(sm.Masterpieces.TryGet(run.Masterpiece, out var loaded) && loaded.Maker == record.Maker && loaded.MadeAt == record.MadeAt
                && loaded.OwnerCount == record.OwnerCount && loaded.FirstOwner == record.FirstOwner && loaded.LastOwner == record.LastOwner && loaded.Adjective == record.Adjective && loaded.Noun == record.Noun,
                "Its record comes back whole: maker, tick, owners, name.");
            Assert.Equal(maker, sim.Identity.GetFriendlyName(loaded.Maker));
            Assert.Equal(run.Name, MasterpieceNames.NameOf(loaded, id => sim.Identity.GetFriendlyName(id)));
            sim.Step(DayClock.TicksPerHour); // and plays on
            Assert.True(sm.Masterpieces.TryGet(run.Masterpiece, out var after) && after.Maker == run.Maker, "It plays on after the load.");
        }

        /// <summary>The measurement behind the seed: every gate seed's two weeks, eight at a time (explicit, a few minutes).</summary>
        [Fact(Explicit = true)]
        public void MeasureAMasterpieceHasAMakerOnTheGateSeeds()
        {
            var seeds = OutpostWatch.GateSeeds;
            var lines = new string[seeds.Length];
            int made = 0, worn = 0, all = 0;
            System.Threading.Tasks.Parallel.For(0, seeds.Length, new System.Threading.Tasks.ParallelOptions { MaxDegreeOfParallelism = 8 }, i =>
            {
                using var sim = LocalSimulation.Create(seeds[i]);
                sim.Respawn(OutpostSession.SessionId);
                var run = Play(sim, Horizon, untilAll: false);
                var failures = run.Watch.DayFailures();
                lines[i] = $"seed {seeds[i]}: {run}; days {(failures.Count == 0 ? "hold" : "FAIL: " + string.Join("; ", failures))}";
                if (run.MadeAt >= 0) System.Threading.Interlocked.Increment(ref made);
                if (run.WornAt >= 0) System.Threading.Interlocked.Increment(ref worn);
                if (run.WornAt >= 0 && failures.Count == 0) System.Threading.Interlocked.Increment(ref all);
            });
            foreach (var line in lines) TestContext.Current.TestOutputHelper?.WriteLine(line);
            TestContext.Current.TestOutputHelper?.WriteLine($"of {seeds.Length} seeds in {Horizon / DayClock.TicksPerDay} days: a masterpiece made on {made}, worn on {worn}, worn with the days holding on {all}");
        }
    }
}
