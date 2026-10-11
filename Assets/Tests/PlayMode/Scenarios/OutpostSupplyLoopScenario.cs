using Simpiens.Core.Session;
using System.Collections;
using NUnit.Framework;
using Simpiens.Diagnostics;
using Simpiens.Simulation;
using Simpiens.Simulation.Economy;
using Simpiens.Testing.Diagnostics;
using Simpiens.Testing.Environment;
using Simpiens.UI.Zones;
using UnityEngine;
using VContainer;

namespace Simpiens.Tests.PlayMode.Scenarios
{
    /// <summary>
    /// Definition-of-Done scenario for §3.1 (Close the Milestone 1 Supply Side).
    /// Runs the real session (SwarmSpawner.Respawn) at 10× and injects nothing: no inventory, teleports,
    /// forced intents or pre-claimed bounties. It only observes, recording the first tick (relative to the
    /// scenario start) at which each outcome emerges, then asserts them against bounded tick deadlines.
    /// </summary>
    public class OutpostSupplyLoopScenario : ISimulationScenario
    {
        public const uint GatherDeadlineTicks = 3000;
        public const uint SaleDeadlineTicks = 3000;
        public const uint ScoutDeadlineTicks = 4000;
        public const uint SupplyDeadlineTicks = 6000;
        private const float WallClockTimeoutSeconds = 240f;

        public string Name => "OutpostSupplyLoopScenario";
        public float DurationSeconds => 0f; // SetupAsync observes until every deadline has passed
        public float TimeMultiplier => 10f;

        /// <summary>Verbose decision trace for the free crawlers, written next to the PlayMode results.</summary>
        public const string TraceLogPath = "Logs/outpost-supply-loop.log";

        private long _gatherTick = -1;
        private int _gatherClaimantTrust;
        private long _saleTick = -1;
        private long _scoutTick = -1;
        private bool _scoutZoneExplored;
        private long _restockTick = -1;
        private long _regrowthTick = -1;
        private int _conservationViolations;
        private int _worstDiscrepancy;
        private long _observedTicks;

        public IEnumerator SetupAsync(IObjectResolver resolver)
        {
            var clock = resolver.Resolve<ISimulationClock>();
            var sim = resolver.Resolve<ISimulationManager>();
            var zones = resolver.Resolve<IFacilityZoneService>();
            var spawner = resolver.Resolve<SwarmSpawner>();
            Assert.IsNotNull(clock, "ISimulationClock not registered.");
            Assert.IsNotNull(sim, "ISimulationManager not registered.");
            Assert.IsNotNull(zones, "IFacilityZoneService not registered.");
            Assert.IsNotNull(spawner, "SwarmSpawner not registered.");
            Assert.IsNotNull(sim.Supply, "ISupplyService not wired into the SimulationManager.");

            if (clock is SimulationClock simClock)
            {
                simClock.SetTimeMultiplier(TimeMultiplier);
            }

            // Crawler-focused decision trace to a file; the console only receives warnings and errors
            var previousLogSettings = SimLog.Settings;
            var traceSettings = SimLogSettings.FromEnvironment();
            SimLogSettings.ApplySpec(traceSettings, "Planning=Trace;Cognition=Debug;Economy=Debug;Supply=Debug;Navigation=Debug;Intervention=Debug;Drives=Info;Console=Warn");
            traceSettings.FilePath = TraceLogPath;
            traceSettings.FocusAgentPrefixes.Add("Crawler");
            SimLog.Configure(traceSettings);

            long startTick = clock.CurrentTick;
            int baselineSalvageStock = TotalSalvageStock(sim);
            float elapsed = 0f;

            try
            {
                while (elapsed < WallClockTimeoutSeconds)
                {
                    long rel = clock.CurrentTick - startTick;
                    _observedTicks = rel;
                    Observe(sim, zones, spawner, rel, (uint)clock.CurrentTick, baselineSalvageStock);

                    if (rel >= SupplyDeadlineTicks) break;

                    elapsed += Time.unscaledDeltaTime;
                    yield return null;
                }
            }
            finally
            {
                if (previousLogSettings != null) SimLog.Configure(previousLogSettings);
                else SimLog.Shutdown();
            }
        }

