using System.Collections.Generic;
using System.Linq;
using Simpiens.Core.Session;
using Simpiens.Hosting;
using Simpiens.Narrative;
using Simpiens.Simulation;
using Simpiens.Simulation.Economy;
using Simpiens.Simulation.Items;
using Simpiens.Simulation.Skills;
using Xunit;

namespace Simpiens.Core.Tests
{
    /// <summary>
    /// S7's Definition of Done (Epic 11, S7.8): the beetle that remembers, on the live outpost with nothing injected. The forge's
    /// player makes blades; the guild's player recruits four and orders clears, armed from the stall, under the Steady rule, as
    /// <c>TheOutpostRemembers</c> does (S7.0: the clears are where crawlers fall, and the bold pairs aren't needed).
    /// <list type="bullet">
    /// <item><b>A name:</b> a creature downs a crawler and takes a name from it, as the chronicle tells it.</item>
    /// <item><b>Hunted:</b> the guild's clear focuses it, the named first (D34).</item>
    /// <item><b>Avenged:</b> it is slain, and its slayer carries "{its victim}'s Avenger" (D35).</item>
    /// </list>
    /// The scripted players act only through commands.
    /// </summary>
    public class TheBeetleThatRemembersTests
    {
        private sealed class Run
        {
            public long NamedAt = -1, FocusedAt = -1, SlainAt = -1;
            public EntityId Creature, Victim, Slayer;
            public string Named = "-", Slain = "-", SlayerEpithet = "-";
            public int Names;
            public bool Avenger;
            public OutpostWatch Watch;

            public bool All => SlainAt >= 0 && FocusedAt >= 0;

            public override string ToString() =>
                $"named {(NamedAt >= 0 ? $"t{NamedAt}: {Named}" : "never")} ({Names} in all); focused {(FocusedAt >= 0 ? "t" + FocusedAt : "never")}; " +
                $"slain {(SlainAt >= 0 ? $"t{SlainAt}: {Slain}" : "never")}; its slayer {(Avenger ? "the avenger, " + SlayerEpithet : SlayerEpithet)}";
        }

        private static Run Play(ulong seed, long horizon, bool untilAll)
        {
            using var sim = LocalSimulation.Create(seed);
            sim.Respawn(OutpostSession.SessionId);
            new WorkshopPlayer(sim, StationKind.Forge, ItemType.IronBlade, batch: 4);
            var guild = new GuildPlayer(sim, targetSize: 4);
            guild.SendClear();
            var watch = new OutpostWatch(sim);
            var run = new Run { Watch = watch };
            var formatter = new TemplateNarrativeFormatter(sim.Identity, sim.Sectors);
            var told = new List<ChronicleEvent>(64);
            var named = new Dictionary<EntityId, (long At, EntityId Victim, string Line)>(); // every creature named, its downing
            var focused = new Dictionary<EntityId, long>(); // and when a party first focused it
            long seen = -1;
            while (sim.Clock.CurrentTick < horizon && !(untilAll && run.All))
            {
                watch.Run(1);
                long now = sim.Clock.CurrentTick;
                told.Clear();
                sim.Chronicle.GetAllRecentEvents(told, 64);
                long newest = seen;
                foreach (var e in told)
                {
                    if (e.SimulationTick <= seen) continue; // each tick's events once
                    if (e.SimulationTick > newest) newest = e.SimulationTick;
                    if (e.EventType == ChronicleEventType.CreatureNamed && !named.ContainsKey(e.ActorAId))
                    {
                        run.Names++;
                        named[e.ActorAId] = (e.SimulationTick, e.ActorBId, formatter.FormatEvent(e));
                    }
                    // The first named creature slain after a party focused it: the DoD's beetle
                    if (e.EventType == ChronicleEventType.NamedCreatureSlain && run.SlainAt < 0 && named.TryGetValue(e.ActorBId, out var name) && focused.TryGetValue(e.ActorBId, out long focus))
                    {
                        (run.Creature, run.Victim, run.NamedAt, run.Named, run.FocusedAt) = (e.ActorBId, name.Victim, name.At, name.Line, focus);
                        run.SlainAt = e.SimulationTick;
                        run.Slayer = e.ActorAId;
                        run.Slain = formatter.FormatEvent(e);
                    }
                }
                seen = newest;
                foreach (var party in sim.Simulation.Guild.Parties)
                    if (party.IsOut && party.Focus != default && named.ContainsKey(party.Focus) && !focused.ContainsKey(party.Focus)) focused[party.Focus] = now;
            }
            if (run.SlainAt < 0 && named.Count > 0) // none avenged: the first named, for the report
            {
                var first = named.OrderBy(kv => kv.Value.At).First();
                (run.Creature, run.Victim, run.NamedAt, run.Named) = (first.Key, first.Value.Victim, first.Value.At, first.Value.Line);
                if (focused.TryGetValue(first.Key, out long f)) run.FocusedAt = f;
            }
            var slayer = run.Slayer != default ? sim.Host.CrawlerStates.FirstOrDefault(c => c.AgentId == run.Slayer) : null;
            if (slayer != null)
            {
                var traits = slayer.Traits;
                run.SlayerEpithet = EpithetRules.Words(traits, id => sim.Identity.GetFriendlyName(id));
                run.Avenger = traits.Epithet == Epithet.Avenger && traits.EpithetSubject == run.Victim;
            }
            watch.Finish();
            return run;
        }

