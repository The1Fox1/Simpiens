using System.Collections;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using Simpiens.Cognition;
using Simpiens.Core.Session;
using Simpiens.Simulation;
using Simpiens.Simulation.Economy;
using Simpiens.Testing.Diagnostics;
using UnityEngine;
using VContainer;

namespace Simpiens.Tests.PlayMode.Scenarios
{
    /// <summary>
    /// Epic MAP Definition of Done (Phase 5): one live run of the outpost's day. The real session, the default seed,
    /// nothing injected. Over <see cref="HorizonTicks"/> (15 sim-minutes) every crawler harvests salvage and sells or
    /// delivers it, crawlers eat and sleep in bunks, they gather at the hearth, both seeded bounties are fulfilled
    /// (the scout first, with no fog leak), trust toward the player rises for most crawlers at the default (fair) prices,
    /// the ledger balances, nobody starves, and the anomaly detector sees nothing at all. The counts come from each
    /// crawler's <c>AgentActivity</c> tally, recorded by the simulation where each act is applied. Trust follows the
    /// economy model's fairness rule: each crawler judges a price by its own valuation (review §3.3 D3).
    /// </summary>
    public class OutpostLifecycleScenario : ISimulationScenario
    {
        public const uint HorizonTicks = 9000;
        public const int RequiredSleepers = 3;
        public const int RequiredEaters = 3;

        /// <summary>At fair prices, trust toward the player must rise for at least two thirds of the crawlers.</summary>
        public const float RequiredTrustRoseShare = 2f / 3f;
        public const float StarvingHunger = 95f;
        public const uint StarvingTicks = 300;
        private const float WallClockTimeoutSeconds = 400f;

        public string Name => "OutpostLifecycleScenario";
        public string SessionId => OutpostSession.SessionId;
        public float DurationSeconds => 0f;
        public float TimeMultiplier => 10f;

        private sealed class CrawlerWatch
        {
            public AutonomousAgent Agent;
            public int TrustStart;
            public int TrustEnd;
            public float PeakHunger;
            public long HungrySince = -1;
            public long LongestHungry;
            public bool Starved;
        }

        private readonly List<CrawlerWatch> _crawlers = new List<CrawlerWatch>(16);
        private long _observedTicks;
        private long _scoutTick = -1, _gatherTick = -1;
        private bool _scoutCharted, _gatherEscrowReleased;
        private int _fogLeaks;
        private string _firstFogLeak;
        private int _conservationViolations, _worstDiscrepancy;
        private string _report;

