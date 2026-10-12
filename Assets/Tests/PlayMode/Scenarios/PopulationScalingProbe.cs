using Simpiens.Core.Session;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using NUnit.Framework;
using Simpiens.Simulation;
using Simpiens.Testing.Diagnostics;
using Simpiens.Testing.Environment;
using UnityEngine;
using VContainer;

namespace Simpiens.Tests.PlayMode.Scenarios
{
    /// <summary>
    /// Measurement only (Epic MAP Phase 3, plan §2.5): the outpost on Floor 1 with 12, 16 and 24 crawlers. For each
    /// population it records cognition latency at the normal test speed (10x) and raw throughput with the clock
    /// unthrottled. Asserts integrity only (ledger conserved, ticks reached). Results: Logs/population-scaling.csv.
    ///
    /// Latency is the span of one evaluation (AgentState.IsEvaluating) in simulation ticks, sampled once per frame.
    /// Most evaluations start and finish inside one frame and are never seen in flight, so the latency columns only
    /// describe the slow ones that span frames (eval_count says how many were caught). Throughput is measured after
    /// a warm-up, so the respawn hitch is not counted.
    /// </summary>
    public class PopulationScalingProbe : ISimulationScenario
    {
        public static readonly int[] Populations = { 12, 16, 24 };
        public const uint LatencyTicks = 600;     // at 10x: one sim-minute
        public const uint ThroughputTicks = 20000; // unthrottled
        private const int WarmupFrames = 10;
        public const float ThroughputMultiplier = 400f;
        public const string ResultsPath = "Logs/population-scaling.csv";
        private const float WallClockTimeoutSeconds = 240f;

        public string Name => "PopulationScalingProbe";
        public string SessionId => OutpostSession.SessionId;
        public float DurationSeconds => 0f;
        public float TimeMultiplier => 10f;

        private readonly StringBuilder _csv = new StringBuilder("crawlers,evals_caught_in_flight,latency_mean_ticks,latency_p95_ticks,latency_max_ticks,throughput_ticks_per_s,throughput_wall_s,ledger_ok\n");
        private readonly List<string> _failures = new List<string>();

        public IEnumerator SetupAsync(IObjectResolver resolver)
        {
            var clock = resolver.Resolve<ISimulationClock>() as SimulationClock;
            var sim = resolver.Resolve<ISimulationManager>();
            var spawner = resolver.Resolve<SwarmSpawner>();
            Assert.IsNotNull(clock, "SimulationClock not registered.");
            Assert.IsNotNull(spawner, "SwarmSpawner not registered.");

            for (int p = 0; p < Populations.Length; p++)
            {
                int n = Populations[p];
                spawner.Respawn(new OutpostSession(n));
                clock.SetTimeMultiplier(TimeMultiplier);

                // 1. Latency at the normal speed
                var spans = new List<long>(512);
                var since = new Dictionary<UnityEngine.GUID, long>();
                long start = clock.CurrentTick;
                float elapsed = 0f;
                while (clock.CurrentTick - start < LatencyTicks && elapsed < WallClockTimeoutSeconds)
                {
                    var agents = spawner.Agents;
                    for (int i = 0; i < agents.Count; i++)
                    {
                        var state = agents[i] != null ? agents[i].State : null;
                        if (state == null) continue;
                        if (state.IsEvaluating)
                        {
                            if (!since.ContainsKey(state.AgentId)) since[state.AgentId] = clock.CurrentTick;
                        }
                        else if (since.TryGetValue(state.AgentId, out long began))
                        {
                            spans.Add(clock.CurrentTick - began);
                            since.Remove(state.AgentId);
                        }
                    }
                    elapsed += Time.unscaledDeltaTime;
                    yield return null;
                }

                // 2. Throughput, unthrottled
                clock.SetTimeMultiplier(ThroughputMultiplier);
                for (int w = 0; w < WarmupFrames; w++) yield return null;
                long tStart = clock.CurrentTick;
                float wall = 0f;
                while (clock.CurrentTick - tStart < ThroughputTicks && wall < WallClockTimeoutSeconds)
                {
                    wall += Time.unscaledDeltaTime;
                    yield return null;
                }
                long advanced = clock.CurrentTick - tStart;
                float throughput = wall > 0f ? advanced / wall : 0f;
                clock.SetTimeMultiplier(TimeMultiplier);

                bool ledgerOk = LedgerConserved(sim, spawner);
                if (!ledgerOk) _failures.Add($"{n} crawlers: ledger not conserved");
                if (advanced < ThroughputTicks) _failures.Add($"{n} crawlers: only {advanced} of {ThroughputTicks} ticks in {WallClockTimeoutSeconds}s");

                spans.Sort();
                double mean = 0;
                for (int i = 0; i < spans.Count; i++) mean += spans[i];
                mean = spans.Count > 0 ? mean / spans.Count : 0;
                long p95 = spans.Count > 0 ? spans[Mathf.Min(spans.Count - 1, (int)(spans.Count * 0.95f))] : 0;
                long max = spans.Count > 0 ? spans[spans.Count - 1] : 0;
                _csv.Append($"{n},{spans.Count},{mean:F2},{p95},{max},{throughput:F0},{wall:F1},{ledgerOk}\n");
                Debug.Log($"[PopulationScalingProbe] {n} crawlers: {spans.Count} evaluations caught in flight, latency mean {mean:F2} / p95 {p95} / max {max} ticks; throughput {throughput:F0} ticks/s ({advanced} ticks in {wall:F1}s)");
            }

            Directory.CreateDirectory(Path.GetDirectoryName(ResultsPath));
            File.WriteAllText(ResultsPath, _csv.ToString());

            // Leave the default session loaded for whatever runs next
            spawner.Respawn(new OutpostSession());
        }

        private static bool LedgerConserved(ISimulationManager sim, SwarmSpawner spawner)
        {
            if (sim?.EconomyLedger == null) return true;
            uint stallGold = 0;
            var stalls = sim.ActiveStalls;
            for (int i = 0; i < stalls.Count; i++) stallGold += stalls[i].TreasuryGold;
            uint agentGold = 0;
            var agents = spawner.Agents;
            for (int i = 0; i < agents.Count; i++)
            {
                var inv = agents[i] != null ? agents[i].State?.Inventory : null;
                if (inv != null) agentGold += (uint)inv.Gold;
            }
            return sim.EconomyLedger.ValidateConservation(stallGold, agentGold, out _);
        }

        public void AssertResults(IVisualAnomalyDetector detector)
        {
            Assert.IsTrue(_failures.Count == 0, "Population scaling probe integrity failures:\n  " + string.Join("\n  ", _failures));
        }
    }
}
