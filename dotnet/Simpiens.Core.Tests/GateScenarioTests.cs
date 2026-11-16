using System.Collections.Generic;
using System.Linq;
using Simpiens.Core.Session;
using Simpiens.Core.Session.Layouts;
using Simpiens.Hosting;
using Simpiens.Simulation;
using Simpiens.Simulation.Creatures;
using Simpiens.Simulation.Economy;
using Simpiens.Simulation.Guild;
using Simpiens.Simulation.Items;
using Simpiens.Simulation.World;
using Xunit;

namespace Simpiens.Core.Tests
{
    /// <summary>
    /// K10's DoD, <c>TheGuildBreaksTheGatekeeper</c> (Epic 10 Part 3, Q13), live and headless with nothing injected: the scripted
    /// player charts the Old Stair with a scout bounty, the workshop players make blades, crossbows, ring coats and draughts, and
    /// the scripted guild player arms and kits four from the stall's stock and orders clears in turn: the web on the approach
    /// when it is charted, then the gate. They break the Gatekeeper; the breach is the world's flag and the chronicle's line.
    /// The days hold, the ledger balances, no intent handler throws.
    /// </summary>
    public class GateScenarioTests
    {
        private sealed class Run
        {
            public long StairChartedAt = -1, WebChartedAt = -1, GateChartedAt = -1, FirstGateSentAt = -1, BreachedAt = -1;
            public int WebClears, GateAttempts, Deaths;
            public EntityId Breaker;
            public readonly HashSet<EntityId> Party = new HashSet<EntityId>();
            public readonly List<float> KeeperLowest = new List<float>();
            public OutpostWatch Watch;
            private static string T(long t) => t >= 0 ? "t" + t : "never";
            public override string ToString() =>
                $"stair charted {T(StairChartedAt)}, web {T(WebChartedAt)}, gate {T(GateChartedAt)}; web clears {WebClears}; gate attempts {GateAttempts} (first {T(FirstGateSentAt)}, keeper left at [{string.Join(", ", KeeperLowest.Select(h => h.ToString("0")))}]); breached {T(BreachedAt)}; deaths {Deaths}";
        }

        /// <summary>The treasury each scripted player leaves untouched.</summary>
        private const uint Reserve = 150;

        private static Run Play(ulong seed, long horizon, bool kitted, bool untilBreached)
        {
            using var sim = LocalSimulation.Create(seed);
            sim.Respawn(OutpostSession.SessionId);
            // Each keeps a reserve in the treasury (as the crafting profile's do, D67; F8's option B)
            // Kitted: ring coats from the forge and two draughts each from the still; four blades (two crossbows measured too: their
            // hides wait on the Matriarch's warren, so a party with bows rarely went, D71)
            var forge = new WorkshopPlayer(sim, StationKind.Forge, ItemType.IronBlade, batch: 4, reserve: Reserve);
            if (kitted)
            {
                forge.Also(ItemType.RingCoat, keep: 4);
                new WorkshopPlayer(sim, StationKind.Still, ItemType.HealingDraught, batch: 2, reserve: Reserve);
            }
            var guild = new GuildPlayer(sim, targetSize: 4, reserve: Reserve);
            guild.SendClears(new[] { CreatureKind.CaveSpinner, CreatureKind.Gatekeeper }, RetreatRule.Steady, draughts: (byte)(kitted ? 2 : 0), stockpile: kitted ? 8 : 0);
            // The player charts the Old Stair with a scout bounty, through the command
            var commands = (Simpiens.Intervention.IPlayerInterventionService)sim.Services.GetService(typeof(Simpiens.Intervention.IPlayerInterventionService));
            commands.QueueInfluence(Simpiens.Intervention.InfluenceEvent.CreatePostBounty("Test", new Simpiens.Intervention.PostBountyCommand(0, BountyType.ScoutSector,
                Floor1Layout.OldStairSectorId, ItemType.None, 1, 40, durationTicks: DayClock.Hours(48))));
            var watch = new OutpostWatch(sim);
            var run = new Run { Watch = watch };
            var creatures = sim.Simulation.Creatures;
            int web = -1, gate = -1;
            for (int l = 0; l < creatures.Lairs.Count; l++)
            {
                if (creatures.Lairs[l].Definition.Key == Floor1Layout.SpinnerWeb) web = l;
                if (creatures.Lairs[l].Definition.GuardsGate) gate = l;
            }
            var party = sim.Simulation.Guild.Parties[0];
            bool wasOut = false;
            float lowest = float.MaxValue;
            while (sim.Clock.CurrentTick < horizon && (!untilBreached || run.BreachedAt < 0))
            {
                watch.Run(1);
                long now = sim.Clock.CurrentTick;
                if (run.StairChartedAt < 0 && sim.Sectors.IsExplored(Floor1Layout.OldStairSectorId)) run.StairChartedAt = now;
                if (run.WebChartedAt < 0 && creatures.IsCharted(web)) run.WebChartedAt = now;
                if (run.GateChartedAt < 0 && creatures.IsCharted(gate)) run.GateChartedAt = now;
                bool atGate = party.IsOut && party.Expedition.IsClear && party.Expedition.LairIndex == gate;
                if (party.IsOut && !wasOut && party.Expedition.IsClear)
                {
                    if (party.Expedition.LairIndex == web) run.WebClears++;
                    if (atGate)
                    {
                        run.GateAttempts++;
                        if (run.FirstGateSentAt < 0) run.FirstGateSentAt = now;
                        lowest = float.MaxValue;
                    }
                }
                if (atGate && creatures.Lairs[gate].MemberIn(0) != default && creatures.TryGet(creatures.Lairs[gate].MemberIn(0), out var keeper)) lowest = System.Math.Min(lowest, keeper.Health);
                if (wasOut && !party.IsOut && lowest != float.MaxValue)
                {
                    run.KeeperLowest.Add(creatures.Lairs[gate].MemberIn(0) == default ? 0f : lowest);
                    lowest = float.MaxValue;
                }
                wasOut = party.IsOut;
                if (atGate) foreach (var m in party.Members) run.Party.Add(m.CrawlerId);
                if (run.BreachedAt < 0 && (sim.Simulation.WorldFlags & WorldFlags.GateBreached) != 0)
                {
                    run.BreachedAt = now;
                    var told = new List<Simpiens.Narrative.ChronicleEvent>();
                    sim.Chronicle.GetAllRecentEvents(told, 512);
                    run.Breaker = told.LastOrDefault(e => e.EventType == Simpiens.Narrative.ChronicleEventType.GateBreached).ActorAId;
                }
            }
            watch.Finish();
            run.Deaths = watch.Deaths;
            return run;
        }

