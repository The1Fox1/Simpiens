using System.Collections.Generic;
using System.Linq;
using System.Text;
using Simpiens.Cognition;
using Simpiens.Core.Session;
using Simpiens.Hosting;
using Simpiens.Simulation;
using Simpiens.Simulation.Economy;
using Simpiens.Simulation.Items;
using Simpiens.Simulation.Life;
using Simpiens.Simulation.Persistence;
using Simpiens.Testing.Environment;
using Simpiens.Simulation.Workshops;
using Xunit;

namespace Simpiens.Core.Tests
{
    /// <summary>
    /// Mood follows the day (Epic LIFE-1, L1): what crawlers think and how their moods read on the real outpost. The
    /// measurement (L1.3) sets the thoughts' values and the bands; the DoD (L1.8) proves moods rise with a meal and a bunk and
    /// fall for a friend's death, with nothing injected.
    /// </summary>
    public sealed class MoodFollowsTheDayTests
    {
        /// <summary>The gate seed of the meal and the bunk (any gate seed's day holds; 424242 is golden's first).</summary>
        private const ulong DaySeed = 424242;

        /// <summary><c>DeathHasItsCost</c>'s seed: its two, unarmed against the beetles under the Bold rule, bring a death.</summary>
        private static readonly ulong DeathSeed = ScenarioSeed.Or(9); // with DeathHasItsCost's, since plan 2's L2.6 (6 since T.2, 9 since L2.7-L2.8)

        /// <summary>
        /// The DoD (Epic LIFE-1, L1.8), the day: on the real outpost with nothing injected, residents think a good meal and a night
        /// in a bunk, and each lifts its mood; the day's thoughts are in each one's life record (the day log's source); and a
        /// save mid-day loads with every live thought, the moods the same.
        /// </summary>
        [Fact]
        public void MoodsRiseWithAMealAndABunk()
        {
            using var sim = LocalSimulation.Create(DaySeed);
            sim.Respawn(OutpostSession.SessionId);
            var watch = new OutpostWatch(sim);
            var lastMood = new Dictionary<EntityId, int>();
            var lastLife = new Dictionary<EntityId, (CrawlerLife Life, long At)>();
            var ate = new HashSet<EntityId>();
            var slept = new HashSet<EntityId>();
            int risen = 0, fallen = 0;
            long saveAt = DayClock.TicksPerDay + DayClock.TicksPerDay / 2; // day 2's afternoon
            LocalSimulation loaded = null;
            while (sim.Clock.CurrentTick < 2 * DayClock.TicksPerDay)
            {
                watch.Run(1);
                long now = sim.Clock.CurrentTick;
                foreach (var crawler in sim.Host.CrawlerStates)
                {
                    if (crawler.IsDrifter) continue;
                    int mood = MoodRules.Of(crawler.Needs, crawler.Health, crawler.IsDowned, crawler.Life, now);
                    // A thought's onset: held now, not at the last look (a bunk's sleep is thought again as each stretch of sleep ends)
                    if (lastLife.TryGetValue(crawler.AgentId, out var was) && lastMood.TryGetValue(crawler.AgentId, out int before))
                        foreach (var thought in new[] { Thought.AGoodMeal, Thought.SleptInABunk })
                        {
                            if (!ThoughtRules.Holds(crawler.Life, thought, now) || ThoughtRules.Holds(was.Life, thought, was.At)) continue;
                            if (thought == Thought.AGoodMeal) ate.Add(crawler.AgentId); else slept.Add(crawler.AgentId);
                            if (mood > before) risen++; else fallen++;
                        }
                    lastMood[crawler.AgentId] = mood;
                    lastLife[crawler.AgentId] = (crawler.Life, now);
                }
                if (now == saveAt) loaded = SaveAndLoad(sim, DaySeed);
            }
            watch.Finish();
            var both = ate.Where(slept.Contains).ToList();
            TestContext.Current.TestOutputHelper?.WriteLine($"ate {ate.Count}, slept {slept.Count}, both {both.Count}; moods rose {risen}, did not {fallen}\n{watch.Report()}");
            Assert.True(both.Count >= 6, $"At least six residents thought a good meal and a night in a bunk ({both.Count}).");
            Assert.True(fallen == 0 && risen > 0, $"Every good meal and every bunk's sleep lifted its thinker's mood ({risen} rose, {fallen} did not).");
            Assert.Equal(0, watch.ConservationViolations);
            Assert.Equal(0, watch.IntentFaults);
            var failures = watch.DayFailures();
            Assert.True(failures.Count == 0, "The days failed:\n  " + string.Join("\n  ", failures));

            // The save mid-day (SaveAndLoad): every live thought came back, and every mood with it
            Assert.NotNull(loaded);
            loaded.Dispose();
        }

