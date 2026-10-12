using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Simpiens.Cognition;
using Simpiens.Cognition.Contracts;
using Simpiens.Cognition.Inventory;
using Simpiens.Cognition.Planning;
using Simpiens.Cognition.Planning.Actions;
using Simpiens.Cognition.Planning.Goals;
using Simpiens.Entities;
using Simpiens.Simulation;
using Simpiens.Simulation.Economy;
using Simpiens.Simulation.Spatial;
using UnityEngine;
using VContainer.Unity;
using Debug = UnityEngine.Debug;

namespace Simpiens.Testing.Economy
{
    /// <summary>
    /// Automated domain validation suite for Epic 9 Slice 2:
    /// The Trade Stall & Commercial Exchange.
    /// Runs headlessly on startup via VContainer IStartable.
    /// </summary>
    public class TradeTransactionValidator : IStartable
    {
        public void Start()
        {
            Debug.Log("[TradeTransactionValidator] Beginning automated validation of Epic 9 Slice 2...");

            ValidateTradeStallLayoutAndZeroAlloc();
            ValidateBuyProvisionsAtomicTransfer();
            ValidateBuyProvisionsInsufficientFunds();
            ValidateBuyProvisionsInventoryFull();
            ValidateSellSalvageAtomicTransfer();
            ValidateTradeToAffinityFairPrice();
            ValidateTradeToAffinityPriceGouging();
            ValidateGOAPCommercialPlanResolution();
            ValidateBountyItemsAreNotSold();

            Debug.Log("<color=green>[TradeTransactionValidator] ALL 9 DOMAIN TESTS PASSED SUCCESSFULLY!</color>");
        }

        private void ValidateTradeStallLayoutAndZeroAlloc()
        {
            int stallSize = Marshal.SizeOf<TradeStallData>();
            Assert(stallSize <= 64, $"TradeStallData size ({stallSize} bytes) exceeds 64-byte threshold.");

            // Verify default empty state
            Assert(!TradeStallData.Empty.IsActive, "TradeStallData.Empty has IsActive == false.");
            Assert(TradeStallData.Empty.StallEntityId == default, "TradeStallData.Empty has default GUID.");

            // Verify constructor and immutable modifiers
            var stallId = GUID.Generate();
            var stall = new TradeStallData(stallId, new Vector2(5f, 10f), rationSellPrice: 5, rationStock: 40, oreBuyPrice: 8, oreStock: 0, herbBuyPrice: 12, herbStock: 0, treasuryGold: 600, interactionRadius: 3.0f);

            Assert(stall.IsActive, "Stall is active.");
            Assert(stall.StallEntityId == stallId, "Stall entity ID preserved.");
            Assert(stall.Position == new Vector2(5f, 10f), "Stall position preserved.");
            Assert(stall.RationSellPrice == 5, "Ration sell price is 5.");
            Assert(stall.RationStock == 40, "Ration stock is 40.");
            Assert(stall.TreasuryGold == 600, "Treasury gold is 600.");
            Assert(stall.InteractionRadius == 3.0f, "Interaction radius is 3.0f.");

            var updatedStock = stall.WithStock(rationStock: 35, oreStock: 10, herbStock: 5);
            Assert(updatedStock.RationStock == 35 && updatedStock.OreStock == 10 && updatedStock.HerbStock == 5, "WithStock updated stocks properly.");
            Assert(updatedStock.TreasuryGold == 600, "Treasury preserved across WithStock.");

            var updatedTreasury = stall.WithTreasury(750);
            Assert(updatedTreasury.TreasuryGold == 750, "WithTreasury updated treasury gold.");

            var updatedPrices = stall.WithPrices(rationSellPrice: 7, oreBuyPrice: 10, herbBuyPrice: 15);
            Assert(updatedPrices.RationSellPrice == 7 && updatedPrices.OreBuyPrice == 10 && updatedPrices.HerbBuyPrice == 15, "WithPrices updated prices.");

            // Test SharedWorldSnapshot buffer integration
            var snapshot = new SharedWorldSnapshot(10, 10, 2.0f, 8, _ => { });
            Assert(snapshot.ActiveStallCount == 0, "Snapshot starts with 0 active stalls.");

            TradeStallData[] testStalls = new TradeStallData[] { stall };
            snapshot.SetStalls(testStalls, 1);
            Assert(snapshot.ActiveStallCount == 1, "Snapshot ActiveStallCount is 1 after SetStalls.");

            bool found = snapshot.TryGetStall(stallId, out var foundStall);
            Assert(found && foundStall.StallEntityId == stallId, "TryGetStall successfully retrieved stall from snapshot.");

            snapshot.Clear();
            Assert(snapshot.ActiveStallCount == 0, "Snapshot ActiveStallCount cleared to 0.");

            Debug.Log("[TradeTransactionValidator] Test 1 Passed: TradeStallData unmanaged layout and snapshot buffering verified.");
        }

