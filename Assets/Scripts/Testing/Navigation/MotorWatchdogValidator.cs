using Simpiens.Simulation.Spatial;
using Simpiens.Testing.Environment;
using System;
using System.Collections.Generic;
using Simpiens.Cognition;
using Simpiens.Cognition.Contracts;
using Simpiens.Cognition.Memory;
using Simpiens.Cognition.Pathfinding;
using Simpiens.Entities;
using Simpiens.Simulation;
using UnityEngine;
using VContainer.Unity;
using Debug = UnityEngine.Debug;

namespace Simpiens.Testing.Navigation
{
    /// <summary>
    /// Automated domain validation suite for Epic Alpha Phase 2:
    /// Autonomous Motor Watchdog (Dedicated Navigation Unstuck Failsafe).
    /// </summary>
    public class MotorWatchdogValidator : IStartable
    {
        public void Start()
        {
            Debug.Log("[MotorWatchdogValidator] Beginning automated validation of Motor Watchdog & Unstuck Reflexes...");

            ValidateStallDetection();
            ValidateTier1ClearanceNudge();
            ValidateTier2RelaxedClearance();
            ValidateTier3DisengageAndBlacklist();
            ValidateZeroAllocationWatchdog();

            Debug.Log("<color=green>[MotorWatchdogValidator] ALL 5 TESTS PASSED SUCCESSFULLY!</color>");
        }

        private void ValidateStallDetection()
        {
            var simManager = new SimulationManager(null, null);
            var node = HeadlessEntities.Prop(null, EntityType.Pawn, Vec2.Zero, id: EntityId.Generate(), name: "[TestNode_Stall]", register: false);

            var path = new PathResponse
            {
                Waypoints = new Vec2[] { new Vec2(5f, 0f) },
                Length = 1,
                IsValid = true
            };

            var state = new ActiveIntentState
            {
                Intent = new WanderIntent(node.Id, path),
                CurrentWaypointIndex = 0,
                LastSampledPosition = Vec2.Zero,
                TimeSinceLastProgress = 1.0f,
                StallCount = 0
            };

            // Call ExecutePathMovement when position hasn't moved for >= 1.0s
            var result = simManager.ExecutePathMovement(node, path, state);

            Assert(state.StallCount >= 1, $"Watchdog incremented StallCount when movement stalled (actual: {state.StallCount})");
            Assert(result == IntentResult.InProgress, "Intent remains in progress during initial unstick attempt.");

            Debug.Log("[PASS] Test 1: Motor stall detection after 1.0s verified.");
        }

        private void ValidateTier1ClearanceNudge()
        {
            var simManager = new SimulationManager(null, null);
            var node = HeadlessEntities.Prop(null, EntityType.Pawn, Vec2.Zero, id: EntityId.Generate(), name: "[TestNode_Tier1]", register: false);

            var targetPos = new Vec2(1f, 0f); // Movement axis is purely horizontal (X)

            var state = new ActiveIntentState
            {
                StallCount = 1,
                LastSampledPosition = Vec2.Zero
            };

            // Apply Tier 1 unstuck reflex
            var result = simManager.ApplyProgressiveUnstuckReflex(node, targetPos, state);

            Assert(result == IntentResult.InProgress, "Tier 1 reflex maintains InProgress intent state.");
            Assert(!SimMath.Approximately(node.Position.Y, 0f), $"Node was orthogonally displaced along Y-axis (y: {node.Position.Y})");
            Assert(SimMath.Approximately(node.Position.X, 0f), "Node retained its position along movement axis.");

            Debug.Log("[PASS] Test 2: Tier 1 Orthogonal clearance nudge breaks deadlock symmetry.");
        }

        private void ValidateTier2RelaxedClearance()
        {
            var simManager = new SimulationManager(null, null);
            var node = HeadlessEntities.Prop(null, EntityType.Pawn, Vec2.Zero, id: EntityId.Generate(), name: "[TestNode_Tier2]", register: false);

            var state = new ActiveIntentState
            {
                StallCount = 3,
                UseRelaxedClearance = false
            };

            var result = simManager.ApplyProgressiveUnstuckReflex(node, new Vec2(1f, 0f), state);

            Assert(result == IntentResult.InProgress, "Tier 2 reflex maintains InProgress intent state.");
            Assert(state.UseRelaxedClearance == true, "Tier 2 reflex activated UseRelaxedClearance on ActiveIntentState.");

            Debug.Log("[PASS] Test 3: Tier 2 Relaxed clearance tolerance activated.");
        }

        private void ValidateTier3DisengageAndBlacklist()
        {
            var clock = new SimulationClock();
            var simManager = new SimulationManager(null, null, clock);
            var goCrawler = HeadlessEntities.Crawler(null, simManager, clock, new Vec2(5f, 5f), id: EntityId.Generate(), name: "[TestNode_Tier3]", register: false);
            var node = goCrawler.Node;
            var agent = goCrawler.Agent;


            var resourceId = EntityId.Generate();
            var state = new ActiveIntentState
            {
                Intent = new HarvestResourceIntent(node.Id, resourceId, default),
                StallCount = 4
            };

            var targetPos = new Vec2(6f, 5f); // 1 unit to the right
            var result = simManager.ApplyProgressiveUnstuckReflex(node, targetPos, state);

            Assert(result == IntentResult.PathBlocked, "Tier 3 reflex returns PathBlocked to terminate the stalled intent.");
            Assert(node.Position.X < 5f, $"Node backed off away from target (actual X: {node.Position.X})");
            Assert(agent.Memory.IsBlacklisted(resourceId, 100), "Target resource was blacklisted in AgentMemory.");

            HeadlessEntities.Remove(null, goCrawler);
            Debug.Log("[PASS] Test 4: Tier 3 Disengage and target blacklisting verified.");
        }

        private void ValidateZeroAllocationWatchdog()
        {
            var simManager = new SimulationManager(null, null);
            var node = HeadlessEntities.Prop(null, EntityType.Pawn, Vec2.Zero, id: EntityId.Generate(), name: "[TestNode_Bench]", register: false);

            var state = new ActiveIntentState
            {
                StallCount = 1,
                LastSampledPosition = Vec2.Zero
            };

            var targetPos = new Vec2(1f, 0f);

            // Warmup
            for (int i = 0; i < 50; i++)
            {
                simManager.ApplyProgressiveUnstuckReflex(node, targetPos, state);
            }

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            long startAlloc = GC.GetAllocatedBytesForCurrentThread();

            for (int i = 0; i < 1000; i++)
            {
                simManager.ApplyProgressiveUnstuckReflex(node, targetPos, state);
            }

            long totalAlloc = GC.GetAllocatedBytesForCurrentThread() - startAlloc;
            Assert(totalAlloc == 0, $"Zero allocation guarantee violated: allocated {totalAlloc} bytes across 1,000 watchdog checks.");

            Debug.Log($"[PASS] Test 5: Zero allocation guarantee verified (0 bytes across 1,000 watchdog evaluations).");
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition)
            {
                throw new Exception($"[ASSERTION FAILED] {message}");
            }
        }
    }
}
