using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Simpiens.Core.Session;
using Simpiens.Simulation;
using Simpiens.Simulation.Persistence;
using Simpiens.Simulation.World;
using Simpiens.Testing.Diagnostics;
using Simpiens.Testing.Environment;
using UnityEngine;
using VContainer;

namespace Simpiens.Tests.PlayMode.Scenarios
{
    /// <summary>
    /// Milestone 1 completion plan, step 3: the live outpost loads back exactly. At <see cref="SaveTick"/> the session is
    /// captured; a fresh session runs for a while; the capture is loaded over it. Capturing again gives the same bytes,
    /// the world hash (without work in progress) matches, and the clock and id counter resume at the save. Saves made
    /// for another format, session, seed or map, or missing a block, are refused with a reason and change nothing.
    /// Then the loaded outpost plays on: every crawler takes up new work.
    /// </summary>
    public class SessionRestoreScenario : ISimulationScenario
    {
        public const long SaveTick = 600;
        public const long FreshTicks = 50;
        public const long TicksAfterLoad = 200;
        private const float WallClockTimeoutSeconds = 90f;

        public string Name => "SessionRestoreScenario";
        public string SessionId => OutpostSession.SessionId;
        public float DurationSeconds => 0f;
        public float TimeMultiplier => 10f;

        private byte[] _savedBytes;
        private byte[] _loadedBytes;
        private WorldStateSample _savedHash;
        private WorldStateSample _loadedHash;
        private long _savedTick = -1;
        private long _savedCounter = -1;
        private long _loadedTick = -1;
        private long _loadedCounter = -1;
        private bool _loaded;
        private string _loadRefusal;
        private readonly List<string> _refusalFailures = new List<string>();
        private readonly List<string> _refusals = new List<string>();
        private long _endTick = -1;
        private int _crawlers;
        private int _crawlersWithWork;

        public IEnumerator SetupAsync(IObjectResolver resolver)
        {
            var clock = resolver.Resolve<ISimulationClock>();
            var sim = resolver.Resolve<ISimulationManager>();
            var spawner = resolver.Resolve<SwarmSpawner>();
            var sectors = resolver.Resolve<ISectorRegistry>();
            var ids = resolver.Resolve<IEntityIdAllocator>();
            Assert.IsNotNull(clock, "ISimulationClock not registered.");
            Assert.IsNotNull(spawner, "SwarmSpawner not registered.");
            clock.SetTimeMultiplier(TimeMultiplier);

            yield return RunUntil(clock, SaveTick);
            _savedTick = clock.CurrentTick;
            _savedCounter = ids.Counter;
            _savedBytes = SaveSerializer.Serialize(spawner.Capture());
            _savedHash = WorldStateHash.Capture(_savedTick, spawner.Agents, sim, sectors, includeWork: false);

            // A fresh session with its own short history, then the load over it
            spawner.Respawn(spawner.Find(SessionId));
            yield return RunUntil(clock, FreshTicks);

            CheckRefusals(spawner, clock);

            _loaded = spawner.TryRestore(SaveSerializer.Deserialize<SessionSave>(_savedBytes), out _loadRefusal);
            if (!_loaded) yield break;
            _loadedTick = clock.CurrentTick;
            _loadedCounter = ids.Counter;
            _loadedBytes = SaveSerializer.Serialize(spawner.Capture());
            _loadedHash = WorldStateHash.Capture(_loadedTick, spawner.Agents, sim, sectors, includeWork: false);

            // The loaded outpost plays on
            yield return RunUntil(clock, _loadedTick + TicksAfterLoad);
            _endTick = clock.CurrentTick;
            var agents = spawner.Agents;
            for (int i = 0; i < agents.Count; i++)
            {
                var state = agents[i] != null ? agents[i].State : null;
                if (state == null) continue;
                _crawlers++;
                if (state.LastDecision.Tick > (uint)_loadedTick) _crawlersWithWork++;
            }
        }

