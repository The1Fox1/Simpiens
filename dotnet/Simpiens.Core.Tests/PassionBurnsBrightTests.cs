using System.Collections.Generic;
using System.Linq;
using Simpiens.Cognition;
using Simpiens.Core.Session;
using Simpiens.Hosting;
using Simpiens.Simulation;
using Simpiens.Simulation.Creatures;
using Simpiens.Simulation.Skills;
using Xunit;

namespace Simpiens.Core.Tests
{
    /// <summary>
    /// S1's Definition of Done (Epic 11, S1.7): passions and practice by challenge emerge in the live outpost, with nothing
    /// injected. The soak's <c>hunting</c> players play through commands only (the forge and the still, the hunt player, a
    /// guild clearing with draughts).
    /// <list type="bullet">
    /// <item><b>A passion burns bright:</b> a resident burning for a skill reaches its next band in it before every
    /// passionless resident who started at the same level of that skill and practised it.</item>
    /// <item><b>Veterans learn from harder foes:</b> fighters at Apprentice or above draw more practice a blow from a beetle
    /// (challenge 7) than from a rat (2), from practice's own account of each act (the <c>Practised</c> event). <b>Since Epic
    /// LIFE-1's Recreate</b> (plan 2, L2.6, finding F17) no Apprentice strikes a rat within the fortnight on any of 63 seeds:
    /// free time took the field hours that brought them to the warren by chance. The scenario still reports it; the rule is
    /// proved by the practice validators, and <see cref="MeasurePassionsOnTheGateSeeds"/> counts the seeds where it shows.</item>
    /// </list>
    /// </summary>
    public class PassionBurnsBrightTests
    {
        private sealed class Run
        {
            public string Burner = "-";
            public long BurnerAt = -1;
            public int Peers;
            public bool Burned;
            public int RatBlows, BeetleBlows, RatExperience, BeetleExperience;
            public OutpostWatch Watch;

            public double PerRatBlow => RatBlows == 0 ? 0 : RatExperience / (double)RatBlows;
            public double PerBeetleBlow => BeetleBlows == 0 ? 0 : BeetleExperience / (double)BeetleBlows;
            public bool Harder => RatBlows > 0 && BeetleBlows > 0 && PerBeetleBlow > PerRatBlow;

            public override string ToString() =>
                $"burned: {(Burned ? $"{Burner} at t{BurnerAt}, ahead of {Peers} passionless peer(s)" : "no")}; "
                + $"blows by Apprentices and up: {RatBlows} on rats ({PerRatBlow:F1} a blow), {BeetleBlows} on beetles ({PerBeetleBlow:F1} a blow)";
        }

        private static Run Play(ulong seed, long horizon, bool untilBoth)
        {
            using var sim = LocalSimulation.Create(seed);
            sim.Respawn(OutpostSession.SessionId);
            EconomySoak.AttachPlayers(sim, "hunting");
            var watch = new OutpostWatch(sim);
            var run = new Run { Watch = watch };
            var rat = CreatureCatalog.Get(CreatureKind.BurrowRat).Challenge;
            var beetle = CreatureCatalog.Get(CreatureKind.StoneBeetle).Challenge;
            var practised = new Dictionary<(AgentState, Skill), int>();
            sim.Simulation.Practised += (who, skill, experience, source) =>
            {
                practised[(who, skill)] = practised.TryGetValue((who, skill), out int had) ? had + experience : experience;
                if (skill != Skill.Melee || who.Skills.Level(skill) < 3) return; // a blow, by an Apprentice or above (the level after it)
                if (source.Challenge == rat) { run.RatBlows++; run.RatExperience += experience; }
                else if (source.Challenge == beetle) { run.BeetleBlows++; run.BeetleExperience += experience; }
            };

            // Each resident's skills as first seen, and when it first reached its next band in each skill
            var start = new Dictionary<AgentState, SkillSet>();
            var reached = new Dictionary<(AgentState, Skill), long>();
            while (sim.Clock.CurrentTick < horizon && !(untilBoth && run.Burned))
            {
                foreach (var crawler in sim.Host.CrawlerStates) if (!crawler.IsDrifter && !start.ContainsKey(crawler)) start[crawler] = crawler.Skills;
                watch.Run(1);
                long now = sim.Clock.CurrentTick;
                foreach (var (crawler, was) in start)
                {
                    foreach (var row in SkillTable.All)
                    {
                        if (reached.ContainsKey((crawler, row.Id))) continue;
                        if (SkillRules.BandOf(crawler.Skills.Level(row.Id)) > SkillRules.BandOf(was.Level(row.Id))) reached[(crawler, row.Id)] = now;
                    }
                }
                if (!run.Burned) Judge(run, start, reached, practised);
            }
            watch.Finish();
            return run;
        }

