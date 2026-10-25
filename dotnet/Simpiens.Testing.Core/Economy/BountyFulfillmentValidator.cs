using Simpiens.Testing.Environment;
using System;
using System.Runtime.InteropServices;
using Simpiens.Cognition;
using Simpiens.Cognition.Contracts;
using Simpiens.Cognition.Planning;
using Simpiens.Cognition.Planning.Actions;
using Simpiens.Cognition.Planning.Goals;
using Simpiens.Entities;
using Simpiens.Intervention;
using Simpiens.Simulation;
using Simpiens.Simulation.Economy;
using Simpiens.Simulation.Spatial;

namespace Simpiens.Testing.Economy
{
    /// <summary>
    /// Automated headless domain validation suite for Epic 9 Slice 3:
    /// The Public Bounty Board & Player Intervention Pipeline.
    /// Runs as a test (ValidatorTests), never at boot.
    /// </summary>
    public class BountyFulfillmentValidator
    {
        public void Start()
        {
            TestLog.Log("[BountyFulfillmentValidator] Beginning automated validation of Epic 9 Slice 3...");

            ValidateBountyContractLayoutAndZeroAlloc();
            ValidatePlayerInterventionPostBounty();
            ValidateClaimBountyAtomic();
            ValidateBountyExpirationAndEscrowRefund();
            ValidateDeliverBountyAtomicTransfer();
            ValidateDeliverBountyAffinityReward();
            ValidateGOAPBountyChainResolution();
            ValidatePlayerInterventionCancelBounty();

            TestLog.Log("<color=green>[BountyFulfillmentValidator] ALL 8 DOMAIN TESTS PASSED SUCCESSFULLY!</color>");
        }

        private void ValidateBountyContractLayoutAndZeroAlloc()
        {
            int structSize = Marshal.SizeOf<BountyContract>();
            Assert(structSize <= 64, $"BountyContract size ({structSize} bytes) exceeds 64-byte threshold.");

            // Verify default empty state
            Assert(BountyContract.Empty.BountyId == 0, "BountyContract.Empty BountyId is 0.");
            Assert(BountyContract.Empty.Type == BountyType.None, "BountyContract.Empty Type is None.");
            Assert(BountyContract.Empty.Status == BountyStatus.Open, "BountyContract.Empty Status is Open.");

            // Verify constructor and immutable modifiers
            var claimantId = EntityId.Generate();
            var stallId = EntityId.Generate();
            var contract = new BountyContract(
                bountyId: 101,
                type: BountyType.GatherResource,
                targetSectorId: 2,
                targetItemType: ItemType.IronOre,
                targetQuantity: 3,
                rewardGold: 75,
                issuedTick: 100,
                expirationTick: 1000,
                targetLocation: new Vec2(15f, -20f),
                claimantId: default,
                creatorStallId: stallId,
                status: BountyStatus.Open);

            Assert(contract.BountyId == 101, "BountyId preserved.");
            Assert(contract.Type == BountyType.GatherResource, "Type preserved.");
            Assert(contract.TargetItemType == ItemType.IronOre, "TargetItemType preserved.");
            Assert(contract.TargetQuantity == 3, "TargetQuantity preserved.");
            Assert(contract.RewardGold == 75, "RewardGold preserved.");
            Assert(contract.IsActive(500), "Contract is active at tick 500.");
            Assert(!contract.IsActive(1500), "Contract is inactive at tick 1500.");
            Assert(!contract.IsClaimed, "Contract starts unclaimed.");

            var claimed = contract.WithClaimant(claimantId);
            Assert(claimed.IsClaimed, "Claimed contract reports IsClaimed = true.");
            Assert(claimed.ClaimantId == claimantId, "ClaimantId preserved.");
            Assert(claimed.Status == BountyStatus.Claimed, "Status is Claimed.");

            var completed = claimed.WithStatus(BountyStatus.Completed);
            Assert(completed.Status == BountyStatus.Completed, "Status updated to Completed.");

            // Test SharedWorldSnapshot integration
            var snapshot = new SharedWorldSnapshot(10, 10, 2.0f, 8, _ => { });
            Assert(snapshot.ActiveBountyCount == 0, "Snapshot starts with 0 active bounties.");

            snapshot.SetBounties(new BountyContract[] { contract, claimed }, 2);
            Assert(snapshot.ActiveBountyCount == 2, "Snapshot ActiveBountyCount is 2.");

            bool found = snapshot.TryGetBounty(101, out var retrieved);
            Assert(found && retrieved.BountyId == 101, "TryGetBounty found contract 101.");

            snapshot.Clear();
            Assert(snapshot.ActiveBountyCount == 0, "Snapshot cleared active bounties.");

            TestLog.Log("[BountyFulfillmentValidator] Test 1 Passed: BountyContract unmanaged layout & snapshot buffering verified.");
        }

