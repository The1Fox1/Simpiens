using System.Collections.Generic;
using System.Linq;
using System.Text;
using Simpiens.Cognition;
using Simpiens.Cognition.Contracts;
using Simpiens.Cognition.Planning;
using Simpiens.Core.Session;
using Simpiens.Hosting;
using Simpiens.Narrative;
using Simpiens.Core.Session.Layouts;
using Simpiens.Simulation;
using Simpiens.Simulation.Guild;
using Xunit;

namespace Simpiens.Core.Tests
{
    /// <summary>
    /// A crawler's day, measured (Epic LIFE-1, L0.3): what residents spend their hours on, by the motive model's families and
    /// by goal; the goals chosen and the plans not found; near-ties and flips at plans' ends (D5's question); who comes
    /// within the gossip radius of whom; and how many chronicle lines a day brings (the day log's question, F5). Reads the
    /// session; changes nothing. Explicit: a measurement for docs/life-model.md §3, re-run after L0.6 and at the plan's gate.
    /// </summary>
    public sealed class ACrawlersDayTests
    {
        private const float GossipRadius = 2.5f;

        /// <summary>The gate seeds for <c>SIMPIENS_LIFE_DAYS</c> game days (3 by default), with no player and with the guild player sending expeditions.</summary>
        [Fact(Explicit = true)]
        public void MeasureACrawlersDay()
        {
            int days = int.TryParse(System.Environment.GetEnvironmentVariable("SIMPIENS_LIFE_DAYS"), out int d) ? d : 3;
            foreach (bool guild in new[] { false, true })
            {
                var seeds = OutpostWatch.GateSeeds;
                var days_ = new DayTally[seeds.Length];
                System.Threading.Tasks.Parallel.For(0, seeds.Length, new System.Threading.Tasks.ParallelOptions { MaxDegreeOfParallelism = 8 },
                    i => days_[i] = Measure(seeds[i], days, guild));
                var total = new DayTally();
                foreach (var tally in days_) total.Add(tally);
                TestContext.Current.TestOutputHelper?.WriteLine($"=== {(guild ? "the guild player (expeditions to the Drowned Gallery)" : "no player")}, {seeds.Length} gate seeds x {days} days ===");
                TestContext.Current.TestOutputHelper?.WriteLine(total.Report());
                foreach (var line in days_.Select((t, i) => $"seed {seeds[i]}: {t.Line()}")) TestContext.Current.TestOutputHelper?.WriteLine(line);
            }
        }

