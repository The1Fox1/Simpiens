using System.Collections.Generic;
using System.Linq;
using Simpiens.Core.Session;
using Simpiens.Hosting;
using Simpiens.Narrative;
using Simpiens.Simulation;
using Simpiens.Simulation.Economy;
using Simpiens.Simulation.Items;
using Simpiens.Simulation.Persistence;
using Simpiens.Simulation.Skills;
using Simpiens.Testing.Environment;
using Xunit;

namespace Simpiens.Core.Tests
{
    /// <summary>
    /// S9's Definition of Done (Epic 11, S9.1, D46): every layer of the epic played together on one seed, and saved mid-scene.
    /// The forge's player makes blades and the still's draughts, the guild's player recruits four and orders clears armed from
    /// the stall (where crawlers fall, F13), all through commands. In one run:
    /// <list type="bullet">
    /// <item><b>Craft:</b> a crawler with a passion for a skill reaches a band in it.</item>
    /// <item><b>Technique and calling:</b> a technique is picked at a band reached, and a calling is earned.</item>
    /// <item><b>Legend:</b> an epithet earned and a scar borne; a creature named for the crawler it downed.</item>
    /// <item><b>Masterworks:</b> a masterpiece made at the forge.</item>
    /// <item><b>Saved mid-scene:</b> with a masterpiece made, the save loads into a fresh session byte for byte and to the same
    /// world hash, and plays on.</item>
    /// </list>
    /// </summary>
    public class EpicElevenTogetherTests
    {
        private sealed class Run
        {
            public long PassionBand = -1, Technique = -1, Calling = -1, Epithet = -1, Scar = -1, Named = -1, Masterpiece = -1;
            public OutpostWatch Watch;
            public bool All => PassionBand >= 0 && Technique >= 0 && Calling >= 0 && Epithet >= 0 && Scar >= 0 && Named >= 0 && Masterpiece >= 0;
            public int Layers => new[] { PassionBand, Technique, Calling, Epithet, Scar, Named, Masterpiece }.Count(t => t >= 0);
            private static string At(long t) => t >= 0 ? $"day {1 + t / DayClock.TicksPerDay}" : "never";
            public override string ToString() =>
                $"a passion's band {At(PassionBand)}, a technique {At(Technique)}, a calling {At(Calling)}, an epithet {At(Epithet)}, a scar {At(Scar)}, a named creature {At(Named)}, a masterpiece {At(Masterpiece)}";
        }

        private static void Players(LocalSimulation sim)
        {
            new WorkshopPlayer(sim, StationKind.Forge, ItemType.IronBlade, batch: 4);
            new WorkshopPlayer(sim, StationKind.Still, ItemType.HealingDraught, batch: 2);
            var guild = new GuildPlayer(sim, targetSize: 4);
            guild.SendClear();
        }

