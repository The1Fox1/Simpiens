using System.Collections.Generic;
using System.Linq;
using Simpiens.Cognition;
using Simpiens.Core.Session;
using Simpiens.Hosting;
using Simpiens.Simulation;
using Simpiens.Simulation.Skills;
using Xunit;

namespace Simpiens.Core.Tests
{
    /// <summary>
    /// S3's Definition of Done (Epic 11, S3.6): teachers emerge in the live outpost, with nothing injected. The guild's
    /// scripted player (the guild profile's four, sent to the Drowned Gallery whenever its seam is back) builds the drill yard
    /// once the stall can spare it, through the station window's command; the caravan shelves the primers.
    /// <list type="bullet">
    /// <item><b>A mentor:</b> a hand practising beside a better one is taught (practice at source Mentor), reaches Apprentice in
    /// that skill, and records its teacher. The epic's scene names a Master; the teacher here is whoever stood three levels
    /// above, and the run says at what level.</item>
    /// <item><b>A manual:</b> one is bought and read (practice at source Manual).</item>
    /// <item><b>The yard:</b> a member drills there (practice at source Yard).</item>
    /// </list>
    /// </summary>
    public class AMasterTeachesAnApprenticeTests
    {
        private sealed class Run
        {
            public string Taught = "-", Read = "-", Drilled = "-";
            public long TaughtAt = -1, ReadAt = -1, DrilledAt = -1, YardAt = -1;
            public int Lessons;
            public OutpostWatch Watch;

            public bool All => TaughtAt >= 0 && ReadAt >= 0 && DrilledAt >= 0;

            public override string ToString() =>
                $"taught: {(TaughtAt >= 0 ? $"{Taught} at t{TaughtAt}" : "no")} ({Lessons} lessons in all); read: {(ReadAt >= 0 ? $"{Read} at t{ReadAt}" : "no")}; "
                + $"yard built at t{YardAt}; drilled: {(DrilledAt >= 0 ? $"{Drilled} at t{DrilledAt}" : "no")}";
        }

        private static Run Play(ulong seed, long horizon, bool untilAll)
        {
            using var sim = LocalSimulation.Create(seed);
            sim.Respawn(OutpostSession.SessionId);
            var guildPlayer = new GuildPlayer(sim, targetSize: EconomySoak.GuildProfileSize).BuildYard(reserve: EconomySoak.HuntingReserve);
            guildPlayer.SendExpeditions(Simpiens.Core.Session.Layouts.Floor1Layout.DrownedGallerySectorId, Simpiens.Core.Session.Layouts.Floor1Layout.GallerySeamYield);
            var watch = new OutpostWatch(sim);
            var run = new Run { Watch = watch };
            var taughtIn = new HashSet<(AgentState, Skill)>();
            var startBand = new Dictionary<(AgentState, Skill), SkillBand>();
            sim.Simulation.Practised += (who, skill, experience, source) =>
            {
                long now = sim.Clock.CurrentTick;
                switch (source.Kind)
                {
                    case PracticeKind.Mentor:
                        run.Lessons++;
                        taughtIn.Add((who, skill));
                        break;
                    case PracticeKind.Manual when run.ReadAt < 0:
                        run.ReadAt = now;
                        run.Read = $"{who.GivenName} read {SkillTable.Get(skill).DisplayName} ({who.Traits.PassionFor(skill)})";
                        break;
                    case PracticeKind.Yard when run.DrilledAt < 0:
                        run.DrilledAt = now;
                        run.Drilled = $"{who.GivenName} drilled {SkillTable.Get(skill).DisplayName}";
                        break;
                }
            };
            foreach (var crawler in sim.Host.CrawlerStates)
                foreach (var row in SkillTable.All) startBand[(crawler, row.Id)] = crawler.Skills.Band(row.Id);

            while (sim.Clock.CurrentTick < horizon && !(untilAll && run.All))
            {
                watch.Run(1);
                if (run.YardAt < 0 && guildPlayer.YardBuiltTick >= 0) run.YardAt = guildPlayer.YardBuiltTick;
                if (run.TaughtAt >= 0) continue;
                // A learner taught in a skill that has since reached Apprentice from below it, its teacher recorded
                foreach (var (who, skill) in taughtIn)
                {
                    var teacher = who.Traits.TeacherOf(skill);
                    if (teacher == default || who.Skills.Band(skill) < SkillBand.Apprentice) continue;
                    if (!startBand.TryGetValue((who, skill), out var was)) was = SkillBand.Novice; // a newcomer: a Novice there at its arrival
                    if (was >= SkillBand.Apprentice) continue;
                    var mentor = sim.Host.CrawlerStates.FirstOrDefault(s => s.AgentId == teacher);
                    run.TaughtAt = sim.Clock.CurrentTick;
                    run.Taught = $"{who.GivenName} to {SkillTable.Get(skill).DisplayName} {who.Skills.Level(skill)}, taught by {mentor?.GivenName ?? "one since gone"}"
                        + (mentor != null ? $" ({SkillRules.NameOf(mentor.Skills.Band(skill))} {mentor.Skills.Level(skill)})" : string.Empty);
                    break;
                }
            }
            watch.Finish();
            return run;
        }

