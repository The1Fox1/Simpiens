using System;
using Simpiens.Cognition;
using Simpiens.Cognition.Contracts;
using Simpiens.Cognition.Pathfinding;
using Simpiens.Entities;
using Simpiens.Intervention;
using Simpiens.Simulation;
using Simpiens.Simulation.Economy;
using UnityEngine;
using VContainer.Unity;

namespace Simpiens.Testing.Environment
{
    /// <summary>
    /// Automated validation suite for Section 2.4:
    /// SimulationManager Modularization, Typed Influence Commands, Authoritative Economy Ledger,
    /// Escrow Solvency, and Zero-Allocation Modular Intent Execution.
    /// </summary>
    public class SimulationManagerModularizationValidator : IStartable
    {
        public void Start()
        {
            RunAllValidationTests();
        }

        public void RunAllValidationTests()
        {
            Debug.Log("[SimulationManagerModularizationValidator] Beginning Section 2.4 Architecture Validation...");

            ValidateTypedBountyPostingAndDraining();
            ValidateEscrowRejectionOnInsufficientFunds();
            ValidateLedgerConservationInvariant();
            ValidateModularIntentDispatch();
            ValidateZeroAllocationsDuringExecution();

            Debug.Log("<color=green>[SimulationManagerModularizationValidator] ALL 5 TESTS PASSED SUCCESSFULLY!</color>");
        }

        private static uint _guidSeed = 1000;
        private static EntityId NextTestGuid() => new EntityId(_guidSeed++, 0xABCD, 0x1234, 0x5678);

        private void ValidateTypedBountyPostingAndDraining()
        {
            var registry = new WorldRegistry();
            var interventionService = new PlayerInterventionService();
            var ledger = new EconomyLedger();
            var simManager = new SimulationManager(registry, null, interventionService: interventionService, economyLedger: ledger);

            var stallId = NextTestGuid();
            var stall = new TradeStallData(stallId, Vector2.zero, treasuryGold: 100);
            simManager.RegisterTradeStall(stall);

            var cmd = InfluenceEvent.CreatePostBounty("Player", new PostBountyCommand(1, BountyType.GatherResource, sectorId: 1, itemType: ItemType.IronOre, quantity: 5, rewardGold: 20, creatorStallId: stallId));
            interventionService.QueueInfluence(cmd);

            simManager.SimulateTick(1);

            Assert(simManager.TryGetBounty(1, out var bounty), "Bounty 1 was posted via typed InfluenceEvent.");
            Assert(bounty.RewardGold == 20, $"Bounty reward is {bounty.RewardGold} instead of 20.");
            Assert(bounty.TargetQuantity == 5, $"Bounty quantity is {bounty.TargetQuantity} instead of 5.");
            Assert(ledger.HasEscrow(1), "Escrow ledger recorded escrow for bounty 1.");
            Assert(ledger.TotalEscrowGold == 20, $"Escrow ledger total is {ledger.TotalEscrowGold} instead of 20.");

            Assert(simManager.TryGetTradeStall(stallId, out var updatedStall), "Stall found in manager.");
            Assert(updatedStall.TreasuryGold == 80, $"Stall treasury deducted to {updatedStall.TreasuryGold} instead of 80.");

            Debug.Log("[SimulationManagerModularizationValidator] Test 1 Passed: Typed bounty command queued, drained, and posted with escrow.");
        }

        private void ValidateEscrowRejectionOnInsufficientFunds()
        {
            var registry = new WorldRegistry();
            var interventionService = new PlayerInterventionService();
            var ledger = new EconomyLedger();
            var simManager = new SimulationManager(registry, null, interventionService: interventionService, economyLedger: ledger);

            var stallId = NextTestGuid();
            var stall = new TradeStallData(stallId, Vector2.zero, treasuryGold: 10); // Only 10 gold in treasury!
            simManager.RegisterTradeStall(stall);

            // Attempt to post a bounty with 50 gold reward from a stall with only 10 gold!
            var cmd = InfluenceEvent.CreatePostBounty("Player", new PostBountyCommand(2, BountyType.GatherResource, sectorId: 1, itemType: ItemType.IronOre, quantity: 5, rewardGold: 50, creatorStallId: stallId));
            interventionService.QueueInfluence(cmd);

            simManager.SimulateTick(2);

            Assert(!simManager.TryGetBounty(2, out _), "Bounty 2 was rejected due to insufficient stall treasury for escrow.");
            Assert(!ledger.HasEscrow(2), "Escrow ledger did not record escrow for rejected bounty 2.");
            Assert(ledger.TotalEscrowGold == 0, "Total escrow gold remains 0.");

            Assert(simManager.TryGetTradeStall(stallId, out var updatedStall), "Stall found in manager.");
            Assert(updatedStall.TreasuryGold == 10, $"Stall treasury remains 10 (actual: {updatedStall.TreasuryGold}).");

            Debug.Log("[SimulationManagerModularizationValidator] Test 2 Passed: Escrow rejection on insufficient treasury verified.");
        }