        private void ValidateBuyProvisionsAtomicTransfer()
        {
            var registry = new WorldRegistry();
            var clock = new SimulationClock();
            var simManager = new SimulationManager(registry, null, clock);

            var stallId = GUID.Generate();
            var stall = new TradeStallData(stallId, Vector2.zero, rationSellPrice: 5, rationStock: 50, treasuryGold: 500);
            simManager.RegisterTradeStall(stall);

            var go = new GameObject("[Test_Buyer]");
            var node = go.AddComponent<NodeController>();
            node.Initialize(GUID.Generate(), new NodeConfiguration(2f));
            node.transform.position = new Vector2(0f, 0.5f); // within interaction radius (2.5f)
            var agent = go.AddComponent<AutonomousAgent>();
            agent.Initialize(node.Id, null, null, simManager, clock);
            agent.Inventory.Gold = 10;
            registry.RegisterNode(node);

            var intentState = new ActiveIntentState
            {
                Intent = new TradeIntent(node.Id, stallId, TradeActionType.BuyItem, ItemType.Ration, quantity: 1),
                CurrentWaypointIndex = 0,
                LastSampledPosition = node.Position
            };

            var result = simManager.ExecuteIntent(node, intentState);
            Assert(result == IntentResult.Success, $"ExecuteIntent returned {result} instead of Success.");

            // Verify agent wallet and inventory
            Assert(agent.Inventory.Gold == 5, $"Agent gold is {agent.Inventory.Gold} instead of 5.");
            Assert(agent.Inventory.HasItem(ItemType.Ration, 1), "Agent inventory contains 1 Ration.");

            // Verify stall state
            Assert(simManager.TryGetTradeStall(stallId, out var updatedStall), "Stall found in manager.");
            Assert(updatedStall.RationStock == 49, $"Stall stock is {updatedStall.RationStock} instead of 49.");
            Assert(updatedStall.TreasuryGold == 505, $"Stall treasury is {updatedStall.TreasuryGold} instead of 505.");

            GameObject.DestroyImmediate(go);
            Debug.Log("[TradeTransactionValidator] Test 2 Passed: BuyProvisions atomic item and currency transfer verified.");
        }

        private void ValidateBuyProvisionsInsufficientFunds()
        {
            var registry = new WorldRegistry();
            var clock = new SimulationClock();
            var simManager = new SimulationManager(registry, null, clock);

            var stallId = GUID.Generate();
            var stall = new TradeStallData(stallId, Vector2.zero, rationSellPrice: 5, rationStock: 50, treasuryGold: 500);
            simManager.RegisterTradeStall(stall);

            var go = new GameObject("[Test_Poor_Buyer]");
            var node = go.AddComponent<NodeController>();
            node.Initialize(GUID.Generate(), new NodeConfiguration(2f));
            node.transform.position = new Vector2(0f, 0.5f);
            var agent = go.AddComponent<AutonomousAgent>();
            agent.Initialize(node.Id, null, null, simManager, clock);
            agent.Inventory.Gold = 2; // Not enough for 5 gold ration
            registry.RegisterNode(node);

            var intentState = new ActiveIntentState
            {
                Intent = new TradeIntent(node.Id, stallId, TradeActionType.BuyItem, ItemType.Ration, quantity: 1),
                CurrentWaypointIndex = 0,
                LastSampledPosition = node.Position
            };

            var result = simManager.ExecuteIntent(node, intentState);
            Assert(result == IntentResult.Failed, $"ExecuteIntent returned {result} instead of Failed for insufficient funds.");

            // Verify agent state untouched
            Assert(agent.Inventory.Gold == 2, "Agent gold remains 2.");
            Assert(!agent.Inventory.HasItem(ItemType.Ration), "Agent does not have ration.");

            // Verify stall state untouched
            simManager.TryGetTradeStall(stallId, out var untouchedStall);
            Assert(untouchedStall.RationStock == 50, "Stall stock remains 50.");
            Assert(untouchedStall.TreasuryGold == 500, "Stall treasury remains 500.");

            GameObject.DestroyImmediate(go);
            Debug.Log("[TradeTransactionValidator] Test 3 Passed: Insufficient funds buy attempt rejected without side-effects.");
        }

