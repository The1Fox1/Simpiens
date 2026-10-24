using System;
using Simpiens.Cognition;
using Simpiens.Cognition.Contracts;
using Simpiens.Cognition.Pathfinding;
using Simpiens.Entities;
using Simpiens.Simulation;
using Simpiens.Simulation.Spatial;

namespace Simpiens.Testing.Environment
{
    /// <summary>
    /// Automated domain validation suite for Section 2.1:
    /// Unified Fixed-Tick Simulation Architecture, Speed Scaling, Pause Integrity,
    /// Periodic Exactness, Visual Interpolation, and Zero-Allocation Invariants.
    /// </summary>
    public class SimulationClockValidator
    {
        public void Start()
        {
            RunAllValidationTests();
        }

        public void RunAllValidationTests()
        {
            TestLog.Log("[SimulationClockValidator] Beginning Section 2.1 Fixed-Tick Simulation Validation...");

            ValidateFixedTickRateAndSpeedScaling();
            ValidatePauseIntegrity();
            ValidatePeriodicHearthExactness();
            ValidateVisualInterpolationAlpha();
            ValidateZeroAllocationOnTickLoop();

            TestLog.Log("<color=green>[SimulationClockValidator] ALL 5 TESTS PASSED SUCCESSFULLY!</color>");
        }

        private void ValidateFixedTickRateAndSpeedScaling()
        {
            var registry = new WorldRegistry();
            var clock = new SimulationClock();
            var simManager = new SimulationManager(registry, null, clock);

            var node = HeadlessEntities.Prop(registry, EntityType.Pawn, Vec2.Zero, id: EntityId.Generate(), name: "[TestNode_Speed]");

            var path = new PathResponse
            {
                Waypoints = new Vec2[] { new Vec2(100f, 0f) },
                Length = 1,
                IsValid = true
            };

            var intent = new WanderIntent(node.Id, path);
            simManager.EnqueueIntent(intent);

            // Advance 10 ticks at 1x speed (1.0 simulation second = 2.0 units traveled)
            clock.AdvanceTicks(10);

            float distAt1x = node.Position.X;
            Assert(SimMath.Approximately(distAt1x, 2.0f), $"At 1x speed, pawn traveled 2.0m across 10 ticks (actual: {distAt1x})");

            // Advance another 10 ticks at 10x speed multiplier
            clock.SetTimeMultiplier(10.0f);
            Assert(SimMath.Approximately(clock.TimeMultiplier, 10.0f), "Clock multiplier updated to 10x.");

            // Reset position to zero for clean distance check across 10 ticks
            node.SetPosition(Vec2.Zero);
            clock.AdvanceTicks(10);

            float distAt10x = node.Position.X;
            Assert(SimMath.Approximately(distAt10x, 2.0f), $"Each logical tick advances exactly 0.2m regardless of speed multiplier (actual: {distAt10x})");

            registry?.Unregister(node);
            simManager.Dispose();
            TestLog.Log("[PASS] Test 1: Fixed-tick rate and speed scaling verified.");
        }

        private void ValidatePauseIntegrity()
        {
            var registry = new WorldRegistry();
            var clock = new SimulationClock();
            var simManager = new SimulationManager(registry, null, clock);

            var goCrawler = HeadlessEntities.Crawler(registry, simManager, clock, Vec2.Zero, id: EntityId.Generate(), name: "[TestNode_Pause]", baseSpeed: 2.0f);
            var node = goCrawler.Node;
            var agent = goCrawler.Agent;

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
            goCrawler.ManualUpdate();

            Assert(clock.CurrentTick == tickBefore, "Clock current tick remained frozen during pause.");
            Assert(agent.Hunger == hungerBefore, "Agent hunger did not decay during pause.");
            Assert(agent.Energy == energyBefore, "Agent energy did not decay during pause.");

            // Resume simulation
            simManager.Resume();
            Assert(!simManager.IsPaused, "SimulationManager reports IsPaused = false after resume.");
            Assert(!clock.IsPaused, "SimulationClock reports IsPaused = false after resume.");

            HeadlessEntities.Remove(registry, goCrawler);
            simManager.Dispose();
            TestLog.Log("[PASS] Test 2: Pause integrity verified (no drift, no decay while paused).");
        }

