using System.Collections.Generic;
using System.Linq;
using Simpiens.Cognition;
using Simpiens.Core.Session;
using Simpiens.Hosting;
using Simpiens.Narrative;
using Simpiens.Simulation;
using Simpiens.Simulation.Creatures;
using Simpiens.Simulation.Economy;
using Simpiens.Simulation.Guild;
using Simpiens.Simulation.Items;
using Xunit;

namespace Simpiens.Core.Tests
{
    /// <summary>
    /// K7's live evidence (Epic 10 Part 3), headless with nothing injected: the scripted workshop player makes blades; the
    /// scripted guild player signs a party on and orders a clear of a charted lair, through commands, as a player would.
    /// <para>
    /// <c>AnArmedPartyClearsANest</c>: it arms four from the stall's stock; they fight together on the party's focus, empty
    /// the lair, carry anyone fallen out (alive) and bring back what they picked up. The ledger balances.
    /// </para>
    /// <para>
    /// <c>AFriendCarriesTheFallenOut</c> (from K5, F6): a member collapses in the lair; the guild sends a mate, who carries it
    /// out of danger; it lives and stands again.
    /// </para>
    /// <para>
    /// <c>DeathHasItsCost</c> (from K5, F6): two sent unarmed against the beetles, under the Bold rule. A member falls and
    /// nobody carries it out in time: it dies. Its friends grieve, the guild loses its contract, its body's pile holds its
    /// belongings and purse, and a newcomer settles two days on. The ledger balances.
    /// </para>
    /// </summary>
    public class ClearScenarioTests
    {
        /// <summary>What a run saw.</summary>
        private sealed class Run
        {
            public long SentAt = -1, FightAt = -1, ClearedAt = -1, HomeAt = -1, StoodAt = -1, DiedAt = -1, NewcomerAt = -1;
            public int Haul = -1, Deaths, Sent, Kills;
            public bool TurnedBack, WasMember, Grieved;
            public float LowestHealth = 100f;
            public EntityId Dead, FirstRescued, FirstRescuer;
            public int BodyStacks = -1, BodyGold = -1;
            public readonly HashSet<EntityId> Collapsed = new HashSet<EntityId>();
            public readonly HashSet<EntityId> Rescued = new HashSet<EntityId>();
            public readonly HashSet<EntityId> Members = new HashSet<EntityId>();
            public readonly List<string> Jitters = new List<string>();
            public OutpostWatch Watch;
            public int RescuedLived;

            public override string ToString() =>
                $"sent {T(SentAt)} ({Sent} in all), fight {T(FightAt)}, cleared {T(ClearedAt)}, home {T(HomeAt)} with {Haul}{(TurnedBack ? " (turned back)" : "")}; "
                + $"kills {Kills}, lowest {LowestHealth:F0}, collapsed {Collapsed.Count}, rescued {Rescued.Count} ({RescuedLived} lived; first stood {T(StoodAt)}), deaths {Deaths}"
                + (DiedAt >= 0 ? $" (first {T(DiedAt)}, member {WasMember}, grieved {Grieved}, body {BodyStacks} stacks and {BodyGold}g, newcomer {T(NewcomerAt)})" : "");

            private static string T(long t) => t >= 0 ? "t" + t : "never";
        }

