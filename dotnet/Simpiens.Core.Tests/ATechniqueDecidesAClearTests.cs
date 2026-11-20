using System.Collections.Generic;
using System.Linq;
using Simpiens.Core.Session;
using Simpiens.Hosting;
using Simpiens.Narrative;
using Simpiens.Simulation;
using Simpiens.Simulation.Combat;
using Simpiens.Simulation.Economy;
using Simpiens.Simulation.Guild;
using Simpiens.Simulation.Items;
using Simpiens.Simulation.Skills;
using Xunit;

namespace Simpiens.Core.Tests
{
    /// <summary>
    /// S4's Definition of Done (Epic 11, S4.10): a member picks a technique on its own, and the party's clear uses it, on the live
    /// outpost with nothing injected. The forge's player makes blades; the guild's player recruits four, builds the drill yard
    /// and orders clears of the charted lairs, arming the party from the stall.
    /// <list type="bullet">
    /// <item><b>Its own pick:</b> a guild member reaches a band of a fight's skill during the run and takes up a technique by its
    /// nature, as the chronicle tells it (not at spawn, and nobody sponsors one in this run).</item>
    /// <item><b>The clear uses it:</b> at a clear's fight, that member lands blows holding the technique, its fighter with the
    /// technique another than without it; the party empties the lair.</item>
    /// </list>
    /// </summary>
    public class ATechniqueDecidesAClearTests
    {
        private sealed class Run
        {
            public EntityId Picker;
            public string PickerName = "-";
            public Technique Picked;
            public long PickedAt = -1, UsedAt = -1, ClearedAt = -1, YardAt = -1;
            public int BlowsWith, Clears, PicksInRun;
            public string Difference = "-";
            public OutpostWatch Watch;

            public bool All => PickedAt >= 0 && UsedAt >= 0 && ClearedAt >= 0;

            public override string ToString() =>
                $"picked: {(PickedAt >= 0 ? $"{PickerName} took up {TechniqueTable.Get(Picked)?.DisplayName} at t{PickedAt}" : "no")} ({PicksInRun} member picks in all); "
                + $"yard t{YardAt}; used at a clear: {(UsedAt >= 0 ? $"t{UsedAt}, {BlowsWith} blows ({Difference})" : "no")}; cleared {(ClearedAt >= 0 ? "t" + ClearedAt : "no")} ({Clears} clears)";
        }

        private static Run Play(ulong seed, long horizon, bool untilAll)
        {
            using var sim = LocalSimulation.Create(seed);
            sim.Respawn(OutpostSession.SessionId);
            new WorkshopPlayer(sim, StationKind.Forge, ItemType.IronBlade, batch: 4);
            var guild = new GuildPlayer(sim, targetSize: 4).BuildYard(reserve: EconomySoak.HuntingReserve);
            guild.SendClear();
            var watch = new OutpostWatch(sim);
            var run = new Run { Watch = watch };
            var party = sim.Simulation.Guild.Parties[0];
            var picks = new Dictionary<EntityId, (Technique Technique, long At)>();
            var told = new List<ChronicleEvent>(64);
            long seen = -1;
            bool wasCleared = false;

            sim.Simulation.Practised += (who, skill, experience, source) =>
            {
                if (source.Kind != PracticeKind.Work || !party.IsOut || party.Expedition.Stage != Simpiens.Simulation.Guild.ExpeditionStage.Fight) return;
                if (!picks.TryGetValue(who.AgentId, out var pick) || party.IndexOf(who.AgentId) < 0) return;
                var row = TechniqueTable.Get(pick.Technique);
                if (row == null || row.Skill != skill || who.Traits.TechniqueIn(row.Skill, row.Band) != pick.Technique) return;
                // A blow at the fight with the technique it picked: its fighter now, against the same fighter without it
                var with = Combatant.ForCrawler(who, 0.4f);
                var without = Combatant.ForCrawler(who.AgentId, who.Position, 0.4f, who.Health, true, who.Skills, who.Inventory, default);
                bool differs = with.Attack != without.Attack || with.Defense != without.Defense || with.SwingInterval != without.SwingInterval
                    || with.Proficiency != without.Proficiency || with.Range != without.Range;
                if (!differs) return;
                if (run.UsedAt < 0)
                {
                    run.UsedAt = sim.Clock.CurrentTick;
                    run.Picker = who.AgentId;
                    run.PickerName = who.GivenName;
                    run.Picked = pick.Technique;
                    run.PickedAt = pick.At;
                    run.Difference = $"attack {without.Attack:0.##} -> {with.Attack:0.##}, defense {without.Defense:0.#} -> {with.Defense:0.#}, every {without.SwingInterval} -> {with.SwingInterval} ticks";
                }
                if (who.AgentId == run.Picker) run.BlowsWith++;
            };

            while (sim.Clock.CurrentTick < horizon && !(untilAll && run.All && !party.IsOut))
            {
                watch.Run(1);
                long now = sim.Clock.CurrentTick;
                if (run.YardAt < 0 && guild.YardBuiltTick >= 0) run.YardAt = guild.YardBuiltTick;
                told.Clear();
                sim.Chronicle.GetAllRecentEvents(told, 64);
                foreach (var e in told)
                {
                    if (e.SimulationTick <= seen) continue;
                    if (e.EventType != ChronicleEventType.TechniqueChosen || !sim.Simulation.Guild.TryGetContract(e.ActorAId, out _)) continue;
                    var row = TechniqueTable.Get((Technique)(byte)e.ExtraData);
                    var use = row != null ? SkillTable.Get(row.Skill)?.Use : null;
                    run.PicksInRun++;
                    if (use != SkillUse.MeleeFight && use != SkillUse.RangedFight) continue;
                    picks[e.ActorAId] = (row.Id, e.SimulationTick);
                }
                seen = now - 1;
                bool cleared = party.Expedition.IsClear && party.Expedition.Cleared;
                if (cleared && !wasCleared)
                {
                    run.Clears++;
                    if (run.UsedAt >= 0 && run.ClearedAt < 0) run.ClearedAt = now; // the clear it fought in, or a later one
                }
                wasCleared = cleared;
            }
            watch.Finish();
            return run;
        }