        private void ValidateBuyProvisionsInventoryFull()
        {
            var registry = new WorldRegistry();
            var clock = new SimulationClock();
            var simManager = new SimulationManager(registry, null, clock);

            var stallId = GUID.Generate();
            var stall = new TradeStallData(stallId, Vector2.zero, rationSellPrice: 5, rationStock: 50, treasuryGold: 500);
            simManager.RegisterTradeStall(stall);

            var go = new GameObject("[Test_Full_Buyer]");
            var node = go.AddComponent<NodeController>();
            node.Initialize(GUID.Generate(), new NodeConfiguration(2f));
            node.transform.position = new Vector2(0f, 0.5f);
            var agent = go.AddComponent<AutonomousAgent>();
            agent.Initialize(node.Id, null, null, simManager, clock);
            agent.Inventory.Gold = 25;

            // Fill all 4 slots with distinct items
            agent.Inventory.TryAddItem(new ItemInstance(1, ItemType.IronOre, 1, 8), out _);
            agent.Inventory.TryAddItem(new ItemInstance(2, ItemType.MedicinalHerb, 1, 12), out _);
            agent.Inventory.TryAddItem(new ItemInstance(3, ItemType.ToolPickaxe, 1, 15), out _);
            agent.Inventory.TryAddItem(new ItemInstance(4, ItemType.BasicWeapon, 1, 20), out _);
            registry.RegisterNode(node);

            var intentState = new ActiveIntentState
            {
                Intent = new TradeIntent(node.Id, stallId, TradeActionType.BuyItem, ItemType.Ration, quantity: 1),
                CurrentWaypointIndex = 0,
                LastSampledPosition = node.Position
            };

            var result = simManager.ExecuteIntent(node, intentState);
            Assert(result == IntentResult.Failed, $"ExecuteIntent returned {result} instead of Failed for full inventory.");

            // Verify gold not deducted
            Assert(agent.Inventory.Gold == 25, "Agent gold remains 25.");
            Assert(!agent.Inventory.HasItem(ItemType.Ration), "Agent did not receive ration.");

            GameObject.DestroyImmediate(go);
            Debug.Log("[TradeTransactionValidator] Test 4 Passed: Full inventory purchase rejected safely.");
        }