        private static Run Play(ulong seed, long horizon, System.Func<Run, bool> until, RetreatRule rule = RetreatRule.Steady, bool arm = true,
            CreatureKind kind = CreatureKind.None, int size = 4)
        {
            using var sim = LocalSimulation.Create(seed);
            sim.Respawn(OutpostSession.SessionId);
            new WorkshopPlayer(sim, StationKind.Forge, ItemType.IronBlade, batch: 4);
            var guild = new GuildPlayer(sim, targetSize: size);
            guild.SendClear(kind, rule, arm);
            var watch = new OutpostWatch(sim);
            var run = new Run { Watch = watch };
            watch.Anomalies.AnomalyFound += a =>
            {
                if (a.Type != AnomalyType.OscillationJitter) return;
                var who = sim.Host.CrawlerStates.FirstOrDefault(c => c.AgentId == a.PrimaryAgentId);
                run.Jitters.Add($"t{a.Tick} {who?.CurrentIntent?.GetType().Name} hp {who?.Health:F0} downed {who?.IsDowned} carrying {who?.Carrying != default} struck {who?.StruckAtTick} member {run.Members.Contains(a.PrimaryAgentId)}: {a.Description}");
            };
            var told = new List<ChronicleEvent>(256);
            var memories = new List<Simpiens.Cognition.Memory.MemoryEvent>(128);
            long seen = -1;
            int killsAtStart = sim.Simulation.Creatures.Kills;
            int newcomersAtDeath = 0;
            var party = sim.Simulation.Guild.Parties[0];
            while (sim.Clock.CurrentTick < horizon && (until == null || !until(run)))
            {
                watch.Run(1);
                long now = sim.Clock.CurrentTick;
                if (party.IsOut) foreach (var m in party.Members) run.Members.Add(m.CrawlerId);
                foreach (var crawler in sim.Host.CrawlerStates)
                {
                    if (crawler.IsCollapsed) run.Collapsed.Add(crawler.AgentId);
                    if (party.IsOut && party.Members.Any(m => m.CrawlerId == crawler.AgentId) && crawler.Health < run.LowestHealth) run.LowestHealth = crawler.Health;
                    if (run.StoodAt < 0 && crawler.AgentId == run.FirstRescued && !crawler.IsDowned) run.StoodAt = now;
                }
                if (run.SentAt < 0 && party.IsOut && party.Expedition.IsClear) run.SentAt = now;
                if (run.FightAt < 0 && party.Expedition.Stage == ExpeditionStage.Fight) run.FightAt = now;
                if (run.FightAt >= 0 && run.ClearedAt < 0 && party.Expedition.Cleared) run.ClearedAt = now;
                if (run.DiedAt >= 0 && run.NewcomerAt < 0 && sim.Host.NewcomersArrived > newcomersAtDeath) run.NewcomerAt = now;

                told.Clear();
                sim.Chronicle.GetAllRecentEvents(told, 64);
                foreach (var e in told)
                {
                    if (e.SimulationTick <= seen) continue;
                    if (e.EventType == ChronicleEventType.CrawlerRescued && run.Members.Contains(e.ActorBId))
                    {
                        if (run.Rescued.Count == 0) { run.FirstRescued = e.ActorBId; run.FirstRescuer = e.ActorAId; }
                        run.Rescued.Add(e.ActorBId);
                    }
                    if (run.SentAt >= 0 && run.HomeAt < 0 && !party.IsOut
                        && (e.EventType == ChronicleEventType.ExpeditionReturned || e.EventType == ChronicleEventType.ExpeditionTurnedBack))
                    {
                        run.HomeAt = e.SimulationTick;
                        run.Haul = (int)e.ExtraData;
                        run.TurnedBack = e.EventType == ChronicleEventType.ExpeditionTurnedBack;
                    }
                    if (e.EventType == ChronicleEventType.CrawlerDied && run.DiedAt < 0)
                    {
                        run.DiedAt = e.SimulationTick;
                        run.Dead = e.ActorAId;
                        run.WasMember = run.Members.Contains(e.ActorAId);
                        newcomersAtDeath = sim.Host.NewcomersArrived;
                        foreach (var pile in sim.Simulation.Piles.Piles)
                        {
                            if (pile.BodyOf != run.Dead) continue;
                            run.BodyStacks = pile.Count;
                            run.BodyGold = pile.Gold;
                        }
                        foreach (var crawler in sim.Host.CrawlerStates)
                        {
                            if (crawler.AgentId == run.Dead || crawler.Memory?.EventLedger == null) continue;
                            memories.Clear();
                            crawler.Memory.EventLedger.GetRecentEvents((uint)now, 100, memories);
                            run.Grieved |= memories.Any(m => m.EventType == Simpiens.Cognition.Memory.MemoryEventType.Grieved && m.SubjectId == run.Dead);
                        }
                    }
                }
                seen = now - 1;
            }
            watch.Finish();
            run.Deaths = watch.Deaths;
            run.Sent = guild.Dispatches;
            run.Kills = sim.Simulation.Creatures.Kills - killsAtStart;
            run.RescuedLived = run.Rescued.Count(id => sim.Host.CrawlerStates.Any(c => c.AgentId == id));
            if (run.DiedAt >= 0 && sim.Simulation.Guild.TryGetContract(run.Dead, out _)) run.WasMember = false; // it kept a contract: not lost
            return run;
        }

        private static void AssertTheDaysHeld(Run run)
        {
            var watch = run.Watch;
            Assert.Equal(0, watch.ConservationViolations);
            Assert.Equal(0, watch.IntentFaults);
            var failures = watch.DayFailures();
            Assert.True(failures.Count == 0, "The days failed:\n  " + string.Join("\n  ", failures));
        }