        /// <summary>The horizon: ten game days.</summary>
        private const long Horizon = 10 * DayClock.TicksPerDay;

        /// <summary>
        /// Gate seed 3 (S7.8, <see cref="MeasureTheBeetleThatRemembersOnTheGateSeeds"/> on S7's build): over ten days a creature is
        /// named, focused by the guild's clear and avenged on 10 of the 23 gate seeds, the days holding on all 10. On this one, the
        /// seed of <c>TheOutpostRemembers</c>, the beetle that downs Kwame is Kwame's Bane at t19052, the party's focus at once, and
        /// Lane slays it at t19218: Kwame's Avenger.
        /// </summary>
        private const ulong Seed = 3;

        [Fact]
        public void TheBeetleThatRemembers()
        {
            var run = Play(Seed, Horizon, untilAll: true);
            var watch = run.Watch;
            TestContext.Current.TestOutputHelper?.WriteLine($"{run}\n{watch.CreatureSummary()}\n{watch.Report()}");
            Assert.True(run.NamedAt >= 0, "A creature downed a crawler and took a name from it, as the chronicle told it.");
            Assert.Contains("has a name now", run.Named);
            Assert.True(run.FocusedAt >= 0, "The guild's clear focused it.");
            Assert.True(run.SlainAt >= 0 && run.SlainAt >= run.NamedAt, "It was slain.");
            Assert.True(run.Avenger, $"Its slayer carries the avenger's epithet for its victim: \"{run.SlayerEpithet}\".");
            Assert.EndsWith("'s Avenger", run.SlayerEpithet);
            Assert.Equal(0, watch.ConservationViolations);
            Assert.Equal(0, watch.IntentFaults);
            var failures = watch.DayFailures();
            Assert.True(failures.Count == 0, "The days failed:\n  " + string.Join("\n  ", failures));
        }

        /// <summary>The measurement behind the seed: every gate seed's ten days, eight at a time (explicit, a few minutes).</summary>
        [Fact(Explicit = true)]
        public void MeasureTheBeetleThatRemembersOnTheGateSeeds()
        {
            var seeds = System.Environment.GetEnvironmentVariable("SIMPIENS_SEEDS") is string list && list.Length > 0
                ? list.Split(',').Select(ulong.Parse).ToArray() : OutpostWatch.GateSeeds;
            var lines = new string[seeds.Length];
            int named = 0, avenged = 0, all = 0;
            System.Threading.Tasks.Parallel.For(0, seeds.Length, new System.Threading.Tasks.ParallelOptions { MaxDegreeOfParallelism = 8 }, i =>
            {
                var run = Play(seeds[i], Horizon, untilAll: false);
                var failures = run.Watch.DayFailures();
                lines[i] = $"seed {seeds[i]}: {run}; days {(failures.Count == 0 ? "hold" : "FAIL: " + string.Join("; ", failures))}";
                if (run.NamedAt >= 0) System.Threading.Interlocked.Increment(ref named);
                if (run.Avenger) System.Threading.Interlocked.Increment(ref avenged);
                if (run.All && run.Avenger && failures.Count == 0) System.Threading.Interlocked.Increment(ref all);
            });
            foreach (var line in lines) TestContext.Current.TestOutputHelper?.WriteLine(line);
            TestContext.Current.TestOutputHelper?.WriteLine($"of {seeds.Length} seeds in {Horizon / DayClock.TicksPerDay} days: a creature named on {named}, avenged on {avenged}, all with the days holding on {all}");
        }
    }
}
