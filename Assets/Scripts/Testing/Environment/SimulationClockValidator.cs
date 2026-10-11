using System;
using Simpiens.Cognition;
using Simpiens.Cognition.Contracts;
using Simpiens.Cognition.Pathfinding;
using Simpiens.Entities;
using Simpiens.Simulation;
using Simpiens.Simulation.Spatial;
using UnityEngine;
using VContainer.Unity;

namespace Simpiens.Testing.Environment
{
    /// <summary>
    /// Automated domain validation suite for Section 2.1:
    /// Unified Fixed-Tick Simulation Architecture, Speed Scaling, Pause Integrity,
    /// Periodic Exactness, Visual Interpolation, and Zero-Allocation Invariants.
    /// </summary>
    public class SimulationClockValidator : IStartable
    {
        public void Start()
        {
            RunAllValidationTests();
        }

        public void RunAllValidationTests()
        {
            Debug.Log("[SimulationClockValidator] Beginning Section 2.1 Fixed-Tick Simulation Validation...");

            ValidateFixedTickRateAndSpeedScaling();
            ValidatePauseIntegrity();
            ValidatePeriodicHearthExactness();
            ValidateVisualInterpolationAlpha();
            ValidateZeroAllocationOnTickLoop();

            Debug.Log("<color=green>[SimulationClockValidator] ALL 5 TESTS PASSED SUCCESSFULLY!</color>");
        }

        private void ValidateFixedTickRateAndSpeedScaling()
        {
            var registry = new WorldRegistry();
            var clock = new SimulationClock();
            var simManager = new SimulationManager(registry, null, clock);

            var go = new GameObject("[TestNode_Speed]");
            var node = go.AddComponent<NodeController>();
            node.Construct(clock, registry);
            node.Initialize(GUID.Generate(), new NodeConfiguration(speed: 2.0f));
            node.transform.position = Vector2.zero;
            registry.RegisterNode(node);

            var path = new PathResponse
            {
                Waypoints = new Vector2[] { new Vector2(100f, 0f) },
                Length = 1,
                IsValid = true
            };

            var intent = new WanderIntent(node.Id, path);
            simManager.EnqueueIntent(intent);

            // Advance 10 ticks at 1x speed (1.0 simulation second = 2.0 units traveled)
            clock.AdvanceTicks(10);

            float distAt1x = node.Position.x;
            Assert(Mathf.Approximately(distAt1x, 2.0f), $"At 1x speed, pawn traveled 2.0m across 10 ticks (actual: {distAt1x})");

            // Advance another 10 ticks at 10x speed multiplier
            clock.SetTimeMultiplier(10.0f);
            Assert(Mathf.Approximately(clock.TimeMultiplier, 10.0f), "Clock multiplier updated to 10x.");

            // Reset position to zero for clean distance check across 10 ticks
            node.SetLogicalPosition(Vector2.zero);
            clock.AdvanceTicks(10);

            float distAt10x = node.Position.x;
            Assert(Mathf.Approximately(distAt10x, 2.0f), $"Each logical tick advances exactly 0.2m regardless of speed multiplier (actual: {distAt10x})");

            GameObject.DestroyImmediate(go);
            simManager.Dispose();
            Debug.Log("[PASS] Test 1: Fixed-tick rate and speed scaling verified.");
        }

        private void ValidatePauseIntegrity()
        {
            var registry = new WorldRegistry();
            var clock = new SimulationClock();
            var simManager = new SimulationManager(registry, null, clock);

            var go = new GameObject("[TestNode_Pause]");
            var node = go.AddComponent<NodeController>();
            node.Construct(clock, registry);
            node.Initialize(GUID.Generate(), new NodeConfiguration(2.0f));
            node.transform.position = Vector2.zero;
            registry.RegisterNode(node);

            var agent = go.AddComponent<AutonomousAgent>();
            agent.Initialize(node.Id, null, null, simManager, clock);
            agent.Hunger = 20.0f;
            agent.Energy = 80.0f;

            // Pause simulation
            simManager.Pause();
            Assert(simManager.IsPaused, "SimulationManager reports IsPaused = true.");
            Assert(clock.IsPaused, "SimulationClock reports IsPaused = true.");

            long tickBefore = clock.CurrentTick;
            float hungerBefore = agent.Hunger;
            float energyBefore = agent.Energy;

            // Ticking while paused must result in zero state mutations
            simManager.Tick();
            agent.ManualUpdate();

            Assert(clock.CurrentTick == tickBefore, "Clock current tick remained frozen during pause.");
            Assert(agent.Hunger == hungerBefore, "Agent hunger did not decay during pause.");
            Assert(agent.Energy == energyBefore, "Agent energy did not decay during pause.");

            // Resume simulation
            simManager.Resume();
            Assert(!simManager.IsPaused, "SimulationManager reports IsPaused = false after resume.");
            Assert(!clock.IsPaused, "SimulationClock reports IsPaused = false after resume.");

            GameObject.DestroyImmediate(go);
            simManager.Dispose();
            Debug.Log("[PASS] Test 2: Pause integrity verified (no drift, no decay while paused).");
        }