        /// <summary>One seed's days, measured.</summary>
        internal static DayTally Measure(ulong seed, int days, bool guild)
        {
            using var sim = LocalSimulation.Create(seed);
            sim.Respawn(OutpostSession.SessionId);
            if (guild) new GuildPlayer(sim, targetSize: 4).SendExpeditions(Floor1Layout.DrownedGallerySectorId, Floor1Layout.GallerySeamYield, RetreatRule.Steady);
            var watch = new OutpostWatch(sim);
            var tally = new DayTally();
            var lastDecision = new Dictionary<EntityId, uint>();
            var recentGoals = new Dictionary<EntityId, (string Goal, uint Tick)[]>();
            var events = new List<ChronicleEvent>(64);
            ulong lastEvent = 0;
            long horizon = days * (long)DayClock.TicksPerDay;
            var residents = new List<AgentState>(32);
            while (sim.Clock.CurrentTick < horizon)
            {
                watch.Run(1);
                residents.Clear();
                foreach (var crawler in sim.Host.CrawlerStates) if (!crawler.IsDrifter && !crawler.IsDowned) residents.Add(crawler);
                tally.ResidentTicks += residents.Count;

                foreach (var crawler in residents)
                {
                    // Where its hours go: the active plan's goal (by family and by name), or what it does without one
                    string category, goalName = null;
                    if (crawler.HasActivePlan && crawler.ActiveGoal != null)
                    {
                        category = crawler.ActiveGoal.Family.ToString();
                        goalName = crawler.ActiveGoal.Name;
                    }
                    else category = crawler.CurrentIntent switch
                    {
                        null => "between decisions",
                        WanderIntent => "wandering",
                        IdleIntent => "idle",
                        _ => "reflex: " + crawler.CurrentIntent.GetType().Name.Replace("Intent", string.Empty),
                    };
                    tally.Bump(tally.ByCategory, category);
                    if (goalName != null) tally.Bump(tally.ByGoal, goalName);

                    // Its decisions: one a tick at most, told apart by the tick they were made at
                    var decision = crawler.LastDecision;
                    if (decision.IsEmpty) continue;
                    if (lastDecision.TryGetValue(crawler.AgentId, out uint seen) && seen == decision.Tick) continue;
                    lastDecision[crawler.AgentId] = decision.Tick;
                    tally.Decisions++;
                    if (decision.Source == "GOAP new plan")
                    {
                        tally.GoalsChosen++;
                        if (decision.Goal2 != null && decision.Priority1 > 0f && decision.Priority1 - decision.Priority2 < 0.05f * decision.Priority1)
                        {
                            tally.NearTies++;
                            tally.Bump(tally.NearTiePairs, decision.Goal1 + " / " + decision.Goal2);
                        }
                        // A flip: A, then B, then A again within a game hour of choosing A first
                        if (!recentGoals.TryGetValue(crawler.AgentId, out var recent)) recentGoals[crawler.AgentId] = recent = new (string, uint)[2];
                        if (recent[0].Goal == decision.Goal1 && recent[1].Goal != null && recent[1].Goal != decision.Goal1 && decision.Tick - recent[0].Tick <= DayClock.TicksPerHour)
                        {
                            tally.Flips++;
                            tally.Bump(tally.FlipPairs, decision.Goal1 + " / " + recent[1].Goal);
                        }
                        recent[0] = recent[1];
                        recent[1] = (decision.Goal1, decision.Tick);
                    }
                    else if (decision.Goal1 != null && decision.Source != null && decision.Source.StartsWith("Tier-3"))
                    {
                        tally.PlansNotFound++;
                        tally.Bump(tally.NotFoundGoals, decision.Goal1);
                        tally.Bump(tally.AfterNotFound, decision.Source);
                    }
                    tally.Bump(tally.BySource, decision.Source);
                }

                // Who comes within the gossip radius of whom
                for (int a = 0; a < residents.Count; a++)
                    for (int b = a + 1; b < residents.Count; b++)
                    {
                        if ((residents[a].Position - residents[b].Position).SqrMagnitude > GossipRadius * GossipRadius) continue;
                        tally.PairTicks++;
                        var key = residents[a].AgentId.CompareTo(residents[b].AgentId) < 0 ? (residents[a].AgentId, residents[b].AgentId) : (residents[b].AgentId, residents[a].AgentId);
                        tally.PairsMet.Add(((int)((sim.Clock.CurrentTick - 1) / DayClock.TicksPerDay), key.Item1, key.Item2));
                    }

                // The chronicle's lines: new ones by their rising ids (at most 32 a tick are seen)
                events.Clear();
                sim.Chronicle.GetAllRecentEvents(events, 32);
                foreach (var e in events)
                {
                    if (e.EventId <= lastEvent) continue;
                    tally.ChronicleLines++;
                    tally.Bump(tally.ByEventType, e.EventType.ToString());
                    bool aboutResident = false;
                    foreach (var crawler in residents) if (crawler.AgentId == e.ActorAId || crawler.AgentId == e.ActorBId) aboutResident = true;
                    if (aboutResident) tally.ChronicleLinesAboutResidents++;
                }
                foreach (var e in events) if (e.EventId > lastEvent) lastEvent = e.EventId;
            }
            tally.Days = days;
            tally.Residents = residents.Count;
            return tally;
        }

        internal sealed class DayTally
        {
            public int Days, Residents;
            public long ResidentTicks, Decisions, GoalsChosen, NearTies, Flips, PlansNotFound, PairTicks, ChronicleLines, ChronicleLinesAboutResidents;
            public readonly Dictionary<string, long> ByCategory = new(), ByGoal = new(), BySource = new(), NotFoundGoals = new(), AfterNotFound = new(), NearTiePairs = new(), FlipPairs = new(), ByEventType = new();
            public readonly HashSet<(int Day, EntityId A, EntityId B)> PairsMet = new();
            private int _seeds = 1;
            private long _distinctPairDays;

            public void Bump(Dictionary<string, long> into, string key) => into[key] = into.TryGetValue(key, out long n) ? n + 1 : 1;

            public void Add(DayTally other)
            {
                if (Days == 0) { _seeds = 0; Days = other.Days; }
                _seeds++;
                Residents += other.Residents;
                ResidentTicks += other.ResidentTicks; Decisions += other.Decisions; GoalsChosen += other.GoalsChosen; NearTies += other.NearTies;
                Flips += other.Flips; PlansNotFound += other.PlansNotFound; PairTicks += other.PairTicks;
                ChronicleLines += other.ChronicleLines; ChronicleLinesAboutResidents += other.ChronicleLinesAboutResidents;
                _distinctPairDays += other.PairsMet.Count;
                foreach (var map in new[] { (ByCategory, other.ByCategory), (ByGoal, other.ByGoal), (BySource, other.BySource), (NotFoundGoals, other.NotFoundGoals),
                                            (AfterNotFound, other.AfterNotFound), (NearTiePairs, other.NearTiePairs), (FlipPairs, other.FlipPairs), (ByEventType, other.ByEventType) })
                    foreach (var pair in map.Item2) map.Item1[pair.Key] = (map.Item1.TryGetValue(pair.Key, out long n) ? n : 0) + pair.Value;
            }

