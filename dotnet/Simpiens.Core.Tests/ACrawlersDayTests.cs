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
using Simpiens.Entities;
using Simpiens.Simulation;
using Simpiens.Simulation.Guild;
using Xunit;

namespace Simpiens.Core.Tests
{
    /// <summary>
    /// A crawler's day, measured (Epic LIFE-1, L0.3): what residents spend their hours on, by the motive model's families and
    /// by goal; the goals chosen and the plans not found; near-ties and flips at plans' ends (D5's question); who comes
    /// within the gossip radius of whom; and how many chronicle lines a day brings (the day log's question, F5); and, from
    /// plan 2's P.1, the day by the clock: what residents do in each game hour, the evening's baseline for fun; and, from plan
    /// 3's P.1, sleep: the share asleep by clock hour, each sleep's length, the hours awake between sleeps, and the hours at
    /// hands-on work (harvesting or a station's work). Reads the session; changes nothing. Explicit: a measurement for
    /// docs/life-model.md §3, §5 and §6, re-run at each plan's gate.
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
            var sleeping = new Dictionary<EntityId, (bool Asleep, long Since, long LastSleepEnd)>();
            var intentionDays = new Dictionary<EntityId, ushort>();
            while (sim.Clock.CurrentTick < horizon)
            {
                watch.Run(1);
                residents.Clear();
                foreach (var crawler in sim.Host.CrawlerStates) if (!crawler.IsDrifter && !crawler.IsDowned) residents.Add(crawler);
                tally.ResidentTicks += residents.Count;
                if (sim.Clock.CurrentTick % DayClock.TicksPerDay == 0)
                {
                    // Boredom at each day's end (plan 2's fun): where it settles, or whether it climbs day on day
                    int day = (int)(sim.Clock.CurrentTick / DayClock.TicksPerDay) - 1;
                    float sum = 0f;
                    foreach (var crawler in residents) sum += crawler.Life.Boredom;
                    if (day < tally.BoredomAtDayEnd.Length && residents.Count > 0) tally.BoredomAtDayEnd[day] += sum / residents.Count;
                }

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
                    int clockHour = DayClock.MinuteOfDay(sim.Clock.CurrentTick - 1) / 60;
                    tally.Bump(tally.ByHour[clockHour], category);

                    // Sleep (plan 3's P.1): asleep in a bunk, resting where it stands, or collapsed
                    bool asleep = crawler.VisualState == AgentVisualState.Sleeping || crawler.VisualState == AgentVisualState.Resting || crawler.IsExhaustionCollapsed;
                    if (asleep) tally.AsleepByHour[clockHour]++;
                    tally.AllByHour[clockHour]++;
                    int chronotype = (int)crawler.Life.Chronotype;
                    if (asleep) tally.AsleepByChronotype[chronotype, clockHour]++;
                    tally.AllByChronotype[chronotype, clockHour]++;
                    if (crawler.VisualState == AgentVisualState.Harvesting) tally.HandsOnTicks++;
                    // Work by purse (plan 3's R.6): under the comfortable purse, up to twice it, and beyond
                    int gold = crawler.Inventory != null ? crawler.Inventory.Gold : 0, purse = sim.Simulation.Economy.ComfortablePurse;
                    int band = gold <= purse ? 0 : gold <= 2 * purse ? 1 : 2;
                    tally.ByPurse[band]++;
                    if (category == "Work") tally.WorkByPurse[band]++;
                    tally.WearinessSum += crawler.Life.Weariness;
                    // The day's intention (plan 3's R.7): each drawn, and a rest day after a windfall
                    if (crawler.Life.IntentionDay != 0 && (!intentionDays.TryGetValue(crawler.AgentId, out ushort seenDay) || seenDay != crawler.Life.IntentionDay))
                    {
                        intentionDays[crawler.AgentId] = crawler.Life.IntentionDay;
                        tally.Intentions[(int)crawler.Life.Intention]++;
                        bool windfall = Simpiens.Simulation.Life.IntentionRules.WindfallWithinADay(crawler.Life.LastWindfall, (uint)sim.Clock.CurrentTick);
                        if (windfall) { tally.DrawnAfterWindfall++; if (crawler.Life.Intention == Simpiens.Simulation.Life.DayIntention.Rest) tally.RestAfterWindfall++; }
                    }
                    if (crawler.IntentionAt((uint)sim.Clock.CurrentTick) == Simpiens.Simulation.Life.DayIntention.Rest)
                    {
                        tally.RestDayTicks++;
                        tally.Bump(tally.RestDayByCategory, category);
                        if (category == "Work") tally.RestDayWorkTicks++;
                        if (category == "Leisure") tally.RestDayLeisureTicks++;
                    }
                    long now = (long)sim.Clock.CurrentTick;
                    if (!sleeping.TryGetValue(crawler.AgentId, out var spell)) sleeping[crawler.AgentId] = (asleep, now, -1);
                    else if (spell.Asleep != asleep)
                    {
                        if (spell.Asleep && now - spell.Since >= SleepAtLeast)
                        {
                            // A sleep ended: its length, and the waking day before it (from the last sleep's end, if any)
                            tally.SleepLengths.Add(now - spell.Since);
                            if (spell.LastSleepEnd >= 0) tally.AwakeSpans.Add(spell.Since - spell.LastSleepEnd);
                            sleeping[crawler.AgentId] = (asleep, now, now);
                        }
                        else sleeping[crawler.AgentId] = (asleep, spell.Asleep ? spell.Since : now, spell.LastSleepEnd);
                    }

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

        /// <summary>A spell asleep this long or more is a sleep; shorter ones (a doze where it stands) don't end a waking day.</summary>
        private static readonly long SleepAtLeast = DayClock.Hours(2);

        internal sealed class DayTally
        {
            public int Days, Residents;
            public readonly long[] AsleepByHour = new long[24], AllByHour = new long[24];
            public readonly long[,] AsleepByChronotype = new long[3, 24], AllByChronotype = new long[3, 24];
            public readonly List<long> SleepLengths = new(), AwakeSpans = new();
            public long HandsOnTicks;
            public readonly long[] ByPurse = new long[3], WorkByPurse = new long[3];
            public double WearinessSum;
            public readonly long[] Intentions = new long[4];
            public long DrawnAfterWindfall, RestAfterWindfall, RestDayTicks, RestDayWorkTicks, RestDayLeisureTicks;
            public readonly Dictionary<string, long> RestDayByCategory = new();
            public long ResidentTicks, Decisions, GoalsChosen, NearTies, Flips, PlansNotFound, PairTicks, ChronicleLines, ChronicleLinesAboutResidents;
            public readonly Dictionary<string, long> ByCategory = new(), ByGoal = new(), BySource = new(), NotFoundGoals = new(), AfterNotFound = new(), NearTiePairs = new(), FlipPairs = new(), ByEventType = new();
            public readonly HashSet<(int Day, EntityId A, EntityId B)> PairsMet = new();
            public readonly double[] BoredomAtDayEnd = new double[16];
            public readonly Dictionary<string, long>[] ByHour = Enumerable.Range(0, 24).Select(_ => new Dictionary<string, long>()).ToArray();
            private int _seeds = 1;
            private long _distinctPairDays;

            public void Bump(Dictionary<string, long> into, string key) => into[key] = into.TryGetValue(key, out long n) ? n + 1 : 1;

            public void Add(DayTally other)
            {
                if (Days == 0) { _seeds = 0; Days = other.Days; }
                _seeds++;
                Residents += other.Residents;
                for (int h = 0; h < 24; h++) { AsleepByHour[h] += other.AsleepByHour[h]; AllByHour[h] += other.AllByHour[h]; }
                for (int c = 0; c < 3; c++)
                    for (int h = 0; h < 24; h++) { AsleepByChronotype[c, h] += other.AsleepByChronotype[c, h]; AllByChronotype[c, h] += other.AllByChronotype[c, h]; }
                SleepLengths.AddRange(other.SleepLengths);
                AwakeSpans.AddRange(other.AwakeSpans);
                HandsOnTicks += other.HandsOnTicks;
                for (int b = 0; b < 3; b++) { ByPurse[b] += other.ByPurse[b]; WorkByPurse[b] += other.WorkByPurse[b]; }
                WearinessSum += other.WearinessSum;
                for (int k = 0; k < 4; k++) Intentions[k] += other.Intentions[k];
                DrawnAfterWindfall += other.DrawnAfterWindfall; RestAfterWindfall += other.RestAfterWindfall;
                RestDayTicks += other.RestDayTicks; RestDayWorkTicks += other.RestDayWorkTicks; RestDayLeisureTicks += other.RestDayLeisureTicks;
                ResidentTicks += other.ResidentTicks; Decisions += other.Decisions; GoalsChosen += other.GoalsChosen; NearTies += other.NearTies;
                Flips += other.Flips; PlansNotFound += other.PlansNotFound; PairTicks += other.PairTicks;
                ChronicleLines += other.ChronicleLines; ChronicleLinesAboutResidents += other.ChronicleLinesAboutResidents;
                _distinctPairDays += other.PairsMet.Count;
                foreach (var map in new[] { (ByCategory, other.ByCategory), (ByGoal, other.ByGoal), (BySource, other.BySource), (NotFoundGoals, other.NotFoundGoals),
                                            (AfterNotFound, other.AfterNotFound), (NearTiePairs, other.NearTiePairs), (FlipPairs, other.FlipPairs), (ByEventType, other.ByEventType),
                                            (RestDayByCategory, other.RestDayByCategory) })
                    foreach (var pair in map.Item2) map.Item1[pair.Key] = (map.Item1.TryGetValue(pair.Key, out long n) ? n : 0) + pair.Value;
                for (int d = 0; d < BoredomAtDayEnd.Length; d++) BoredomAtDayEnd[d] += other.BoredomAtDayEnd[d];
                for (int h = 0; h < 24; h++)
                    foreach (var pair in other.ByHour[h]) ByHour[h][pair.Key] = (ByHour[h].TryGetValue(pair.Key, out long n) ? n : 0) + pair.Value;
            }

            private double ResidentDays => ResidentTicks / (double)DayClock.TicksPerDay;
            private double SeedDays => _seeds * (double)Days;
            private string PerResidentDay(long count) => (count / ResidentDays).ToString("F1");
            private string Hours(long ticks) => (ticks / (double)DayClock.TicksPerHour / ResidentDays).ToString("F1") + " h";

            /// <summary>Hours: the mean, and the 10th, 50th and 90th percentiles.</summary>
            private static string Spread(List<long> ticks)
            {
                if (ticks.Count == 0) return "none";
                var sorted = ticks.OrderBy(t => t).ToArray();
                double H(long t) => t / (double)DayClock.TicksPerHour;
                return $"mean {sorted.Average(t => H(t)):F1} h (p10 {H(sorted[sorted.Length / 10]):F1}, p50 {H(sorted[sorted.Length / 2]):F1}, p90 {H(sorted[sorted.Length * 9 / 10]):F1})";
            }

            public string Line() =>
                $"asleep at 02:00 {(AllByHour[2] > 0 ? 100.0 * AsleepByHour[2] / AllByHour[2] : 0):F0}%, at 18:00 {(AllByHour[18] > 0 ? 100.0 * AsleepByHour[18] / AllByHour[18] : 0):F0}%; awake between sleeps {Spread(AwakeSpans)}; " +
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
                text.AppendLine("Boredom at each day's end, the mean resident: " + string.Join(", ", Enumerable.Range(0, Days).Select(d => (BoredomAtDayEnd[d] / _seeds).ToString("F1"))));
                text.AppendLine($"Hands-on work (harvesting or a station's work): {Hours(HandsOnTicks)} a resident-day.");
                string[] bands = { "at most the comfortable purse", "up to twice it", "beyond" };
                text.AppendLine("Work by purse (plan 3's R.6): " + string.Join("; ", Enumerable.Range(0, 3).Select(b =>
                    $"{bands[b]} {(ResidentTicks > 0 ? 100.0 * ByPurse[b] / ResidentTicks : 0):F0}% of the time, at work {(ByPurse[b] > 0 ? 100.0 * WorkByPurse[b] / ByPurse[b] : 0):F0}% of it")));
                text.AppendLine($"Weariness, the mean resident over the run: {(ResidentTicks > 0 ? WearinessSum / ResidentTicks : 0):F1}.");
                long drawn = Intentions.Sum();
                text.AppendLine($"Intentions drawn (plan 3's R.7): {drawn} ({drawn / ResidentDays:F2} a resident-day); work {Intentions[0]}, rest {Intentions[1]}, training {Intentions[2]}, a want's {Intentions[3]}; " +
                    $"after a windfall {DrawnAfterWindfall}, of them rest days {RestAfterWindfall}; on a rest day, at work {(RestDayTicks > 0 ? 100.0 * RestDayWorkTicks / RestDayTicks : 0):F0}% and at leisure {(RestDayTicks > 0 ? 100.0 * RestDayLeisureTicks / RestDayTicks : 0):F0}% of the time.");
                text.AppendLine("  A rest day's hours: " + string.Join(", ", RestDayByCategory.OrderByDescending(p => p.Value).Take(7).Select(p => $"{p.Key} {100.0 * p.Value / System.Math.Max(1, RestDayTicks):F0}%")));
                text.AppendLine($"Sleeps (asleep {SleepAtLeast / DayClock.TicksPerHour} h or more): {SleepLengths.Count / ResidentDays:F2} a resident-day; length {Spread(SleepLengths)}.");
                text.AppendLine($"Hours awake between sleeps: {Spread(AwakeSpans)} ({AwakeSpans.Count} waking days).");
                text.AppendLine("Asleep by chronotype (plan 3's R.3), the share at each hour:");
                text.AppendLine("  chronotype  " + string.Join(" ", new[] { 0, 3, 5, 6, 7, 9, 10, 12, 18, 21, 22, 23 }.Select(h => $"{h:D2}:00")));
                foreach (var (c, name) in new[] { (1, "lark      "), (0, "average   "), (2, "owl       ") })
                    text.AppendLine($"  {name}  " + string.Join(" ", new[] { 0, 3, 5, 6, 7, 9, 10, 12, 18, 21, 22, 23 }.Select(h =>
                        AllByChronotype[c, h] > 0 ? (100.0 * AsleepByChronotype[c, h] / AllByChronotype[c, h]).ToString("F0").PadLeft(4) + "%" : "    -")));
                text.AppendLine("The day by the clock: what residents do in each game hour, as a share of their time (plan 2's P.1; asleep, plan 3's P.1):");
                text.AppendLine("  hour   work social   rest leisure  unweighted  other  asleep");
                for (int i = 0; i < 24; i++)
                {
                    int h = (6 + i) % 24; // from 06:00, the day's start
                    var hour = ByHour[h];
                    long all = hour.Values.Sum();
                    if (all == 0) continue;
                    string Share(string key) => (100.0 * hour.GetValueOrDefault(key) / all).ToString("F0").PadLeft(5) + "%";
                    long known = hour.GetValueOrDefault("Work") + hour.GetValueOrDefault("Social") + hour.GetValueOrDefault("Rest") + hour.GetValueOrDefault("Leisure") + hour.GetValueOrDefault("Unweighted");
                    string asleepShare = AllByHour[h] > 0 ? (100.0 * AsleepByHour[h] / AllByHour[h]).ToString("F0").PadLeft(6) + "%" : "      -";
                    text.AppendLine($"  {h:D2}:00 {Share("Work")} {Share("Social")} {Share("Rest")} {Share("Leisure")}      {Share("Unweighted")} {(100.0 * (all - known) / all).ToString("F0").PadLeft(5)}% {asleepShare}");
                }
                text.AppendLine($"Chronicle lines a day: {ChronicleLines / SeedDays:F0} outpost-wide (the ring holds 2,048), {ChronicleLinesAboutResidents / SeedDays:F0} naming a resident ({ChronicleLinesAboutResidents / ResidentDays:F1} a resident-day); by type:");
                foreach (var pair in ByEventType.OrderByDescending(p => p.Value).Take(10)) text.AppendLine($"  {pair.Key}: {pair.Value / SeedDays:F1} a day");
                return text.ToString();
            }
        }
    }
}