        private void ValidatePeriodicHearthExactness()
        {
            var registry = new WorldRegistry();
            var clock = new SimulationClock();
            var simManager = new SimulationManager(registry, null, clock);

            var anchorId = GUID.Generate();
            simManager.RegisterAnchor(anchorId, Vector2.zero, 3.0f);

            var go = new GameObject("[TestNode_Periodic]");
            var node = go.AddComponent<NodeController>();
            node.Construct(clock, registry);
            node.Initialize(GUID.Generate(), new NodeConfiguration(2.0f));
            node.transform.position = Vector2.zero; // Inside campfire radius
            registry.RegisterNode(node);

            var agent = go.AddComponent<AutonomousAgent>();
            agent.Initialize(node.Id, null, null, simManager, clock);
            agent.Frustration = 10.0f;

            // Advance 100 ticks. Ticks 50 and 100 should fire EvaluateHearthProximity,
            // each relieving exactly 1.0 frustration (total relief = 2.0f, leaving 8.0f).
            clock.AdvanceTicks(100);

            Assert(Mathf.Approximately(agent.Frustration, 8.0f), 
                $"Periodic hearth bonding fired exactly 2 times across 100 ticks (Frustration expected: 8.0, actual: {agent.Frustration})");

            GameObject.DestroyImmediate(go);
            simManager.Dispose();
            Debug.Log("[PASS] Test 3: Periodic hearth dynamics exactness verified (no duplicate frame fires).");
        }

        private void ValidateVisualInterpolationAlpha()
        {
            var clock = new SimulationClock();
            clock.SetTimeMultiplier(1.0f);

            // InterpolationAlpha should be clamped [0, 1]
            Assert(clock.InterpolationAlpha >= 0f && clock.InterpolationAlpha <= 1f, "InterpolationAlpha is clamped in [0, 1].");

            var go = new GameObject("[TestNode_Interpolation]");
            var node = go.AddComponent<NodeController>();
            node.Construct(clock, null);
            node.Initialize(GUID.Generate(), new NodeConfiguration(2.0f));
            node.transform.position = Vector2.zero;

            Assert(node.LogicalPosition == Vector2.zero, "Initial LogicalPosition matches transform.position.");
            Assert(node.PreviousLogicalPosition == Vector2.zero, "Initial PreviousLogicalPosition matches transform.position.");

            // Step logical position
            node.SetLogicalPosition(new Vector2(10f, 0f));
            Assert(node.LogicalPosition == new Vector2(10f, 0f), "LogicalPosition updated to target coordinate.");
            Assert(node.PreviousLogicalPosition == Vector2.zero, "PreviousLogicalPosition records previous tick coordinate.");

            GameObject.DestroyImmediate(go);
            Debug.Log("[PASS] Test 4: Visual interpolation alpha and logical coordinate tracking verified.");
        }

        private void ValidateZeroAllocationOnTickLoop()
        {
            var registry = new WorldRegistry();
            var grid = new SpatialHashGrid();
            grid.Initialize(20f, 20f, 1f);
            var clock = new SimulationClock();
            var simManager = new SimulationManager(registry, grid, clock);

            var go = new GameObject("[TestNode_ZeroGC_Tick]");
            var node = go.AddComponent<NodeController>();
            node.Construct(clock, registry);
            node.Initialize(GUID.Generate(), new NodeConfiguration(2.0f));
            node.transform.position = Vector2.zero;
            registry.RegisterNode(node);

            var agent = go.AddComponent<AutonomousAgent>();
            agent.Initialize(node.Id, null, null, simManager, clock);
            agent.Energy = 100f;
            agent.Hunger = 0f;
            agent.Social = 100f;
            agent.HasActiveIntent = true;

            var idleIntent = new IdleIntent(node.Id, 10000f);
            simManager.EnqueueIntent(idleIntent);
            simManager.ProcessQueuedIntents();

            // Warmup
            for (int i = 0; i < 50; i++)
            {
                agent.ManualTick(clock.CurrentTick);
                simManager.Tick();
            }

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            long startAlloc = GC.GetAllocatedBytesForCurrentThread();

            for (int i = 0; i < 1000; i++)
            {
                agent.ManualTick(clock.CurrentTick);
                simManager.Tick();
            }

            long totalAlloc = GC.GetAllocatedBytesForCurrentThread() - startAlloc;
            Assert(totalAlloc == 0, $"Zero allocation guarantee violated: allocated {totalAlloc} bytes across 1,000 fixed ticks.");

            GameObject.DestroyImmediate(go);
            simManager.Dispose();
            Debug.Log("[PASS] Test 5: Strict zero-allocation guarantee verified on fixed tick loop (0 bytes across 1,000 ticks).");
        }

        private void Assert(bool condition, string message)
        {
            if (!condition)
            {
                throw new Exception($"[SimulationClockValidator ASSERTION FAILED] {message}");
            }
        }
    }
}