        /// <summary>
        /// The DoD (Epic LIFE-1, L1.8), a death: under <c>DeathHasItsCost</c>'s players (input through commands), a crawler dies;
        /// each friend who grieves thinks "{0} died" naming the dead, its mood falls within the hour, and grief takes moods to Low.
        /// </summary>
        [Fact]
        public void MoodFallsForAFriendsDeath()
        {
            using var sim = LocalSimulation.Create(DeathSeed);
            sim.Respawn(OutpostSession.SessionId);
            Seat(sim, Players.Death);
            var watch = new OutpostWatch(sim);
            var lastMood = new Dictionary<EntityId, int>();
            var lastLife = new Dictionary<EntityId, (CrawlerLife Life, long At)>();
            var grieving = new Dictionary<EntityId, (long Since, int Before, EntityId Dead)>();
            var griefs = new List<(EntityId Who, int Before, int After, EntityId Dead)>();
            long until = 10L * DayClock.TicksPerDay;
            while (sim.Clock.CurrentTick < until)
            {
                watch.Run(1);
                long now = sim.Clock.CurrentTick;
                foreach (var crawler in sim.Host.CrawlerStates)
                {
                    int mood = MoodRules.Of(crawler.Needs, crawler.Health, crawler.IsDowned, crawler.Life, now);
                    // Grief's onset: held now, not at the last look (a death is told after the tick, so its thought carries the next tick)
                    if (!grieving.ContainsKey(crawler.AgentId) && lastMood.TryGetValue(crawler.AgentId, out int before) && lastLife.TryGetValue(crawler.AgentId, out var was)
                        && ThoughtRules.Holds(crawler.Life, Thought.FriendDied, now) && !ThoughtRules.Holds(was.Life, Thought.FriendDied, was.At))
                        for (int i = 0; i < CrawlerLife.ThoughtCapacity; i++)
                        {
                            var slot = crawler.Life.ThoughtAt(i);
                            if (slot.Thought == (ushort)Thought.FriendDied && ThoughtRules.IsLive(slot, now))
                                grieving[crawler.AgentId] = (now, before, crawler.Life.SubjectAt(slot.Subject));
                        }
                    if (grieving.TryGetValue(crawler.AgentId, out var g) && now - g.Since == DayClock.TicksPerHour)
                    {
                        griefs.Add((crawler.AgentId, g.Before, mood, g.Dead));
                        if (until > now + 1) until = now + 1; // the first griefs an hour on: enough
                    }
                    lastMood[crawler.AgentId] = mood;
                    lastLife[crawler.AgentId] = (crawler.Life, now);
                }
            }
            watch.Finish();
            TestContext.Current.TestOutputHelper?.WriteLine($"deaths {watch.Deaths}; griefs: {string.Join(", ", griefs.Select(g => $"{g.Before} to {g.After}"))}");
            Assert.True(watch.Deaths >= 1, "A crawler died.");
            Assert.True(griefs.Count >= 1, "A friend grieved.");
            var living = new HashSet<EntityId>(sim.Host.CrawlerStates.Select(c => c.AgentId));
            Assert.All(griefs, g => Assert.True(g.Dead != default && !living.Contains(g.Dead), "The thought names the dead."));
            Assert.All(griefs, g => Assert.True(g.After < g.Before, $"Its mood fell: {g.Before} to {g.After}."));
            Assert.Contains(griefs, g => MoodRules.BandOf(g.After) <= MoodBand.Low);
        }

