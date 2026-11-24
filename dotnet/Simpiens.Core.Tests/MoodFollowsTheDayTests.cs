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
            var lastMood = new Dictionary<EntityId, int>();
            long horizon = days * (long)DayClock.TicksPerDay;
            while (sim.Clock.CurrentTick < horizon)
            {
                watch.Run(1);
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
            public long ResidentTicks, MoodSum;
            public int Lowest = int.MaxValue, Highest = int.MinValue;
            public readonly long[] Band = new long[6];
            public readonly long[] Thought = new long[ThoughtTable.Capacity];
            public readonly List<(int Before, int After)> Griefs = new();

            public void Add(MoodTally o)
            {
                Seeds += o.Seeds; Deaths += o.Deaths; ResidentTicks += o.ResidentTicks; MoodSum += o.MoodSum;
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