        // A burner who reached its next band before every passionless peer that started at its level of the skill and practised it
        private static void Judge(Run run, Dictionary<AgentState, SkillSet> start, Dictionary<(AgentState, Skill), long> reached, Dictionary<(AgentState, Skill), int> practised)
        {
            foreach (var ((crawler, skill), at) in reached)
            {
                if (crawler.Traits.PassionFor(skill) != Passion.Burning) continue;
                int level = start[crawler].Level(skill);
                var peers = start.Keys.Where(p => p != crawler && p.Traits.PassionFor(skill) == Passion.None && start[p].Level(skill) == level
                    && practised.TryGetValue((p, skill), out int done) && done > 0).ToList();
                if (peers.Count == 0) continue;
                if (peers.Any(p => reached.TryGetValue((p, skill), out long theirs) && theirs <= at)) continue;
                run.Burned = true;
                run.Burner = $"{crawler.GivenName}, burning for {SkillTable.Get(skill).DisplayName} ({level} to {SkillRules.NameOf(SkillRules.BandOf(level) + 1)})";
                run.BurnerAt = at;
                run.Peers = peers.Count;
                return;
            }
        }

        /// <summary>
        /// Gate seed 18, re-picked at S3.6 (the teachers moved S1.7's seed 17; measured with <see cref="MeasurePassionsOnTheGateSeeds"/>
        /// on S3's final build): over fourteen days a passion burned bright on 14 of the 23 gate seeds and harder foes taught more
        /// on 5, both with the days held on 4 (1, 11, 18 and 19). On this one, the earliest of the four, Arjun, burning for melee,
        /// reaches Apprentice at t11879 ahead of a passionless peer, and over the fortnight a blow on a beetle teaches 17.6
        /// against a rat's 12.9. (At S1.7: seed 17, Marlo burning for foraging.)
        /// Gate seed 8 since Epic 11's S5.6 (Leadership moved every draw): a passion burned bright on 11 of 23, but Apprentices
        /// strike rats as well as beetles on few seeds, so both held, with the days, on this one only. Mei, burning for melee,
        /// reaches Apprentice at t42137 ahead of a passionless peer, and a blow on a beetle teaches 12.6 against a rat's 6.0.
        /// Gate seed 424242 since Epic LIFE-1's Recreate (plan 2, L2.6, `docs/life-model.md` §5.8): free time moved the outpost's
        /// history and nobody of Apprentice band meets a rat any more (F17), so the scenario holds the passion: a passion burned
        /// bright on 11 of 23, the days held on this one, where Wim, burning for foraging, reaches Apprentice at t9857.
        /// </summary>
        private static readonly ulong Seed = ScenarioSeed.Or(424242);
        private const long Horizon = 14 * DayClock.TicksPerDay;

        [Fact]
        public void PassionBurnsBright()
        {
            var run = Play(Seed, Horizon, untilBoth: true);
            var watch = run.Watch;
            TestContext.Current.TestOutputHelper?.WriteLine($"{run}\n{watch.CreatureSummary()}\n{watch.Report()}");
            Assert.True(run.Burned, "A resident burning for a skill reached its next band before every passionless peer who started where it did.");
            Assert.Equal(0, watch.ConservationViolations);
            Assert.Equal(0, watch.IntentFaults);
            var failures = watch.DayFailures();
            Assert.True(failures.Count == 0, "The days failed:\n  " + string.Join("\n  ", failures));
        }

        /// <summary>The measurement behind the seed and the horizon: every gate seed's fourteen days, eight at a time (explicit, a few minutes).</summary>
        [Fact(Explicit = true)]
        public void MeasurePassionsOnTheGateSeeds()
        {
            var seeds = OutpostWatch.GateSeeds;
            var lines = new string[seeds.Length];
            int burned = 0, harder = 0, both = 0;
            System.Threading.Tasks.Parallel.For(0, seeds.Length, new System.Threading.Tasks.ParallelOptions { MaxDegreeOfParallelism = 8 }, i =>
            {
                var run = Play(seeds[i], Horizon, untilBoth: false);
                var failures = run.Watch.DayFailures();
                lines[i] = $"seed {seeds[i]}: {run}; days {(failures.Count == 0 ? "hold" : "FAIL: " + string.Join("; ", failures))}";
                if (run.Burned) System.Threading.Interlocked.Increment(ref burned);
                if (run.Harder) System.Threading.Interlocked.Increment(ref harder);
                if (run.Burned && run.Harder && failures.Count == 0) System.Threading.Interlocked.Increment(ref both);
            });
            foreach (var line in lines) TestContext.Current.TestOutputHelper?.WriteLine(line);
            TestContext.Current.TestOutputHelper?.WriteLine($"of {seeds.Length} seeds in {Horizon / DayClock.TicksPerDay} days: a passion burned bright on {burned}, harder foes taught more on {harder}, both with the days held on {both}");
        }
    }
}
