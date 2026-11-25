using System.Collections.Generic;
using System.Linq;
using Simpiens.Cognition.Planning;
using Simpiens.Core.Session;
using Simpiens.Core.Session.Layouts;
using Simpiens.Hosting;
using Simpiens.Simulation;
using Simpiens.Simulation.Creatures;
using Simpiens.Simulation.Economy;
using Simpiens.Simulation.Guild;
using Simpiens.Simulation.Items;
using Simpiens.Simulation.Life;
using Simpiens.Simulation.Persistence;
using Simpiens.Testing.Environment;
using Xunit;

namespace Simpiens.Core.Tests
{
    /// <summary>
    /// Epic LIFE-1's gate scenario (plan 3, G.1): every life-core Definition of Done on one seed, live on the real outpost with
    /// nothing injected, the tavern player keeping its evening counter, the guild player sending expeditions, the workshop player
    /// arming residents and the hunt player posting hunts: moods rise with a good meal and a bunk's sleep (L1); wants arise and come
    /// true (L1, plan 2); the tavern serves in the evening on two days (L2); a resident paid a windfall rests the next day and works
    /// the day after (L3). Then saved, loaded byte for byte to the same world hash, and played on with its players for a game day.
    /// </summary>
    public sealed class LifeOneTogetherTests
    {
        /// <summary>
        /// Gate seed 1, picked by <see cref="MeasureLifeOneTogether"/> on plan 3's final build: over eight days with every player,
        /// moods rose with every meal and bunk's sleep on 17 of 23 seeds, wants came true on 22, the tavern served in the evening on
        /// two days or more on 22, and a day off came after a windfall on 9; every beat with the days held on 6 (1, 7, 8, 12, 15 and
        /// 17). On this one, the first, the day off comes on day 4.
        /// </summary>
        private static readonly ulong Seed = ScenarioSeed.Or(1);

        private const int Days = 8;

        [Fact]
        public void LifeOneTogether()
        {
            using var sim = LocalSimulation.Create(Seed);
            sim.Respawn(OutpostSession.SessionId);
            Players(sim);
            var run = Play(sim, Days, untilAll: true);
            var watch = run.Watch;
            TestContext.Current.TestOutputHelper?.WriteLine($"{run}\n{watch.Report()}");
            Assert.True(run.AteAndSlept >= 6 && run.MoodFell == 0 && run.MoodRose > 0, $"Moods rose with a good meal and a bunk's sleep ({run}).");
            Assert.True(run.WantsArisen > 0 && run.WantsFulfilled > 0, $"Wants arose and came true ({run}).");
            Assert.True(run.ServedDays.Count >= 2 && run.EveningTakings * 2 >= run.Takings && run.Takings > 0, $"The tavern served in the evening on two days ({run}).");
            Assert.True(run.DayOffDone, $"A resident paid a windfall rested the next day and worked the day after ({run}).");
            Assert.Equal(0, watch.ConservationViolations);
            Assert.Equal(0, watch.IntentFaults);
            var failures = watch.DayFailures();
            Assert.True(failures.Count == 0, "The days failed:\n  " + string.Join("\n  ", failures));

            // Saved with every beat on record: loaded into a fresh session byte for byte and to the same world hash, then played on
            long tick = sim.Clock.CurrentTick;
            byte[] saved = SaveSerializer.Serialize(sim.Host.Capture());
            var hash = WorldStateHash.Capture(tick, sim.Host, sim.Simulation, sim.Sectors, includeWork: false);
            using var loaded = LocalSimulation.Create(Seed);
            loaded.Respawn(OutpostSession.SessionId);
            Assert.True(loaded.Host.TryRestore(SaveSerializer.Deserialize<SessionSave>(saved), out string refusal), $"The save was refused: {refusal}");
            Assert.Equal(saved, SaveSerializer.Serialize(loaded.Host.Capture()));
            Assert.Null(hash.DescribeDifference(WorldStateHash.Capture(tick, loaded.Host, loaded.Simulation, loaded.Sectors, includeWork: false)));
            Players(loaded);
            var after = new OutpostWatch(loaded);
            after.Run(DayClock.TicksPerDay);
            after.Finish();
            Assert.Equal(0, after.ConservationViolations);
            Assert.Equal(0, after.IntentFaults);
        }