        private void ValidatePlayerInterventionPostBounty()
        {
            var registry = new WorldRegistry();
            var clock = new SimulationClock();
            var intervention = new PlayerInterventionService();
            var simManager = new SimulationManager(registry, null, clock, null, null, null, intervention);

            var stallId = EntityId.Generate();
            var stall = new TradeStallData(stallId, Vec2.Zero, rationSellPrice: 5, rationStock: 50, treasuryGold: 500);
            simManager.RegisterTradeStall(stall);

            // Player queues a PostBounty influence event
            intervention.QueueInfluence(new InfluenceEvent("Player", "PostBounty", "1,GatherResource,0,IronOre,2,50,500,5.0,5.0"));

            // SimulationManager ticks and drains intervention queue
            simManager.Tick();

            Assert(simManager.TryGetBounty(1, out var bounty), "Bounty 1 was created from influence event.");
            Assert(bounty.TargetItemType == ItemType.IronOre, "Bounty target item is IronOre.");
            Assert(bounty.TargetQuantity == 2, "Bounty target quantity is 2.");
            Assert(bounty.RewardGold == 50, "Bounty reward is 50 gold.");
            Assert(bounty.Status == BountyStatus.Open, "Bounty status is Open.");

            // Verify escrow deduction from stall treasury (500 - 50 = 450)
            Assert(simManager.TryGetTradeStall(stallId, out var updatedStall), "Stall found.");
            Assert(updatedStall.TreasuryGold == 450, $"Stall treasury is {updatedStall.TreasuryGold} instead of 450 (escrow deducted).");

            TestLog.Log("[BountyFulfillmentValidator] Test 2 Passed: Player intervention PostBounty and gold escrow verified.");
        }

        private void ValidateClaimBountyAtomic()
        {
            var registry = new WorldRegistry();
            var clock = new SimulationClock();
            var simManager = new SimulationManager(registry, null, clock);

            // Live bounties must be escrowed by a stall (§3.1), so register one that can cover the reward
            var stall = new TradeStallData(EntityId.Generate(), Vec2.Zero, rationSellPrice: 5, rationStock: 50, treasuryGold: 500);
            simManager.RegisterTradeStall(stall);

            var contract = new BountyContract(1, BountyType.GatherResource, 0, ItemType.IronOre, 1, 30, 0, 1000);
            Assert(simManager.TryRegisterBounty(contract), "Escrowed bounty registered.");

            var node1 = HeadlessEntities.Prop(registry, EntityType.Pawn, Vec2.Zero, id: EntityId.Generate(), name: "[Test_Claimant1]");

            var node2 = HeadlessEntities.Prop(registry, EntityType.Pawn, Vec2.Zero, id: EntityId.Generate(), name: "[Test_Claimant2]");

            var state1 = new ActiveIntentState { Intent = new ClaimBountyIntent(node1.Id, 1) };
            var res1 = simManager.ExecuteIntent(node1, state1);
            Assert(res1 == IntentResult.Success, $"First claimant got {res1} instead of Success.");

            Assert(simManager.TryGetBounty(1, out var claimed), "Bounty retrieved.");
            Assert(claimed.Status == BountyStatus.Claimed, "Bounty is Claimed.");
            Assert(claimed.ClaimantId == node1.Id, "Claimant is node1.");

            // Second claimant attempt on already claimed bounty must fail
            var state2 = new ActiveIntentState { Intent = new ClaimBountyIntent(node2.Id, 1) };
            var res2 = simManager.ExecuteIntent(node2, state2);
            Assert(res2 == IntentResult.Failed, $"Second claimant got {res2} instead of Failed.");

            registry?.Unregister(node1);
            registry?.Unregister(node2);
            TestLog.Log("[BountyFulfillmentValidator] Test 3 Passed: Atomic bounty claiming and double-claim prevention verified.");
        }

