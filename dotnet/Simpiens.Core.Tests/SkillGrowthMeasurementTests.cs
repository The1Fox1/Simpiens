using System.Collections.Generic;
using System.Linq;
using System.Text;
using Simpiens.Core.Session;
using Simpiens.Hosting;
using Simpiens.Simulation;
using Simpiens.Simulation.Skills;
using Xunit;

namespace Simpiens.Core.Tests
{
    /// <summary>
    /// How crawlers grow (Epic 11, S0.3; measured again at S1.5 and at the plan's close): every gate seed over 14 game days,
    /// with no player and with the soak's <c>hunting</c> players. It records the levels and bands each resident gains in
    /// each skill, who holds two or more skills past Apprentice (the mastery budget's pressure, D7), practice by kind of
    /// work, the day each band is first reached, and how many reach Journeyman (the asking wage's reach, D8). Explicit,
    /// several minutes; SIMPIENS_GROWTH_DAYS measures another horizon. The results go to docs/skills-model.md.
    /// </summary>
    public class SkillGrowthMeasurementTests
    {
        private sealed class Growth
        {
            public int Residents, Journeymen, Masters, TwoPastApprentice, ThreePastApprentice, Deaths;
            public readonly int[] LevelsGained = new int[SkillTable.Capacity];
            public readonly int[] ReachedApprentice = new int[SkillTable.Capacity];
            public readonly int[] ReachedJourneyman = new int[SkillTable.Capacity];
            public readonly int[] ReachedMaster = new int[SkillTable.Capacity];
            public readonly int[] Practice = new int[SkillTable.Capacity];
            public readonly int[] Peak = new int[SkillTable.Capacity];
            public long FirstJourneyman = -1, FirstMaster = -1;
            public string FirstJourneymanWho = "-";
            // Techniques (Epic 11, S4.7): held at the end, and picked during the run (held at the end, not at the start), by id and band
            public readonly int[] HeldAtEnd = new int[256];
            public readonly int[] PickedInRun = new int[256];
            public readonly int[] PickedByBand = new int[4];
            // Callings (Epic 11, S5.6): held at the end, and how many residents' changed during the run
            public readonly int[] CallingAtEnd = new int[256];
            public int CallingsChanged;
            // Deeds (Epic 11, S6.0, D26): each resident's count gained in the run, by deed; downings by their cause
            public readonly List<int>[] DeedsGained = Enumerable.Range(0, DeedTable.Capacity).Select(_ => new List<int>()).ToArray();
            public int DownedByCreature, DownedByHazard, DownedMembers, Downings;
            public readonly Dictionary<EntityId, int> DownedTimes = new Dictionary<EntityId, int>();
            // Legend (Epic 11, S6.5): epithets and earned traits held at the end, by id; scars borne
            public readonly int[] EpithetAtEnd = new int[256];
            public readonly int[] TraitAtEnd = new int[256];
            public int Scars;
        }

