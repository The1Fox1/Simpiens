using System.Collections.Generic;
using System.Linq;
using Simpiens.Cognition;
using Simpiens.Cognition.Planning.Handlers;
using Simpiens.Core.Session;
using Simpiens.Hosting;
using Simpiens.Simulation;
using Simpiens.Simulation.Creatures;
using Simpiens.Simulation.Economy;
using Simpiens.Simulation.Guild;
using Simpiens.Simulation.Items;
using Simpiens.Simulation.Skills;
using Simpiens.Simulation.Workshops;
using Xunit;

namespace Simpiens.Core.Tests
{
    /// <summary>
    /// K9's DoD, <c>PracticeMakesAVeteran</c> (Epic 10 Part 3), live and headless with nothing injected: the scripted
    /// workshop player builds the forge, hires the best smith it can and keeps it making blades; the scripted guild player
    /// signs four on, arms them from the stall's stock and orders a clear, through commands, as a player would. Within
    /// seven game days a guild member's melee rises through its fights, a resident's mining rises so that it mines faster,
    /// and the smith's practice raises the grade of what it makes; nobody dies and the days' checks hold.
    /// <para>
    /// Seven days, not the plan's three (Epic 10 Part 3, D53): measured on the 23 gate seeds, all three came within three days on none,
    /// within seven on 14 and within ten on 15. The smith sets the pace: the forge stands only on day 1.3 to 2.8, when the
    /// stall affords it, and ten makes take a Novice to Apprentice as W4's ten took a hand to Fine. In three days melee rose
    /// on 11 seeds and mining on 19.
    /// </para>
    /// </summary>
    public class PracticeMakesAVeteranTests
    {
        /// <summary>What a run saw.</summary>
        private sealed class Run
        {
            public long MeleeRoseAt = -1, MiningRoseAt = -1, GradeRoseAt = -1, HiredAt = -1;
            public string Veteran = "-", Miner = "-", Smith = "-";
            public int MembersWhoFought;
            public uint Made;
            public OutpostWatch Watch;

            public bool AllThree => MeleeRoseAt >= 0 && MiningRoseAt >= 0 && GradeRoseAt >= 0;

            public override string ToString() =>
                $"melee rose {T(MeleeRoseAt)} ({Veteran}; {MembersWhoFought} members fought), mining rose {T(MiningRoseAt)} ({Miner}), "
                + $"the smith hired {T(HiredAt)}, its grade rose {T(GradeRoseAt)} ({Smith}; {Made} made)";

            private static string T(long t) => t >= 0 ? $"t{t} (day {1 + t / (double)DayClock.TicksPerDay:F1})" : "never";
        }

        private static Run Play(ulong seed, long horizon, bool untilAll)
        {
            using var sim = LocalSimulation.Create(seed);
            sim.Respawn(OutpostSession.SessionId);
            new WorkshopPlayer(sim, StationKind.Forge, ItemType.IronBlade, batch: 4);
            var guild = new GuildPlayer(sim, targetSize: 4);
            guild.SendClear(CreatureKind.None, RetreatRule.Steady, arm: true);
            var watch = new OutpostWatch(sim);
            var run = new Run { Watch = watch };

            // Each crawler's skills as it was first seen (at spawn, or on arriving): what practice is measured from
            var start = new Dictionary<EntityId, SkillSet>();
            var fought = new HashSet<EntityId>();
            var workshops = sim.Simulation.Workshops;
            int forge = workshops.IndexOf(StationKind.Forge);
            EntityId smith = default;
            byte hiredGrade = 0;
            int hiredLevel = 0;
            while (sim.Clock.CurrentTick < horizon && !(untilAll && run.AllThree))
            {
                foreach (var crawler in sim.Host.CrawlerStates) if (!start.ContainsKey(crawler.AgentId)) start[crawler.AgentId] = crawler.Skills;
                watch.Run(1);
                long now = sim.Clock.CurrentTick;
                foreach (var crawler in sim.Host.CrawlerStates)
                {
                    if (!start.TryGetValue(crawler.AgentId, out var was)) continue;
                    if (sim.Simulation.Guild.IsMember(crawler.AgentId) && crawler.Skills.Experience(Skill.Melee) > was.Experience(Skill.Melee))
                    {
                        fought.Add(crawler.AgentId); // blows landed: melee's only practice
                        if (run.MeleeRoseAt < 0 && crawler.Skills.Level(Skill.Melee) > was.Level(Skill.Melee))
                        {
                            run.MeleeRoseAt = now;
                            run.Veteran = $"{crawler.GivenName}, Melee {was.Level(Skill.Melee)} to {crawler.Skills.Level(Skill.Melee)}";
                        }
                    }
                    if (run.MiningRoseAt < 0 && !crawler.IsDrifter
                        && HarvestResourceIntentHandler.SkillSpeed(crawler.Skills, ItemType.IronOre) > HarvestResourceIntentHandler.SkillSpeed(was, ItemType.IronOre))
                    {
                        run.MiningRoseAt = now;
                        run.Miner = $"{crawler.GivenName}, Mining {was.Level(Skill.Mining)} to {crawler.Skills.Level(Skill.Mining)}: pace {HarvestResourceIntentHandler.SkillSpeed(was, ItemType.IronOre):F2} to {HarvestResourceIntentHandler.SkillSpeed(crawler.Skills, ItemType.IronOre):F2}";
                    }
                }
                var artisan = forge >= 0 ? workshops.ArtisanAt(forge) : default;
                var hand = artisan != default ? sim.Host.CrawlerStates.FirstOrDefault(c => c.AgentId == artisan) : null;
                if (hand != null && artisan != smith)
                {
                    smith = artisan; // hired (or a new hand)
                    hiredGrade = WorkshopService.GradeOf(hand, StationKind.Forge);
                    hiredLevel = WorkshopService.LevelOf(hand, StationKind.Forge);
                    if (run.HiredAt < 0) run.HiredAt = now;
                }
                else if (hand != null && run.GradeRoseAt < 0 && WorkshopService.GradeOf(hand, StationKind.Forge) > hiredGrade)
                {
                    run.GradeRoseAt = now;
                    run.Smith = $"{hand.GivenName}, Smithing {hiredLevel} to {WorkshopService.LevelOf(hand, StationKind.Forge)}: {Grades.NameOf(hiredGrade)} to {Grades.NameOf(WorkshopService.GradeOf(hand, StationKind.Forge))}";
                }
            }
            watch.Finish();
            run.MembersWhoFought = fought.Count;
            run.Made = forge >= 0 ? workshops[forge].Made : 0;
            if (run.GradeRoseAt < 0 && smith != default)
            {
                var hand = sim.Host.CrawlerStates.FirstOrDefault(c => c.AgentId == smith);
                if (hand != null) run.Smith = $"{hand.GivenName}, Smithing {hiredLevel} to {WorkshopService.LevelOf(hand, StationKind.Forge)} ({hand.Skills.Experience(Skill.Smithing)} xp)";
            }
            return run;
        }