        private void ValidateBountyExpirationAndEscrowRefund()
        {
            var registry = new WorldRegistry();
            var clock = new SimulationClock();
            var intervention = new PlayerInterventionService();
            var simManager = new SimulationManager(registry, null, clock, null, null, null, intervention);

            var stallId = EntityId.Generate();
            var stall = new TradeStallData(stallId, Vec2.Zero, rationSellPrice: 5, rationStock: 50, treasuryGold: 500);
            simManager.RegisterTradeStall(stall);

            // Post bounty with 20 tick lifespan (duration = 20)
            intervention.QueueInfluence(new InfluenceEvent("Player", "PostBounty", "2,GatherResource,0,IronOre,1,40,20"));
            simManager.Tick(); // tick 0 -> posted, expires at tick 20

            Assert(simManager.TryGetTradeStall(stallId, out var escrowedStall), "Stall retrieved.");
            Assert(escrowedStall.TreasuryGold == 460, "40 gold escrowed.");

            // Advance clock to tick 30 (expired)
            clock.AdvanceTicks(30);

            simManager.Tick(); // Ticks at current clock tick (> 20)

            Assert(simManager.TryGetBounty(2, out var expiredBounty), "Bounty retrieved.");
            Assert(expiredBounty.Status == BountyStatus.Expired, $"Bounty status is {expiredBounty.Status} instead of Expired.");

            // Verify escrow gold was refunded back to stall treasury (460 + 40 = 500)
            Assert(simManager.TryGetTradeStall(stallId, out var refundedStall), "Stall retrieved.");
            Assert(refundedStall.TreasuryGold == 500, $"Stall treasury is {refundedStall.TreasuryGold} instead of 500 (refunded).");

            TestLog.Log("[BountyFulfillmentValidator] Test 4 Passed: Bounty expiration and escrow refund verified.");
        }