        private static Growth Play(ulong seed, string profile, long horizon)
        {
            using var sim = LocalSimulation.Create(seed);
            sim.Respawn(OutpostSession.SessionId);
            if (profile != "none") EconomySoak.AttachPlayers(sim, profile);
            var watch = new OutpostWatch(sim);
            var growth = new Growth();
            var start = new Dictionary<EntityId, SkillSet>();
            var last = new Dictionary<EntityId, SkillSet>();
            var startTraits = new Dictionary<EntityId, CrawlerTraits>();
            var lastTraits = new Dictionary<EntityId, CrawlerTraits>();
            var wasDowned = new HashSet<EntityId>();
            while (sim.Clock.CurrentTick < horizon)
            {
                foreach (var crawler in sim.Host.CrawlerStates)
                {
                    if (crawler.IsDrifter) continue;
                    if (!start.ContainsKey(crawler.AgentId))
                    {
                        start[crawler.AgentId] = crawler.Skills;
                        startTraits[crawler.AgentId] = crawler.Traits;
                    }
                }
                watch.Run(1);
                long now = sim.Clock.CurrentTick;
                foreach (var crawler in sim.Host.CrawlerStates)
                {
                    if (crawler.IsDrifter || !start.ContainsKey(crawler.AgentId)) continue;
                    last[crawler.AgentId] = crawler.Skills;
                    lastTraits[crawler.AgentId] = crawler.Traits;
                    // A downing: its cause, whose it was (Epic 11, S6.0)
                    if (crawler.IsDowned && wasDowned.Add(crawler.AgentId))
                    {
                        growth.Downings++;
                        if (crawler.DownedBy == DeathCause.Creature) growth.DownedByCreature++;
                        else growth.DownedByHazard++;
                        if (sim.Simulation.Guild.TryGetContract(crawler.AgentId, out _)) growth.DownedMembers++;
                        growth.DownedTimes[crawler.AgentId] = (growth.DownedTimes.TryGetValue(crawler.AgentId, out int n) ? n : 0) + 1;
                    }
                    else if (!crawler.IsDowned) wasDowned.Remove(crawler.AgentId);
                    var all = SkillTable.All;
                    for (int i = 0; i < all.Count; i++)
                    {
                        int level = crawler.Skills.Level(all[i].Id);
                        if (growth.FirstJourneyman < 0 && level >= 6 && start[crawler.AgentId].Level(all[i].Id) < 6)
                        {
                            growth.FirstJourneyman = now;
                            growth.FirstJourneymanWho = $"{crawler.GivenName}, {all[i].DisplayName}";
                        }
                        if (growth.FirstMaster < 0 && level >= 9 && start[crawler.AgentId].Level(all[i].Id) < 9) growth.FirstMaster = now;
                    }
                }
            }
            watch.Finish();
            growth.Deaths = watch.Deaths;
            for (int s = 0; s < SkillTable.Capacity; s++) growth.Practice[s] = watch.PracticeBySkill[s];
            foreach (var (id, was) in start)
            {
                var now = last.TryGetValue(id, out var seen) ? seen : was;
                growth.Residents++;
                int past = 0;
                bool journeyman = false, master = false;
                foreach (var row in SkillTable.All)
                {
                    int s = (byte)row.Id, before = was.Level(row.Id), after = now.Level(row.Id);
                    growth.LevelsGained[s] += after - before;
                    growth.Peak[s] = System.Math.Max(growth.Peak[s], after);
                    if (before < 3 && after >= 3) growth.ReachedApprentice[s]++;
                    if (before < 6 && after >= 6) growth.ReachedJourneyman[s]++;
                    if (before < 9 && after >= 9) growth.ReachedMaster[s]++;
                    if (after >= 6) { past++; journeyman = true; }
                    if (after >= 9) master = true;
                }
                var traitsThen = startTraits[id];
                var traitsNow = lastTraits.TryGetValue(id, out var seenTraits) ? seenTraits : traitsThen;
                foreach (var row in SkillTable.All)
                    for (var band = SkillBand.Apprentice; band <= SkillBand.Master; band++)
                    {
                        var held = traitsNow.TechniqueIn(row.Id, band);
                        if (held == Technique.None) continue;
                        growth.HeldAtEnd[(byte)held]++;
                        if (traitsThen.TechniqueIn(row.Id, band) != held)
                        {
                            growth.PickedInRun[(byte)held]++;
                            growth.PickedByBand[(byte)band]++;
                        }
                    }
                growth.CallingAtEnd[(byte)traitsNow.Calling]++;
                growth.EpithetAtEnd[(byte)traitsNow.Epithet]++;
                for (int t = 0; t < CrawlerTraits.EarnedTraitSlots; t++) growth.TraitAtEnd[(byte)traitsNow.TraitAt(t)]++;
                growth.Scars += traitsNow.ScarCount;
                for (int deed = 1; deed < DeedTable.Capacity; deed++)
                    if (DeedTable.Get((Deed)deed) != null) growth.DeedsGained[deed].Add(traitsNow.Count((Deed)deed) - traitsThen.Count((Deed)deed));
                if (traitsNow.Calling != traitsThen.Calling) growth.CallingsChanged++;
                if (journeyman) growth.Journeymen++;
                if (master) growth.Masters++;
                if (past >= 2) growth.TwoPastApprentice++;
                if (past >= 3) growth.ThreePastApprentice++;
            }
            return growth;
        }

