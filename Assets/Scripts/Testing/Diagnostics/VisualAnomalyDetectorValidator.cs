using System;
using Simpiens.Cognition;
using Simpiens.Entities;
using Simpiens.Simulation;
using UnityEngine;
using VContainer.Unity;
using Debug = UnityEngine.Debug;

namespace Simpiens.Testing.Diagnostics
{
    /// <summary>
    /// Automated test harness for VisualAnomalyDetector.
    /// Validates heading jitter detection, pawn stacking, state thrashing, and zero-allocation performance.
    /// </summary>
    public class VisualAnomalyDetectorValidator : IStartable
    {
        public void Start()
        {
            Debug.Log("[VisualAnomalyDetectorValidator] Beginning automated validation of Visual Anomaly Detector...");

            ValidateJitterDetection();
            ValidateStackingDetection();
            ValidateStateThrashingDetection();
            ValidateZeroAllocation();

            Debug.Log("<color=green>[VisualAnomalyDetectorValidator] ALL 4 TESTS PASSED SUCCESSFULLY!</color>");
        }

        private void ValidateJitterDetection()
        {
            var registry = new WorldRegistry();
            var detector = new VisualAnomalyDetector(registry);

            var go = new GameObject("[TestNode_Jitter]");
            var node = go.AddComponent<NodeController>();
            node.Initialize(GUID.Generate(), new NodeConfiguration(2f));
            node.transform.position = Vector2.zero;
            registry.RegisterNode(node);

            // Establish initial position
            detector.Tick(0.05f, 0.0f);

            // Forward movement
            node.transform.position = new Vector2(0.5f, 0f);
            detector.Tick(0.05f, 0.1f);

            // Reversal 1 (backward)
            node.transform.position = new Vector2(0.0f, 0f);
            detector.Tick(0.05f, 0.2f);

            // Reversal 2 (forward)
            node.transform.position = new Vector2(0.5f, 0f);
            detector.Tick(0.05f, 0.3f);

            // Reversal 3 (backward - 3 reversals in 0.3s)
            node.transform.position = new Vector2(0.0f, 0f);
            detector.Tick(0.05f, 0.4f);

            Assert(detector.JitterCount >= 1, $"Jitter was detected (actual JitterCount: {detector.JitterCount})");

            GameObject.DestroyImmediate(go);
            Debug.Log("[PASS] Test 1: Rapid heading jitter/oscillation detection verified.");
        }

        private void ValidateStackingDetection()
        {
            var registry = new WorldRegistry();
            var detector = new VisualAnomalyDetector(registry);

            var goA = new GameObject("[TestNode_StackA]");
            var nodeA = goA.AddComponent<NodeController>();
            nodeA.Initialize(GUID.Generate(), new NodeConfiguration(2f));
            nodeA.transform.position = Vector2.zero;
            registry.RegisterNode(nodeA);

            var goB = new GameObject("[TestNode_StackB]");
            var nodeB = goB.AddComponent<NodeController>();
            nodeB.Initialize(GUID.Generate(), new NodeConfiguration(2f));
            nodeB.transform.position = new Vector2(0.1f, 0f); // 0.1m apart (tight stacking)
            registry.RegisterNode(nodeB);

            // Stacking evaluation runs every 5 frames and requires duration >= 1.0s
            // Run 30 frames with dt=0.05s (total 1.5s)
            for (int f = 0; f < 30; f++)
            {
                detector.Tick(0.05f, f * 0.05f);
            }

            Assert(detector.StackingCount >= 1, $"Stacking was detected after 1.5s proximity (actual: {detector.StackingCount})");

            GameObject.DestroyImmediate(goA);
            GameObject.DestroyImmediate(goB);
            Debug.Log("[PASS] Test 2: Unnatural pawn stacking/clumping detection verified.");
        }

        private void ValidateStateThrashingDetection()
        {
            var registry = new WorldRegistry();
            var detector = new VisualAnomalyDetector(registry);

            var go = new GameObject("[TestNode_Thrash]");
            var node = go.AddComponent<NodeController>();
            node.Initialize(GUID.Generate(), new NodeConfiguration(2f));
            registry.RegisterNode(node);

            var agent = go.AddComponent<AutonomousAgent>();
            agent.VisualState = AgentVisualState.Idle;
            detector.Tick(0.05f, 0.0f);

            // Rapid state switching: 4 state changes within 0.4s
            agent.VisualState = AgentVisualState.Walking;
            detector.Tick(0.05f, 0.1f);

            agent.VisualState = AgentVisualState.Panicking;
            detector.Tick(0.05f, 0.2f);

            agent.VisualState = AgentVisualState.Yielding;
            detector.Tick(0.05f, 0.3f);

            agent.VisualState = AgentVisualState.Sharing;
            detector.Tick(0.05f, 0.4f);

            Assert(detector.ThrashingCount >= 1, $"Visual state thrashing was detected (actual: {detector.ThrashingCount})");

            GameObject.DestroyImmediate(go);
            Debug.Log("[PASS] Test 3: High-frequency visual state thrashing detection verified.");
        }

        private void ValidateZeroAllocation()
        {
            var registry = new WorldRegistry();
            var detector = new VisualAnomalyDetector(registry);

            var nodes = new GameObject[5];
            for (int i = 0; i < 5; i++)
            {
                nodes[i] = new GameObject($"[TestNode_Bench_{i}]");
                var n = nodes[i].AddComponent<NodeController>();
                n.Initialize(GUID.Generate(), new NodeConfiguration(2f));
                n.transform.position = new Vector2(i * 2f, 0f);
                registry.RegisterNode(n);
            }

            // Warmup
            for (int i = 0; i < 50; i++)
            {
                detector.Tick(0.02f, i * 0.02f);
            }

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            long startAlloc = GC.GetAllocatedBytesForCurrentThread();

            for (int i = 0; i < 500; i++)
            {
                detector.Tick(0.02f, 10f + i * 0.02f);
            }

            long totalAlloc = GC.GetAllocatedBytesForCurrentThread() - startAlloc;
            Assert(totalAlloc == 0, $"Zero allocation guarantee violated: allocated {totalAlloc} bytes across 500 frames.");

            for (int i = 0; i < 5; i++)
            {
                GameObject.DestroyImmediate(nodes[i]);
            }

            Debug.Log("[PASS] Test 4: Zero allocation guarantee verified (0 bytes across 500 continuous evaluation frames).");
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
