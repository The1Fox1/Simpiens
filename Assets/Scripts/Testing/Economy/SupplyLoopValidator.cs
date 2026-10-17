using Simpiens.Testing.Environment;
using System;
using System.Collections.Generic;
using Simpiens.Cognition;
using Simpiens.Cognition.Contracts;
using Simpiens.Cognition.Evaluators;
using Simpiens.Cognition.Inventory;
using Simpiens.Cognition.Memory;
using Simpiens.Cognition.Pathfinding;
using Simpiens.Cognition.Planning;
using Simpiens.Cognition.Planning.Actions;
using Simpiens.Cognition.Planning.Goals;
using Simpiens.Entities;
using Simpiens.Simulation;
using Simpiens.Simulation.Economy;
using Simpiens.Simulation.Spatial;
using Simpiens.UI.Zones;

namespace Simpiens.Testing.Economy
{
    /// <summary>
    /// Headless mechanism validation for §3.1 (Close the Milestone 1 Supply Side):
    /// typed harvesting, inventory-full failure, deterministic regrowth, the supply caravan, Scout bounty
    /// fulfilment, salvage bounty planning, the F1 Tier-1 → GOAP regression, and zero-GC supply ticking.
    /// These inject preconditions by design; live emergence is asserted by OutpostSupplyLoopScenario.
    /// </summary>
    public class SupplyLoopValidator
    {
        private static uint _guidSeed = 31000;
        private static EntityId NextGuid() => new EntityId(_guidSeed++, 0x51A1, 0x3001, 0xC0DE);

        public void Start()
        {
            TestLog.Log("[SupplyLoopValidator] Beginning automated validation of §3.1 supply loop mechanisms...");

            ValidateTypedHarvest();
            ValidateInventoryFullHarvestFails();
            ValidateDeterministicRegrowth();
            ValidateSupplyCaravan();
            ValidateCaravanSalvageQuota();
            ValidateOutpostUpkeep();
            ValidateScoutBountyFulfilment();
            ValidateSalvageBountyPlan();
            ValidateWellFedAgentReachesGoap();
            ValidateZeroAllocSupplyTicks();
            ValidateActivityTally();
            ValidateCarriedFoodIsEaten();

            TestLog.Log("<color=green>[SupplyLoopValidator] ALL 12 DOMAIN TESTS PASSED SUCCESSFULLY!</color>");
        }

        // ---------------------------------------------------------------------------------------------
        // 1. Ore yields IronOre into inventory and leaves hunger alone; food still relieves hunger
        // ---------------------------------------------------------------------------------------------
        private void ValidateTypedHarvest()
        {
            var registry = new WorldRegistry();
            var sim = new SimulationManager(registry, null);

            var oreId = CreateResourceNode(registry, sim, new Vec2(0f, 0f), ItemType.IronOre, yield: 3, regrowTicks: 0, out var oreGo);
            var foodId = CreateResourceNode(registry, sim, new Vec2(4f, 0f), ItemType.Ration, yield: 3, regrowTicks: 0, out var foodGo);
            var agentGo = CreateAgent(registry, sim, new Vec2(0.5f, 0f), out var node, out var agent);
            agent.Hunger = 70f;

            var result = RunIntent(sim, new HarvestResourceIntent(node.Id, oreId, null));
            Assert(result == IntentResult.Success, $"Ore harvest returned {result} instead of Success.");
            Assert(agent.Inventory.GetItemCount(ItemType.IronOre) == 1, "Ore harvest placed 1 IronOre in inventory.");
            Assert(SimMath.Approximately(agent.Hunger, 70f), $"Ore harvest left hunger unchanged (hunger {agent.Hunger}).");
            Assert(sim.TryGetResource(oreId, out var ore) && ore.RemainingYield == 2, "Ore node yield decremented to 2.");

            node.SetPosition(new Vec2(4.5f, 0f));
            result = RunIntent(sim, new HarvestResourceIntent(node.Id, foodId, null));
            Assert(result == IntentResult.Success, $"Food harvest returned {result} instead of Success.");
            Assert(SimMath.Approximately(agent.Hunger, 20f), $"Food harvest relieved 50 hunger (hunger {agent.Hunger}).");
            Assert(agent.Inventory.GetItemCount(ItemType.IronOre) == 1, "Food harvest did not add salvage.");

            Destroy(oreGo, foodGo, agentGo);
            TestLog.Log("[SupplyLoopValidator] Test 1 Passed: Typed harvest (ore → inventory, food → hunger relief).");
        }

