using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using Simpiens.Core.Session;
using Simpiens.Hosting;
using Simpiens.Simulation;
using Simpiens.Simulation.Economy;
using Simpiens.Testing.Environment;

namespace Simpiens.Host
{
    /// <summary>The console host's commands. Each returns the process exit code: 0 when everything held.</summary>
    internal static class Commands
    {
        public const string Usage =
@"simpiens: the Simpiens simulation, headless (Epic CORE Phase 1)

  simpiens run    [--session outpost] [--seed N] [--ticks 9000]
      Plays a session and prints one line per sim-minute, then its world hash.
  simpiens sweep  [--seeds 1..20] [--ticks 9000] [--verbose]
      The outpost's day on each seed, judged by the lifecycle Definition of Done (OutpostLifecycleScenario's checks).
  simpiens soak   [--profile fair|greedy|generous|all] [--seeds 20261009,1,2 | --seeds 1..20] [--ticks 18000] [--csv DIR]
      The economy soak: each profile and seed for 30 sim-minutes, judged against its pace band (docs/economy-model.md §7).
  simpiens golden check  [--baseline Baselines/golden-world-hash.txt] [--seeds ...]
      Compares headless runs with the golden world hashes (the default: every seed in the file).
  simpiens golden record --out PATH [--seeds 20261009]
      Writes headless runs' hashes in the baseline format (diagnosis; the committed file is re-recorded with
      scripts/golden-hashes.sh).