        private void ValidateSellSalvageAtomicTransfer()
        {
            var registry = new WorldRegistry();
            var clock = new SimulationClock();
            var simManager = new SimulationManager(registry, null, clock);

            var stallId = GUID.Generate();
            var stall = new TradeStallData(stallId, Vector2.zero, oreBuyPrice: 8, oreStock: 0, treasuryGold: 500);
            simManager.RegisterTradeStall(stall);

            var go = new GameObject("[Test_Seller]");
            var node = go.AddComponent<NodeController>();
            node.Initialize(GUID.Generate(), new NodeConfiguration(2f));
            node.transform.position = new Vector2(0f, 0.5f);
            var agent = go.AddComponent<AutonomousAgent>();
            agent.Initialize(node.Id, null, null, simManager, clock);
            agent.Inventory.Gold = 0;
            agent.Inventory.TryAddItem(new ItemInstance(10, ItemType.IronOre, 2, 8), out _);
            registry.RegisterNode(node);

            var intentState = new ActiveIntentState
            {
                Intent = new TradeIntent(node.Id, stallId, TradeActionType.SellItem, ItemType.IronOre, quantity: 2),
                CurrentWaypointIndex = 0,
                LastSampledPosition = node.Position
            };

            var result = simManager.ExecuteIntent(node, intentState);
            Assert(result == IntentResult.Success, $"ExecuteIntent returned {result} instead of Success.");

            // Verify agent gained 16 gold and lost ore
            Assert(agent.Inventory.Gold == 16, $"Agent gold is {agent.Inventory.Gold} instead of 16.");
            Assert(!agent.Inventory.HasItem(ItemType.IronOre), "Agent inventory no longer contains IronOre.");

            // Verify stall state
            simManager.TryGetTradeStall(stallId, out var updatedStall);
            Assert(updatedStall.OreStock == 2, $"Stall ore stock is {updatedStall.OreStock} instead of 2.");
            Assert(updatedStall.TreasuryGold == 484, $"Stall treasury is {updatedStall.TreasuryGold} instead of 484.");

            GameObject.DestroyImmediate(go);
            Debug.Log("[TradeTransactionValidator] Test 5 Passed: SellSalvage atomic item and currency transfer verified.");
        }

        private void ValidateTradeToAffinityFairPrice()
        {
            var registry = new WorldRegistry();
            var clock = new SimulationClock();
            var simManager = new SimulationManager(registry, null, clock);

            var stallId = GUID.Generate();
            var stall = new TradeStallData(stallId, Vector2.zero, rationSellPrice: 5, rationStock: 50, treasuryGold: 500);
            simManager.RegisterTradeStall(stall);

            var go = new GameObject("[Test_Affinity_Fair]");
            var node = go.AddComponent<NodeController>();
            node.Initialize(GUID.Generate(), new NodeConfiguration(2f));
            node.transform.position = new Vector2(0f, 0.5f);
            var agent = go.AddComponent<AutonomousAgent>();
            agent.Initialize(node.Id, null, null, simManager, clock);
            agent.Inventory.Gold = 10;
            registry.RegisterNode(node);

            // Initial affinity toward player is neutral
            var initialAffinity = agent.Memory.GetAffinity(SimulationManager.PlayerEntityId, 0);
            Assert(initialAffinity.Trust == 0 && initialAffinity.Warmth == 0, "Initial affinity is neutral.");

            var intentState = new ActiveIntentState
            {
                Intent = new TradeIntent(node.Id, stallId, TradeActionType.BuyItem, ItemType.Ration, quantity: 1),
                CurrentWaypointIndex = 0,
                LastSampledPosition = node.Position
            };

            simManager.ExecuteIntent(node, intentState);

            var postAffinity = agent.Memory.GetAffinity(SimulationManager.PlayerEntityId, 0);
            Assert(postAffinity.Trust == 10, $"Post-trade Trust is {postAffinity.Trust} instead of 10.");
            Assert(postAffinity.Warmth == 5, $"Post-trade Warmth is {postAffinity.Warmth} instead of 5.");
            Assert(postAffinity.Respect == 5, $"Post-trade Respect is {postAffinity.Respect} instead of 5.");

            GameObject.DestroyImmediate(go);
            Debug.Log("[TradeTransactionValidator] Test 6 Passed: Fair trade increases dyadic Trust, Warmth, and Respect.");
        }