            private double ResidentDays => ResidentTicks / (double)DayClock.TicksPerDay;
            private double SeedDays => _seeds * (double)Days;
            private string PerResidentDay(long count) => (count / ResidentDays).ToString("F1");
            private string Hours(long ticks) => (ticks / (double)DayClock.TicksPerHour / ResidentDays).ToString("F1") + " h";

            public string Line() =>
                $"work {Hours(ByCategory.GetValueOrDefault("Work"))}, wandering {Hours(ByCategory.GetValueOrDefault("wandering"))}, idle {Hours(ByCategory.GetValueOrDefault("idle"))}; " +
                $"{PerResidentDay(GoalsChosen)} goals and {PerResidentDay(PlansNotFound)} plans not found a resident-day; {Flips} flips; " +
                $"{PairsMet.Count / (double)Days:F0} pairs met a day; {ChronicleLines / (double)Days:F0} chronicle lines a day";

            public string Report()
            {
                var text = new StringBuilder();
                text.AppendLine($"Residents: {Residents / (double)_seeds:F1} a seed at the end; {ResidentDays:F0} resident-days.");
                text.AppendLine("Hours a resident-day, by family or what it does without a plan:");
                foreach (var pair in ByCategory.OrderByDescending(p => p.Value)) text.AppendLine($"  {pair.Key}: {Hours(pair.Value)} ({100.0 * pair.Value / ResidentTicks:F0}%)");
                text.AppendLine("Hours a resident-day, by goal (with an active plan):");
                foreach (var pair in ByGoal.OrderByDescending(p => p.Value)) text.AppendLine($"  {pair.Key}: {Hours(pair.Value)}");
                text.AppendLine($"Decisions a resident-day: {PerResidentDay(Decisions)}; by source:");
                foreach (var pair in BySource.OrderByDescending(p => p.Value)) text.AppendLine($"  {pair.Key}: {PerResidentDay(pair.Value)}");
                text.AppendLine($"Goals chosen (a new plan): {PerResidentDay(GoalsChosen)} a resident-day.");
                text.AppendLine($"Plans not found: {PerResidentDay(PlansNotFound)} a resident-day ({(GoalsChosen + PlansNotFound > 0 ? 100.0 * PlansNotFound / (GoalsChosen + PlansNotFound) : 0):F1}% of goals picked); by goal:");
                foreach (var pair in NotFoundGoals.OrderByDescending(p => p.Value)) text.AppendLine($"  {pair.Key}: {PerResidentDay(pair.Value)}");
                text.AppendLine("  and what followed:");
                foreach (var pair in AfterNotFound.OrderByDescending(p => p.Value)) text.AppendLine($"    {pair.Key}: {PerResidentDay(pair.Value)}");
                text.AppendLine($"Near ties at a choice (the first within 5% of the second): {PerResidentDay(NearTies)} a resident-day ({(GoalsChosen > 0 ? 100.0 * NearTies / GoalsChosen : 0):F1}% of choices); the commonest:");
                foreach (var pair in NearTiePairs.OrderByDescending(p => p.Value).Take(6)) text.AppendLine($"  {pair.Key}: {pair.Value}");
                text.AppendLine($"Flips (A, B, A again within a game hour): {PerResidentDay(Flips)} a resident-day; the commonest:");
                foreach (var pair in FlipPairs.OrderByDescending(p => p.Value).Take(6)) text.AppendLine($"  {pair.Key}: {pair.Value}");
                text.AppendLine($"Together (two residents within {GossipRadius} units): {_distinctPairDays / SeedDays:F1} distinct pairs a day; {PairTicks / (double)DayClock.TicksPerHour / SeedDays:F1} pair-hours a day.");
                text.AppendLine($"Chronicle lines a day: {ChronicleLines / SeedDays:F0} outpost-wide (the ring holds 1,024), {ChronicleLinesAboutResidents / SeedDays:F0} naming a resident ({ChronicleLinesAboutResidents / ResidentDays:F1} a resident-day); by type:");
                foreach (var pair in ByEventType.OrderByDescending(p => p.Value).Take(10)) text.AppendLine($"  {pair.Key}: {pair.Value / SeedDays:F1} a day");
                return text.ToString();
            }
        }
    }
}
