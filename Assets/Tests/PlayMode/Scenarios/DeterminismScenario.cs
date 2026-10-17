using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Simpiens.Core.Session;
using Simpiens.Simulation;
using Simpiens.Simulation.Economy;
using Simpiens.Simulation.World;
using Simpiens.Testing.Diagnostics;
using Simpiens.Testing.Environment;
using UnityEngine;
using VContainer;

namespace Simpiens.Tests.PlayMode.Scenarios
{
    /// <summary>
    /// Epic MAP Phase 5 determinism check (producer decision D2 = A: assert the strongest property that holds in 3 of 3
    /// full runs). The live outpost runs twice from a respawn with the same seed, and the world is hashed every
    /// <see cref="SampleEveryTicks"/> ticks up to <see cref="HorizonTicks"/> (<see cref="WorldStateHash"/>). The two
    /// runs must be identical at every sample; a failure names the first tick and what differs.
    /// Decisions land on a fixed tick (the CognitionScheduler, ROADMAP §1A.2 #6, resolved by Epic CORE C1), so this holds
    /// alone or inside the suite, on a quiet or a busy machine.
    /// </summary>
    public class DeterminismScenario : ISimulationScenario
    {
        public const long HorizonTicks = 3000;
        public const long SampleEveryTicks = 100;
        private const float WallClockTimeoutSeconds = 150f;

        public string Name => "DeterminismScenario";
        public string SessionId => OutpostSession.SessionId;
        public float DurationSeconds => 0f;
        public float TimeMultiplier => 10f;

        private sealed class Run
        {
            public readonly List<WorldStateSample> Samples = new List<WorldStateSample>(32);
            public long ScoutTick = -1, GatherTick = -1, FirstCycleTick = -1, ThirdSleeperTick = -1;
            public long EndTick;
        }

        private readonly Run[] _runs = { new Run(), new Run() };
        private string _report;

        public IEnumerator SetupAsync(IObjectResolver resolver)
        {
            var clock = resolver.Resolve<ISimulationClock>();
            var sim = resolver.Resolve<ISimulationManager>();
            var spawner = resolver.Resolve<SwarmSpawner>();
            var sectors = resolver.Resolve<ISectorRegistry>();
            Assert.IsNotNull(clock, "ISimulationClock not registered.");
            Assert.IsNotNull(spawner, "SwarmSpawner not registered.");
            var session = spawner.Find(SessionId);
            Assert.IsNotNull(session, $"Session '{SessionId}' is not registered.");
            if (clock is SimulationClock simClock) simClock.SetTimeMultiplier(TimeMultiplier);

            for (int r = 0; r < _runs.Length; r++)
            {
                var run = _runs[r];
                spawner.Respawn(session); // rewinds the clock to tick 0 with the same seed
                run.Samples.Add(WorldStateHash.Capture(clock.CurrentTick, spawner.Host, sim, sectors));

                // Sample on the tick itself (after the simulation's own tick handler), never on a frame
                void OnTick(long tick)
                {
                    if (run.ScoutTick < 0 && sim.TryGetBounty(OutpostSession.SeedScoutBountyId, out var s) && s.Status == BountyStatus.Completed) run.ScoutTick = tick;
                    if (run.GatherTick < 0 && sim.TryGetBounty(OutpostSession.SeedGatherBountyId, out var g) && g.Status == BountyStatus.Completed) run.GatherTick = tick;
                    if (tick % SampleEveryTicks == 0 && tick <= HorizonTicks) run.Samples.Add(WorldStateHash.Capture(tick, spawner.Host, sim, sectors));
                }

                clock.OnSimulationTick += OnTick;
                float elapsed = 0f;
                try
                {
                    while (clock.CurrentTick < HorizonTicks && elapsed < WallClockTimeoutSeconds)
                    {
                        elapsed += Time.unscaledDeltaTime;
                        yield return null;
                    }
                }
                finally
                {
                    clock.OnSimulationTick -= OnTick;
                }
                run.EndTick = clock.CurrentTick;

                // Milestones from the activity tally
                var sleeps = new List<long>(16);
                var agents = spawner.Agents;
                for (int i = 0; i < agents.Count; i++)
                {
                    if (agents[i] == null || agents[i].State == null) continue;
                    var a = agents[i].State.Activity;
                    if (a.FirstCycleTick >= 0 && (run.FirstCycleTick < 0 || a.FirstCycleTick < run.FirstCycleTick)) run.FirstCycleTick = a.FirstCycleTick;
                    if (a.FirstSleepTick >= 0) sleeps.Add(a.FirstSleepTick);
                }
                sleeps.Sort();
                if (sleeps.Count >= 3) run.ThirdSleeperTick = sleeps[2];
            }
            _report = BuildReport();
        }

        private string BuildReport()
        {
            var sb = new System.Text.StringBuilder("[DeterminismScenario]");
            for (int r = 1; r < _runs.Length; r++)
            {
                for (int q = 0; q < r; q++)
                {
                    string d = Compare(_runs[q], _runs[r], out long tick);
                    sb.Append($" | run {q}-{r}: ").Append(d == null ? "identical at every sample" : $"first divergence at t{tick}: {d}");
                    if (q == 0 && r == 1) { FirstDivergenceTick = tick; Identical = d == null; }
                }
            }
            for (int r = 0; r < _runs.Length; r++)
            {
                var x = _runs[r];
                sb.Append($" | run {r} milestones: first cycle {x.FirstCycleTick}, scout {x.ScoutTick}, gather {x.GatherTick}, third sleeper {x.ThirdSleeperTick}");
            }
            return sb.ToString();
        }

        private static string Compare(Run a, Run b, out long tick)
        {
            tick = -1;
            int n = SimMath.Min(a.Samples.Count, b.Samples.Count);
            for (int i = 0; i < n; i++)
            {
                if (a.Samples[i].Tick != b.Samples[i].Tick) { tick = a.Samples[i].Tick; return $"sample ticks differ ({a.Samples[i].Tick} vs {b.Samples[i].Tick})"; }
                string what = a.Samples[i].DescribeDifference(b.Samples[i]);
                if (what != null) { tick = a.Samples[i].Tick; return what; }
            }
            return a.Samples.Count == b.Samples.Count ? null : "sample counts differ";
        }

        public long FirstDivergenceTick { get; private set; } = -1;
        public bool Identical { get; private set; }

        public void AssertResults(IVisualAnomalyDetector detector)
        {
            Debug.Log(_report);
            var a = _runs[0];
            var b = _runs[1];
            Assert.IsTrue(a.EndTick >= HorizonTicks && b.EndTick >= HorizonTicks, $"A run ended early (t{a.EndTick}, t{b.EndTick}; wall-clock timeout).");
            Assert.AreEqual(HorizonTicks / SampleEveryTicks + 1, a.Samples.Count, "Run A missed samples.");
            Assert.IsTrue(Identical, $"Same seed, different world: first divergence at t{FirstDivergenceTick}. {_report}");
        }
    }
}