        private void Observe(ISimulationManager sim, IFacilityZoneService zones, SwarmSpawner spawner, long rel, uint absoluteTick, int baselineSalvageStock)
        {
            // Gather bounty fulfilled, and the claimant's trust toward the player rose
            int deliveredSalvage = 0;
            if (sim.TryGetBounty(SwarmSpawner.SeedGatherBountyId, out var gather) && gather.Status == BountyStatus.Completed)
            {
                deliveredSalvage = gather.TargetQuantity;
                if (_gatherTick < 0)
                {
                    _gatherTick = rel;
                    if (sim.Agents != null && sim.Agents.TryGetAgent(gather.ClaimantId, out var claimant) && claimant.Memory != null)
                    {
                        _gatherClaimantTrust = claimant.Memory.GetAffinity(SimulationManager.PlayerEntityId, absoluteTick).Trust;
                    }
                }
            }

            // Salvage sold at the stall: stock rose beyond what bounty deliveries account for
            if (_saleTick < 0 && TotalSalvageStock(sim) - baselineSalvageStock - deliveredSalvage > 0)
            {
                _saleTick = rel;
            }

            // Scout bounty fulfilled and its sector explored
            if (_scoutTick < 0 && sim.TryGetBounty(SwarmSpawner.SeedScoutBountyId, out var scout) && scout.Status == BountyStatus.Completed)
            {
                _scoutTick = rel;
                _scoutZoneExplored = zones.IsSectorExplored(FacilityZoneService.HerbHollowZoneId);
            }

            // Supply faucets
            if (_restockTick < 0 && sim.Supply.RestockCount > 0) _restockTick = rel;
            if (_regrowthTick < 0 && sim.Supply.RegrowthCount > 0) _regrowthTick = rel;

            // Ledger conservation: stalls + agents + escrow == faucets - sinks
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

        private static int TotalSalvageStock(ISimulationManager sim)
        {
            int total = 0;
            var stalls = sim.ActiveStalls;
            for (int i = 0; i < stalls.Count; i++)
            {
                total += stalls[i].OreStock + stalls[i].HerbStock;
            }
            return total;
        }

        public void AssertResults(IVisualAnomalyDetector detector)
        {
            Assert.GreaterOrEqual(_observedTicks, (long)SupplyDeadlineTicks,
                $"Observation ended early at tick {_observedTicks} (wall-clock timeout); simulation ran too slowly.");

            Assert.IsTrue(_gatherTick >= 0 && _gatherTick <= GatherDeadlineTicks,
                $"Gather bounty not completed within {GatherDeadlineTicks} ticks (completed at {_gatherTick}).");
            Assert.Greater(_gatherClaimantTrust, 0, "Gather bounty claimant's Trust toward the player did not rise.");

            Assert.IsTrue(_saleTick >= 0 && _saleTick <= SaleDeadlineTicks,
                $"No salvage sale reached the stall within {SaleDeadlineTicks} ticks (first at {_saleTick}).");

            Assert.IsTrue(_scoutTick >= 0 && _scoutTick <= ScoutDeadlineTicks,
                $"Scout bounty not completed within {ScoutDeadlineTicks} ticks (completed at {_scoutTick}).");
            Assert.IsTrue(_scoutZoneExplored, "Scout bounty completed but Herb Hollow was not marked explored.");

            Assert.IsTrue(_restockTick >= 0 && _restockTick <= SupplyDeadlineTicks,
                $"No supply caravan restock within {SupplyDeadlineTicks} ticks.");
            Assert.IsTrue(_regrowthTick >= 0 && _regrowthTick <= SupplyDeadlineTicks,
                $"No resource regrowth within {SupplyDeadlineTicks} ticks.");

            Assert.AreEqual(0, _conservationViolations,
                $"Ledger conservation violated on {_conservationViolations} observations (worst discrepancy {_worstDiscrepancy}g).");

            Assert.AreEqual(0, detector.StackingCount, $"Pawn stacking detected ({detector.StackingCount}).");
            Assert.AreEqual(0, detector.ThrashingCount, $"Visual state thrashing detected ({detector.ThrashingCount}).");

            Debug.Log($"[OutpostSupplyLoopScenario] gather@{_gatherTick} (trust {_gatherClaimantTrust}), sale@{_saleTick}, scout@{_scoutTick}, restock@{_restockTick}, regrowth@{_regrowthTick}");
        }
    }
}