        // ---------------------------------------------------------------------------------------------
        // 2. A full inventory fails the harvest without consuming yield
        // ---------------------------------------------------------------------------------------------
        private void ValidateInventoryFullHarvestFails()
        {
            var registry = new WorldRegistry();
            var sim = new SimulationManager(registry, null);

            var herbId = CreateResourceNode(registry, sim, Vec2.Zero, ItemType.MedicinalHerb, yield: 2, regrowTicks: 0, out var herbGo);
            var agentGo = CreateAgent(registry, sim, new Vec2(0.5f, 0f), out var node, out var agent);

            // Fill all slots with item types that cannot stack with MedicinalHerb
            agent.Inventory.TryAddItem(new ItemInstance(1, ItemType.Ration, 1, 5), out _);
            agent.Inventory.TryAddItem(new ItemInstance(2, ItemType.IronOre, 1, 8), out _);
            agent.Inventory.TryAddItem(new ItemInstance(4, ItemType.ToolPickaxe, 1, 30), out _);
            agent.Inventory.TryAddItem(new ItemInstance(5, ItemType.BasicWeapon, 1, 40), out _);
            Assert(agent.Inventory.OccupiedSlots == agent.Inventory.Capacity, "Inventory is full before harvest.");

            var result = RunIntent(sim, new HarvestResourceIntent(node.Id, herbId, null));
            Assert(result == IntentResult.Failed, $"Full-inventory harvest returned {result} instead of Failed.");
            Assert(sim.TryGetResource(herbId, out var herb) && herb.RemainingYield == 2, "No yield lost on failed harvest.");
            Assert(!agent.Inventory.HasItem(ItemType.MedicinalHerb), "No herb added to a full inventory.");

            Destroy(herbGo, agentGo);
            TestLog.Log("[SupplyLoopValidator] Test 2 Passed: Inventory-full harvest fails without losing yield.");
        }

        // ---------------------------------------------------------------------------------------------
        // 3. Regrowth: dormant at +49, harvestable again exactly at +RegrowTicks
        // ---------------------------------------------------------------------------------------------
        private void ValidateDeterministicRegrowth()
        {
            var registry = new WorldRegistry();
            var sim = new SimulationManager(registry, null);

            var bushId = CreateResourceNode(registry, sim, Vec2.Zero, ItemType.Ration, yield: 1, regrowTicks: 50, out var bushGo);
            var agentGo = CreateAgent(registry, sim, new Vec2(0.5f, 0f), out var node, out var agent);

            var result = RunIntent(sim, new HarvestResourceIntent(node.Id, bushId, null));
            Assert(result == IntentResult.Success, $"Depleting harvest returned {result}.");
            Assert(sim.TryGetResource(bushId, out var bush) && bush.IsDormant, "Depleted regrowable node is dormant, not removed.");
            Assert(registry.GetEntity(bushId) == null, "Dormant node is unregistered from the world (imperceptible).");

            uint depletedAt = bush.DepletedAtTick;
            while (sim.CurrentTick < depletedAt + 49) sim.Tick();
            Assert(bush.IsDormant, $"Node still dormant at depletion +49 (tick {sim.CurrentTick}).");
            Assert(sim.Supply.RegrowthCount == 0, "No regrowth recorded before +50.");

            sim.Tick();
            Assert(sim.CurrentTick == depletedAt + 50, "Advanced to depletion +50.");
            Assert(!bush.IsDormant && bush.RemainingYield == bush.MaxYield, "Node regrew to MaxYield at exactly +50.");
            Assert(registry.GetEntity(bushId) != null, "Regrown node re-registered with the world.");
            Assert(sim.Supply.RegrowthCount == 1, "RegrowthCount is 1.");

            Destroy(bushGo, agentGo);
            TestLog.Log("[SupplyLoopValidator] Test 3 Passed: Deterministic dormant regrowth at exactly RegrowTicks.");
        }

        // ---------------------------------------------------------------------------------------------
        // 4. Caravan restocks to cap, pays from treasury as a named sink, skips when unaffordable
        // ---------------------------------------------------------------------------------------------
        private void ValidateSupplyCaravan()
        {
            var registry = new WorldRegistry();
            var ledger = new EconomyLedger();
            var supply = new SupplyService(new SupplyTuning(restockIntervalTicks: 20, rationStockCap: 50, wholesaleRationCost: 2));
            var sim = new SimulationManager(registry, null, economyLedger: ledger, supplyService: supply);

            var richId = NextGuid();
            var poorId = NextGuid();
            sim.RegisterTradeStall(new TradeStallData(richId, new Vec2(0f, 0f), rationStock: 10, treasuryGold: 500));
            var brokeId = NextGuid();
            sim.RegisterTradeStall(new TradeStallData(poorId, new Vec2(20f, 0f), rationStock: 0, treasuryGold: 10));
            sim.RegisterTradeStall(new TradeStallData(brokeId, new Vec2(40f, 0f), rationStock: 0, treasuryGold: 1));
            ledger.RecordFaucet(511, "InitialSeed");

            while (sim.CurrentTick < 20) sim.Tick();

            Assert(sim.TryGetTradeStall(richId, out var rich), "Rich stall retrieved.");
            Assert(rich.RationStock == 50, $"Rich stall restocked to cap (stock {rich.RationStock}).");
            Assert(rich.TreasuryGold == 420, $"Rich stall paid 40 × 2g = 80g (treasury {rich.TreasuryGold}).");
            // A poor stall gets what it can pay for (review §3.3 D6); one that can't pay for a single ration is skipped
            Assert(sim.TryGetTradeStall(poorId, out var poor) && poor.RationStock == 5 && poor.TreasuryGold == 0, $"Poor stall got a partial restock of 5 for 10g (stock {poor.RationStock}, treasury {poor.TreasuryGold}).");
            Assert(sim.TryGetTradeStall(brokeId, out var broke) && broke.RationStock == 0 && broke.TreasuryGold == 1, "A stall that can't pay for one ration is skipped.");
            Assert(supply.RestockCount == 2 && supply.PartialRestockCount == 1, $"RestockCount {supply.RestockCount} (expected 2), partial {supply.PartialRestockCount} (expected 1).");
            Assert(supply.SkippedRestockCount == 1, $"SkippedRestockCount is {supply.SkippedRestockCount} instead of 1 (broke stall).");
            Assert(ledger.TotalSinkedGold == 90, $"SupplyCaravan sink recorded 80g + 10g (got {ledger.TotalSinkedGold}).");
            Assert(ledger.ValidateConservation(rich.TreasuryGold + poor.TreasuryGold + broke.TreasuryGold, 0, out int disc), $"Ledger conservation holds (discrepancy {disc}).");

            // Already at cap: next visit is a no-op for the rich stall (the poor and broke stalls have no gold left)
            while (sim.CurrentTick < 40) sim.Tick();
            Assert(supply.RestockCount == 2, "Full stall not restocked again.");

            TestLog.Log("[SupplyLoopValidator] Test 4 Passed: Supply caravan respects cap, delivers what a stall can pay for, and conserves the ledger.");
        }