        // The session saved now, loaded into a fresh one of the same seed: the save again byte for byte, every crawler's life
        // record and mood the same, the world hash the same
        private static LocalSimulation SaveAndLoad(LocalSimulation played, ulong seed)
        {
            long tick = played.Clock.CurrentTick;
            byte[] saved = SaveSerializer.Serialize(played.Host.Capture());
            var hash = WorldStateHash.Capture(tick, played.Host, played.Simulation, played.Sectors, includeWork: false);
            var loaded = LocalSimulation.Create(seed);
            loaded.Respawn(OutpostSession.SessionId);
            Assert.True(loaded.Host.TryRestore(SaveSerializer.Deserialize<SessionSave>(saved), out string refusal), $"The save was refused: {refusal}");
            Assert.Equal(saved, SaveSerializer.Serialize(loaded.Host.Capture()));
            Assert.Null(hash.DescribeDifference(WorldStateHash.Capture(tick, loaded.Host, loaded.Simulation, loaded.Sectors, includeWork: false)));
            int thoughts = 0;
            foreach (var crawler in played.Host.CrawlerStates)
            {
                var back = loaded.Host.CrawlerStates.First(c => c.AgentId == crawler.AgentId);
                Assert.Equal(crawler.Life, back.Life);
                Assert.Equal(MoodRules.Of(crawler.Needs, crawler.Health, crawler.IsDowned, crawler.Life, tick), MoodRules.Of(back.Needs, back.Health, back.IsDowned, back.Life, tick));
                for (int i = 0; i < CrawlerLife.ThoughtCapacity; i++) if (ThoughtRules.IsLive(back.Life.ThoughtAt(i), tick)) thoughts++;
            }
            Assert.True(thoughts > 0, "Live thoughts were saved and loaded.");
            return loaded;
        }

        /// <summary>
        /// Moods on the gate seeds for <c>SIMPIENS_LIFE_DAYS</c> game days (3 by default), with no player and with the guild's
        /// clears (as <c>TheOutpostRemembers</c>, which bring downings and deaths): time in each band, the mean and the spread,
        /// thoughts thought a resident-day, and mood before and an hour after a friend's death.
        /// </summary>
        [Fact(Explicit = true)]
        public void MeasureMoodOnTheGateSeeds()
        {
            int days = int.TryParse(System.Environment.GetEnvironmentVariable("SIMPIENS_LIFE_DAYS"), out int d) ? d : 3;
            foreach (var players in new[] { Players.None, Players.Clears, Players.Death })
            {
                var seeds = OutpostWatch.GateSeeds;
                int runDays = players == Players.Death ? 10 : days; // DeathHasItsCost's horizon
                var tallies = new MoodTally[seeds.Length];
                System.Threading.Tasks.Parallel.For(0, seeds.Length, new System.Threading.Tasks.ParallelOptions { MaxDegreeOfParallelism = 8 },
                    i => tallies[i] = Measure(seeds[i], runDays, players));
                var total = new MoodTally();
                foreach (var t in tallies) total.Add(t);
                string name = players switch
                {
                    Players.Clears => "the guild's clears (forge and guild players, four armed, Steady)",
                    Players.Death => "DeathHasItsCost's players (two unarmed against the beetles, Bold)",
                    _ => "no player",
                };
                TestContext.Current.TestOutputHelper?.WriteLine($"=== {name}, {seeds.Length} gate seeds x {runDays} days ===");
                TestContext.Current.TestOutputHelper?.WriteLine(total.Report());
            }
        }

        internal enum Players { None, Clears, Death }

        /// <summary>The scripted players of a run: none; the clears of <c>TheOutpostRemembers</c>; or <c>DeathHasItsCost</c>'s, which bring a death.</summary>
        internal static void Seat(LocalSimulation sim, Players players)
        {
            if (players == Players.None) return;
            new WorkshopPlayer(sim, StationKind.Forge, ItemType.IronBlade, batch: 4);
            if (players == Players.Clears) new GuildPlayer(sim, targetSize: 4).SendClear();
            else new GuildPlayer(sim, targetSize: 2).SendClear(Simpiens.Simulation.Creatures.CreatureKind.StoneBeetle, Simpiens.Simulation.Guild.RetreatRule.Bold, arm: false);
        }