Logging: SIMPIENS_LOG=""Default=Info;Planning=Debug"" SIMPIENS_LOG_FILE=Logs/sim.log (or -simlog ...), to standard error.
Exit code: 0 when every run held, 1 when one failed, 64 for a bad command line.";

        public static int Run(Options options, CancellationToken ct)
        {
            switch (options.Command)
            {
                case "run": return Play(options, ct);
                case "sweep": return Sweep(options, ct);
                case "soak": return Soak(options, ct);
                case "golden": return Golden(options, ct);
                case "help":
                case "--help":
                    Console.WriteLine(Usage);
                    return 0;
                default:
                    Console.Error.WriteLine($"Unknown command '{options.Command}'.\n\n{Usage}");
                    return 64;
            }
        }

        private static int Play(Options options, CancellationToken ct)
        {
            string sessionId = options.Text("session", OutpostSession.SessionId);
            ulong seed = options.Seeds(new[] { SimulationSettings.DefaultSeed })[0];
            long ticks = options.Long("ticks", 9000);

            using var sim = LocalSimulation.Create(seed);
            sim.Respawn(sessionId);
            var manager = sim.Simulation;
            Console.WriteLine($"session {sessionId}, seed {seed}, {sim.Host.CrawlerStates.Count} crawlers, {ticks} ticks");
            Console.WriteLine("    tick  treasury  crawler gold  trust mean  bounties open  restocks  regrowths");

            var watch = Stopwatch.StartNew();
            long start = sim.Clock.CurrentTick;
            for (long rel = 0; rel <= ticks && !ct.IsCancellationRequested; rel++)
            {
                if (rel % EconomySoak.SampleIntervalTicks == 0 || rel == ticks) Console.WriteLine(Line(sim, rel));
                if (rel < ticks) sim.Step(1);
            }
            watch.Stop();

            var hash = WorldStateHash.Capture(sim.Clock.CurrentTick, sim.Host, manager, sim.Sectors);
            long ran = sim.Clock.CurrentTick - start;
            Console.WriteLine($"world hash at t{sim.Clock.CurrentTick}: {hash.Total:x16} ({ran} ticks in {watch.ElapsedMilliseconds} ms, {ran * 1000.0 / Math.Max(1, watch.ElapsedMilliseconds):F0} ticks/s)");
            foreach (var crawler in Crawlers(sim)) Console.WriteLine("  " + crawler);
            return ct.IsCancellationRequested ? 1 : 0;
        }

        private static string Line(LocalSimulation sim, long rel)
        {
            var manager = sim.Simulation;
            uint treasury = 0;
            for (int i = 0; i < manager.ActiveStalls.Count; i++) treasury += manager.ActiveStalls[i].TreasuryGold;
            int gold = 0;
            float trust = 0f;
            var crawlers = sim.Host.CrawlerStates;
            for (int i = 0; i < crawlers.Count; i++)
            {
                gold += crawlers[i].Inventory != null ? crawlers[i].Inventory.Gold : 0;
                trust += crawlers[i].Memory != null ? crawlers[i].Memory.GetAffinity(SimulationManager.PlayerEntityId, (uint)sim.Clock.CurrentTick).Trust : 0;
            }
            int open = 0;
            for (int i = 0; i < manager.ActiveBounties.Count; i++)
                if (manager.ActiveBounties[i].Status == BountyStatus.Open || manager.ActiveBounties[i].Status == BountyStatus.Claimed) open++;
            int restocks = manager.Supply != null ? manager.Supply.RestockCount : 0;
            int regrowths = manager.Supply != null ? manager.Supply.RegrowthCount : 0;
            return string.Format(CultureInfo.InvariantCulture, "{0,8}  {1,8}g  {2,11}g  {3,10:F1}  {4,13}  {5,8}  {6,9}",
                rel, treasury, gold, crawlers.Count > 0 ? trust / crawlers.Count : 0f, open, restocks, regrowths);
        }

        private static IEnumerable<string> Crawlers(LocalSimulation sim)
        {
            var host = sim.Host;
            for (int i = 0; i < host.CrawlerStates.Count; i++) yield return $"{host.CrawlerNames[i]}: {host.CrawlerStates[i].Activity}";
        }

        private static int Sweep(Options options, CancellationToken ct)
        {
            var seeds = options.Seeds(EconomySoak.DefaultSeeds);
            long ticks = options.Long("ticks", 9000);
            bool verbose = options.Flag("verbose");
            int failed = 0, runs = 0;
            var watch = Stopwatch.StartNew();
            foreach (var seed in seeds)
            {
                if (ct.IsCancellationRequested) return 1;
                using var sim = LocalSimulation.Create(seed);
                sim.Respawn(OutpostSession.SessionId);
                var day = new OutpostWatch(sim);
                day.Run(ticks);
                day.Finish();
                var failures = day.DayFailures();
                runs++;
                if (failures.Count > 0) failed++;
                Console.WriteLine($"{(failures.Count == 0 ? "PASS" : "FAIL")}  seed {seed,-9} scout t{day.ScoutTick}, gather t{day.GatherTick}, first sale t{day.SaleTick}; " +
                                  $"cycles {day.Crawlers.Sum(c => c.State.Activity.Cycles)}, meals {day.Crawlers.Sum(c => c.State.Activity.Meals)}, " +
                                  $"trust up {day.Crawlers.Count(c => c.TrustEnd > c.TrustStart)}/{day.Crawlers.Count}, peak hunger {day.Crawlers.Max(c => c.PeakHunger):F0}");
                foreach (var failure in failures) Console.WriteLine($"      {failure}");
                if (verbose) Console.Write(day.Report());
            }
            Console.WriteLine($"{runs - failed}/{runs} seeds pass the outpost's day ({ticks} ticks each, {watch.ElapsedMilliseconds} ms)");
            return failed == 0 ? 0 : 1;
        }

        private static int Soak(Options options, CancellationToken ct)
        {
            string profile = options.Text("profile", "all").ToLowerInvariant();
            var profiles = profile == "all" ? EconomySoak.Profiles : new[] { profile };
            var seeds = options.Seeds(EconomySoak.DefaultSeeds);
            long ticks = options.Long("ticks", EconomySoak.HorizonTicks);
            string csv = options.Text("csv");

            int failed = 0, runs = 0;
            var watch = Stopwatch.StartNew();
            foreach (var p in profiles)
            {
                foreach (var seed in seeds)
                {
                    if (ct.IsCancellationRequested) return 1;
                    var result = EconomySoak.Run(seed, p, ticks);
                    var failures = result.Failures();
                    runs++;
                    if (failures.Count > 0) failed++;
                    Console.WriteLine($"{(failures.Count == 0 ? "PASS" : "FAIL")}  {result.Summary()}");
                    foreach (var failure in failures) Console.WriteLine($"      {failure}");
                    if (csv != null) result.WriteCsv(Path.Combine(csv, $"economy-soak-{result.Player.Name}-seed{seed}.csv"));
                }
            }
            Console.WriteLine($"{runs - failed}/{runs} soaks within their pace bands ({watch.ElapsedMilliseconds} ms)");
            return failed == 0 ? 0 : 1;
        }

        private static int Golden(Options options, CancellationToken ct)
        {
            if (options.Verb == "record")
            {
                string output = options.Text("out") ?? throw new ArgumentException("golden record needs --out PATH.");
                if (File.Exists(output)) File.Delete(output);
                foreach (var seed in options.Seeds(new[] { SimulationSettings.DefaultSeed }))
                {
                    if (ct.IsCancellationRequested) return 1;
                    GoldenWorldHash.Write(output, seed, OutpostSession.SessionId, GoldenSamples(seed));
                    Console.WriteLine($"recorded seed {seed} to {output}");
                }
                return 0;
            }
            if (options.Verb != null && options.Verb != "check") throw new ArgumentException($"golden {options.Verb}: use check or record.");

            string path = options.Text("baseline") ?? FindBaseline();
            if (path == null || !File.Exists(path)) throw new ArgumentException($"No baseline file (looked for {GoldenWorldHash.RelativePath} above {Directory.GetCurrentDirectory()}); pass --baseline PATH.");
            var baselines = GoldenWorldHash.Read(path);
            var seeds = new List<ulong>(options.Seeds(null) ?? (IEnumerable<ulong>)baselines.Keys);
            seeds.Sort();
            int failed = 0;
            foreach (var seed in seeds)
            {
                if (ct.IsCancellationRequested) return 1;
                baselines.TryGetValue(seed, out var baseline);
                var watch = Stopwatch.StartNew();
                string difference = GoldenWorldHash.Compare(baseline, OutpostSession.SessionId, GoldenSamples(seed), out long tick);
                if (difference != null) failed++;
                Console.WriteLine(difference == null
                    ? $"PASS  seed {seed}: {baseline.Samples.Count} samples to t{GoldenWorldHash.HorizonTicks} match ({watch.ElapsedMilliseconds} ms)"
                    : $"FAIL  seed {seed} at t{tick}: {difference}");
            }
            Console.WriteLine($"{seeds.Count - failed}/{seeds.Count} seeds match {path}");
            return failed == 0 ? 0 : 1;
        }

        /// <summary>The outpost's world hash every 100 ticks to t3,000, as GoldenWorldHashScenario samples it.</summary>
        private static List<WorldStateSample> GoldenSamples(ulong seed)
        {
            using var sim = LocalSimulation.Create(seed);
            sim.Respawn(OutpostSession.SessionId);
            var samples = new List<WorldStateSample>(32) { WorldStateHash.Capture(sim.Clock.CurrentTick, sim.Host, sim.Simulation, sim.Sectors) };
            sim.Clock.OnSimulationTick += tick =>
            {
                if (tick % GoldenWorldHash.SampleEveryTicks == 0 && tick <= GoldenWorldHash.HorizonTicks)
                    samples.Add(WorldStateHash.Capture(tick, sim.Host, sim.Simulation, sim.Sectors));
            };
            sim.RunTo(GoldenWorldHash.HorizonTicks);
            return samples;
        }

        private static string FindBaseline()
        {
            for (var dir = new DirectoryInfo(Directory.GetCurrentDirectory()); dir != null; dir = dir.Parent)
            {
                string candidate = Path.Combine(dir.FullName, GoldenWorldHash.RelativePath);
                if (File.Exists(candidate)) return candidate;
            }
            return null;
        }
    }
}