        // ---------------------------------------------------------------------------------------------
        // 4b. The caravan buys salvage up to its quota per visit (named faucet) before restocking (review §3.3 D1)
        // ---------------------------------------------------------------------------------------------
        private void ValidateCaravanSalvageQuota()
        {
            var registry = new WorldRegistry();
            var ledger = new EconomyLedger();
            var tuning = new SupplyTuning(restockIntervalTicks: 20, rationStockCap: 50, wholesaleRationCost: 2,
                oreQuota: 5, herbQuota: 5, oreMarketPrice: 24, herbMarketPrice: 30);
            var supply = new SupplyService(tuning);
            var sim = new SimulationManager(registry, null, economyLedger: ledger, supplyService: supply);
            Assert(tuning.MaxSalvagePaymentPerVisit == 270, $"Faucet ceiling 5x24 + 5x30 = 270g per visit (got {tuning.MaxSalvagePaymentPerVisit}).");

            // Broke stall holding salvage: the quota's revenue (5x24 + 5x30 = 270g) funds the 100g restock
            var stallId = NextGuid();
            sim.RegisterTradeStall(new TradeStallData(stallId, Vec2.Zero, rationStock: 0, oreStock: 12, herbStock: 7, treasuryGold: 0));

            while (sim.CurrentTick < 20) sim.Tick();

            Assert(sim.TryGetTradeStall(stallId, out var stall), "Stall retrieved.");
            Assert(stall.OreStock == 7 && stall.HerbStock == 2, $"The caravan bought exactly its quota (ore {stall.OreStock}, herb {stall.HerbStock}).");
            Assert(stall.RationStock == 50, $"Salvage revenue funded the restock (stock {stall.RationStock}).");
            Assert(stall.TreasuryGold == 170, $"Treasury = 270g salvage - 100g restock (got {stall.TreasuryGold}).");
            Assert(supply.SalvageSaleVisits == 1 && ledger.GetFaucetTotal(SupplyService.SalvageSalesFaucetName) == 270,
                $"CaravanSalvageSales faucet recorded 270g (got {ledger.GetFaucetTotal(SupplyService.SalvageSalesFaucetName)}).");
            Assert(ledger.ValidateConservation(stall.TreasuryGold, 0, out int disc), $"Ledger conservation holds (discrepancy {disc}).");

            // Second visit: 5 more ore, the last 2 herbs (120 + 60 = 180g); third: the last 2 ore (48g)
            while (sim.CurrentTick < 40) sim.Tick();
            Assert(sim.TryGetTradeStall(stallId, out stall) && stall.OreStock == 2 && stall.HerbStock == 0, $"Second visit bought 5 ore + 2 herbs (ore {stall.OreStock}, herb {stall.HerbStock}).");
            while (sim.CurrentTick < 60) sim.Tick();
            Assert(sim.TryGetTradeStall(stallId, out stall) && stall.OreStock == 0, $"Third visit bought the last 2 ore (ore {stall.OreStock}).");
            Assert(ledger.GetFaucetTotal(SupplyService.SalvageSalesFaucetName) == 270 + 180 + 48, $"Faucet total 498g (got {ledger.GetFaucetTotal(SupplyService.SalvageSalesFaucetName)}).");
            while (sim.CurrentTick < 80) sim.Tick();
            Assert(supply.SalvageSaleVisits == 3, $"No salvage, no purchase: {supply.SalvageSaleVisits} visits bought salvage (expected 3).");

            TestLog.Log("[SupplyLoopValidator] Test 4b Passed: The caravan buys salvage up to its quota per visit, as a named faucet.");
        }