        /// <summary>The gate seeds for <see cref="Days"/> days with every player: which beats come on which seed.</summary>
        [Fact(Explicit = true)]
        public void MeasureLifeOneTogether()
        {
            var seeds = OutpostWatch.GateSeeds;
            var lines = new string[seeds.Length];
            var all = new bool[seeds.Length];
            System.Threading.Tasks.Parallel.For(0, seeds.Length, new System.Threading.Tasks.ParallelOptions { MaxDegreeOfParallelism = 8 }, i =>
            {
                using var sim = LocalSimulation.Create(seeds[i]);
                sim.Respawn(OutpostSession.SessionId);
                Players(sim);
                var run = Play(sim, Days, untilAll: false);
                var failures = run.Watch.DayFailures();
                all[i] = run.AllBeats && failures.Count == 0;
                lines[i] = $"seed {seeds[i]}: {run}; days {(failures.Count == 0 ? "hold" : "FAIL: " + string.Join("; ", failures))}";
            });
            foreach (var line in lines) TestContext.Current.TestOutputHelper?.WriteLine(line);
            TestContext.Current.TestOutputHelper?.WriteLine($"every beat with the days held on {all.Count(b => b)} of {seeds.Length}");
        }

        private static void Players(LocalSimulation sim)
        {
            new TavernPlayer(sim, reserve: EconomySoak.TavernReserve);
            new WorkshopPlayer(sim, StationKind.Forge, ItemType.IronBlade);
            new HuntPlayer(sim, CreatureKind.None);
            new GuildPlayer(sim, targetSize: 4).SendExpeditions(Floor1Layout.DrownedGallerySectorId, Floor1Layout.GallerySeamYield, RetreatRule.Steady);
        }

        private sealed class Run
        {
            public OutpostWatch Watch;
            public int AteAndSlept, MoodRose, MoodFell, WantsArisen, WantsFulfilled;
            public uint Takings, EveningTakings;
            public readonly HashSet<int> ServedDays = new HashSet<int>();
            public bool DayOffDone;
            public string DayOff = "none";

            public bool AllBeats => AteAndSlept >= 6 && MoodFell == 0 && MoodRose > 0 && WantsArisen > 0 && WantsFulfilled > 0
                && ServedDays.Count >= 2 && Takings > 0 && EveningTakings * 2 >= Takings && DayOffDone;

            public override string ToString() =>
                $"ate and slept {AteAndSlept} (moods rose {MoodRose}, fell {MoodFell}); wants arisen {WantsArisen}, come true {WantsFulfilled}; " +
                $"tavern {Takings}g ({EveningTakings}g in the evening) on days [{string.Join(",", ServedDays.OrderBy(d => d))}]; day off {DayOff}";
        }

