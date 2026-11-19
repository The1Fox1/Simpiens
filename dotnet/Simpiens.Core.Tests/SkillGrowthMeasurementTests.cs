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
            while (sim.Clock.CurrentTick < horizon)
            {
                foreach (var crawler in sim.Host.CrawlerStates)
                {
                    if (crawler.IsDrifter) continue;
                    if (!start.ContainsKey(crawler.AgentId)) start[crawler.AgentId] = crawler.Skills;
                }
                watch.Run(1);
                long now = sim.Clock.CurrentTick;
                foreach (var crawler in sim.Host.CrawlerStates)
                {
                    if (crawler.IsDrifter || !start.ContainsKey(crawler.AgentId)) continue;
                    last[crawler.AgentId] = crawler.Skills;
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