        // ---------------------------------------------------------------------------------------------
        // 4c. Outpost upkeep (review §3.3 D2): paid each visit as a named sink; a stall that can't pay in full pays what
        // it has and is in arrears until a visit finds the upkeep paid in full
        // ---------------------------------------------------------------------------------------------
        private void ValidateOutpostUpkeep()
        {
            var registry = new WorldRegistry();
            var ledger = new EconomyLedger();
            var supply = new SupplyService(new SupplyTuning(restockIntervalTicks: 20, rationStockCap: 50, wholesaleRationCost: 2,
                oreQuota: 0, herbQuota: 0, oreMarketPrice: 0, herbMarketPrice: 0, upkeepPerVisit: 100));
            var sim = new SimulationManager(registry, null, economyLedger: ledger, supplyService: supply);

            var stallId = NextGuid();
            sim.RegisterTradeStall(new TradeStallData(stallId, Vec2.Zero, rationStock: 50, treasuryGold: 150));
            ledger.RecordFaucet(150, "InitialSeed");

            while (sim.CurrentTick < 20) sim.Tick();
            Assert(sim.TryGetTradeStall(stallId, out var stall) && stall.TreasuryGold == 50 && !stall.InArrears, $"First visit paid 100g upkeep (treasury {stall.TreasuryGold}, arrears {stall.InArrears}).");
            Assert(supply.UpkeepPaidCount == 1 && ledger.GetSinkTotal(SupplyService.UpkeepSinkName) == 100, "Upkeep recorded as the OutpostUpkeep sink.");

            while (sim.CurrentTick < 40) sim.Tick();
            Assert(sim.TryGetTradeStall(stallId, out stall) && stall.TreasuryGold == 0 && stall.InArrears, $"Second visit: paid the last 50g and went into arrears (treasury {stall.TreasuryGold}, arrears {stall.InArrears}).");
            Assert(supply.UpkeepShortCount == 1 && ledger.GetSinkTotal(SupplyService.UpkeepSinkName) == 150, "A short payment records only what was paid (no debt).");
            Assert(ledger.ValidateConservation(stall.TreasuryGold, 0, out int disc), $"Ledger conservation holds (discrepancy {disc}).");

            // Rations sold in arrears refill the treasury; the next visit pays in full and clears arrears
            sim.UpdateStall(stall.WithTreasury(300));
            ledger.RecordFaucet(300, "TestIncome");
            while (sim.CurrentTick < 60) sim.Tick();
            Assert(sim.TryGetTradeStall(stallId, out stall) && stall.TreasuryGold == 200 && !stall.InArrears, $"Third visit paid in full and cleared arrears (treasury {stall.TreasuryGold}, arrears {stall.InArrears}).");
            Assert(ledger.ValidateConservation(stall.TreasuryGold, 0, out disc), $"Ledger conservation holds after recovery (discrepancy {disc}).");

            TestLog.Log("[SupplyLoopValidator] Test 4c Passed: Upkeep is a named sink; a stall that can't pay goes into arrears and recovers.");
        }

        // ---------------------------------------------------------------------------------------------
        // 5. Scout: entering the sector sets the report; delivery pays from escrow and explores the zone
        // ---------------------------------------------------------------------------------------------
        private void ValidateScoutBountyFulfilment()
        {
            var registry = new WorldRegistry();
            var ledger = new EconomyLedger();
            var zones = Simpiens.Testing.World.TestWorlds.CommonsYardSectors();
            var sim = new SimulationManager(registry, null, sectors: zones, economyLedger: ledger);

            int sector = Simpiens.Testing.World.CommonsYardFixture.HerbHollowSectorId;
            Assert(!zones.IsExplored(sector), "Herb Hollow starts unexplored.");

            var stallId = NextGuid();
            var stallPos = new Vec2(0f, 9f);
            sim.RegisterTradeStall(new TradeStallData(stallId, stallPos, treasuryGold: 200, interactionRadius: 3f));
            ledger.RecordFaucet(200 + 10, "InitialSeed");

            // Agent stands just west of Herb Hollow's bounds (x = 5)
            var agentGo = CreateAgent(registry, sim, new Vec2(4.5f, 10f), out var node, out var agent);

            var bounty = new BountyContract(42, BountyType.ScoutSector, (byte)sector, ItemType.None, 1, 15, 0, 5000,
                claimantId: node.Id, creatorStallId: stallId, status: BountyStatus.Claimed);
            sim.RegisterBounty(bounty);
            Assert(ledger.HasEscrow(42) && ledger.GetEscrowAmount(42) == 15, "Scout bounty reward locked in escrow at registration.");
            Assert(sim.TryGetBounty(42, out var registered) && registered.TargetLocation != default, "Default target location derived from the sector.");

            var path = new PathResponse();
            path.Initialize(4, null);
            path.Waypoints[0] = new Vec2(6.5f, 10f);
            path.Length = 1;
            path.IsValid = true;

            var result = RunIntent(sim, new ScoutIntent(node.Id, 42, (byte)sector, path));
            Assert(result == IntentResult.Success, $"ScoutIntent returned {result} instead of Success.");
            Assert(agent.ScoutReportBountyId == 42, "Scout report recorded on entering the sector.");

            // Report at the stall
            node.SetPosition(stallPos + new Vec2(1f, 0f));
            int goldBefore = agent.Inventory.Gold;
            result = RunIntent(sim, new DeliverBountyIntent(node.Id, 42, stallId));
            Assert(result == IntentResult.Success, $"Scout report delivery returned {result} instead of Success.");
            Assert(agent.Inventory.Gold == goldBefore + 15, $"Reward paid (gold {agent.Inventory.Gold}, expected {goldBefore + 15}).");
            Assert(!ledger.HasEscrow(42) && ledger.TotalEscrowGold == 0, "Escrow released.");
            Assert(zones.IsExplored(sector), "Herb Hollow marked explored.");
            Assert(agent.ScoutReportBountyId == 0, "Scout report consumed.");
            Assert(sim.TryGetBounty(42, out var done) && done.Status == BountyStatus.Completed, "Scout bounty Completed.");

            Assert(sim.TryGetTradeStall(stallId, out var stall), "Stall retrieved.");
            Assert(ledger.ValidateConservation(stall.TreasuryGold, (uint)agent.Inventory.Gold, out int disc), $"Ledger conservation holds (discrepancy {disc}).");

            // F8: a bounty that cannot be escrowed is rejected instead of minting gold on delivery
            var broke = new TradeStallData(NextGuid(), Vec2.Zero, treasuryGold: 5);
            sim.RegisterTradeStall(broke);
            bool accepted = sim.TryRegisterBounty(new BountyContract(43, BountyType.GatherResource, 0, ItemType.IronOre, 1, 50, 0, 5000, creatorStallId: broke.StallEntityId));
            Assert(!accepted && !sim.TryGetBounty(43, out _), "Un-escrowable bounty rejected at registration.");

            Destroy(agentGo);
            TestLog.Log("[SupplyLoopValidator] Test 5 Passed: Scout bounty fulfilment via escrow and sector exploration.");
        }