        private static Run Play(LocalSimulation sim, int days, bool untilAll)
        {
            var run = new Run { Watch = new OutpostWatch(sim) };
            sim.Simulation.Served += (crawler, item, price) =>
            {
                run.ServedDays.Add((int)DayClock.Day(sim.Clock.CurrentTick));
                run.Takings += price;
                int hour = DayClock.HourOfDay(sim.Clock.CurrentTick);
                if (hour >= 18 || hour < 2) run.EveningTakings += price;
            };
            var lastMood = new Dictionary<EntityId, int>();
            var lastLife = new Dictionary<EntityId, (CrawlerLife Life, long At)>();
            var ate = new HashSet<EntityId>();
            var slept = new HashSet<EntityId>();
            var wantsHeld = new Dictionary<EntityId, ushort[]>();
            var drawnSeen = new Dictionary<EntityId, ushort>();
            var offs = new List<(EntityId Crawler, long Day, long Venue, long NextWork, bool NextRest, bool NextDrawn)>();
            var fulfilled = WantTable.All.Select(w => w.Fulfilled).ToHashSet();
            long horizon = days * (long)DayClock.TicksPerDay;
            while (sim.Clock.CurrentTick < horizon && !(untilAll && run.AllBeats && ate.Count > 0))
            {
                run.Watch.Run(1);
                long now = sim.Clock.CurrentTick;
                foreach (var crawler in sim.Host.CrawlerStates)
                {
                    if (crawler.IsDrifter) continue;
                    // L1: a good meal and a bunk's sleep lift the mood
                    int mood = MoodRules.Of(crawler.Needs, crawler.Health, crawler.IsDowned, crawler.Life, now);
                    if (lastLife.TryGetValue(crawler.AgentId, out var was) && lastMood.TryGetValue(crawler.AgentId, out int before))
                        foreach (var thought in new[] { Thought.AGoodMeal, Thought.SleptInABunk })
                        {
                            if (!ThoughtRules.Holds(crawler.Life, thought, now) || ThoughtRules.Holds(was.Life, thought, was.At)) continue;
                            if (thought == Thought.AGoodMeal) ate.Add(crawler.AgentId); else slept.Add(crawler.AgentId);
                            if (mood > before) run.MoodRose++; else run.MoodFell++;
                        }
                    lastMood[crawler.AgentId] = mood;
                    lastLife[crawler.AgentId] = (crawler.Life, now);
                    // Wants arisen
                    if (!wantsHeld.TryGetValue(crawler.AgentId, out var held)) wantsHeld[crawler.AgentId] = held = new ushort[CrawlerLife.WantCapacity];
                    for (int w = 0; w < CrawlerLife.WantCapacity; w++)
                    {
                        ushort want = crawler.Life.WantAt(w).Want;
                        if (want != 0 && held[w] != want) run.WantsArisen++;
                        held[w] = want;
                    }
                    // L3: a day off after a windfall, its hearth or venue hour, and the day after's work
                    var life = crawler.Life;
                    var family = crawler.HasActivePlan && crawler.ActiveGoal != null ? crawler.ActiveGoal.Family : ActivityFamily.Unweighted;
                    drawnSeen.TryGetValue(crawler.AgentId, out ushort seenDay);
                    if (life.IntentionDay != 0 && life.IntentionDay != seenDay)
                    {
                        drawnSeen[crawler.AgentId] = life.IntentionDay;
                        long day = life.IntentionDay - 1;
                        for (int o = 0; o < offs.Count; o++)
                            if (offs[o].Crawler == crawler.AgentId && offs[o].Day == day - 1) offs[o] = (offs[o].Crawler, offs[o].Day, offs[o].Venue, offs[o].NextWork, life.Intention == DayIntention.Rest, true);
                        if (life.Intention == DayIntention.Rest && IntentionRules.WindfallWithinADay(life.LastWindfall, (uint)now)) offs.Add((crawler.AgentId, day, 0, 0, false, false));
                    }
                    long today = crawler.RoutineAt((uint)now).Day;
                    for (int o = 0; o < offs.Count; o++)
                    {
                        var off = offs[o];
                        if (off.Crawler != crawler.AgentId) continue;
                        if (today == off.Day && (family == ActivityFamily.Leisure || family == ActivityFamily.Social)) off.Venue++;
                        if (today == off.Day + 1 && off.NextDrawn && !off.NextRest && family == ActivityFamily.Work) off.NextWork++;
                        offs[o] = off;
                        if (!run.DayOffDone && off.Venue >= DayClock.TicksPerHour && off.NextWork >= DayClock.TicksPerHour)
                        {
                            run.DayOffDone = true;
                            run.DayOff = $"a day off on day {off.Day + 1} after a windfall, {off.Venue / (double)DayClock.TicksPerHour:F1} h at the hearth or a venue, work the day after";
                        }
                    }
                }
                run.AteAndSlept = ate.Count(slept.Contains);
                run.WantsFulfilled = 0;
                for (int t = 0; t < run.Watch.ThoughtsThought.Length; t++) if (fulfilled.Contains((Thought)t)) run.WantsFulfilled += run.Watch.ThoughtsThought[t];
            }
            run.Watch.Finish();
            return run;
        }
    }
}