        /// <summary>The default seed: a member's melee 1 to 2 on day 2.9, a resident's mining 0 to 1 on day 3.6, the smith (a Novice 2 when hired) Fine on day 4.3, at its fifth make.</summary>
        private const ulong Seed = SimulationSettings.DefaultSeed;

        private const long Horizon = 7 * DayClock.TicksPerDay;

        [Fact]
        public void PracticeMakesAVeteran()
        {
            var run = Play(Seed, Horizon, untilAll: true);
            TestContext.Current.TestOutputHelper?.WriteLine($"{run}\n{run.Watch.CreatureSummary()}\n{run.Watch.Report()}");
            Assert.True(run.MeleeRoseAt >= 0, "A guild member's melee rose through its fights.");
            Assert.True(run.MiningRoseAt >= 0, "A miner's mining rose: it mines faster.");
            Assert.True(run.GradeRoseAt >= 0, "The smith's practice raised the grade of what it makes.");
            Assert.Equal(0, run.Watch.Deaths);
            Assert.Equal(0, run.Watch.ConservationViolations);
            Assert.Equal(0, run.Watch.IntentFaults);
            var failures = run.Watch.DayFailures();
            Assert.True(failures.Count == 0, "The days failed:\n  " + string.Join("\n  ", failures));
        }

        /// <summary>
        /// The measurement behind the seed and the horizon: every gate seed's seven days, eight at a time (explicit, a few
        /// minutes); SIMPIENS_PRACTICE_DAYS measures another horizon.
        /// </summary>
        [Fact(Explicit = true)]
        public void MeasurePracticeOnTheGateSeeds()
        {
            int Days = int.TryParse(System.Environment.GetEnvironmentVariable("SIMPIENS_PRACTICE_DAYS"), out int d) ? d : (int)(Horizon / DayClock.TicksPerDay);
            var seeds = OutpostWatch.GateSeeds;
            var lines = new string[seeds.Length];
            var counts = new int[4];
            var gate = new object();
            System.Threading.Tasks.Parallel.For(0, seeds.Length, new System.Threading.Tasks.ParallelOptions { MaxDegreeOfParallelism = 8 }, i =>
            {
                var run = Play(seeds[i], Days * DayClock.TicksPerDay, untilAll: false);
                var failures = run.Watch.DayFailures();
                lines[i] = $"seed {seeds[i]}: {run}; deaths {run.Watch.Deaths}; days {(failures.Count == 0 ? "hold" : "FAIL: " + string.Join("; ", failures))}";
                lock (gate)
                {
                    if (run.MeleeRoseAt >= 0) counts[0]++;
                    if (run.MiningRoseAt >= 0) counts[1]++;
                    if (run.GradeRoseAt >= 0) counts[2]++;
                    if (run.AllThree && failures.Count == 0 && run.Watch.Deaths == 0) counts[3]++;
                }
            });
            foreach (var line in lines) TestContext.Current.TestOutputHelper?.WriteLine(line);
            TestContext.Current.TestOutputHelper?.WriteLine($"in {Days} days, of {seeds.Length} seeds: melee rose on {counts[0]}, mining on {counts[1]}, a smith's grade on {counts[2]}; all three with the days held on {counts[3]}");
        }
    }
}