        public IEnumerator SetupAsync(IObjectResolver resolver)
        {
            var clock = resolver.Resolve<ISimulationClock>();
            var sim = resolver.Resolve<ISimulationManager>();
            var spawner = resolver.Resolve<SwarmSpawner>();
            var sectors = resolver.Resolve<Simpiens.Simulation.World.ISectorRegistry>();
            Assert.IsNotNull(clock, "ISimulationClock not registered.");
            Assert.IsNotNull(sim, "ISimulationManager not registered.");
            Assert.IsNotNull(spawner, "SwarmSpawner not registered.");
            if (clock is SimulationClock simClock) simClock.SetTimeMultiplier(TimeMultiplier);

            long start = clock.CurrentTick;
            var agents = spawner.Agents;
            for (int i = 0; i < agents.Count; i++)
            {
                var agent = agents[i];
                if (agent == null || agent.State == null) continue;
                _crawlers.Add(new CrawlerWatch { Agent = agent, TrustStart = Trust(agent, (uint)start) });
            }
            Assert.Greater(_crawlers.Count, 0, "The outpost spawned no crawlers.");

            float elapsed = 0f;
            while (elapsed < WallClockTimeoutSeconds)
            {
                long rel = clock.CurrentTick - start;
                _observedTicks = rel;
                Observe(sim, spawner, sectors, rel);
                if (rel >= HorizonTicks) break;
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            uint end = (uint)clock.CurrentTick;
            for (int i = 0; i < _crawlers.Count; i++) _crawlers[i].TrustEnd = Trust(_crawlers[i].Agent, end);
            _report = BuildReport();
        }

        private void Observe(ISimulationManager sim, SwarmSpawner spawner, Simpiens.Simulation.World.ISectorRegistry sectors, long rel)
        {
            // The seeded bounties: the scout charts the Deep Seam, then the Gather bounty is paid from escrow
            if (_scoutTick < 0 && sim.TryGetBounty(OutpostSession.SeedScoutBountyId, out var scout) && scout.Status == BountyStatus.Completed)
            {
                _scoutTick = rel;
                _scoutCharted = sectors != null && sectors.IsExplored(OutpostSession.SeedBountySectorId);
            }
            if (_gatherTick < 0 && sim.TryGetBounty(OutpostSession.SeedGatherBountyId, out var gather) && gather.Status == BountyStatus.Completed)
            {
                _gatherTick = rel;
                _gatherEscrowReleased = sim.EconomyLedger != null && !sim.EconomyLedger.HasEscrow(OutpostSession.SeedGatherBountyId);
            }
            if (_scoutTick < 0) CheckFog(spawner, rel);

            // Hunger: starving means StarvingTicks in a row at StarvingHunger or more
            for (int i = 0; i < _crawlers.Count; i++)
            {
                var c = _crawlers[i];
                float hunger = c.Agent.State.Hunger;
                if (hunger > c.PeakHunger) c.PeakHunger = hunger;
                if (hunger >= StarvingHunger)
                {
                    if (c.HungrySince < 0) c.HungrySince = rel;
                    long stretch = rel - c.HungrySince;
                    if (stretch > c.LongestHungry) c.LongestHungry = stretch;
                    if (stretch >= StarvingTicks) c.Starved = true;
                }
                else
                {
                    c.HungrySince = -1;
                }
            }

            // Ledger conservation: stalls + crawlers + escrow == faucets - sinks
            if (sim.EconomyLedger != null)
            {
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
                if (!sim.EconomyLedger.ValidateConservation(stallGold, agentGold, out int discrepancy))
                {
                    _conservationViolations++;
                    if (Mathf.Abs(discrepancy) > Mathf.Abs(_worstDiscrepancy)) _worstDiscrepancy = discrepancy;
                }
            }
        }

        private void CheckFog(SwarmSpawner spawner, long rel)
        {
            var map = spawner.World?.Map;
            if (map == null) return;
            for (int i = 0; i < _crawlers.Count; i++)
            {
                var memory = _crawlers[i].Agent.Memory;
                if (memory == null) continue;
                foreach (var kvp in memory.SpatialMemoryMap)
                {
                    var record = kvp.Value;
                    if (!Simpiens.Cognition.Memory.AgentMemory.IsLandmark(record.Type)) continue;
                    if (map.FindSector(record.LastKnownLocation).Id != OutpostSession.SeedBountySectorId) continue;
                    _fogLeaks++;
                    _firstFogLeak ??= $"t{rel}: {_crawlers[i].Agent.name} knew {record.Type} at {record.LastKnownLocation}";
                }
            }
        }

        private int RequiredTrustRose => Mathf.CeilToInt(_crawlers.Count * RequiredTrustRoseShare);

        private static int Trust(AutonomousAgent agent, uint tick) =>
            agent.Memory != null ? agent.Memory.GetAffinity(SimulationManager.PlayerEntityId, tick).Trust : 0;

        private string BuildReport()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"[OutpostLifecycleScenario] observed {_observedTicks} ticks; scout@{_scoutTick} (charted {_scoutCharted}), gather@{_gatherTick} (escrow released {_gatherEscrowReleased}); ledger violations {_conservationViolations}; fog leaks {_fogLeaks}");
            int trustRose = 0;
            for (int i = 0; i < _crawlers.Count; i++) if (_crawlers[i].TrustEnd > _crawlers[i].TrustStart) trustRose++;
            sb.AppendLine($"  trust toward the player rose for {trustRose} of {_crawlers.Count} crawlers (need {RequiredTrustRose})");
            for (int i = 0; i < _crawlers.Count; i++)
            {
                var c = _crawlers[i];
                sb.AppendLine($"  {c.Agent.name}: {c.Agent.State.Activity}; trust {c.TrustStart} -> {c.TrustEnd}; peak hunger {c.PeakHunger:F0}, longest at >= {StarvingHunger:F0}: {c.LongestHungry} ticks{(c.Starved ? " STARVED" : "")}");
            }
            return sb.ToString();
        }