        internal static MoodTally Measure(ulong seed, int days, Players players)
        {
            using var sim = LocalSimulation.Create(seed);
            sim.Respawn(OutpostSession.SessionId);
            Seat(sim, players);
            var watch = new OutpostWatch(sim);
            var tally = new MoodTally();
            var grieving = new Dictionary<EntityId, (long Since, int Before)>();
            var held = new HashSet<EntityId>();
            var refused = new HashSet<(uint Dispatch, EntityId Member)>();
            var lastMood = new Dictionary<EntityId, int>();
            long horizon = days * (long)DayClock.TicksPerDay;
            while (sim.Clock.CurrentTick < horizon)
            {
                watch.Run(1);
                // Orders refused on a trip, by the stage the member judged them at (plan 2's O.2, D25): at the muster (dispatch's order) or later
                foreach (var party in sim.Simulation.Guild.Parties)
                {
                    var record = party.Expedition;
                    if (record.Stage == Simpiens.Simulation.Guild.ExpeditionStage.None) continue;
                    for (int m = 0; m < party.Members.Count; m++)
                    {
                        var member = party.Members[m];
                        if (member.Status != Simpiens.Simulation.Guild.ExpeditionMemberStatus.StayedBehind && member.Status != Simpiens.Simulation.Guild.ExpeditionMemberStatus.Deserted) continue;
                        if (!refused.Add((record.DispatchTick, member.CrawlerId))) continue;
                        if (record.Stage == Simpiens.Simulation.Guild.ExpeditionStage.Muster) tally.RefusedAtMuster++; else tally.RefusedOnTheWay++;
                    }
                }
                long now = sim.Clock.CurrentTick;
                foreach (var crawler in sim.Host.CrawlerStates)
                {
                    if (crawler.IsDrifter) continue;
                    int mood = MoodRules.Of(crawler.Needs, crawler.Health, crawler.IsDowned, crawler.Life, now);
                    tally.ResidentTicks++;
                    tally.Band[(int)MoodRules.BandOf(mood)]++;
                    tally.MoodSum += mood;
                    if (mood < tally.Lowest) tally.Lowest = mood;
                    if (mood > tally.Highest) tally.Highest = mood;
                    tally.FrustrationSum += crawler.Frustration;
                    if (crawler.Frustration >= 60f) tally.FrustratedTicks++; // the face's line
                    if (crawler.DefiantTimer > 0f) tally.DefiantTicks++;      // an order defied, shown
                    // Under orders (out with the guild's party, or on shift), and pursuing something else (plan 2's O.1, D25)
                    if (crawler.Expedition.IsActive || crawler.Station.IsActive)
                    {
                        tally.OrderedTicks++;
                        var goal = crawler.HasActivePlan ? crawler.ActiveGoal : null;
                        if (goal != null && goal.Motive != Simpiens.Cognition.Planning.Motive.Duty && goal.Motive != Simpiens.Cognition.Planning.Motive.Aid
                            && goal.Motive != Simpiens.Cognition.Planning.Motive.Hunger && goal.Family != Simpiens.Cognition.Planning.ActivityFamily.Unweighted) tally.OrdersAsideTicks++;
                    }

                    // A friend's death: mood the tick before it was thought, and an hour after
                    bool grieves = ThoughtRules.Holds(crawler.Life, Thought.FriendDied, now);
                    if (grieves && !held.Contains(crawler.AgentId))
                        grieving[crawler.AgentId] = (now, lastMood.TryGetValue(crawler.AgentId, out int was) ? was : mood);
                    if (grieves) held.Add(crawler.AgentId); else held.Remove(crawler.AgentId);
                    if (grieving.TryGetValue(crawler.AgentId, out var g) && now - g.Since >= DayClock.TicksPerHour)
                    {
                        tally.Griefs.Add((g.Before, mood));
                        grieving.Remove(crawler.AgentId);
                    }
                    lastMood[crawler.AgentId] = mood;
                }
            }
            for (int t = 0; t < watch.ThoughtsThought.Length; t++) tally.Thought[t] += watch.ThoughtsThought[t];
            tally.Deaths += watch.Deaths;
            tally.Seeds = 1;
            return tally;
        }