        private void ValidateDeliverBountyAtomicTransfer()
        {
            var registry = new WorldRegistry();
            var clock = new SimulationClock();
            var simManager = new SimulationManager(registry, null, clock);

            var stallId = EntityId.Generate();
            var stall = new TradeStallData(stallId, Vec2.Zero, rationSellPrice: 5, rationStock: 50, oreBuyPrice: 8, oreStock: 0, herbBuyPrice: 12, herbStock: 0, treasuryGold: 500);
            simManager.RegisterTradeStall(stall);

            var goCrawler = HeadlessEntities.Crawler(null, simManager, clock, new Vec2(0.5f, 0.5f), id: EntityId.Generate(), name: "[Test_Deliverer]", register: false);
            var node = goCrawler.Node;
            var agent = goCrawler.Agent;
            agent.Inventory.Gold = 10;
            // Agent holds 2 IronOre in inventory
            agent.Inventory.TryAddItem(new ItemInstance(1, ItemType.IronOre, 2, 8), out _);
            registry.Register(node);

            // Register and claim bounty
            var contract = new BountyContract(3, BountyType.GatherResource, 0, ItemType.IronOre, 2, 60, 0, 1000, claimantId: node.Id, creatorStallId: stallId, status: BountyStatus.Claimed);
            simManager.RegisterBounty(contract);

            var deliverState = new ActiveIntentState
            {
                Intent = new DeliverBountyIntent(node.Id, 3, stallId),
                CurrentWaypointIndex = 0,
                LastSampledPosition = node.Position
            };

            var result = simManager.ExecuteIntent(node, deliverState);
            Assert(result == IntentResult.Success, $"ExecuteIntent returned {result} instead of Success.");

            // Agent should have 0 IronOre, +60 Gold (10 + 60 = 70)
            Assert(!agent.Inventory.HasItem(ItemType.IronOre), "Agent inventory no longer has IronOre.");
            Assert(agent.Inventory.Gold == 70, $"Agent gold is {agent.Inventory.Gold} instead of 70.");

            // Stall should have +2 OreStock
            Assert(simManager.TryGetTradeStall(stallId, out var updatedStall), "Stall retrieved.");
            Assert(updatedStall.OreStock == 2, $"Stall OreStock is {updatedStall.OreStock} instead of 2.");

            // Bounty status should be Completed
            Assert(simManager.TryGetBounty(3, out var completedBounty), "Bounty retrieved.");
            Assert(completedBounty.Status == BountyStatus.Completed, "Bounty status is Completed.");

            HeadlessEntities.Remove(registry, goCrawler);
            TestLog.Log("[BountyFulfillmentValidator] Test 5 Passed: Atomic bounty delivery, item removal, gold award, and stall restocking verified.");
        }

        private void ValidateDeliverBountyAffinityReward()
        {
            var registry = new WorldRegistry();
            var clock = new SimulationClock();
            var simManager = new SimulationManager(registry, null, clock);

            var stallId = EntityId.Generate();
            var stall = new TradeStallData(stallId, Vec2.Zero, rationSellPrice: 5, rationStock: 50, treasuryGold: 500);
            simManager.RegisterTradeStall(stall);

            var goCrawler = HeadlessEntities.Crawler(null, simManager, clock, Vec2.Zero, id: EntityId.Generate(), name: "[Test_Affinity_Deliverer]", register: false);
            var node = goCrawler.Node;
            var agent = goCrawler.Agent;
            agent.Inventory.TryAddItem(new ItemInstance(1, ItemType.MedicinalHerb, 1, 10), out _);
            registry.Register(node);

            var contract = new BountyContract(4, BountyType.GatherResource, 0, ItemType.MedicinalHerb, 1, 40, 0, 1000, claimantId: node.Id, creatorStallId: stallId, status: BountyStatus.Claimed);
            simManager.RegisterBounty(contract);

            var state = new ActiveIntentState { Intent = new DeliverBountyIntent(node.Id, 4, stallId) };
            simManager.ExecuteIntent(node, state);

            // Check affinity towards PlayerEntityId (+15 Trust, +10 Warmth, +10 Respect)
            var aff = agent.Memory.GetAffinity(SimulationManager.PlayerEntityId, 0);
            Assert(aff.Trust == 15, $"Affinity Trust is {aff.Trust} instead of 15.");
            Assert(aff.Warmth == 10, $"Affinity Warmth is {aff.Warmth} instead of 10.");
            Assert(aff.Respect == 10, $"Affinity Respect is {aff.Respect} instead of 10.");

            HeadlessEntities.Remove(registry, goCrawler);
            TestLog.Log("[BountyFulfillmentValidator] Test 6 Passed: Dyadic Trade-to-Affinity boost (+15 Trust, +10 Warmth, +10 Respect) verified.");
        }

