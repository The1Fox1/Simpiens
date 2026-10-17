using System.Collections;
using System.Collections.Generic;
using System.IO;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using Simpiens.Core.Session;
using Simpiens.Simulation;
using Simpiens.Simulation.Economy;
using Simpiens.Simulation.Persistence;
using Simpiens.Simulation.World;
using Simpiens.Testing.Diagnostics;
using Simpiens.Testing.Environment;
using UnityEngine;
using VContainer;

namespace Simpiens.Tests.PlayMode.Scenarios
{
    /// <summary>
    /// The live Definition of Done for save and load (Milestone 1 completion plan, step 5), on the outpost with nothing
    /// injected. The outpost plays until a crawler has claimed the seeded Gather bounty, and is saved to a file through
    /// the quick-save slot at that tick. A fresh session runs; then the file is loaded. The loaded game is the saved one:
    /// the file re-encoded from the loaded session is byte for byte the file on disk, and the world hash (without work in
    /// progress) matches. The loaded outpost then plays <see cref="PlayTicks"/> more: the anomaly detector sees nothing,
    /// the ledger balances throughout, nobody starves and every crawler eats, a harvest-to-sell cycle completes, and the
    /// claimed bounty is delivered and paid from escrow by its claimant, who had to pick that work up again after the load.
    /// </summary>
    public class SaveLoadScenario : ISimulationScenario
    {
        public const long LatestSaveTick = 2000;
        public const long FreshTicks = 100;
        public const long PlayTicks = 3000;
        public const float StarvingHunger = 95f;
        public const uint StarvingTicks = 300;
        private const float WallClockTimeoutSeconds = 120f;

        public string Name => "SaveLoadScenario";
        public string SessionId => OutpostSession.SessionId;
        public float DurationSeconds => 0f;
        public float TimeMultiplier => 10f;

        private sealed class CrawlerWatch
        {
            public Simpiens.Cognition.AutonomousAgent Agent;
            public int MealsAtSave;
            public int CyclesAtSave;
            public long HungrySince = -1;
            public long LongestHungry;
        }

        private readonly List<CrawlerWatch> _crawlers = new List<CrawlerWatch>(16);
        private string _directory;
        private SaveOutcome _saved;
        private SaveOutcome _loaded;
        private long _saveTick = -1;
        private BountyContract _gatherAtSave;
        private WorldStateSample _savedHash;
        private WorldStateSample _loadedHash;
        private bool _fileMatches;
        private string _fileDifference;
        private long _loadTick = -1;
        private long _endTick = -1;
        private long _gatherDeliveredTick = -1;
        private bool _gatherEscrowReleased;
        private int _conservationViolations;
        private int _worstDiscrepancy;
        private int _anomaliesAtLoad = -1;

