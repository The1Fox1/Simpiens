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
            var go = new GameObject("[TestNode_Stall]");
            var node = go.AddComponent<NodeController>();
            node.Initialize(GUID.Generate(), new NodeConfiguration(2f));
            node.transform.position = Vector2.zero;

            var path = new PathResponse
            {
                Waypoints = new Vector2[] { new Vector2(5f, 0f) },
                Length = 1,
                IsValid = true
            };

            var state = new ActiveIntentState
            {
                Intent = new WanderIntent(node.Id, path),
                CurrentWaypointIndex = 0,
                LastSampledPosition = Vector2.zero,
                TimeSinceLastProgress = 1.0f,
                StallCount = 0
            };

            // Call ExecutePathMovement when position hasn't moved for >= 1.0s
            var result = simManager.ExecutePathMovement(node, path, state);

            Assert(state.StallCount >= 1, $"Watchdog incremented StallCount when movement stalled (actual: {state.StallCount})");
            Assert(result == IntentResult.InProgress, "Intent remains in progress during initial unstick attempt.");

            GameObject.DestroyImmediate(go);
            Debug.Log("[PASS] Test 1: Motor stall detection after 1.0s verified.");
        }

        private void ValidateTier1ClearanceNudge()
        {
            var simManager = new SimulationManager(null, null);
            var go = new GameObject("[TestNode_Tier1]");
            var node = go.AddComponent<NodeController>();
            node.Initialize(GUID.Generate(), new NodeConfiguration(2f));
            node.transform.position = Vector2.zero;

            var targetPos = new Vector2(1f, 0f); // Movement axis is purely horizontal (X)

            var state = new ActiveIntentState
            {
                StallCount = 1,
                LastSampledPosition = Vector2.zero
            };

            // Apply Tier 1 unstuck reflex
            var result = simManager.ApplyProgressiveUnstuckReflex(node, targetPos, state);

            Assert(result == IntentResult.InProgress, "Tier 1 reflex maintains InProgress intent state.");
            Assert(!Mathf.Approximately(node.Position.y, 0f), $"Node was orthogonally displaced along Y-axis (y: {node.Position.y})");
            Assert(Mathf.Approximately(node.Position.x, 0f), "Node retained its position along movement axis.");

            GameObject.DestroyImmediate(go);
            Debug.Log("[PASS] Test 2: Tier 1 Orthogonal clearance nudge breaks deadlock symmetry.");
        }

        private void ValidateTier2RelaxedClearance()
        {
            var simManager = new SimulationManager(null, null);
            var go = new GameObject("[TestNode_Tier2]");
            var node = go.AddComponent<NodeController>();
            node.Initialize(GUID.Generate(), new NodeConfiguration(2f));

            var state = new ActiveIntentState
            {
                StallCount = 3,
                UseRelaxedClearance = false
            };

            var result = simManager.ApplyProgressiveUnstuckReflex(node, new Vector2(1f, 0f), state);

            Assert(result == IntentResult.InProgress, "Tier 2 reflex maintains InProgress intent state.");
            Assert(state.UseRelaxedClearance == true, "Tier 2 reflex activated UseRelaxedClearance on ActiveIntentState.");

            GameObject.DestroyImmediate(go);
            Debug.Log("[PASS] Test 3: Tier 2 Relaxed clearance tolerance activated.");
        }

        private void ValidateTier3DisengageAndBlacklist()
        {
            var clock = new SimulationClock();
            var simManager = new SimulationManager(null, null, clock);
            var go = new GameObject("[TestNode_Tier3]");
            var node = go.AddComponent<NodeController>();
            node.Initialize(GUID.Generate(), new NodeConfiguration(2f));
            node.transform.position = new Vector2(5f, 5f);

            var agent = go.AddComponent<AutonomousAgent>();
            agent.Initialize(node.Id, null, null, simManager, clock);

            var resourceId = GUID.Generate();
            var state = new ActiveIntentState
            {
                Intent = new HarvestResourceIntent(node.Id, resourceId, default),
                StallCount = 4
            };

            var targetPos = new Vector2(6f, 5f); // 1 unit to the right
            var result = simManager.ApplyProgressiveUnstuckReflex(node, targetPos, state);

            Assert(result == IntentResult.PathBlocked, "Tier 3 reflex returns PathBlocked to terminate the stalled intent.");
            Assert(node.Position.x < 5f, $"Node backed off away from target (actual X: {node.Position.x})");
            Assert(agent.Memory.IsBlacklisted(resourceId, 100), "Target resource was blacklisted in AgentMemory.");

            GameObject.DestroyImmediate(go);
            Debug.Log("[PASS] Test 4: Tier 3 Disengage and target blacklisting verified.");
        }

        private void ValidateZeroAllocationWatchdog()
        {
            var simManager = new SimulationManager(null, null);
            var go = new GameObject("[TestNode_Bench]");
            var node = go.AddComponent<NodeController>();
            node.Initialize(GUID.Generate(), new NodeConfiguration(2f));

            var state = new ActiveIntentState
            {
                StallCount = 1,
                LastSampledPosition = Vector2.zero
            };

            var targetPos = new Vector2(1f, 0f);

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

            GameObject.DestroyImmediate(go);
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