        private void ValidatePeriodicHearthExactness()
        {
            var registry = new WorldRegistry();
            var clock = new SimulationClock();
            var simManager = new SimulationManager(registry, null, clock);

            var anchorId = EntityId.Generate();
            simManager.RegisterAnchor(anchorId, Vec2.Zero, 3.0f);

            var goCrawler = HeadlessEntities.Crawler(registry, simManager, clock, Vec2.Zero, id: EntityId.Generate(), name: "[TestNode_Periodic]", baseSpeed: 2.0f);
            var node = goCrawler.Node;
            var agent = goCrawler.Agent;

            agent.Frustration = 10.0f;

            // Advance 100 ticks. Ticks 50 and 100 should fire EvaluateHearthProximity,
            // each relieving exactly 1.0 frustration (total relief = 2.0f, leaving 8.0f).
            clock.AdvanceTicks(100);

            Assert(SimMath.Approximately(agent.Frustration, 8.0f), 
                $"Periodic hearth bonding fired exactly 2 times across 100 ticks (Frustration expected: 8.0, actual: {agent.Frustration})");

            HeadlessEntities.Remove(registry, goCrawler);
            simManager.Dispose();
            TestLog.Log("[PASS] Test 3: Periodic hearth dynamics exactness verified (no duplicate frame fires).");
        }

        private void ValidateVisualInterpolationAlpha()
        {
            var clock = new SimulationClock();
            clock.SetTimeMultiplier(1.0f);

            // InterpolationAlpha should be clamped [0, 1]
            Assert(clock.InterpolationAlpha >= 0f && clock.InterpolationAlpha <= 1f, "InterpolationAlpha is clamped in [0, 1].");

            var node = HeadlessEntities.Prop(null, EntityType.Pawn, Vec2.Zero, id: EntityId.Generate(), name: "[TestNode_Interpolation]", register: false);

            Assert(node.Position == Vec2.Zero, "Initial LogicalPosition matches transform.position.");
            Assert(node.PreviousPosition == Vec2.Zero, "Initial PreviousLogicalPosition matches transform.position.");

            // Step logical position
            node.SetPosition(new Vec2(10f, 0f));
            Assert(node.Position == new Vec2(10f, 0f), "LogicalPosition updated to target coordinate.");
            Assert(node.PreviousPosition == Vec2.Zero, "PreviousLogicalPosition records previous tick coordinate.");

            TestLog.Log("[PASS] Test 4: Visual interpolation alpha and logical coordinate tracking verified.");
        }

        private void ValidateZeroAllocationOnTickLoop()
        {
            var registry = new WorldRegistry();
            var grid = new SpatialHashGrid();
            grid.Initialize(20f, 20f, 1f);
            var clock = new SimulationClock();
            var simManager = new SimulationManager(registry, grid, clock);

            var goCrawler = HeadlessEntities.Crawler(registry, simManager, clock, Vec2.Zero, id: EntityId.Generate(), name: "[TestNode_ZeroGC_Tick]", baseSpeed: 2.0f);
            var node = goCrawler.Node;
            var agent = goCrawler.Agent;

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
                goCrawler.Tick(clock.CurrentTick);
                simManager.Tick();
            }

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            long startAlloc = GC.GetAllocatedBytesForCurrentThread();

            for (int i = 0; i < 1000; i++)
            {
                goCrawler.Tick(clock.CurrentTick);
                simManager.Tick();
            }

            long totalAlloc = GC.GetAllocatedBytesForCurrentThread() - startAlloc;
            Assert(totalAlloc == 0, $"Zero allocation guarantee violated: allocated {totalAlloc} bytes across 1,000 fixed ticks.");

            HeadlessEntities.Remove(registry, goCrawler);
            simManager.Dispose();
            TestLog.Log("[PASS] Test 5: Strict zero-allocation guarantee verified on fixed tick loop (0 bytes across 1,000 ticks).");
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