        // ---------------------------------------------------------------------------------------------
        // 6. From a clean world state the planner builds Claim → Search → Gather → Travel → Deliver
        // ---------------------------------------------------------------------------------------------
        private void ValidateSalvageBountyPlan()
        {
            var snapshot = new SharedWorldSnapshot(10, 10, 2.0f, 8, _ => { });
            snapshot.SetBounties(new[] { new BountyContract(1, BountyType.GatherResource, 12, ItemType.IronOre, 1, 25, 0, 5000, new Vec2(-8f, 10f)) }, 1);
            snapshot.SetStalls(new[] { new TradeStallData(NextGuid(), new Vec2(20f, 20f)) }, 1);

            var memory = new AgentMemory();
            var inventory = new AgentInventory();
            var context = new AgentContext(NextGuid(), Vec2.Zero, new AgentNeeds(10f, 90f, 80f), 0f, snapshot, memory, 1,
                inventory: inventory.GetSnapshot());
            var state = WorldStateBuilder.BuildWorldState(context, context.Inventory);

            Assert(!state.Get(StateFact.HasActiveBounty) && !state.Get(StateFact.KnowsSalvageLocation) && !state.Get(StateFact.AtTradeStall),
                "Clean state: no bounty, no salvage knowledge, not at stall.");

            var graph = BuildProductionGraph();
            var plan = new GoapPlan();
            bool resolved = new GoapPlanner().Plan(state, new FulfillBountyGoal(), graph, context, plan);

            Assert(resolved, "Planner resolved FulfillBountyGoal from a clean state.");
            Assert(plan.Actions.Count == 5, $"Plan has {plan.Actions.Count} steps instead of 5.");

            int claim = IndexOf<ClaimGatherBountyAction>(plan), search = IndexOf<SearchSalvageAction>(plan), gather = IndexOf<GatherSalvageAction>(plan);
            int travel = IndexOf<TravelToStallAction>(plan), deliver = IndexOf<DeliverBountyAction>(plan);
            Assert(claim >= 0 && search >= 0 && gather >= 0 && travel >= 0 && deliver >= 0, "Plan contains ClaimGather, SearchSalvage, GatherSalvage, TravelToStall, DeliverBounty.");
            Assert(search < gather && gather < deliver && claim < deliver && travel < deliver, "Plan steps are causally ordered.");
            Assert(IndexOf<ClaimScoutBountyAction>(plan) < 0, "No Scout claim without an open Scout bounty (F5).");
            Assert(gather < travel, "TravelToStall comes after GatherSalvage (gathering walks away from the stall).");

            // Starting at the stall must not let the planner skip the walk back: gathering clears AtTradeStall
            var atStall = new GoapPlan();
            Assert(new GoapPlanner().Plan(state.With(StateFact.AtTradeStall, true), new FulfillBountyGoal(), graph, context, atStall),
                "Planner resolved FulfillBountyGoal from the stall.");
            int gatherAt = IndexOf<GatherSalvageAction>(atStall), travelAt = IndexOf<TravelToStallAction>(atStall), deliverAt = IndexOf<DeliverBountyAction>(atStall);
            Assert(gatherAt >= 0 && travelAt > gatherAt && deliverAt > travelAt,
                $"From the stall the plan still returns after gathering (gather {gatherAt}, travel {travelAt}, deliver {deliverAt}).");

            TestLog.Log("[SupplyLoopValidator] Test 6 Passed: Salvage bounty plan resolved from a clean world state.");
        }