        private void ValidateLedgerConservationInvariant()
        {
            var ledger = new EconomyLedger();

            uint initialStallGold = 100;
            uint initialAgentGold = 50;

            // Faucet: Initial economy seed
            ledger.RecordFaucet(150, "InitialSeed");

            var stall = new TradeStallData(NextTestGuid(), Vector2.zero, treasuryGold: initialStallGold);

            // 1. Post bounty: lock 40 gold in escrow
            ushort bountyId = 10;
            uint bountyReward = 40;
            bool locked = ledger.TryLockEscrow(stall.StallEntityId, bountyId, bountyReward, ref stall);
            Assert(locked, "Escrow lock succeeded.");
            Assert(stall.TreasuryGold == 60, "Stall treasury is now 60.");
            Assert(ledger.TotalEscrowGold == 40, "Escrow ledger tracks 40 gold.");

            // Conservation check during escrow
            Assert(ledger.ValidateConservation(stall.TreasuryGold, initialAgentGold, out int disc1), $"Invariant holds during escrow (disc: {disc1}).");

            // 2. Deliver bounty: release 40 gold to agent
            var agentId = NextTestGuid();
            bool released = ledger.TryReleaseEscrowToClaimant(bountyId, agentId, null, out uint releasedAmount);
            Assert(released, "Escrow release succeeded.");
            Assert(releasedAmount == 40, "Released amount is 40.");
            Assert(ledger.TotalEscrowGold == 0, "Escrow ledger total is now 0.");

            uint updatedAgentGold = initialAgentGold + releasedAmount; // 90 gold

            // Conservation check after payout
            Assert(ledger.ValidateConservation(stall.TreasuryGold, updatedAgentGold, out int disc2), $"Invariant holds after payout (disc: {disc2}).");
            Assert(disc2 == 0, "Discrepancy is exactly 0.");

            Debug.Log("[SimulationManagerModularizationValidator] Test 3 Passed: Gold conservation invariant (Delta Circulation == Faucets - Sinks) verified.");
        }

        private void ValidateModularIntentDispatch()
        {
            var registry = new WorldRegistry();
            var handlerRegistry = IntentHandlerRegistry.CreateDefault();
            var simManager = new SimulationManager(registry, null, intentRegistry: handlerRegistry);

            var go = new GameObject("[Test_ModularPawn]");
            var node = go.AddComponent<NodeController>();
            node.Initialize(NextTestGuid(), new NodeConfiguration(2f));
            var agent = go.AddComponent<AutonomousAgent>();
            agent.Initialize(node.Id, null, null, simManager, null);
            registry.RegisterNode(node);

            var path = new PathResponse
            {
                Waypoints = new Vector2[] { new Vector2(5f, 5f) },
                Length = 1,
                IsValid = true
            };

            var state = new ActiveIntentState
            {
                Intent = new WanderIntent(node.Id, path),
                CurrentWaypointIndex = 0,
                LastSampledPosition = node.Position
            };

            var result = simManager.ExecuteIntent(node, state);
            Assert(result == IntentResult.InProgress || result == IntentResult.Success, "Modular WanderIntentHandler executed successfully.");
            Assert(agent.VisualState == AgentVisualState.Walking, "Agent visual state set to Walking by WanderIntentHandler.");

            GameObject.DestroyImmediate(go);
            Debug.Log("[SimulationManagerModularizationValidator] Test 4 Passed: Modular intent handler registration and dispatch verified.");
        }

        private void ValidateZeroAllocationsDuringExecution()
        {
            var registry = new WorldRegistry();
            var handlerRegistry = IntentHandlerRegistry.CreateDefault();
            var simManager = new SimulationManager(registry, null, intentRegistry: handlerRegistry);

            var go = new GameObject("[Test_ZeroAllocPawn]");
            var node = go.AddComponent<NodeController>();
            node.Initialize(NextTestGuid(), new NodeConfiguration(2f));
            var agent = go.AddComponent<AutonomousAgent>();
            agent.Initialize(node.Id, null, null, simManager, null);
            registry.RegisterNode(node);

            var path = new PathResponse
            {
                Waypoints = new Vector2[] { new Vector2(1f, 0f) },
                Length = 1,
                IsValid = true
            };

            var state = new ActiveIntentState
            {
                Intent = new WanderIntent(node.Id, path),
                CurrentWaypointIndex = 0,
                LastSampledPosition = node.Position
            };

            // Warm up JIT and cache lines
            for (int i = 0; i < 5; i++)
            {
                simManager.ExecuteIntent(node, state);
            }

            // Measurement
            long bytesBefore = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 20; i++)
            {
                simManager.ExecuteIntent(node, state);
            }
            long bytesAfter = GC.GetAllocatedBytesForCurrentThread();
            long delta = bytesAfter - bytesBefore;

            Assert(delta == 0, $"Hot-path ExecuteIntent allocated {delta} bytes (expected 0 bytes).");

            GameObject.DestroyImmediate(go);
            Debug.Log("[SimulationManagerModularizationValidator] Test 5 Passed: Zero GC allocations on modular intent execution hot path verified.");
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