        public void AssertResults(IVisualAnomalyDetector detector)
        {
            Debug.Log(_report);
            var failures = new List<string>();

            if (_observedTicks < HorizonTicks) failures.Add($"Observation ended early at tick {_observedTicks} (wall-clock timeout).");

            var noCycle = new List<string>();
            int sleepers = 0, eaters = 0, gatherings = 0, trustRose = 0;
            var starved = new List<string>();
            for (int i = 0; i < _crawlers.Count; i++)
            {
                var c = _crawlers[i];
                var a = c.Agent.State.Activity;
                if (a.Cycles == 0) noCycle.Add(c.Agent.name);
                if (a.BunkSleeps > 0) sleepers++;
                if (a.Meals > 0) eaters++;
                gatherings += a.HearthGatherings;
                if (c.TrustEnd > c.TrustStart) trustRose++;
                if (c.Starved) starved.Add($"{c.Agent.name} ({c.LongestHungry} ticks)");
            }

            if (noCycle.Count > 0) failures.Add($"{noCycle.Count} of {_crawlers.Count} crawlers never closed a harvest -> sell cycle: {string.Join(", ", noCycle)}.");
            if (sleepers < RequiredSleepers) failures.Add($"Only {sleepers} crawlers slept in a bunk (need {RequiredSleepers}).");
            if (eaters < RequiredEaters) failures.Add($"Only {eaters} crawlers ate (need {RequiredEaters}).");
            if (gatherings == 0) failures.Add("No hearth gathering (2 or more crawlers at the hearth together).");
            if (_scoutTick < 0 || !_scoutCharted) failures.Add($"The seeded Scout bounty was not fulfilled with the Deep Seam charted (completed at {_scoutTick}, charted {_scoutCharted}).");
            if (_fogLeaks > 0) failures.Add($"Crawlers knew Deep Seam landmarks before the scout reported ({_fogLeaks} observations; first {_firstFogLeak}).");
            if (_gatherTick < 0 || !_gatherEscrowReleased) failures.Add($"The seeded Gather bounty was not fulfilled and paid from escrow (completed at {_gatherTick}, escrow released {_gatherEscrowReleased}).");
            if (trustRose < RequiredTrustRose) failures.Add($"Trust toward the player rose for only {trustRose} of {_crawlers.Count} crawlers at fair prices (need {RequiredTrustRose}).");
            if (detector.TotalAnomalies > 0) failures.Add($"{detector.TotalAnomalies} anomalies (jitter {detector.JitterCount}, stacking {detector.StackingCount}, penetration {detector.PenetrationCount}, thrashing {detector.ThrashingCount}, breach {detector.BoundaryBreachCount}).");
            if (_conservationViolations > 0) failures.Add($"Ledger conservation violated on {_conservationViolations} observations (worst {_worstDiscrepancy}g).");
            if (starved.Count > 0) failures.Add($"{starved.Count} crawlers starved (hunger >= {StarvingHunger} for {StarvingTicks} ticks): {string.Join(", ", starved)}.");

            Assert.IsTrue(failures.Count == 0, "Outpost lifecycle failures:\n  " + string.Join("\n  ", failures));
        }
    }
}