        private static void Log(Run run) =>
            TestContext.Current.TestOutputHelper?.WriteLine($"{run}\n{run.Watch.CreatureSummary()}\n{run.Watch.Report()}");

        /// <summary>
        /// Gate seed 20261009 since Epic 11's teachers (S3.6, <see cref="MeasureClearsOnTheGateSeeds"/> on S3's final build): an
        /// armed four clearing a lair on 21 of the 23 gate seeds, nobody dead, a fallen member carried out alive on 14; seed 3,
        /// K8's, now comes home with nothing picked up. On this one, the earliest clear with a haul, a member falls and is carried
        /// out alive, and the party comes home at t18824 with one of the lair's material. (Since K8: seed 3, 22 of 23 cleared.)
        /// </summary>
        private const ulong ClearSeed = 20261009;

        [Fact]
        public void AnArmedPartyClearsANest()
        {
            var run = Play(ClearSeed, 8 * DayClock.TicksPerDay, r => r.HomeAt >= 0);
            Log(run);
            Assert.True(run.SentAt >= 0, "The guild player ordered a clear.");
            Assert.True(run.FightAt >= 0, "The party reached the lair and fought.");
            Assert.True(run.ClearedAt >= 0, "It emptied the lair.");
            Assert.True(run.HomeAt >= 0 && !run.TurnedBack, "It came home as ordered.");
            Assert.True(run.Haul > 0, "With what it picked up of the lair's material.");
            Assert.True(run.Watch.DeedTotal(Simpiens.Simulation.Skills.Deed.ClearWon) >= 1, "Home from a won clear: a deed for each member with the party (Epic 11, S0.5).");
            Assert.True(run.Collapsed.All(run.Rescued.Contains), "Everyone who fell was carried out.");
            Assert.Equal(0, run.Deaths);
            AssertTheDaysHeld(run);
        }

        /// <summary>Gate seed 6: a member falls in the lair, the guild sends a mate who carries it out, and it stands again (K7.4's measurement; at S3.6 three fall and all three are carried out alive, the first standing at t26502).</summary>
        private const ulong CarrySeed = 6;

        [Fact]
        public void AFriendCarriesTheFallenOut()
        {
            var run = Play(CarrySeed, 8 * DayClock.TicksPerDay, r => r.StoodAt >= 0);
            Log(run);
            Assert.NotEmpty(run.Collapsed);
            Assert.True(run.FirstRescued != default, "A fallen member was carried out of danger.");
            Assert.True(run.Members.Contains(run.FirstRescuer) && run.FirstRescuer != run.FirstRescued, "By a party mate.");
            Assert.True(run.StoodAt >= 0, "It lived and stood again.");
            Assert.Equal(0, run.Deaths);
            AssertTheDaysHeld(run);
        }

        /// <summary>
        /// Gate seed 8: measured (since K8) two sent unarmed against the beetles under the Bold rule losing a member on 2 of the
        /// 23 gate seeds in eight days (9 before the fight's start sent the whole party in together; with four, the mates carry
        /// everyone out); seed 7 until the Matriarch joined the warren (Epic 10 Part 3, K10.2a) moved every creature's id and
        /// timing: still 2 of 23 (seeds 8 and 17), here first at t30345.
        /// Seed 62 since passions and practice by challenge (Epic 11, S1.5), off the gate seeds: over ten days a death on 1 of
        /// the 23 (seed 20, too late for its newcomer within the horizon) and on 12 of seeds 21 to 100, rescuers now carrying
        /// most of the fallen out (Epic 11's finding F4). On this one the first death is at t16981, and a newcomer settles at t37141.
        /// Gate seed 16 since Epic 11's teachers (S3.6, on S3's final build): over ten days a death on 7 of the 23 gate seeds; on
        /// this one, the earliest whose newcomer settles within the horizon, the first death is at t25051 and a newcomer settles
        /// at t45211.
        /// Gate seed 11 since named creatures grow and lead (Epic 11, S7.2; on 16 both fallen are now carried out alive): over ten
        /// days a death on 3 of the 23 gate seeds (5, 8 and 11); on this one, the earliest, the first death is at t46765 and a
        /// newcomer settles at t66925.
        /// Gate seed 8 since grudges run both ways (Epic 11, S7.3; on 11 the fallen are now carried out alive): over ten days a
        /// death on 2 of the 23 gate seeds (5 and 8); on this one, the earlier, the first death is at t53385 and a newcomer
        /// settles at t73545.
        /// </summary>
        private const ulong DeathSeed = 8;