        public IEnumerator SetupAsync(IObjectResolver resolver)
        {
            var clock = resolver.Resolve<ISimulationClock>();
            var sim = resolver.Resolve<ISimulationManager>();
            var spawner = resolver.Resolve<SwarmSpawner>();
            var sectors = resolver.Resolve<ISectorRegistry>();
            var detector = resolver.Resolve<IVisualAnomalyDetector>();
            Assert.IsNotNull(clock, "ISimulationClock not registered.");
            Assert.IsNotNull(spawner, "SwarmSpawner not registered.");

            // The scenario's own slot: a test never touches the player's quick save
            _directory = Path.Combine(Application.temporaryCachePath, "SaveLoadScenario");
            if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
            var slot = new SaveGameService(spawner, _directory);
            clock.SetTimeMultiplier(TimeMultiplier);

            // 1. Play until a crawler holds the seeded Gather bounty, and save at that tick
            float elapsed = 0f;
            while (elapsed < WallClockTimeoutSeconds && clock.CurrentTick < LatestSaveTick && !GatherClaimed(sim))
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
            _saveTick = clock.CurrentTick;
            sim.TryGetBounty(OutpostSession.SeedGatherBountyId, out _gatherAtSave);
            _savedHash = WorldStateHash.Capture(_saveTick, spawner.Agents, sim, sectors, includeWork: false);
            var saving = slot.QuickSaveAsync(); // captures now, between ticks; the file is written off the main thread
            yield return saving.ToCoroutine(outcome => _saved = outcome);
            if (!_saved.Succeeded) yield break;

            // 2. A fresh session with its own history, then the load
            spawner.Respawn(spawner.Find(SessionId));
            yield return RunFor(clock, FreshTicks);
            _loaded = slot.QuickLoad();
            if (!_loaded.Succeeded) yield break;
            _loadTick = clock.CurrentTick;
            _loadedHash = WorldStateHash.Capture(_loadTick, spawner.Agents, sim, sectors, includeWork: false);
            byte[] onDisk = File.ReadAllBytes(slot.QuickSavePath);
            byte[] reEncoded = SaveFile.Encode(spawner.Capture());
            _fileMatches = SameBytes(onDisk, reEncoded, out _fileDifference);
            _anomaliesAtLoad = detector != null ? detector.TotalAnomalies : 0;

            var agents = spawner.Agents;
            for (int i = 0; i < agents.Count; i++)
            {
                var state = agents[i] != null ? agents[i].State : null;
                if (state == null) continue;
                _crawlers.Add(new CrawlerWatch { Agent = agents[i], MealsAtSave = state.Activity.Meals, CyclesAtSave = state.Activity.Cycles });
            }

            // 3. The loaded outpost plays on
            elapsed = 0f;
            while (elapsed < WallClockTimeoutSeconds && clock.CurrentTick < _loadTick + PlayTicks)
            {
                Observe(sim, spawner, clock.CurrentTick);
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
            Observe(sim, spawner, clock.CurrentTick);
            _endTick = clock.CurrentTick;
            if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
        }

        private static bool GatherClaimed(ISimulationManager sim)
        {
            return sim.TryGetBounty(OutpostSession.SeedGatherBountyId, out var gather) && gather.Status == BountyStatus.Claimed;
        }

        private void Observe(ISimulationManager sim, SwarmSpawner spawner, long tick)
        {
            if (_gatherDeliveredTick < 0 && sim.TryGetBounty(OutpostSession.SeedGatherBountyId, out var gather) && gather.Status == BountyStatus.Completed)
            {
                _gatherDeliveredTick = tick;
                _gatherEscrowReleased = sim.EconomyLedger != null && !sim.EconomyLedger.HasEscrow(OutpostSession.SeedGatherBountyId);
            }

            for (int i = 0; i < _crawlers.Count; i++)
            {
                var c = _crawlers[i];
                if (c.Agent == null || c.Agent.State == null) continue;
                if (c.Agent.State.Hunger >= StarvingHunger)
                {
                    if (c.HungrySince < 0) c.HungrySince = tick;
                    if (tick - c.HungrySince > c.LongestHungry) c.LongestHungry = tick - c.HungrySince;
                }
                else
                {
                    c.HungrySince = -1;
                }
            }

            if (sim.EconomyLedger != null)
            {
                uint stallGold = 0;
                for (int i = 0; i < sim.ActiveStalls.Count; i++) stallGold += sim.ActiveStalls[i].TreasuryGold;
                uint agentGold = 0;
                var agents = spawner.Agents;
                for (int i = 0; i < agents.Count; i++)
                {
                    var inv = agents[i] != null ? agents[i].State?.Inventory : null;
                    if (inv != null) agentGold += (uint)inv.Gold;
                }
                if (!sim.EconomyLedger.ValidateConservation(stallGold, agentGold, out int discrepancy))
                {
                    _conservationViolations++;
                    if (Mathf.Abs(discrepancy) > Mathf.Abs(_worstDiscrepancy)) _worstDiscrepancy = discrepancy;
                }
            }
        }

        private static IEnumerator RunFor(ISimulationClock clock, long ticks)
        {
            long until = clock.CurrentTick + ticks;
            float elapsed = 0f;
            while (clock.CurrentTick < until && elapsed < WallClockTimeoutSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        private static bool SameBytes(byte[] a, byte[] b, out string difference)
        {
            difference = null;
            if (a.Length != b.Length) difference = $"{a.Length} vs {b.Length} bytes";
            int n = Mathf.Min(a.Length, b.Length);
            for (int i = 0; i < n && difference == null; i++)
            {
                if (a[i] != b[i]) difference = $"first difference at byte {i} of {a.Length}";
            }
            return difference == null;
        }

        public void AssertResults(IVisualAnomalyDetector detector)
        {
            int ate = 0;
            int cyclesAfter = 0;
            var hungry = new List<string>();
            var noMeal = new List<string>();
            for (int i = 0; i < _crawlers.Count; i++)
            {
                var c = _crawlers[i];
                var activity = c.Agent.State.Activity;
                if (activity.Meals > c.MealsAtSave) ate++;
                else noMeal.Add(c.Agent.name);
                cyclesAfter += activity.Cycles - c.CyclesAtSave;
                if (c.LongestHungry >= StarvingTicks) hungry.Add($"{c.Agent.name} ({c.LongestHungry} ticks)");
            }

            Debug.Log($"[SaveLoadScenario] saved t{_saveTick} with the Gather bounty {_gatherAtSave.Status} by {Simpiens.Diagnostics.SimLog.Agent(_gatherAtSave.ClaimantId)}; " +
                      $"{_saved.Message} {_loaded.Message} Played to t{_endTick}: delivered t{_gatherDeliveredTick}, {cyclesAfter} cycles, {ate}/{_crawlers.Count} crawlers ate, " +
                      $"ledger violations {_conservationViolations}, anomalies {detector.TotalAnomalies}");

            Assert.AreEqual(BountyStatus.Claimed, _gatherAtSave.Status, $"No crawler claimed the Gather bounty by t{LatestSaveTick}: nothing was in progress at the save.");
            Assert.IsTrue(_saved.Succeeded, $"The quick save failed: {_saved.Message}");
            Assert.AreEqual(_saveTick, _saved.Tick, "The save holds the tick it was taken at.");
            Assert.IsTrue(_loaded.Succeeded, $"The quick load was refused: {_loaded.Message}");
            Assert.AreEqual(_saveTick, _loadTick, "The clock resumes at the saved tick.");
            Assert.IsTrue(_fileMatches, $"The loaded session re-encodes differently from the file on disk: {_fileDifference}.");
            string hashDifference = _savedHash.DescribeDifference(_loadedHash);
            Assert.IsNull(hashDifference, $"The world hash differs after the load: {hashDifference}");

            Assert.GreaterOrEqual(_endTick, _loadTick + PlayTicks, $"The loaded outpost stopped at t{_endTick} (wall-clock timeout).");
            Assert.AreEqual(0, detector.TotalAnomalies, $"{detector.TotalAnomalies} anomalies ({_anomaliesAtLoad} before the load): jitter {detector.JitterCount}, stacking {detector.StackingCount}, penetration {detector.PenetrationCount}, thrashing {detector.ThrashingCount}, breach {detector.BoundaryBreachCount}.");
            Assert.AreEqual(0, _conservationViolations, $"The ledger was out of balance on {_conservationViolations} frames after the load (worst {_worstDiscrepancy}g).");
            Assert.IsEmpty(hungry, $"Crawlers starved after the load (hunger >= {StarvingHunger} for {StarvingTicks} ticks): {string.Join(", ", hungry)}.");
            Assert.IsEmpty(noMeal, $"Crawlers that never ate after the load: {string.Join(", ", noMeal)}.");
            Assert.Greater(cyclesAfter, 0, "No harvest-to-sell cycle completed after the load.");
            Assert.Greater(_gatherDeliveredTick, _loadTick, "The Gather bounty claimed before the save was not delivered after the load.");
            Assert.IsTrue(_gatherEscrowReleased, "The delivered bounty's reward was not paid from escrow.");
        }
    }
}