        private void ValidateTradeToAffinityPriceGouging()
        {
            var registry = new WorldRegistry();
            var clock = new SimulationClock();
            var simManager = new SimulationManager(registry, null, clock);

            var stallId = GUID.Generate();
            // Price gouging: 10 gold for ration (> 150% of 5)
            var stall = new TradeStallData(stallId, Vector2.zero, rationSellPrice: 10, rationStock: 50, treasuryGold: 500);
            simManager.RegisterTradeStall(stall);

            var go = new GameObject("[Test_Affinity_Gouged]");
            var node = go.AddComponent<NodeController>();
            node.Initialize(GUID.Generate(), new NodeConfiguration(2f));
            node.transform.position = new Vector2(0f, 0.5f);
            var agent = go.AddComponent<AutonomousAgent>();
            agent.Initialize(node.Id, null, null, simManager, clock);
            agent.Inventory.Gold = 20;
            registry.RegisterNode(node);

            var intentState = new ActiveIntentState
            {
                Intent = new TradeIntent(node.Id, stallId, TradeActionType.BuyItem, ItemType.Ration, quantity: 1),
                CurrentWaypointIndex = 0,
                LastSampledPosition = node.Position
            };

            simManager.ExecuteIntent(node, intentState);

            var postAffinity = agent.Memory.GetAffinity(SimulationManager.PlayerEntityId, 0);
            Assert(postAffinity.Trust == -15, $"Post-gouging Trust is {postAffinity.Trust} instead of -15.");
            Assert(postAffinity.Warmth == -10, $"Post-gouging Warmth is {postAffinity.Warmth} instead of -10.");

            GameObject.DestroyImmediate(go);
            Debug.Log("[TradeTransactionValidator] Test 7 Passed: Price gouging imposes negative Trust and Warmth penalties.");
        }

        /// <summary>
        /// Units owed to a claimed Gather bounty are not for sale (Epic MAP Phase 3: on Floor 1 the claimant kept
        /// selling the ore it had gathered for the bounty, delaying the contract by about 3,000 ticks).
        /// </summary>
        private void ValidateBountyItemsAreNotSold()
        {
            var agentId = new GUID(41u, 1u, 1u, 1u);
            var stall = new TradeStallData(new GUID(42u, 2u, 2u, 2u), Vector2.zero, oreBuyPrice: 20, herbBuyPrice: 25, treasuryGold: 500);
            var claimed = new BountyContract(7, BountyType.GatherResource, 0, ItemType.IronOre, 1, 25, 0, 6000,
                claimantId: agentId, creatorStallId: stall.StallEntityId, status: BountyStatus.Claimed);

            AgentContext Context(int ore, int herb, bool withClaim)
            {
                var snapshot = new SharedWorldSnapshot(16, 16, 1f, 8, null);
                snapshot.SetStalls(new[] { stall }, 1);
                if (withClaim) snapshot.SetBounties(new[] { claimed }, 1);
                var inventory = new AgentInventory(AgentInventory.DefaultCapacity, startingGold: 10);
                for (int i = 0; i < ore; i++) inventory.TryAddItem(new ItemInstance((ushort)(100 + i), ItemType.IronOre, 1, 8), out _);
                for (int i = 0; i < herb; i++) inventory.TryAddItem(new ItemInstance((ushort)(200 + i), ItemType.MedicinalHerb, 1, 12), out _);
                return new AgentContext(agentId, new Vector2(1f, 0f), new AgentNeeds(20f, 80f, 60f), 0f, snapshot,
                    new Simpiens.Cognition.Memory.AgentMemory(), 10, inventory: inventory.GetSnapshot());
            }

            var goal = new LiquidateSalvageGoal();
            var sell = new SellSalvageAction();

            var owed = Context(1, 0, withClaim: true);
            Assert(!ResourceTargeting.HoldsSellable(owed, ItemType.IronOre), "The one ore owed to the bounty is not sellable.");
            Assert(!goal.IsValid(owed), "LiquidateSalvage is not valid when every unit held is owed to a bounty.");
            Assert(ResourceTargeting.AffordableHeldSalvage(owed, stall) == ItemType.None, "No held salvage is offered for sale.");
            Assert(sell.CreateIntent(owed) is IdleIntent, "SellSalvage sells nothing when everything held is owed.");

            var spare = Context(2, 0, withClaim: true);
            Assert(goal.IsValid(spare) && ResourceTargeting.AffordableHeldSalvage(spare, stall) == ItemType.IronOre, "A second ore beyond the bounty may be sold.");

            var herbToo = Context(1, 1, withClaim: true);
            Assert(sell.CreateIntent(herbToo) is TradeIntent trade && trade.ItemType == ItemType.MedicinalHerb, "With owed ore and a herb, the herb is sold.");

            var noClaim = Context(1, 0, withClaim: false);
            Assert(goal.IsValid(noClaim) && ResourceTargeting.HoldsSellable(noClaim, ItemType.IronOre), "Without a claim the ore is for sale as before.");

            Debug.Log("[TradeTransactionValidator] Test 9 Passed: units owed to a claimed Gather bounty are never sold.");
        }