        [Fact]
        public void DeathHasItsCost()
        {
            var run = Play(DeathSeed, 10 * DayClock.TicksPerDay, r => r.NewcomerAt >= 0, RetreatRule.Bold, arm: false, CreatureKind.StoneBeetle, size: 2);
            Log(run);
            Assert.True(run.DiedAt >= 0, "A member fell and nobody carried it out in time.");
            Assert.True(run.WasMember, "It was the guild's, and the guild lost its contract.");
            Assert.True(run.Grieved, "Its friends grieve.");
            Assert.True(run.BodyStacks > 0 || run.BodyGold > 0, "Its body's pile holds its belongings and purse.");
            Assert.True(run.NewcomerAt >= 0, "A newcomer settled.");
            Assert.Equal(0, run.Watch.ConservationViolations);
            Assert.Equal(0, run.Watch.IntentFaults);
        }

        /// <summary>The measurement behind the clear's and the carry's seeds: every gate seed's eight days, eight at a time (explicit, a few minutes).</summary>
        [Fact(Explicit = true)]
        public void MeasureClearsOnTheGateSeeds()
        {
            int cleared = 0, carried = 0, died = 0;
            var seeds = OutpostWatch.GateSeeds;
            var lines = new string[seeds.Length];
            System.Threading.Tasks.Parallel.For(0, seeds.Length, new System.Threading.Tasks.ParallelOptions { MaxDegreeOfParallelism = 8 }, i =>
            {
                var run = Play(seeds[i], 8 * DayClock.TicksPerDay, null);
                if (run.ClearedAt >= 0) System.Threading.Interlocked.Increment(ref cleared);
                if (run.RescuedLived > 0) System.Threading.Interlocked.Increment(ref carried);
                if (run.Deaths > 0) System.Threading.Interlocked.Increment(ref died);
                var failures = run.Watch.DayFailures();
                lines[i] = $"seed {seeds[i]}: {run}; days {(failures.Count == 0 ? "hold" : "FAIL: " + string.Join("; ", failures))}";
            });
            foreach (var line in lines) TestContext.Current.TestOutputHelper?.WriteLine(line);
            TestContext.Current.TestOutputHelper?.WriteLine($"a lair cleared on {cleared}, a fallen member carried out alive on {carried}, a death on {died} of {OutpostWatch.GateSeeds.Length} seeds");
        }

        /// <summary>
        /// The measurement behind the death's seed: two unarmed against the beetles, under the Bold rule, over the DoD's ten days
        /// (explicit, a few minutes). SIMPIENS_SEEDS ("21..100") measures other seeds than the gate's, eight at a time.
        /// </summary>
        [Fact(Explicit = true)]
        public void MeasureUnderArmedClearsOnTheGateSeeds()
        {
            var seeds = OutpostWatch.GateSeeds;
            var range = System.Environment.GetEnvironmentVariable("SIMPIENS_SEEDS");
            if (!string.IsNullOrEmpty(range) && range.Contains(".."))
            {
                var ends = range.Split("..");
                ulong from = ulong.Parse(ends[0]), to = ulong.Parse(ends[1]);
                seeds = Enumerable.Range(0, (int)(to - from + 1)).Select(i => from + (ulong)i).ToArray();
            }
            var lines = new string[seeds.Length];
            int died = 0;
            System.Threading.Tasks.Parallel.For(0, seeds.Length, new System.Threading.Tasks.ParallelOptions { MaxDegreeOfParallelism = 8 }, i =>
            {
                var run = Play(seeds[i], 10 * DayClock.TicksPerDay, null, RetreatRule.Bold, arm: false, CreatureKind.StoneBeetle, size: 2);
                if (run.Deaths > 0) System.Threading.Interlocked.Increment(ref died);
                var failures = run.Watch.DayFailures();
                lines[i] = $"seed {seeds[i]}: {run}; days {(failures.Count == 0 ? "hold" : "FAIL: " + string.Join("; ", failures))}{(run.Jitters.Count > 0 ? "; jitter " + string.Join(" | ", run.Jitters) : "")}";
            });
            foreach (var line in lines) TestContext.Current.TestOutputHelper?.WriteLine(line);
            TestContext.Current.TestOutputHelper?.WriteLine($"a death on {died} of {seeds.Length} seeds");
        }
    }
}