        /// <summary>
        /// The default seed (the DoD's, Q13): measured (K10.6) four blades kitted with ring coats and two draughts each break the
        /// Gatekeeper on 17 of the 23 gate seeds in sixteen days, 1.5 attempts each, and on none unkitted; here on the fourth
        /// attempt, at t126676 (day 12.6), the keeper left at 37, 31 and 55 by the three before. The bound: within sixteen days and
        /// six attempts.
        /// </summary>
        [Fact]
        public void TheGuildBreaksTheGatekeeper()
        {
            var run = Play(SimulationSettings.DefaultSeed, 16 * DayClock.TicksPerDay, kitted: true, untilBreached: true);
            var watch = run.Watch;
            TestContext.Current.TestOutputHelper?.WriteLine($"{run}\n{watch.CreatureSummary()}\n{watch.Report()}");
            Assert.True(run.StairChartedAt >= 0, "The scout charted the Old Stair.");
            Assert.True(run.GateChartedAt >= run.StairChartedAt, "The gate's lair was charted with its sector.");
            Assert.True(run.GateAttempts >= 1 && run.GateAttempts <= MaxAttempts, $"The guild went at the gate {run.GateAttempts} times (at most {MaxAttempts}).");
            Assert.True(run.BreachedAt > run.FirstGateSentAt, "The Gatekeeper fell and the gate is breached.");
            Assert.True(run.Breaker != default && run.Party.Contains(run.Breaker), "The chronicle names the member who broke it.");
            Assert.Equal(0, run.Deaths);
            Assert.Equal(0, watch.ConservationViolations);
            Assert.Equal(0, watch.IntentFaults);
            var failures = watch.DayFailures();
            Assert.True(failures.Count == 0, "The days failed:\n  " + string.Join("\n  ", failures));
        }

        /// <summary>The most attempts at the gate the DoD allows (Q13: "more than one attempt is allowed"; K10.6 measured 4 on the default seed).</summary>
        private const int MaxAttempts = 6;

        /// <summary>
        /// The measurement behind the DoD (explicit, a minute or two): every gate seed's sixteen days, eight at a time, with four
        /// blades, kitted with ring coats and two draughts each or not.
        /// </summary>
        [Theory(Explicit = true)]
        [InlineData(true)]
        [InlineData(false)]
        public void MeasureTheGateOnTheGateSeeds(bool kitted)
        {
            var seeds = new ulong[] { SimulationSettings.DefaultSeed }.Concat(OutpostWatch.GateSeeds).Distinct().ToArray();
            var lines = new string[seeds.Length];
            int breached = 0, attempts = 0;
            var gate = new object();
            System.Threading.Tasks.Parallel.For(0, seeds.Length, new System.Threading.Tasks.ParallelOptions { MaxDegreeOfParallelism = 8 }, i =>
            {
                var run = Play(seeds[i], 16 * DayClock.TicksPerDay, kitted, untilBreached: false);
                var failures = run.Watch.DayFailures();
                lines[i] = $"seed {seeds[i]}: {run}; days {(failures.Count == 0 ? "hold" : "FAIL: " + string.Join("; ", failures))}";
                lock (gate)
                {
                    if (run.BreachedAt >= 0) { breached++; attempts += run.GateAttempts; }
                }
            });
            foreach (var line in lines) TestContext.Current.TestOutputHelper?.WriteLine(line);
            TestContext.Current.TestOutputHelper?.WriteLine($"{(kitted ? "kitted" : "unkitted")}: the gate breached on {breached} of {seeds.Length} seeds in sixteen days ({(breached > 0 ? attempts / (float)breached : 0f):0.0} attempts each)");
        }
    }
}
