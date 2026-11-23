using System.Collections.Generic;
using System.Linq;
using Simpiens.Cognition;
using Simpiens.Core.Session;
using Simpiens.Hosting;
using Simpiens.Simulation;
using Simpiens.Simulation.Economy;
using Simpiens.Simulation.Items;
using Simpiens.Simulation.Skills;
using Simpiens.Simulation.Workshops;
using Xunit;

namespace Simpiens.Core.Tests
{
    /// <summary>
    /// What the stations make, by grade (Epic 11, S8.0): every gate seed over 14 game days (SIMPIENS_GROWTH_DAYS for another
    /// horizon), with the soak's <c>crafting</c> and <c>hunting</c> players. It counts each finished make of a worn piece by
    /// the grade it came out at and its maker's passion for the work, the makers of Masterwork pieces and the day of the
    /// first, so that inspiration's chance (plan 4's D41) is set from what a seed makes. Explicit, a few minutes. The
    /// results go to docs/skills-model.md §5.14.
    /// </summary>
    public class MasterworkMeasurementTests
    {
        private sealed class Makes
        {
            public readonly int[] ByGrade = new int[4];
            public readonly int[] MasterworkByPassion = new int[3];
            public readonly HashSet<EntityId> MasterworkMakers = new HashSet<EntityId>();
            public readonly Dictionary<EntityId, int> MasterworkByMaker = new Dictionary<EntityId, int>();
            public int Unworn, StationsBuilt;
            public long FirstMasterwork = -1;
        }

        private static Makes Play(ulong seed, string profile, long horizon)
        {
            using var sim = LocalSimulation.Create(seed);
            sim.Respawn(OutpostSession.SessionId);
            EconomySoak.AttachPlayers(sim, profile);
            var workshops = sim.Simulation.Workshops;
            var makes = new Makes();
            var made = new uint[workshops.Count];
            var grade = new byte[workshops.Count];
            var maker = new AgentState[workshops.Count];
            var order = new ItemType[workshops.Count];
            while (sim.Clock.CurrentTick < horizon)
            {
                // The grade a make comes out at is its artisan's before the tick (WorkshopService.Work reads it first)
                for (int i = 0; i < workshops.Count; i++)
                {
                    made[i] = workshops[i].Made;
                    order[i] = workshops[i].Order; // a make that closes its order leaves none
                    maker[i] = null;
                    var artisan = workshops.ArtisanAt(i);
                    if (artisan != default) maker[i] = sim.Host.CrawlerStates.FirstOrDefault(s => s.AgentId == artisan);
                    grade[i] = WorkshopService.GradeOf(maker[i], workshops[i].Kind);
                }
                sim.Step(1);
                for (int i = 0; i < workshops.Count; i++)
                {
                    var station = workshops[i];
                    if (station.Made <= made[i]) continue;
                    var item = order[i];
                    if (item == ItemType.None || ItemCatalog.Get(item)?.Equipment == null)
                    {
                        makes.Unworn++;
                        continue;
                    }
                    makes.ByGrade[grade[i]]++;
                    if (grade[i] < Grades.Masterwork || maker[i] == null) continue;
                    var skill = SkillTable.SkillFor(SkillWork.Make(station.Kind));
                    makes.MasterworkByPassion[(byte)maker[i].Traits.PassionFor(skill)]++;
                    makes.MasterworkMakers.Add(maker[i].AgentId);
                    makes.MasterworkByMaker[maker[i].AgentId] = (makes.MasterworkByMaker.TryGetValue(maker[i].AgentId, out int n) ? n : 0) + 1;
                    if (makes.FirstMasterwork < 0) makes.FirstMasterwork = sim.Clock.CurrentTick;
                }
            }
            for (int i = 0; i < workshops.Count; i++) if (workshops[i].Built) makes.StationsBuilt++;
            return makes;
        }

        [Fact(Explicit = true)]
        public void MeasureMasterworkMakesOnTheGateSeeds()
        {
            int days = int.TryParse(System.Environment.GetEnvironmentVariable("SIMPIENS_GROWTH_DAYS"), out int d) ? d : 14;
            long horizon = days * DayClock.TicksPerDay;
            var seeds = OutpostWatch.GateSeeds;
            var out_ = TestContext.Current.TestOutputHelper;
            foreach (var profile in new[] { "crafting", "hunting" })
            {
                var runs = new Makes[seeds.Length];
                System.Threading.Tasks.Parallel.For(0, seeds.Length, new System.Threading.Tasks.ParallelOptions { MaxDegreeOfParallelism = 8 }, i => runs[i] = Play(seeds[i], profile, horizon));
                out_?.WriteLine($"== {profile}: {seeds.Length} gate seeds, {days} days ==");
                for (int i = 0; i < seeds.Length; i++)
                {
                    var m = runs[i];
                    out_?.WriteLine($"seed {seeds[i]}: stations {m.StationsBuilt}; worn makes Standard {m.ByGrade[0]}, Fine {m.ByGrade[1]}, Masterwork {m.ByGrade[2]} "
                        + $"(by {m.MasterworkMakers.Count} makers, first {Day(m.FirstMasterwork)}); unworn {m.Unworn}");
                }
                int withMasterwork = runs.Count(r => r.ByGrade[2] > 0);
                var perMaker = runs.SelectMany(r => r.MasterworkByMaker.Values).ToList();
                out_?.WriteLine($"-- {profile} summary --");
                out_?.WriteLine($"seeds with a Masterwork piece made: {withMasterwork}/{seeds.Length}, first on {Range(runs.Select(r => r.FirstMasterwork))}");
                out_?.WriteLine($"worn makes in all: Standard {runs.Sum(r => r.ByGrade[0])}, Fine {runs.Sum(r => r.ByGrade[1])}, Masterwork {runs.Sum(r => r.ByGrade[2])}; unworn {runs.Sum(r => r.Unworn)}");
                out_?.WriteLine($"Masterwork makes a seed where any: {(withMasterwork > 0 ? runs.Where(r => r.ByGrade[2] > 0).Average(r => r.ByGrade[2]) : 0):F1} (most {runs.Max(r => r.ByGrade[2])}); "
                    + $"makers {perMaker.Count}, a maker's {(perMaker.Count > 0 ? perMaker.Average() : 0):F1} (most {(perMaker.Count > 0 ? perMaker.Max() : 0)})");
                out_?.WriteLine($"Masterwork makes by the maker's passion: none {runs.Sum(r => r.MasterworkByPassion[0])}, keen {runs.Sum(r => r.MasterworkByPassion[1])}, burning {runs.Sum(r => r.MasterworkByPassion[2])}");
                foreach (var p in new[] { 0.03, 0.05, 0.08, 0.10 })
                {
                    // The chance a seed sees at least one masterpiece at p a Masterwork make (passion's multiplier left out)
                    double seedsWithOne = runs.Sum(r => 1 - System.Math.Pow(1 - p, r.ByGrade[2]));
                    double expected = runs.Sum(r => r.ByGrade[2]) * p;
                    out_?.WriteLine($"  at {p:P0} a make: a masterpiece expected on {seedsWithOne:F1} seeds, {expected:F1} in all");
                }
            }
        }

        private static string Day(long tick) => tick >= 0 ? $"day {1 + tick / (double)DayClock.TicksPerDay:F1}" : "never";

        private static string Range(IEnumerable<long> ticks)
        {
            var seen = ticks.Where(t => t >= 0).ToList();
            return seen.Count == 0 ? "never" : $"{Day(seen.Min())} to {Day(seen.Max())}";
        }
    }
}
