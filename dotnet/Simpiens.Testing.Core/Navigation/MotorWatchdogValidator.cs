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
using Simpiens.Simulation.Movement;

namespace Simpiens.Testing.Navigation
{
    /// <summary>
    /// Automated domain validation suite for Epic Alpha Phase 2:
    /// Autonomous Motor Watchdog (Dedicated Navigation Unstuck Failsafe).
    /// </summary>
    public class MotorWatchdogValidator
    {
        public void Start()
        {
            TestLog.Log("[MotorWatchdogValidator] Beginning automated validation of Motor Watchdog & Unstuck Reflexes...");

            ValidateStallDetection();
            ValidateTier1ClearanceNudge();
            ValidateTier2RelaxedClearance();
            ValidateTier3DisengageAndBlacklist();
            ValidateZeroAllocationWatchdog();
            ValidateStandaloneLocomotion();

            TestLog.Log("<color=green>[MotorWatchdogValidator] ALL 6 TESTS PASSED SUCCESSFULLY!</color>");
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

            TestLog.Log("[PASS] Test 1: Motor stall detection after 1.0s verified.");
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

            TestLog.Log("[PASS] Test 2: Tier 1 Orthogonal clearance nudge breaks deadlock symmetry.");
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

            TestLog.Log("[PASS] Test 3: Tier 2 Relaxed clearance tolerance activated.");
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
            TestLog.Log("[PASS] Test 4: Tier 3 Disengage and target blacklisting verified.");
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

            TestLog.Log($"[PASS] Test 5: Zero allocation guarantee verified (0 bytes across 1,000 watchdog evaluations).");
        }

        private void ValidateStandaloneLocomotion()
        {
            // 1. Isolated locomotion with null partition, registry, and clock
            var isolatedLocomotion = new Locomotion();
            var isolatedId = EntityId.Generate();
            var isolatedPawn = new WorldEntity(isolatedId, EntityType.Pawn, Vec2.Zero, 0.35f, agent: new AgentState(isolatedId));

            Assert(isolatedLocomotion.TryDisplacePawn(isolatedPawn, new Vec2(1f, 1f)), "Unbounded pawn can displace");
            Assert(isolatedPawn.Position == new Vec2(1f, 1f), "Pawn moved to target position");
            Assert(!isolatedLocomotion.IsPositionBlocked(new Vec2(2f, 2f), isolatedId), "Position unblocked without spatial partition");
            Assert(!isolatedLocomotion.IsCrowdedMove(isolatedPawn, new Vec2(2f, 2f)), "Movement uncrowded without spatial partition");

            // 2. Wired locomotion with spatial grid, registry, and simulation clock
            var registry = new WorldRegistry();
            var grid = new SpatialHashGrid();
            grid.Initialize(20, 20, 1f);
            var clock = new SimulationClock();
            clock.AdvanceTicks(50);

            grid.StaticGrid.WorldToCell(new Vec2(0.5f, 0.5f), out int cx, out int cy);
            grid.StaticGrid.SetWall(cx, cy);

            var occupantId = EntityId.Generate();
            var occupant = new WorldEntity(occupantId, EntityType.Pawn, new Vec2(3f, 0f), 0.35f, agent: new AgentState(occupantId));
            registry.Register(occupant);

            var moverId = EntityId.Generate();
            var mover = new WorldEntity(moverId, EntityType.Pawn, new Vec2(1f, 0f), 0.35f, agent: new AgentState(moverId));
            registry.Register(mover);

            grid.UpdateFromRegistry(registry);

            var wiredLocomotion = new Locomotion(grid, registry, clock);

            // Wall collision rejection
            Assert(!wiredLocomotion.TryDisplacePawn(mover, new Vec2(0.5f, 0.5f)), "Moving into wall cell is rejected");

            // Crowding separation
            mover.SetPosition(new Vec2(2.7f, 0f));
            Assert(wiredLocomotion.IsCrowdedMove(mover, new Vec2(2.8f, 0f)), "Stepping closer to occupant is crowded");
            Assert(!wiredLocomotion.IsCrowdedMove(mover, new Vec2(2.5f, 0f)), "Stepping apart is not crowded");

            // Path movement arrival without stacking
            mover.SetPosition(new Vec2(1f, 0f));
            var path = new PathResponse
            {
                Waypoints = new[] { new Vec2(3f, 0f) },
                Length = 1,
                IsValid = true
            };
            var moveState = new ActiveIntentState
            {
                Intent = new WanderIntent(mover.Id, path),
                CurrentWaypointIndex = 0,
                LastSampledPosition = mover.Position
            };
            IntentResult stepResult = IntentResult.InProgress;
            for (int i = 0; i < 40 && stepResult == IntentResult.InProgress; i++)
            {
                stepResult = wiredLocomotion.ExecutePathMovement(mover, path, moveState);
                grid.UpdateFromRegistry(registry);
            }
            Assert(stepResult == IntentResult.Success, "Mover arrives successfully next to occupied destination");
            float gap = Vec2.Distance(mover.Position, occupant.Position);
            Assert(gap >= 0.35f, $"Mover did not stack on occupant (gap: {gap})");

            // Tier 3 unstuck disengage and resource blacklist duration
            var resourceId = EntityId.Generate();
            var unstuckState = new ActiveIntentState
            {
                Intent = new HarvestResourceIntent(mover.Id, resourceId, default),
                StallCount = 4
            };
            var unstuckResult = wiredLocomotion.ApplyProgressiveUnstuckReflex(mover, new Vec2(4f, 0f), unstuckState);
            Assert(unstuckResult == IntentResult.PathBlocked, "Tier 3 unstuck returns PathBlocked");
            Assert(mover.Agent.Memory.IsBlacklisted(resourceId, 349), "Resource is blacklisted at tick 349 (50 + 300 - 1)");
            Assert(!mover.Agent.Memory.IsBlacklisted(resourceId, 350), "Resource blacklist expires at tick 350");

            TestLog.Log("[PASS] Test 6: Standalone Locomotion validation verified without SimulationManager.");
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
