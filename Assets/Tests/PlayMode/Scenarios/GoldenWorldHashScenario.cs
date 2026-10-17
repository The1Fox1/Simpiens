using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Simpiens.Core.Session;
using Simpiens.Simulation;
using Simpiens.Simulation.World;
using Simpiens.Testing.Diagnostics;
using Simpiens.Testing.Environment;
using UnityEngine;
using VContainer;

namespace Simpiens.Tests.PlayMode.Scenarios
{
    /// <summary>
    /// Epic CORE C2: the outpost, respawned with this process's seed, must reproduce its golden world hashes
    /// (<see cref="GoldenWorldHash"/>) at every sample to t3,000. A mechanical refactor that changes behaviour fails here
    /// and names the first tick and what differs. With <c>SIMPIENS_RECORD_GOLDEN=1</c> the run records this seed's
    /// baseline instead (<c>scripts/golden-hashes.sh</c>). A seed without a baseline is inconclusive, except the default
    /// seed, which must have one.
    /// </summary>
    public class GoldenWorldHashScenario : ISimulationScenario
    {
        private const float WallClockTimeoutSeconds = 150f;

        public string Name => "GoldenWorldHashScenario";
        public string SessionId => OutpostSession.SessionId;
        public float DurationSeconds => 0f;
        public float TimeMultiplier => 10f;

        private readonly List<WorldStateSample> _samples = new List<WorldStateSample>(32);
        private ulong _seed;
        private long _endTick;

        public static string BaselinePath => Path.Combine(Path.GetDirectoryName(Application.dataPath), GoldenWorldHash.RelativePath);

        public IEnumerator SetupAsync(IObjectResolver resolver)
        {
            var clock = resolver.Resolve<ISimulationClock>();
            var sim = resolver.Resolve<ISimulationManager>();
            var spawner = resolver.Resolve<SwarmSpawner>();
            var sectors = resolver.Resolve<ISectorRegistry>();
            var settings = resolver.Resolve<SimulationSettings>();
            _seed = settings != null ? settings.Seed : SimulationSettings.DefaultSeed;
            var session = spawner.Find(SessionId);
            Assert.IsNotNull(session, $"Session '{SessionId}' is not registered.");
            if (clock is SimulationClock simClock) simClock.SetTimeMultiplier(TimeMultiplier);

            spawner.Respawn(session); // tick 0, this seed
            _samples.Add(WorldStateHash.Capture(clock.CurrentTick, spawner.Host, sim, sectors));

            // Sample on the tick itself (after the simulation's and the agents' ticks), never on a frame
            void OnTick(long tick)
            {
                if (tick % GoldenWorldHash.SampleEveryTicks == 0 && tick <= GoldenWorldHash.HorizonTicks)
                {
                    _samples.Add(WorldStateHash.Capture(tick, spawner.Host, sim, sectors));
                }
            }

            clock.OnSimulationTick += OnTick;
            float elapsed = 0f;
            try
            {
                while (clock.CurrentTick < GoldenWorldHash.HorizonTicks && elapsed < WallClockTimeoutSeconds)
                {
                    elapsed += Time.unscaledDeltaTime;
                    yield return null;
                }
            }
            finally
            {
                clock.OnSimulationTick -= OnTick;
            }
            _endTick = clock.CurrentTick;
        }

        public void AssertResults(IVisualAnomalyDetector detector)
        {
            Assert.IsTrue(_endTick >= GoldenWorldHash.HorizonTicks, $"The run ended early at t{_endTick} (wall-clock timeout).");
            Assert.AreEqual(GoldenWorldHash.HorizonTicks / GoldenWorldHash.SampleEveryTicks + 1, _samples.Count, "The run missed samples.");

            string path = BaselinePath;
            if (GoldenWorldHash.RecordRequested)
            {
                GoldenWorldHash.Write(path, _seed, SessionId, _samples);
                Debug.Log($"[GoldenWorldHashScenario] recorded seed {_seed}: {_samples.Count} samples to t{GoldenWorldHash.HorizonTicks} in {path}");
                return;
            }

            var all = GoldenWorldHash.Read(path);
            if (!all.TryGetValue(_seed, out var baseline))
            {
                Assert.AreNotEqual(SimulationSettings.DefaultSeed, _seed, $"No golden baseline for the default seed {_seed} in {path}.");
                Assert.Inconclusive($"No golden baseline for seed {_seed}; record one with scripts/golden-hashes.sh record {_seed}.");
            }

            string difference = GoldenWorldHash.Compare(baseline, SessionId, _samples, out long tick);
            Debug.Log(difference == null
                ? $"[GoldenWorldHashScenario] seed {_seed}: identical to the golden baseline at every sample to t{GoldenWorldHash.HorizonTicks}"
                : $"[GoldenWorldHashScenario] seed {_seed}: first difference at t{tick}: {difference}");
            Assert.IsNull(difference, $"Seed {_seed} no longer reproduces its golden world hashes: first difference at t{tick}: {difference}");
        }
    }
}