        [Fact(Explicit = true)]
        public void MeasureGrowthOnTheGateSeeds()
        {
            int days = int.TryParse(System.Environment.GetEnvironmentVariable("SIMPIENS_GROWTH_DAYS"), out int d) ? d : 14;
            long horizon = days * DayClock.TicksPerDay;
            var seeds = OutpostWatch.GateSeeds;
            var out_ = TestContext.Current.TestOutputHelper;
            foreach (var profile in new[] { "none", "hunting" })
            {
                var runs = new Growth[seeds.Length];
                System.Threading.Tasks.Parallel.For(0, seeds.Length, new System.Threading.Tasks.ParallelOptions { MaxDegreeOfParallelism = 8 }, i => runs[i] = Play(seeds[i], profile, horizon));
                out_?.WriteLine($"== {profile}: {seeds.Length} gate seeds, {days} days ==");
                for (int i = 0; i < seeds.Length; i++)
                {
                    var g = runs[i];
                    out_?.WriteLine($"seed {seeds[i]}: residents {g.Residents}, journeymen {g.Journeymen} (first {Day(g.FirstJourneyman)}: {g.FirstJourneymanWho}), masters {g.Masters} (first {Day(g.FirstMaster)}), "
                        + $"two past Apprentice {g.TwoPastApprentice}, three {g.ThreePastApprentice}, deaths {g.Deaths}; levels {Per(g.LevelsGained)}");
                }
                var sum = new StringBuilder();
                sum.Append($"-- {profile} summary --\n");
                sum.Append($"seeds with a Journeyman made: {runs.Count(r => r.FirstJourneyman >= 0)}/{seeds.Length}, first on day {Range(runs.Select(r => r.FirstJourneyman))}\n");
                sum.Append($"seeds with a Master made: {runs.Count(r => r.FirstMaster >= 0)}/{seeds.Length}\n");
                sum.Append($"residents a seed: {runs.Average(r => r.Residents):F1}; holding a Journeyman's skill at the end: {runs.Average(r => r.Journeymen):F2} (most {runs.Max(r => r.Journeymen)})\n");
                sum.Append($"residents with two skills past Apprentice: {runs.Sum(r => r.TwoPastApprentice)} in all, on {runs.Count(r => r.TwoPastApprentice > 0)} seeds; three: {runs.Sum(r => r.ThreePastApprentice)}\n");
                foreach (var row in SkillTable.InDisplayOrder)
                {
                    int s = (byte)row.Id;
                    sum.Append($"  {row.DisplayName,-9} levels gained {runs.Average(r => r.LevelsGained[s]):F1}/seed; reached Apprentice {runs.Sum(r => r.ReachedApprentice[s])}, Journeyman {runs.Sum(r => r.ReachedJourneyman[s])}, Master {runs.Sum(r => r.ReachedMaster[s])}; practice {runs.Average(r => r.Practice[s]):F0} xp/seed; highest level {runs.Max(r => r.Peak[s])}\n");
                }
                sum.Append($"techniques picked in the run: {runs.Average(r => r.PickedByBand.Sum()):F1} a seed (Apprentice {runs.Sum(r => r.PickedByBand[1])}, Journeyman {runs.Sum(r => r.PickedByBand[2])}, Master {runs.Sum(r => r.PickedByBand[3])} in all)\n");
                sum.Append($"callings at the end ({runs.Sum(r => r.CallingsChanged)} changed in the run):");
                foreach (var row in CallingTable.All)
                {
                    int held = runs.Sum(r => r.CallingAtEnd[(byte)row.Id]);
                    if (held > 0) sum.Append($" {row.DisplayName} {held};");
                }
                sum.Append($" none {runs.Sum(r => r.CallingAtEnd[0])}\n");
                sum.Append("held at the end (picked in the run):");
                foreach (var row in TechniqueTable.All)
                {
                    int held = runs.Sum(r => r.HeldAtEnd[(byte)row.Id]);
                    if (held > 0) sum.Append($" {row.DisplayName} {held} ({runs.Sum(r => r.PickedInRun[(byte)row.Id])});");
                }
                // The deeds (Epic 11, S6.0, D26): per resident, how many reach each count, so the epithets' and traits' lines are set from it
                sum.Append($"\ndeeds gained in the run, of {runs.Sum(r => r.Residents)} residents (residents reaching 1 / 2 / 3 / 5 / 10 / 20 / 40; most; seeds with someone at 3 / 5):\n");
                foreach (var row in DeedTable.All)
                {
                    int deed = (byte)row.Id;
                    var each = runs.SelectMany(r => r.DeedsGained[deed]).ToArray();
                    string reach = string.Join(" / ", new[] { 1, 2, 3, 5, 10, 20, 40 }.Select(k => each.Count(n => n >= k)));
                    sum.Append($"  {row.DisplayName,-18} total {each.Sum()}; {reach}; most {(each.Length > 0 ? each.Max() : 0)}; seeds {runs.Count(r => r.DeedsGained[deed].Any(n => n >= 3))} / {runs.Count(r => r.DeedsGained[deed].Any(n => n >= 5))}\n");
                }
                sum.Append($"downings: {runs.Sum(r => r.Downings)} ({runs.Sum(r => r.DownedByCreature)} by a creature, {runs.Sum(r => r.DownedByHazard)} by a hazard; {runs.Sum(r => r.DownedMembers)} of guild members) on {runs.Count(r => r.Downings > 0)} seeds; "
                    + $"crawlers downed twice or more {runs.Sum(r => r.DownedTimes.Values.Count(n => n >= 2))}; deaths {runs.Sum(r => r.Deaths)}\n");
                // Legend (Epic 11, S6.5)
                sum.Append($"epithets held at the end: {runs.Sum(r => r.Residents - r.EpithetAtEnd[0])} residents, on {runs.Count(r => r.Residents - r.EpithetAtEnd[0] > 0)} seeds:");
                foreach (var row in EpithetTable.All)
                {
                    int held = runs.Sum(r => r.EpithetAtEnd[(byte)row.Id]);
                    if (held > 0) sum.Append($" {row.Name} {held};");
                }
                sum.Append("\nearned traits held at the end:");
                foreach (var row in EarnedTraitTable.All)
                {
                    int held = runs.Sum(r => r.TraitAtEnd[(byte)row.Id]);
                    if (held > 0) sum.Append($" {row.Words} {held};");
                }
                sum.Append($" scars borne {runs.Sum(r => r.Scars)}\n");
                out_?.WriteLine(sum.ToString());
            }
        }

        private static string Day(long tick) => tick >= 0 ? $"day {1 + tick / (double)DayClock.TicksPerDay:F1}" : "never";

        private static string Range(IEnumerable<long> ticks)
        {
            var seen = ticks.Where(t => t >= 0).Select(t => 1 + t / (double)DayClock.TicksPerDay).ToArray();
            return seen.Length == 0 ? "-" : $"{seen.Min():F1}-{seen.Max():F1}";
        }

        private static string Per(int[] levels)
        {
            var text = new StringBuilder();
            foreach (var row in SkillTable.InDisplayOrder)
            {
                int n = levels[(byte)row.Id];
                if (n == 0) continue;
                if (text.Length > 0) text.Append(", ");
                text.Append(row.DisplayName).Append(" +").Append(n);
            }
            return text.Length > 0 ? text.ToString() : "none";
        }
    }
}
