using System;
using Simpiens.Cognition;
using Simpiens.Cognition.Contracts;
using Simpiens.Cognition.Pathfinding;
using Simpiens.Entities;
using Simpiens.Intervention;
using Simpiens.Simulation;
using Simpiens.Simulation.Economy;
using Simpiens.Simulation.Spatial;

namespace Simpiens.Testing.Environment
{
    /// <summary>
    /// Automated validation suite for Section 2.4:
    /// SimulationManager Modularization, Typed Influence Commands, Authoritative Economy Ledger,
    /// Escrow Solvency, and Zero-Allocation Modular Intent Execution.
    /// </summary>
    public class SimulationManagerModularizationValidator
    {
        public void Start()
        {
            RunAllValidationTests();
        }

        public void RunAllValidationTests()
        {
            TestLog.Log("[SimulationManagerModularizationValidator] Beginning Section 2.4 Architecture Validation...");

            ValidateTypedBountyPostingAndDraining();
            ValidateEscrowRejectionOnInsufficientFunds();
            ValidateLedgerConservationInvariant();
            ValidateModularIntentDispatch();
            ValidateZeroAllocationsDuringExecution();
            ValidateThrowingHandlerIsContained();

            TestLog.Log("<color=green>[SimulationManagerModularizationValidator] ALL 6 TESTS PASSED SUCCESSFULLY!</color>");
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
            var stall = new TradeStallData(stallId, Vec2.Zero, treasuryGold: 100);
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

            TestLog.Log("[SimulationManagerModularizationValidator] Test 1 Passed: Typed bounty command queued, drained, and posted with escrow.");
        }

        private void ValidateEscrowRejectionOnInsufficientFunds()
        {
            var registry = new WorldRegistry();
            var interventionService = new PlayerInterventionService();
            var ledger = new EconomyLedger();
            var simManager = new SimulationManager(registry, null, interventionService: interventionService, economyLedger: ledger);

            var stallId = NextTestGuid();
            var stall = new TradeStallData(stallId, Vec2.Zero, treasuryGold: 10); // Only 10 gold in treasury!
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

            TestLog.Log("[SimulationManagerModularizationValidator] Test 2 Passed: Escrow rejection on insufficient treasury verified.");
        }

        private void ValidateLedgerConservationInvariant()
        {
            var ledger = new EconomyLedger();

            uint initialStallGold = 100;
            uint initialAgentGold = 50;

            // Faucet: Initial economy seed
            ledger.RecordFaucet(150, "InitialSeed");

            var stall = new TradeStallData(NextTestGuid(), Vec2.Zero, treasuryGold: initialStallGold);

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

            TestLog.Log("[SimulationManagerModularizationValidator] Test 3 Passed: Gold conservation invariant (Delta Circulation == Faucets - Sinks) verified.");
        }