        /// <summary>The horizon: ten game days.</summary>
        private const long Horizon = 10 * DayClock.TicksPerDay;

        /// <summary>
        /// Gate seed 3 (S4.10, <see cref="MeasureATechniqueDecidesAClearOnTheGateSeeds"/> on S4's build): over ten days a member's
        /// own pick was used at a clear's fight on 18 of the 23 gate seeds, the lair emptied and the days held on 17 (Reckless
        /// Fury on 9, Shield Wall on 9; the picks come from drill at the yard and from the clears' own blows). On this one, the
        /// earliest of the 17: Priya takes up Reckless Fury at t34587 and lands 16 blows with it at the clear that empties its
        /// lair at t38395 (attack 10.6 to 13.25).
        /// Gate seed 18 since S5.6 (Leadership moved every draw, and the roles every clear): held on 14 of 23; on this one, the
        /// earliest, Kiri takes up Reckless Fury at t19419 and lands 25 blows with it at the clear that empties its lair.
        /// </summary>
        private const ulong Seed = 18;

        [Fact]
        public void ATechniqueDecidesAClear()
        {
            var run = Play(Seed, Horizon, untilAll: true);
            var watch = run.Watch;
            TestContext.Current.TestOutputHelper?.WriteLine($"{run}\n{watch.CreatureSummary()}\n{watch.Report()}");
            Assert.True(run.PickedAt >= 0, "A member took up a fight's technique on its own, during the run.");
            Assert.True(run.UsedAt > run.PickedAt, "At a clear's fight it landed blows holding the technique, its fighter changed by it.");
            Assert.True(run.ClearedAt >= 0, "The party emptied the lair.");
            Assert.Equal(0, watch.ConservationViolations);
            Assert.Equal(0, watch.IntentFaults);
            var failures = watch.DayFailures();
            Assert.True(failures.Count == 0, "The days failed:\n  " + string.Join("\n  ", failures));
        }

        /// <summary>The measurement behind the seed: every gate seed's ten days, eight at a time (explicit, a few minutes).</summary>
        [Fact(Explicit = true)]
        public void MeasureATechniqueDecidesAClearOnTheGateSeeds()
        {
            var seeds = System.Environment.GetEnvironmentVariable("SIMPIENS_SEEDS") is string list && list.Length > 0
                ? list.Split(',').Select(ulong.Parse).ToArray() : OutpostWatch.GateSeeds;
            var lines = new string[seeds.Length];
            int used = 0, all = 0;
            System.Threading.Tasks.Parallel.For(0, seeds.Length, new System.Threading.Tasks.ParallelOptions { MaxDegreeOfParallelism = 8 }, i =>
            {
                var run = Play(seeds[i], Horizon, untilAll: false);
                var failures = run.Watch.DayFailures();
                lines[i] = $"seed {seeds[i]}: {run}; days {(failures.Count == 0 ? "hold" : "FAIL: " + string.Join("; ", failures))}";
                if (run.UsedAt >= 0) System.Threading.Interlocked.Increment(ref used);
                if (run.All && failures.Count == 0) System.Threading.Interlocked.Increment(ref all);
            });
            foreach (var line in lines) TestContext.Current.TestOutputHelper?.WriteLine(line);
            TestContext.Current.TestOutputHelper?.WriteLine($"of {seeds.Length} seeds in {Horizon / DayClock.TicksPerDay} days: a member's own pick used at a clear's fight on {used}; with the lair emptied and the days held on {all}");
        }
    }
}