        internal sealed class MoodTally
        {
            public int Seeds, Deaths;
            public long ResidentTicks, MoodSum, FrustratedTicks, DefiantTicks, OrderedTicks, OrdersAsideTicks, RefusedAtMuster, RefusedOnTheWay;
            public double FrustrationSum;
            public int Lowest = int.MaxValue, Highest = int.MinValue;
            public readonly long[] Band = new long[6];
            public readonly long[] Thought = new long[ThoughtTable.Capacity];
            public readonly List<(int Before, int After)> Griefs = new();

            public void Add(MoodTally o)
            {
                Seeds += o.Seeds; Deaths += o.Deaths; ResidentTicks += o.ResidentTicks; MoodSum += o.MoodSum;
                FrustrationSum += o.FrustrationSum; FrustratedTicks += o.FrustratedTicks; DefiantTicks += o.DefiantTicks; OrderedTicks += o.OrderedTicks; OrdersAsideTicks += o.OrdersAsideTicks; RefusedAtMuster += o.RefusedAtMuster; RefusedOnTheWay += o.RefusedOnTheWay;
                if (o.Lowest < Lowest) Lowest = o.Lowest;
                if (o.Highest > Highest) Highest = o.Highest;
                for (int i = 0; i < Band.Length; i++) Band[i] += o.Band[i];
                for (int i = 0; i < Thought.Length; i++) Thought[i] += o.Thought[i];
                Griefs.AddRange(o.Griefs);
            }

            public string Report()
            {
                double residentDays = ResidentTicks / (double)DayClock.TicksPerDay;
                var text = new StringBuilder();
                text.AppendLine($"{residentDays:F0} resident-days; mood mean {MoodSum / (double)ResidentTicks:F1}, lowest {Lowest}, highest {Highest}; deaths {Deaths}.");
                text.AppendLine($"Frustration: mean {FrustrationSum / ResidentTicks:F1}; at 60 or more (the face) {100.0 * FrustratedTicks / ResidentTicks:F2}% of the time; defiant {DefiantTicks / residentDays:F1} ticks a resident-day.");
                text.AppendLine($"Under orders: {OrderedTicks / (double)DayClock.TicksPerHour / residentDays:F2} h a resident-day, of it on a plan other than duty, aid or hunger {(OrderedTicks > 0 ? 100.0 * OrdersAsideTicks / OrderedTicks : 0):F2}% (plan 2's O.1); members who stayed behind or deserted a trip: {RefusedAtMuster} at the muster, {RefusedOnTheWay} on the way (O.2).");
                text.AppendLine("Time in each band: " + string.Join(", ", new[] { MoodBand.Breaking, MoodBand.Low, MoodBand.Steady, MoodBand.Good, MoodBand.Happy }
                    .Select(b => $"{b} {100.0 * Band[(int)b] / ResidentTicks:F1}%")));
                text.AppendLine("Thoughts thought a resident-day:");
                foreach (var row in ThoughtTable.All.OrderByDescending(r => Thought[(ushort)r.Id]))
                    if (Thought[(ushort)row.Id] > 0) text.AppendLine($"  {row.Name} ({row.Value:+0;-0}): {Thought[(ushort)row.Id] / residentDays:F2}");
                if (Griefs.Count > 0)
                    text.Append($"A friend's death, {Griefs.Count} griefs: mood {Griefs.Average(g => g.Before):F1} before, {Griefs.Average(g => g.After):F1} an hour after; " +
                        $"Low or below after: {Griefs.Count(g => MoodRules.BandOf(g.After) <= MoodBand.Low)}.");
                else text.Append("No friend died.");
                return text.ToString();
            }
        }
    }
}