        // ---------------------------------------------------------------------------------------------
        // 7. F1 regression: a well-fed agent with an open bounty receives a GOAP intent, not Idle/Wander
        // ---------------------------------------------------------------------------------------------
        private void ValidateWellFedAgentReachesGoap()
        {
            var registry = new WorldRegistry();
            var grid = new SpatialHashGrid();
            grid.Initialize(40f, 40f, 1f);
            var sim = new SimulationManager(registry, grid);

            var stallId = NextGuid();
            sim.RegisterTradeStall(new TradeStallData(stallId, new Vec2(10f, 0f), treasuryGold: 300));
            sim.RegisterBounty(new BountyContract(1, BountyType.GatherResource, 0, ItemType.IronOre, 1, 25, 0, 5000, new Vec2(3f, 0f), creatorStallId: stallId));

            // A perceivable ore vein: knowledge comes from vision through the typed snapshot, not injection
            CreateResourceNode(registry, sim, new Vec2(3f, 0f), ItemType.IronOre, yield: 3, regrowTicks: 0, out var oreGo);
            var agentGo = CreateAgent(registry, sim, Vec2.Zero, out var node, out var agent);
            agent.Hunger = 10f;
            sim.Tick(); // publish the snapshot (stall, bounty, typed ore node)

            var cognition = new AgentCognitionService(null, new AmbientWanderEvaluator(null));
            var goals = new List<GoapGoal> { new SatiateHungerGoal(), new FulfillBountyGoal(), new EarnIncomeGoal() };
            var evaluators = new ICognitiveEvaluator[] { new SurvivalUtilityEvaluator(null) };

            cognition.BeginEvaluation(agent, grid, sim, null, new GoapPlanner(), BuildProductionGraph(), goals, evaluators);
            Assert(sim.Cognition.PendingCount == 1, "Cognition evaluation dispatched.");
            // Land the decision (dispatched at t0: no clock) without running a tick, so the intent is still unexecuted
            sim.Cognition.ApplyDue(CognitionScheduler.LatencyTicks);

            var intent = agent.CurrentIntent;
            Assert(intent != null, "An intent was produced.");
            Assert(!(intent is IdleIntent) && !(intent is WanderIntent), $"Well-fed agent got {intent?.GetType().Name} instead of a GOAP intent.");
            Assert(agent.ActiveGoal is FulfillBountyGoal, $"Active goal is {agent.ActiveGoal?.Name ?? "null"} instead of FulfillBounty.");
            Assert(intent is ClaimBountyIntent || intent is HarvestResourceIntent, $"First GOAP step emitted {intent.GetType().Name}.");
            Assert(agent.Memory.SpatialMemoryMap.Count > 0, "Ore vein perceived.");
            foreach (var kvp in agent.Memory.SpatialMemoryMap)
            {
                if (kvp.Value.Type == EntityType.Resource)
                {
                    Assert(kvp.Value.ResourceKind == ItemType.IronOre, "Perceived resource carries its IronOre kind into memory.");
                }
            }

            Destroy(oreGo, agentGo);
            TestLog.Log("[SupplyLoopValidator] Test 7 Passed: F1 regression — well-fed agent reaches Tier-2 GOAP.");
        }

        // ---------------------------------------------------------------------------------------------
        // 8. Zero GC bytes over 1,000 supply ticks (regrowth + restock exercised)
        // ---------------------------------------------------------------------------------------------
        private void ValidateZeroAllocSupplyTicks()
        {
            var registry = new WorldRegistry();
            var ledger = new EconomyLedger();
            var supply = new SupplyService(new SupplyTuning(restockIntervalTicks: 100, rationStockCap: 50, wholesaleRationCost: 1));
            var sim = new SimulationManager(registry, null, economyLedger: ledger, supplyService: supply);

            var stallId = NextGuid();
            sim.RegisterTradeStall(new TradeStallData(stallId, Vec2.Zero, rationStock: 0, treasuryGold: 100000));

            // One dormant node regrows during warm-up; 16 more regrow inside the measured window
            RegisterDormantNode(sim, supply, 50);
            for (int i = 0; i < 16; i++)
            {
                RegisterDormantNode(sim, supply, (uint)(150 + i * 55));
            }

            // Warm-up ticks 1..100 exercise the regrowth and caravan paths once (JIT, first-call paths)
            for (uint t = 1; t <= 100; t++)
            {
                supply.Tick(t, sim);
            }
            Assert(supply.RegrowthCount == 1 && supply.RestockCount == 1, "Warm-up exercised one regrowth and one restock.");

            long before = GC.GetAllocatedBytesForCurrentThread();
            for (uint t = 101; t <= 1100; t++)
            {
                if (t % 100 == 0)
                {
                    // Drain stock so every caravan visit actually restocks
                    sim.TryGetTradeStall(stallId, out var s);
                    sim.UpdateStall(s.WithRationStock(0));
                }
                supply.Tick(t, sim);
            }
            long after = GC.GetAllocatedBytesForCurrentThread();

            Assert(supply.RegrowthCount == 17, $"All 17 dormant nodes regrew (got {supply.RegrowthCount}).");
            Assert(supply.RestockCount == 11, $"11 caravan restocks (got {supply.RestockCount}).");
            Assert(after - before == 0, $"Supply ticking allocated {after - before} bytes over 1,000 ticks.");

            TestLog.Log("[SupplyLoopValidator] Test 8 Passed: Zero GC allocations over 1,000 supply ticks.");
        }