        private void ValidateGOAPCommercialPlanResolution()
        {
            var graph = new ActionGraph();
            var travelStall = new TravelToStallAction();
            var buyProvisions = new BuyProvisionsAction();
            var eatFood = new EatCarriedFoodAction();
            var sellSalvage = new SellSalvageAction();

            graph.RegisterAction(travelStall);
            graph.RegisterAction(buyProvisions);
            graph.RegisterAction(eatFood);
            graph.RegisterAction(sellSalvage);

            var planner = new GoapPlanner();
            var context = default(AgentContext);

            // Scenario 1: PurchaseProvisionsGoal
            // Initial: AtTradeStall = false, HasProvisions = false
            var goal1 = new PurchaseProvisionsGoal();
            var initial1 = WorldState.Empty
                .With(StateFact.AtTradeStall, false)
                .With(StateFact.HasProvisions, false);

            var plan1 = new GoapPlan();
            bool success1 = planner.Plan(initial1, goal1, graph, context, plan1);
            Assert(success1, "Plan1 for PurchaseProvisionsGoal was successfully resolved.");
            Assert(plan1.Actions.Count == 2, $"Plan1 contains {plan1.Actions.Count} actions instead of 2.");
            Assert(plan1.Actions[0] == travelStall, "Plan1 Step 1 is TravelToStallAction.");
            Assert(plan1.Actions[1] == buyProvisions, "Plan1 Step 2 is BuyProvisionsAction.");

            // Scenario 2: SatiateHungerGoal resolving via buying food at stall
            // Initial: IsHungry = true, HasFoodInInventory = false, AtTradeStall = false
            var goal2 = new SatiateHungerGoal();
            var initial2 = WorldState.Empty
                .With(StateFact.IsHungry, true)
                .With(StateFact.HasFoodInInventory, false)
                .With(StateFact.AtTradeStall, false);

            var plan2 = new GoapPlan();
            bool success2 = planner.Plan(initial2, goal2, graph, context, plan2);
            Assert(success2, "Plan2 for SatiateHungerGoal via trade stall was successfully resolved.");
            Assert(plan2.Actions.Count == 3, $"Plan2 contains {plan2.Actions.Count} actions instead of 3.");
            Assert(plan2.Actions[0] == travelStall, "Plan2 Step 1 is TravelToStallAction.");
            Assert(plan2.Actions[1] == buyProvisions, "Plan2 Step 2 is BuyProvisionsAction.");
            Assert(plan2.Actions[2] == eatFood, "Plan2 Step 3 is EatCarriedFoodAction.");

            // Scenario 3: LiquidateSalvageGoal
            // Initial: HasSalvageInInventory = true, AtTradeStall = false
            var goal3 = new LiquidateSalvageGoal();
            var initial3 = WorldState.Empty
                .With(StateFact.HasSalvageInInventory, true)
                .With(StateFact.AtTradeStall, false);

            var plan3 = new GoapPlan();
            bool success3 = planner.Plan(initial3, goal3, graph, context, plan3);
            Assert(success3, "Plan3 for LiquidateSalvageGoal was successfully resolved.");
            Assert(plan3.Actions.Count == 2, $"Plan3 contains {plan3.Actions.Count} actions instead of 2.");
            Assert(plan3.Actions[0] == travelStall, "Plan3 Step 1 is TravelToStallAction.");
            Assert(plan3.Actions[1] == sellSalvage, "Plan3 Step 2 is SellSalvageAction.");

            Debug.Log("[TradeTransactionValidator] Test 8 Passed: GOAP commercial plan chaining (Purchase, Hunger via Stall, Liquidate) verified.");
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