        private void ValidateModularIntentDispatch()
        {
            var registry = new WorldRegistry();
            var handlerRegistry = IntentHandlerRegistry.CreateDefault();
            var simManager = new SimulationManager(registry, null, intentRegistry: handlerRegistry);

            var goCrawler = HeadlessEntities.Crawler(null, simManager, null, Vec2.Zero, id: NextTestGuid(), name: "[Test_ModularPawn]", register: false);
            var node = goCrawler.Node;
            var agent = goCrawler.Agent;
            registry.Register(node);

            var path = new PathResponse
            {
                Waypoints = new Vec2[] { new Vec2(5f, 5f) },
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

            HeadlessEntities.Remove(registry, goCrawler);
            TestLog.Log("[SimulationManagerModularizationValidator] Test 4 Passed: Modular intent handler registration and dispatch verified.");
        }

        private void ValidateZeroAllocationsDuringExecution()
        {
            var registry = new WorldRegistry();
            var handlerRegistry = IntentHandlerRegistry.CreateDefault();
            var simManager = new SimulationManager(registry, null, intentRegistry: handlerRegistry);

            var goCrawler = HeadlessEntities.Crawler(null, simManager, null, Vec2.Zero, id: NextTestGuid(), name: "[Test_ZeroAllocPawn]", register: false);
            var node = goCrawler.Node;
            var agent = goCrawler.Agent;
            registry.Register(node);

            var path = new PathResponse
            {
                Waypoints = new Vec2[] { new Vec2(1f, 0f) },
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

            HeadlessEntities.Remove(registry, goCrawler);
            TestLog.Log("[SimulationManagerModularizationValidator] Test 5 Passed: Zero GC allocations on modular intent execution hot path verified.");
        }

        // Wanders in place: throws for one agent, finishes at once for the others
        private sealed class ThrowingWanderHandler : IntentHandlerBase<WanderIntent>
        {
            public EntityId Thrower;
            protected override IntentResult Execute(WorldEntity node, WanderIntent intent, ActiveIntentState state, IIntentExecutionContext context)
            {
                if (node.Id == Thrower) throw new InvalidOperationException("handler fault (injected)");
                return IntentResult.Success;
            }
        }

        /// <summary>
        /// ROADMAP §1A.2 #17: a handler that throws, or a completion callback that throws, aborts that intent only. The
        /// agents after it in the same tick still run, every finished intent is removed once (none runs again on the next
        /// tick), each path goes back to its pool once, and both faults are counted with a description of the first.
        /// </summary>
        private void ValidateThrowingHandlerIsContained()
        {
            var registry = new WorldRegistry();
            var handlers = new IntentHandlerRegistry();
            var handler = new ThrowingWanderHandler();
            handlers.RegisterHandler(handler);
            var simManager = new SimulationManager(registry, null, intentRegistry: handlers);

            // Agent-id order: the thrower runs first, the one whose completion throws second, a plain one last
            var ids = new[] { new EntityId(1, 0, 0, 0), new EntityId(2, 0, 0, 0), new EntityId(3, 0, 0, 0) };
            var returns = new int[ids.Length];
            var outcomes = new IntentResult?[ids.Length];
            for (int i = 0; i < ids.Length; i++)
            {
                registry.Register(new WorldEntity(ids[i], EntityType.Pawn, new Vec2(i * 3f, 0f), 0.3f, name: $"[Test_Fault{i}]"));
                int index = i;
                var path = new PathResponse();
                path.Initialize(4, _ => returns[index]++);
                path.MarkRented();
                var intent = new WanderIntent(ids[i], path);
                intent.OnComplete = outcome =>
                {
                    outcomes[index] = outcome;
                    if (index == 1) throw new InvalidOperationException("completion fault (injected)");
                };
                simManager.EnqueueIntent(intent);
            }
            handler.Thrower = ids[0];

            simManager.Tick();
            Assert(simManager.IntentFaults == 2, $"2 faults counted (got {simManager.IntentFaults}).");
            Assert(simManager.FirstIntentFault != null && simManager.FirstIntentFault.Contains("WanderIntent") && simManager.FirstIntentFault.Contains("executing")
                && simManager.FirstIntentFault.Contains("handler fault"), $"The first fault is described: {simManager.FirstIntentFault}");
            Assert(outcomes[0] == IntentResult.Aborted, $"The throwing handler's intent is aborted (got {outcomes[0]}).");
            Assert(outcomes[1] == IntentResult.Success && outcomes[2] == IntentResult.Success, $"The agents after it still ran ({outcomes[1]}, {outcomes[2]}).");
            Assert(simManager.ActiveIntents.Count == 0, $"Every finished intent is removed ({simManager.ActiveIntents.Count} left).");

            simManager.Tick();
            Assert(simManager.IntentFaults == 2, $"Nothing runs again on the next tick ({simManager.IntentFaults} faults).");
            for (int i = 0; i < ids.Length; i++) Assert(returns[i] == 1, $"Path {i} returned to its pool once (got {returns[i]}).");

            TestLog.Log("[SimulationManagerModularizationValidator] Test 6 Passed: A throwing handler or completion aborts its own intent only, is counted, and every finished intent is removed once.");
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