        // Plays until every layer has shown (or the horizon), each layer's first tick from the chronicle
        private static Run Play(LocalSimulation sim, long horizon, bool untilAll)
        {
            var watch = new OutpostWatch(sim);
            var run = new Run { Watch = watch };
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
                    long t = e.SimulationTick;
                    switch (e.EventType)
                    {
                        case ChronicleEventType.SkillBandReached when run.PassionBand < 0:
                            var crawler = sim.Host.CrawlerStates.FirstOrDefault(c => c.AgentId == e.ActorAId);
                            if (crawler != null && crawler.Traits.PassionFor((Skill)((int)e.ExtraData / 256)) != Passion.None) run.PassionBand = t;
                            break;
                        case ChronicleEventType.TechniqueChosen when run.Technique < 0: run.Technique = t; break;
                        case ChronicleEventType.CallingEarned when run.Calling < 0: run.Calling = t; break;
                        case ChronicleEventType.EpithetEarned when run.Epithet < 0: run.Epithet = t; break;
                        case ChronicleEventType.ScarTaken when run.Scar < 0: run.Scar = t; break;
                        case ChronicleEventType.CreatureNamed when run.Named < 0: run.Named = t; break;
                        case ChronicleEventType.MasterpieceMade when run.Masterpiece < 0: run.Masterpiece = t; break;
                    }
                }
                seen = newest;
            }
            watch.Finish();
            return run;
        }

        /// <summary>The horizon: fourteen game days.</summary>
        private const long Horizon = 14 * DayClock.TicksPerDay;

        /// <summary>
        /// Gate seed 5 (S9.1, <see cref="MeasureEpicElevenTogetherOnTheGateSeeds"/>): every layer by day 7 (a passion's band, a
        /// technique, an epithet, a scar and a named creature on day 3; a calling and a masterpiece on day 7), the days holding.
        /// The only gate seed with all seven in two weeks: a scar and a name need a downing on a clear (9 and 8 seeds), a
        /// masterpiece a Journeyman at the forge (4 seeds with these players); see F19 for the trust check most fail.
        /// </summary>
        private const ulong Seed = 5;

        [Fact]
        public void EpicElevenPlaysTogether()
        {
            using var sim = LocalSimulation.Create(Seed);
            sim.Respawn(OutpostSession.SessionId);
            Players(sim);
            var run = Play(sim, Horizon, untilAll: true);
            var watch = run.Watch;
            TestContext.Current.TestOutputHelper?.WriteLine($"{run}\n{watch.CreatureSummary()}\n{watch.Report()}");
            Assert.True(run.PassionBand >= 0, "A crawler with a passion for a skill reached a band in it.");
            Assert.True(run.Technique >= 0, "A technique was picked at a band reached.");
            Assert.True(run.Calling >= 0, "A calling was earned.");
            Assert.True(run.Epithet >= 0, "An epithet was earned.");
            Assert.True(run.Scar >= 0, "A downed crawler bears a scar.");
            Assert.True(run.Named >= 0, "A creature took a name from the crawler it downed.");
            Assert.True(run.Masterpiece >= 0, "A masterpiece was made at the forge.");
            Assert.Equal(0, watch.ConservationViolations);
            Assert.Equal(0, watch.IntentFaults);
            var failures = watch.DayFailures();
            Assert.True(failures.Count == 0, "The days failed:\n  " + string.Join("\n  ", failures));

            // Saved mid-scene, the masterpiece made and every layer on record: loaded into a fresh session byte for byte and
            // to the same world hash, then played on with its players for a game day
            long tick = sim.Clock.CurrentTick;
            byte[] saved = SaveSerializer.Serialize(sim.Host.Capture());
            var hash = WorldStateHash.Capture(tick, sim.Host, sim.Simulation, sim.Sectors, includeWork: false);
            using var loaded = LocalSimulation.Create(Seed);
            loaded.Respawn(OutpostSession.SessionId);
            Assert.True(loaded.Host.TryRestore(SaveSerializer.Deserialize<SessionSave>(saved), out string refusal), $"The save was refused: {refusal}");
            Assert.Equal(saved, SaveSerializer.Serialize(loaded.Host.Capture()));
            Assert.Null(hash.DescribeDifference(WorldStateHash.Capture(tick, loaded.Host, loaded.Simulation, loaded.Sectors, includeWork: false)));
            Assert.True(loaded.Simulation.Workshops.Masterpieces.Count >= 1, "The masterpiece came back.");
            Players(loaded);
            var after = new OutpostWatch(loaded);
            after.Run(DayClock.TicksPerDay);
            after.Finish();
            Assert.Equal(0, after.ConservationViolations);
            Assert.Equal(0, after.IntentFaults);
        }

        /// <summary>The measurement behind the seed: every gate seed's two weeks, eight at a time (explicit, a few minutes).</summary>
        [Fact(Explicit = true)]
        public void MeasureEpicElevenTogetherOnTheGateSeeds()
        {
            var seeds = OutpostWatch.GateSeeds;
            var lines = new string[seeds.Length];
            int all = 0;
            System.Threading.Tasks.Parallel.For(0, seeds.Length, new System.Threading.Tasks.ParallelOptions { MaxDegreeOfParallelism = 8 }, i =>
            {
                using var sim = LocalSimulation.Create(seeds[i]);
                sim.Respawn(OutpostSession.SessionId);
                Players(sim);
                var run = Play(sim, Horizon, untilAll: false);
                var failures = run.Watch.DayFailures();
                lines[i] = $"seed {seeds[i]}: {run.Layers} of 7: {run}; days {(failures.Count == 0 ? "hold" : "FAIL: " + string.Join("; ", failures))}";
                if (run.All && failures.Count == 0) System.Threading.Interlocked.Increment(ref all);
            });
            foreach (var line in lines) TestContext.Current.TestOutputHelper?.WriteLine(line);
            TestContext.Current.TestOutputHelper?.WriteLine($"of {seeds.Length} seeds in {Horizon / DayClock.TicksPerDay} days: every layer with the days holding on {all}");
        }
    }
}