        /// <summary>The horizon: two weeks, S0.3's.</summary>
        private const long Horizon = 14 * DayClock.TicksPerDay;

        /// <summary>
        /// Gate seed 13 (S3.6's measurement, <see cref="MeasureTeachersOnTheGateSeeds"/>): over two weeks a learner was taught to
        /// Apprentice on 22 of the 23 gate seeds, a primer read on all 23 and the yard drilled on all 23, the days held on the
        /// 22; every teacher found was an Apprentice three or more levels up, never yet a Master (nobody reaches Master in two
        /// weeks, F1), mostly at a seam. On this one, the earliest of the 22: Jory reads the ranged primer at t3637, the yard
        /// stands by t11456 and Zara drills there at t18208, and Avery, taught at the herbs by Vesna (Apprentice 5), reaches
        /// Foraging 3 at t19354 with Vesna recorded as its teacher.
        /// </summary>
        private const ulong Seed = 13;

        [Fact]
        public void AMasterTeachesAnApprentice()
        {
            var run = Play(Seed, Horizon, untilAll: true);
            var watch = run.Watch;
            TestContext.Current.TestOutputHelper?.WriteLine($"{run}\n{watch.Report()}");
            Assert.True(run.TaughtAt >= 0, "A hand taught beside a better one reached Apprentice in that skill, its teacher recorded.");
            Assert.True(run.ReadAt >= 0, "A manual was bought and read.");
            Assert.True(run.DrilledAt >= 0, "A member drilled at the yard the player built.");
            Assert.True(watch.DeedTotal(Deed.Taught) > 0, "A mentor counted a crawler taught.");
            Assert.Equal(0, watch.ConservationViolations);
            Assert.Equal(0, watch.IntentFaults);
            var failures = watch.DayFailures();
            Assert.True(failures.Count == 0, "The days failed:\n  " + string.Join("\n  ", failures));
        }

        /// <summary>The measurement behind the seed: every gate seed's two weeks, eight at a time (explicit, a few minutes).</summary>
        [Fact(Explicit = true)]
        public void MeasureTeachersOnTheGateSeeds()
        {
            var seeds = System.Environment.GetEnvironmentVariable("SIMPIENS_SEEDS") is string list && list.Length > 0
                ? list.Split(',').Select(ulong.Parse).ToArray() : OutpostWatch.GateSeeds;
            var lines = new string[seeds.Length];
            int taught = 0, read = 0, drilled = 0, all = 0;
            System.Threading.Tasks.Parallel.For(0, seeds.Length, new System.Threading.Tasks.ParallelOptions { MaxDegreeOfParallelism = 8 }, i =>
            {
                var run = Play(seeds[i], Horizon, untilAll: false);
                var failures = run.Watch.DayFailures();
                lines[i] = $"seed {seeds[i]}: {run}; days {(failures.Count == 0 ? "hold" : "FAIL: " + string.Join("; ", failures))}";
                if (run.TaughtAt >= 0) System.Threading.Interlocked.Increment(ref taught);
                if (run.ReadAt >= 0) System.Threading.Interlocked.Increment(ref read);
                if (run.DrilledAt >= 0) System.Threading.Interlocked.Increment(ref drilled);
                if (run.All && failures.Count == 0) System.Threading.Interlocked.Increment(ref all);
            });
            foreach (var line in lines) TestContext.Current.TestOutputHelper?.WriteLine(line);
            TestContext.Current.TestOutputHelper?.WriteLine($"of {seeds.Length} seeds in {Horizon / DayClock.TicksPerDay} days: taught to Apprentice on {taught}, a manual read on {read}, the yard drilled on {drilled}; all with the days held on {all}");
        }
    }
}