        // ---------------------------------------------------------------------------------------------
        // 10. The activity tally (Epic MAP Phase 5): each act counts once for the crawler that did it;
        //     a harvest → sell cycle closes only when the sale follows a harvest
        // ---------------------------------------------------------------------------------------------
        private void ValidateActivityTally()
        {
            var registry = new WorldRegistry();
            var sim = new SimulationManager(registry, null);

            var stallId = NextGuid();
            sim.RegisterTradeStall(new TradeStallData(stallId, Vec2.Zero, treasuryGold: 200, interactionRadius: 3f));
            var oreId = CreateResourceNode(registry, sim, new Vec2(6f, 0f), ItemType.IronOre, yield: 3, regrowTicks: 0, out var oreGo);
            var foodId = CreateResourceNode(registry, sim, new Vec2(-6f, 0f), ItemType.Ration, yield: 3, regrowTicks: 0, out var foodGo);
            var agentGo = CreateAgent(registry, sim, new Vec2(1f, 0f), out var node, out var agent);
            var otherGo = CreateAgent(registry, sim, new Vec2(1f, 2f), out _, out var other);
            agent.Inventory.Gold = 50;
            agent.Hunger = 70f;
            var empty = agent.Activity;
            Assert(empty.Cycles == 0 && empty.Meals == 0 && empty.FirstCycleTick == -1 && empty.FirstMealTick == -1, "A new crawler starts with an empty tally.");

            // A sale of salvage the crawler did not harvest counts as a sale, not a cycle
            agent.Inventory.TryAddItem(new ItemInstance((ushort)ItemType.IronOre, ItemType.IronOre, 1, EconomyTuning.Default.BaseValue(ItemType.IronOre)), out _);
            var result = RunIntent(sim, new TradeIntent(node.Id, stallId, TradeActionType.SellItem, ItemType.IronOre));
            Assert(result == IntentResult.Success, $"Sale returned {result}.");
            Assert(agent.Activity.SalvageSales == 1 && agent.Activity.Cycles == 0, $"A sale without a harvest closes no cycle ({agent.Activity}).");

            // Harvest, then sell: one cycle
            node.SetPosition(new Vec2(6.5f, 0f));
            result = RunIntent(sim, new HarvestResourceIntent(node.Id, oreId, null));
            Assert(result == IntentResult.Success, $"Ore harvest returned {result}.");
            Assert(agent.Activity.SalvageHarvests == 1 && agent.Activity.FirstHarvestTick >= 0 && agent.Activity.Meals == 0, $"The ore harvest counts once and is no meal ({agent.Activity}).");
            node.SetPosition(new Vec2(1f, 0f));
            result = RunIntent(sim, new TradeIntent(node.Id, stallId, TradeActionType.SellItem, ItemType.IronOre));
            Assert(result == IntentResult.Success, $"Second sale returned {result}.");
            var a = agent.Activity;
            Assert(a.SalvageSales == 2 && a.Cycles == 1 && a.FirstCycleTick >= a.FirstHarvestTick, $"Harvest then sale closes one cycle ({a}).");

            // A purchase counts, and eating harvested food is a meal
            result = RunIntent(sim, new TradeIntent(node.Id, stallId, TradeActionType.BuyItem, ItemType.Ration));
            Assert(result == IntentResult.Success && agent.Activity.RationPurchases == 1, $"The ration purchase counts ({result}, {agent.Activity}).");
            node.SetPosition(new Vec2(-5.5f, 0f));
            result = RunIntent(sim, new HarvestResourceIntent(node.Id, foodId, null));
            Assert(result == IntentResult.Success, $"Food harvest returned {result}.");
            a = agent.Activity;
            Assert(a.Meals == 1 && a.FirstMealTick >= 0 && a.SalvageHarvests == 1, $"Harvested food is one meal and no salvage harvest ({a}).");

            // Nothing leaks to another crawler
            var o = other.Activity;
            Assert(o.SalvageHarvests == 0 && o.SalvageSales == 0 && o.Meals == 0 && o.RationPurchases == 0, $"Another crawler's tally is untouched ({o}).");

            Destroy(oreGo, foodGo, agentGo, otherGo);
            TestLog.Log("[SupplyLoopValidator] Test 10 Passed: The activity tally counts each act once for its crawler; a cycle needs a harvest before the sale.");
        }