        private void ValidateGOAPBountyChainResolution()
        {
            var graph = new ActionGraph();
            var claimAction = new ClaimGatherBountyAction();
            var claimScoutAction = new ClaimScoutBountyAction();
            var travelStall = new TravelToStallAction(null);
            var deliverAction = new DeliverBountyAction();

            graph.RegisterAction(claimAction);
            graph.RegisterAction(claimScoutAction);
            graph.RegisterAction(travelStall);
            graph.RegisterAction(deliverAction);

            // An open Gather bounty must be visible on the board for the typed claim action to be valid
            var snapshot = new SharedWorldSnapshot(10, 10, 2.0f, 8, _ => { });
            var open = new BountyContract(7, BountyType.GatherResource, 0, ItemType.IronOre, 1, 30, 0, 1000, status: BountyStatus.Open);
            snapshot.SetBounties(new BountyContract[] { open }, 1);

            var planner = new GoapPlanner();
            var goal = new FulfillBountyGoal();
            var context = new AgentContext(EntityId.Generate(), Vec2.Zero, hunger: 20f, energy: 80f, frustration: 0f, snapshot: snapshot, memory: null, currentTick: 0);

            // Agent already carries the bounty item, has not claimed the bounty and is not at the stall yet
            var initial = WorldState.Empty
                .With(StateFact.HasActiveBounty, false)
                .With(StateFact.HasBountyItem, true)
                .With(StateFact.HasSalvageInInventory, true)
                .With(StateFact.AtTradeStall, false);

            var plan = new GoapPlan();
            bool resolved = planner.Plan(initial, goal, graph, context, plan);

            Assert(resolved, "GOAP planner successfully resolved plan for FulfillBountyGoal.");
            Assert(plan.Actions.Count == 3, $"Plan contains {plan.Actions.Count} actions instead of 3.");

            // Expected chain: ClaimGatherBountyAction -> TravelToStallAction -> DeliverBountyAction
            Assert(plan.Actions[0] == claimAction, "Step 1 is ClaimGatherBountyAction (Scout claim rejected: no open Scout bounty).");
            Assert(plan.Actions[1] == travelStall, "Step 2 is TravelToStallAction.");
            Assert(plan.Actions[2] == deliverAction, "Step 3 is DeliverBountyAction.");

            TestLog.Log("[BountyFulfillmentValidator] Test 7 Passed: GOAP bounty chain resolution (ClaimGather -> Travel -> Deliver) verified.");
        }

        private void ValidatePlayerInterventionCancelBounty()
        {
            var registry = new WorldRegistry();
            var clock = new SimulationClock();
            var intervention = new PlayerInterventionService();
            var simManager = new SimulationManager(registry, null, clock, null, null, null, intervention);

            var stallId = EntityId.Generate();
            var stall = new TradeStallData(stallId, Vec2.Zero, rationSellPrice: 5, rationStock: 50, treasuryGold: 500);
            simManager.RegisterTradeStall(stall);

            // Post bounty #5 with 50 gold reward
            intervention.QueueInfluence(new InfluenceEvent("Player", "PostBounty", "5,GatherResource,0,IronOre,1,50,500"));
            simManager.Tick();

            Assert(simManager.TryGetBounty(5, out var posted), "Bounty 5 posted.");
            Assert(simManager.TryGetTradeStall(stallId, out var escrowedStall), "Stall found.");
            Assert(escrowedStall.TreasuryGold == 450, "50 gold escrowed.");

            // Now cancel bounty #5
            intervention.QueueInfluence(new InfluenceEvent("Player", "CancelBounty", "5"));
            simManager.Tick();

            Assert(simManager.TryGetBounty(5, out var cancelled), "Bounty 5 found.");
            Assert(cancelled.Status == BountyStatus.Cancelled, "Bounty 5 is Cancelled.");

            // Verify escrow gold was refunded back to stall treasury (450 + 50 = 500)
            Assert(simManager.TryGetTradeStall(stallId, out var refundedStall), "Stall found.");
            Assert(refundedStall.TreasuryGold == 500, $"Stall treasury is {refundedStall.TreasuryGold} instead of 500.");

            TestLog.Log("[BountyFulfillmentValidator] Test 8 Passed: Player intervention CancelBounty and escrow refund verified.");
        }

        private void Assert(bool condition, string message)
        {
            if (!condition)
            {
                throw new Exception($"[ASSERTION FAILED] {message}");
            }
        }
    }
}
