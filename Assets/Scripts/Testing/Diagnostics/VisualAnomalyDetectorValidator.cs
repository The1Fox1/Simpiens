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
            ValidateJitterIsSpeedIndependent();
            ValidateJitterIgnoresNormalTurns();

            Debug.Log("<color=green>[VisualAnomalyDetectorValidator] ALL 7 TESTS PASSED SUCCESSFULLY!</color>");
        }

        private void ValidateJitterDetection()
        {
            var registry = new WorldRegistry();
            var detector = new VisualAnomalyDetector(registry);
            var go = CreatePawn(registry, "[TestNode_Jitter]", out var node);

            // Back and forth every tick: 3 reversals within 4 ticks
            float[] xs = { 0f, 0.5f, 0f, 0.5f, 0f };
            for (int t = 0; t < xs.Length; t++)
            {
                node.transform.position = (new Vec2(xs[t], 0f)).ToUnity();
                detector.EvaluateJitterTick(t);
            }
            Assert(detector.JitterCount == 1, $"Jitter was detected once (actual JitterCount: {detector.JitterCount})");
            var record = detector.GetRecentAnomalies(1)[0];
            Assert(record.Type == VisualAnomalyType.OscillationJitter && record.Description.Contains("last positions"), $"The alert names the type and the pawn's last positions ({record.Description}).");

            GameObject.DestroyImmediate(go);
            Debug.Log("[PASS] Test 1: Rapid heading jitter/oscillation detection verified.");
        }

        /// <summary>
        /// Epic MAP Phase 5 (producer decision D1 = A): jitter is judged in simulation ticks, so the same motion gives
        /// the same verdict at 1x and 10x, however the frames fall. The frame-based Tick no longer judges jitter.
        /// </summary>
        private void ValidateJitterIsSpeedIndependent()
        {
            int Run(float multiplier, float frameSeconds)
            {
                var registry = new WorldRegistry();
                var clock = new SimulationClock();
                clock.SetTimeMultiplier(multiplier);
                var detector = new VisualAnomalyDetector(registry, clock);
                var go = CreatePawn(registry, "[TestNode_JitterSpeed]", out var node);
                float now = 0f;
                for (int t = 0; t < 12; t++)
                {
                    node.transform.position = (new Vec2(t % 2 == 0 ? 0f : 0.5f, 0f)).ToUnity();
                    clock.AdvanceTicks(1);
                    // At 10x a frame covers ten times the sim time: the frame clock must not change the verdict
                    now += frameSeconds;
                    detector.Tick(frameSeconds, now);
                }
                GameObject.DestroyImmediate(go);
                return detector.JitterCount;
            }

            int atPlaySpeed = Run(1f, 0.1f);
            int atTestSpeed = Run(10f, 0.01f);
            Assert(atPlaySpeed >= 1 && atPlaySpeed == atTestSpeed, $"The same twitch is flagged the same at 1x and 10x ({atPlaySpeed} vs {atTestSpeed}).");

            // Frames alone (no simulation ticks) never judge jitter, however fast the pawn moves between them
            var reg = new WorldRegistry();
            var frameOnly = new VisualAnomalyDetector(reg);
            var g = CreatePawn(reg, "[TestNode_FrameOnly]", out var n);
            for (int f = 0; f < 12; f++)
            {
                n.transform.position = (new Vec2(f % 2 == 0 ? 0f : 0.5f, 0f)).ToUnity();
                frameOnly.Tick(0.016f, f * 0.016f);
            }
            Assert(frameOnly.JitterCount == 0, $"Frame ticks judge no jitter ({frameOnly.JitterCount}).");
            GameObject.DestroyImmediate(g);
            Debug.Log("[PASS] Test 6: Jitter is judged in simulation ticks: the same twitch gives the same verdict at 1x and 10x.");
        }

        /// <summary>Normal movement is not jitter: turning back once, or slow back-and-forth beyond the window.</summary>
        private void ValidateJitterIgnoresNormalTurns()
        {
            var registry = new WorldRegistry();
            var detector = new VisualAnomalyDetector(registry);
            var go = CreatePawn(registry, "[TestNode_Turns]", out var node);

            // Walk east 5 ticks, turn back once, walk west 5 ticks (arrive at a stall, then leave)
            long t = 0;
            for (int k = 0; k <= 5; k++, t++) { node.transform.position = new Vector3(k * 0.2f, 0f, 0f); detector.EvaluateJitterTick(t); }
            for (int k = 4; k >= 0; k--, t++) { node.transform.position = new Vector3(k * 0.2f, 0f, 0f); detector.EvaluateJitterTick(t); }
            Assert(detector.JitterCount == 0, $"Turning back once is not jitter ({detector.JitterCount}).");

            // Back and forth every 3 ticks: 3 reversals span 6 ticks, beyond the 5-tick window
            float x = 0f;
            int dir = 1;
            for (int k = 0; k < 18; k++, t++)
            {
                if (k % 3 == 0) dir = -dir;
                x += dir * 0.2f;
                node.transform.position = (new Vec2(x, 5f)).ToUnity();
                detector.EvaluateJitterTick(t);
            }
            Assert(detector.JitterCount == 0, $"Back and forth every 3 ticks is not jitter ({detector.JitterCount}).");

            GameObject.DestroyImmediate(go);
            Debug.Log("[PASS] Test 7: Turning back once, or slow back-and-forth, is not jitter.");
        }

        private static GameObject CreatePawn(WorldRegistry registry, string name, out NodeController node)
        {
            var go = new GameObject(name);
            node = go.AddComponent<NodeController>();
            node.Initialize(EntityId.Generate(), new NodeConfiguration(2f));
            node.transform.position = Vec2.Zero.ToUnity();
            registry.RegisterNode(node);
            return go;
        }

        private void ValidateStackingDetection()
        {
            var registry = new WorldRegistry();
            var detector = new VisualAnomalyDetector(registry);

            var goA = new GameObject("[TestNode_StackA]");
            var nodeA = goA.AddComponent<NodeController>();
            nodeA.Initialize(EntityId.Generate(), new NodeConfiguration(2f));
            nodeA.transform.position = Vec2.Zero.ToUnity();
            registry.RegisterNode(nodeA);

            var goB = new GameObject("[TestNode_StackB]");
            var nodeB = goB.AddComponent<NodeController>();
            nodeB.Initialize(EntityId.Generate(), new NodeConfiguration(2f));
            nodeB.transform.position = (new Vec2(0.1f, 0f)).ToUnity(); // 0.1m apart (tight stacking)
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
            node.Initialize(EntityId.Generate(), new NodeConfiguration(2f));
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
                n.Initialize(EntityId.Generate(), new NodeConfiguration(2f));
                n.transform.position = (new Vec2(i * 2f, 0f)).ToUnity();
                registry.RegisterNode(n);
            }

            // Warmup
            for (int i = 0; i < 50; i++)
            {
                detector.Tick(0.02f, i * 0.02f);
                detector.EvaluateJitterTick(i);
            }

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            long startAlloc = GC.GetAllocatedBytesForCurrentThread();

            for (int i = 0; i < 500; i++)
            {
                detector.Tick(0.02f, 10f + i * 0.02f);
                detector.EvaluateJitterTick(50 + i);
            }

            long totalAlloc = GC.GetAllocatedBytesForCurrentThread() - startAlloc;
            Assert(totalAlloc == 0, $"Zero allocation guarantee violated: allocated {totalAlloc} bytes across 500 frames and ticks.");

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
            var map = new MapDefinitionBuilder("PenetrationProbe", new RectF(-10f, -10f, 20f, 20f))
                .WallRun(new Vec2(3f, -2f), new Vec2(3f, 2f))
                .Build();
            var worldMap = new WorldMap(null, null);
            worldMap.Load(map);
            var detector = new VisualAnomalyDetector(registry, null, null, worldMap);

            var go = new GameObject("[TestNode_Penetration]");
            var node = go.AddComponent<NodeController>();
            node.Initialize(EntityId.Generate(), new NodeConfiguration(2f));
            node.transform.position = Vec2.Zero.ToUnity();
            registry.RegisterNode(node);

            for (int f = 0; f < 20; f++)
            {
                detector.Tick(0.05f, f * 0.05f);
            }
            Assert(detector.PenetrationCount == 0, $"A pawn clear of the wall must not be flagged (actual: {detector.PenetrationCount})");

            node.transform.position = (new Vec2(3f, 0f)).ToUnity();
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