        // ---------------------------------------------------------------------------------------------
        // 11. A hungry crawler eats the ration it carries (Epic MAP Phase 5): the hunger reflex eats it, starvation
        //     panic counts it as known food, and the meal applies without a plan
        // ---------------------------------------------------------------------------------------------
        private void ValidateCarriedFoodIsEaten()
        {
            var id = NextGuid();
            var carrying = new AgentInventory();
            carrying.TryAddItem(new ItemInstance((ushort)ItemType.Ration, ItemType.Ration, 1, EconomyTuning.Default.BaseValue(ItemType.Ration)), out _);
            var starving = new AgentContext(id, Vec2.Zero, new AgentNeeds(90f, 60f, 60f), 0f, null, null, 1, inventory: carrying);
            var empty = new AgentContext(id, Vec2.Zero, new AgentNeeds(90f, 60f, 60f), 0f, null, null, 1, inventory: new AgentInventory());

            var reflex = new SurvivalUtilityEvaluator(null).EvaluateAsync(starving, default).GetAwaiter().GetResult();
            Assert(reflex is EatCarriedFoodIntent, $"The hunger reflex eats a carried ration (got {reflex?.GetType().Name ?? "null"}).");
            var panic = new MentalBreakEvaluator(null).EvaluateAsync(starving, default).GetAwaiter().GetResult();
            Assert(panic == null, $"A starving crawler with a ration does not panic (got {panic?.GetType().Name}).");
            panic = new MentalBreakEvaluator(null).EvaluateAsync(empty, default).GetAwaiter().GetResult();
            Assert(panic is PanicIntent, $"Without food anywhere it still panics (got {panic?.GetType().Name ?? "null"}).");
            Assert(new EatCarriedFoodAction().CreateIntent(starving) is EatCarriedFoodIntent, "The GOAP eat step emits the same intent.");

            // The meal applies with no plan at all
            var registry = new WorldRegistry();
            var sim = new SimulationManager(registry, null);
            var agentGo = CreateAgent(registry, sim, Vec2.Zero, out var node, out var agent);
            agent.Hunger = 90f;
            agent.HasFoodInInventory = true;
            var result = RunIntent(sim, new EatCarriedFoodIntent(node.Id));
            Assert(result == IntentResult.Success, $"Eating returned {result}.");
            Assert(SimMath.Approximately(agent.Hunger, 50f) && !agent.HasFoodInInventory && agent.Activity.Meals == 1,
                $"The ration relieved 40 hunger and was used up (hunger {agent.Hunger}, carries {agent.HasFoodInInventory}, meals {agent.Activity.Meals}).");

            result = RunIntent(sim, new EatCarriedFoodIntent(node.Id));
            Assert(result == IntentResult.TargetMissing && SimMath.Approximately(agent.Hunger, 50f) && agent.Activity.Meals == 1,
                $"With nothing left to eat the intent fails and changes nothing ({result}, hunger {agent.Hunger}).");

            Destroy(agentGo);
            TestLog.Log("[SupplyLoopValidator] Test 11 Passed: A hungry crawler eats the ration it carries, by reflex or plan, instead of panicking.");
        }

        // ---------------------------------------------------------------------------------------------
        // Helpers
        // ---------------------------------------------------------------------------------------------
        private static ActionGraph BuildProductionGraph()
        {
            var graph = new ActionGraph();
            graph.RegisterAction(new SearchResourceAction());
            graph.RegisterAction(new TravelToResourceAction());
            graph.RegisterAction(new HarvestResourceAction());
            graph.RegisterAction(new EatCarriedFoodAction());
            graph.RegisterAction(new TravelToStallAction(null));
            graph.RegisterAction(new BuyProvisionsAction());
            graph.RegisterAction(new SellSalvageAction());
            graph.RegisterAction(new ClaimGatherBountyAction());
            graph.RegisterAction(new ClaimScoutBountyAction());
            graph.RegisterAction(new DeliverBountyAction());
            graph.RegisterAction(new SearchSalvageAction());
            graph.RegisterAction(new GatherSalvageAction());
            graph.RegisterAction(new ScoutSectorAction());
            graph.RegisterAction(new ReportScoutBountyAction());
            return graph;
        }

        private static void RegisterDormantNode(SimulationManager sim, SupplyService supply, uint readyTick)
        {
            var id = NextGuid();
            sim.RegisterResource(new ResourceData { EntityId = id, RemainingYield = 0, MaxYield = 3, RegrowTicks = 1, IsDormant = true });
            supply.RegisterDormant(id, readyTick);
        }

        private static int IndexOf<T>(GoapPlan plan) where T : GoapAction
        {
            for (int i = 0; i < plan.Actions.Count; i++)
            {
                if (plan.Actions[i] is T) return i;
            }
            return -1;
        }

        /// <summary>
        /// Enqueues an intent and ticks the simulation until it completes (consequences applied by the manager).
        /// </summary>
        private static IntentResult RunIntent(SimulationManager sim, AgentIntent intent, int maxTicks = 200)
        {
            IntentResult? outcome = null;
            intent.OnComplete = r => outcome = r;
            sim.EnqueueIntent(intent);

            for (int i = 0; i < maxTicks && !outcome.HasValue; i++)
            {
                sim.Tick();
            }

            Assert(outcome.HasValue, $"{intent.GetType().Name} did not complete within {maxTicks} ticks.");
            return outcome.Value;
        }

        private static EntityId CreateResourceNode(WorldRegistry registry, SimulationManager sim, Vec2 pos, ItemType kind, int yield, uint regrowTicks, out WorldEntity node)
        {
            var id = NextGuid();
            node = HeadlessEntities.Prop(registry, EntityType.Resource, pos, id: id, resourceKind: kind, name: $"[Test_{kind}Node]");
            sim.RegisterResource(new ResourceData { EntityId = id, RemainingYield = yield, MaxYield = yield, YieldItem = kind, RegrowTicks = regrowTicks });
            return id;
        }

        private static TestCrawler CreateAgent(WorldRegistry registry, SimulationManager sim, Vec2 pos, out WorldEntity node, out AgentState agent)
        {
            var crawler = HeadlessEntities.Crawler(registry, sim, null, pos, id: NextGuid(), name: "[Test_SupplyAgent]");
            node = crawler.Node;
            agent = crawler.Agent;
            return crawler;
        }

        // Each test owns its registries, so cleanup only stops the crawlers (what destroying their GameObjects did)
        private static void Destroy(params object[] objects)
        {
            for (int i = 0; i < objects.Length; i++)
            {
                if (objects[i] is TestCrawler crawler) crawler.Runtime.Dispose();
            }
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition)
            {
                TestLog.LogError($"[SupplyLoopValidator] FAILED: {message}");
                throw new Exception($"Assertion failed: {message}");
            }
        }
    }
}
