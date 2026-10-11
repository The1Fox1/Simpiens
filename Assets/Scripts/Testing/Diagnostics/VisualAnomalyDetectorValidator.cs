using System;
using Simpiens.Cognition;
using Simpiens.Entities;
using Simpiens.Simulation;
using Simpiens.Simulation.World;
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
            ValidateMapWallPenetration();

            Debug.Log("<color=green>[VisualAnomalyDetectorValidator] ALL 5 TESTS PASSED SUCCESSFULLY!</color>");
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

        private void ValidateMapWallPenetration()
        {
            // Static map walls are data (no colliders): penetration is checked against the loaded map's wall geometry
            var registry = new WorldRegistry();
            var map = new MapDefinitionBuilder("PenetrationProbe", new Rect(-10f, -10f, 20f, 20f))
                .WallRun(new Vector2(3f, -2f), new Vector2(3f, 2f))
                .Build();
            var worldMap = new WorldMap(null, null);
            worldMap.Load(map);
            var detector = new VisualAnomalyDetector(registry, null, null, worldMap);

            var go = new GameObject("[TestNode_Penetration]");
            var node = go.AddComponent<NodeController>();
            node.Initialize(GUID.Generate(), new NodeConfiguration(2f));
            node.transform.position = Vector2.zero;
            registry.RegisterNode(node);

            for (int f = 0; f < 20; f++)
            {
                detector.Tick(0.05f, f * 0.05f);
            }
            Assert(detector.PenetrationCount == 0, $"A pawn clear of the wall must not be flagged (actual: {detector.PenetrationCount})");

            node.transform.position = new Vector2(3f, 0f);
            for (int f = 20; f < 40; f++)
            {
                detector.Tick(0.05f, f * 0.05f);
            }
            Assert(detector.PenetrationCount >= 1, $"A pawn inside a map wall must be flagged (actual: {detector.PenetrationCount})");
            Assert(detector.BoundaryBreachCount == 0, "A pawn inside the playable bounds must not breach them.");

            GameObject.DestroyImmediate(go);
            Debug.Log("[PASS] Test 5: Penetration of static map walls is detected from map geometry.");
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