        /// <summary>Each broken save is refused with a reason, and the running session is left exactly as it was.</summary>
        private void CheckRefusals(SwarmSpawner spawner, ISimulationClock clock)
        {
            long tick = clock.CurrentTick;
            byte[] before = SaveSerializer.Serialize(spawner.Capture());

            TryBroken("another format version", s => s.Header.FormatVersion = SessionSaveHeader.CurrentFormatVersion + 1);
            TryBroken("an unknown session", s => s.Header.SessionId = "no-such-session");
            TryBroken("another seed", s => s.Header.Seed = s.Header.Seed + 1);
            TryBroken("another map", s => s.Header.MapFingerprint = s.Header.MapFingerprint ^ 0xFFUL);
            TryBroken("a missing block", s => s.World = null);

            byte[] after = SaveSerializer.Serialize(spawner.Capture());
            if (clock.CurrentTick != tick || !SameBytes(before, after)) _refusalFailures.Add("a refused load changed the running session");

            void TryBroken(string what, System.Action<SessionSave> breakIt)
            {
                var broken = SaveSerializer.Deserialize<SessionSave>(_savedBytes);
                breakIt(broken);
                if (spawner.TryRestore(broken, out string reason)) _refusalFailures.Add($"a save with {what} was loaded");
                else if (string.IsNullOrEmpty(reason)) _refusalFailures.Add($"a save with {what} was refused without a reason");
                else _refusals.Add($"{what}: {reason}");
            }
        }

        private static IEnumerator RunUntil(ISimulationClock clock, long tick)
        {
            float elapsed = 0f;
            while (clock.CurrentTick < tick && elapsed < WallClockTimeoutSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        private static bool SameBytes(byte[] a, byte[] b)
        {
            if (a == null || b == null || a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++)
            {
                if (a[i] != b[i]) return false;
            }
            return true;
        }

        private static int FirstDifference(byte[] a, byte[] b)
        {
            int n = Mathf.Min(a.Length, b.Length);
            for (int i = 0; i < n; i++)
            {
                if (a[i] != b[i]) return i;
            }
            return n;
        }

        public void AssertResults(IVisualAnomalyDetector detector)
        {
            Debug.Log($"[SessionRestoreScenario] saved t{_savedTick} ({_savedBytes?.Length ?? 0} bytes), loaded at t{_loadedTick}; " +
                      $"refusals: {string.Join(" | ", _refusals)}; after {TicksAfterLoad} ticks {_crawlersWithWork}/{_crawlers} crawlers decided afresh");

            Assert.GreaterOrEqual(_savedTick, SaveTick, "The outpost didn't reach the save tick (wall-clock timeout).");
            Assert.IsTrue(_refusalFailures.Count == 0, "Refusals: " + string.Join("; ", _refusalFailures));
            Assert.IsTrue(_loaded, $"The save was refused: {_loadRefusal}");
            Assert.AreEqual(_savedTick, _loadedTick, "The clock resumes at the saved tick.");
            Assert.AreEqual(_savedCounter, _loadedCounter, "The id counter resumes at the saved count.");

            if (!SameBytes(_savedBytes, _loadedBytes))
            {
                Assert.Fail($"The loaded session captures differently: {_savedBytes.Length} vs {_loadedBytes.Length} bytes, first difference at byte {FirstDifference(_savedBytes, _loadedBytes)}.");
            }
            string hashDifference = _savedHash.DescribeDifference(_loadedHash);
            Assert.IsNull(hashDifference, $"The world hash differs after the load: {hashDifference}");

            Assert.GreaterOrEqual(_endTick, _loadedTick + TicksAfterLoad, "The loaded outpost stopped (wall-clock timeout).");
            Assert.AreEqual(_crawlers, _crawlersWithWork, $"Only {_crawlersWithWork} of {_crawlers} crawlers decided afresh after the load.");
        }
    }
}
