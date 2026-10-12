using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
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
using UnityEngine;
using VContainer.Unity;
using Debug = UnityEngine.Debug;

namespace Simpiens.Testing.Economy
{
    /// <summary>
    /// Headless mechanism validation for §3.1 (Close the Milestone 1 Supply Side):
    /// typed harvesting, inventory-full failure, deterministic regrowth, the supply caravan, Scout bounty
    /// fulfilment, salvage bounty planning, the F1 Tier-1 → GOAP regression, and zero-GC supply ticking.
    /// These inject preconditions by design; live emergence is asserted by OutpostSupplyLoopScenario.
    /// </summary>
    public class SupplyLoopValidator : IStartable
    {
        private static uint _guidSeed = 31000;
        private static GUID NextGuid() => new GUID(_guidSeed++, 0x51A1, 0x3001, 0xC0DE);

        public void Start()
        {
            Debug.Log("[SupplyLoopValidator] Beginning automated validation of §3.1 supply loop mechanisms...");

            ValidateTypedHarvest();
            ValidateInventoryFullHarvestFails();
            ValidateDeterministicRegrowth();
            ValidateSupplyCaravan();
            ValidateProvisionalSalvageExport();
            ValidateScoutBountyFulfilment();
            ValidateSalvageBountyPlan();
            ValidateWellFedAgentReachesGoap();
            ValidateZeroAllocSupplyTicks();

            Debug.Log("<color=green>[SupplyLoopValidator] ALL 9 DOMAIN TESTS PASSED SUCCESSFULLY!</color>");
        }

        // ---------------------------------------------------------------------------------------------
        // 1. Ore yields IronOre into inventory and leaves hunger alone; food still relieves hunger
        // ---------------------------------------------------------------------------------------------
        private void ValidateTypedHarvest()
        {
            var registry = new WorldRegistry();
            var sim = new SimulationManager(registry, null);

            var oreId = CreateResourceNode(registry, sim, new Vector2(0f, 0f), ItemType.IronOre, yield: 3, regrowTicks: 0, out var oreGo);
            var foodId = CreateResourceNode(registry, sim, new Vector2(4f, 0f), ItemType.Ration, yield: 3, regrowTicks: 0, out var foodGo);
            var agentGo = CreateAgent(registry, sim, new Vector2(0.5f, 0f), out var node, out var agent);
            agent.Hunger = 70f;

            var result = RunIntent(sim, new HarvestResourceIntent(node.Id, oreId, null));
            Assert(result == IntentResult.Success, $"Ore harvest returned {result} instead of Success.");
            Assert(agent.Inventory.GetItemCount(ItemType.IronOre) == 1, "Ore harvest placed 1 IronOre in inventory.");
            Assert(Mathf.Approximately(agent.Hunger, 70f), $"Ore harvest left hunger unchanged (hunger {agent.Hunger}).");
            Assert(sim.TryGetResource(oreId, out var ore) && ore.RemainingYield == 2, "Ore node yield decremented to 2.");

            node.SetLogicalPosition(new Vector2(4.5f, 0f));
            result = RunIntent(sim, new HarvestResourceIntent(node.Id, foodId, null));
            Assert(result == IntentResult.Success, $"Food harvest returned {result} instead of Success.");
            Assert(Mathf.Approximately(agent.Hunger, 20f), $"Food harvest relieved 50 hunger (hunger {agent.Hunger}).");
            Assert(agent.Inventory.GetItemCount(ItemType.IronOre) == 1, "Food harvest did not add salvage.");

            Destroy(oreGo, foodGo, agentGo);
            Debug.Log("[SupplyLoopValidator] Test 1 Passed: Typed harvest (ore → inventory, food → hunger relief).");
        }

        // ---------------------------------------------------------------------------------------------
        // 2. A full inventory fails the harvest without consuming yield
        // ---------------------------------------------------------------------------------------------
        private void ValidateInventoryFullHarvestFails()
        {
            var registry = new WorldRegistry();
            var sim = new SimulationManager(registry, null);

            var herbId = CreateResourceNode(registry, sim, Vector2.zero, ItemType.MedicinalHerb, yield: 2, regrowTicks: 0, out var herbGo);
            var agentGo = CreateAgent(registry, sim, new Vector2(0.5f, 0f), out var node, out var agent);

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
            Debug.Log("[SupplyLoopValidator] Test 2 Passed: Inventory-full harvest fails without losing yield.");
        }

        // ---------------------------------------------------------------------------------------------
        // 3. Regrowth: dormant at +49, harvestable again exactly at +RegrowTicks
        // ---------------------------------------------------------------------------------------------
        private void ValidateDeterministicRegrowth()
        {
            var registry = new WorldRegistry();
            var sim = new SimulationManager(registry, null);

            var bushId = CreateResourceNode(registry, sim, Vector2.zero, ItemType.Ration, yield: 1, regrowTicks: 50, out var bushGo);
            var agentGo = CreateAgent(registry, sim, new Vector2(0.5f, 0f), out var node, out var agent);

            var result = RunIntent(sim, new HarvestResourceIntent(node.Id, bushId, null));
            Assert(result == IntentResult.Success, $"Depleting harvest returned {result}.");
            Assert(sim.TryGetResource(bushId, out var bush) && bush.IsDormant, "Depleted regrowable node is dormant, not removed.");
            Assert(registry.GetNode(bushId) == null, "Dormant node is unregistered from the world (imperceptible).");

            uint depletedAt = bush.DepletedAtTick;
            while (sim.CurrentTick < depletedAt + 49) sim.Tick();
            Assert(bush.IsDormant, $"Node still dormant at depletion +49 (tick {sim.CurrentTick}).");
            Assert(sim.Supply.RegrowthCount == 0, "No regrowth recorded before +50.");

            sim.Tick();
            Assert(sim.CurrentTick == depletedAt + 50, "Advanced to depletion +50.");
            Assert(!bush.IsDormant && bush.RemainingYield == bush.MaxYield, "Node regrew to MaxYield at exactly +50.");
            Assert(registry.GetNode(bushId) != null, "Regrown node re-registered with the world.");
            Assert(sim.Supply.RegrowthCount == 1, "RegrowthCount is 1.");

            Destroy(bushGo, agentGo);
            Debug.Log("[SupplyLoopValidator] Test 3 Passed: Deterministic dormant regrowth at exactly RegrowTicks.");
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
            sim.RegisterTradeStall(new TradeStallData(richId, new Vector2(0f, 0f), rationStock: 10, treasuryGold: 500));
            sim.RegisterTradeStall(new TradeStallData(poorId, new Vector2(20f, 0f), rationStock: 0, treasuryGold: 10));
            ledger.RecordFaucet(510, "InitialSeed");

            while (sim.CurrentTick < 20) sim.Tick();

            Assert(sim.TryGetTradeStall(richId, out var rich), "Rich stall retrieved.");
            Assert(rich.RationStock == 50, $"Rich stall restocked to cap (stock {rich.RationStock}).");
            Assert(rich.TreasuryGold == 420, $"Rich stall paid 40 × 2g = 80g (treasury {rich.TreasuryGold}).");
            Assert(sim.TryGetTradeStall(poorId, out var poor) && poor.RationStock == 0 && poor.TreasuryGold == 10, "Unaffordable restock skipped.");
            Assert(supply.RestockCount == 1, $"RestockCount is {supply.RestockCount} instead of 1.");
            Assert(supply.SkippedRestockCount == 1, $"SkippedRestockCount is {supply.SkippedRestockCount} instead of 1 (poor stall).");
            Assert(ledger.TotalSinkedGold == 80, $"SupplyCaravan sink recorded 80g (got {ledger.TotalSinkedGold}).");
            Assert(ledger.ValidateConservation(rich.TreasuryGold + poor.TreasuryGold, 0, out int disc), $"Ledger conservation holds (discrepancy {disc}).");

            // Already at cap: next visit is a no-op
            while (sim.CurrentTick < 40) sim.Tick();
            Assert(supply.RestockCount == 1, "Full stall not restocked again.");

            Debug.Log("[SupplyLoopValidator] Test 4 Passed: Supply caravan respects cap, treasury and ledger conservation.");
        }

        // ---------------------------------------------------------------------------------------------
        // 4b. PROVISIONAL export: caravan buys salvage above the reserve (named faucet) before restocking
        // ---------------------------------------------------------------------------------------------
        private void ValidateProvisionalSalvageExport()
        {
            var registry = new WorldRegistry();
            var ledger = new EconomyLedger();
            var supply = new SupplyService(new SupplyTuning(restockIntervalTicks: 20, rationStockCap: 50, wholesaleRationCost: 2,
                provisionalOreExportPrice: 24, provisionalHerbExportPrice: 30, provisionalExportReserve: 10));
            var sim = new SimulationManager(registry, null, economyLedger: ledger, supplyService: supply);

            // Broke stall holding surplus salvage: export revenue (5×24 + 2×30 = 180g) must fund the 100g restock
            var stallId = NextGuid();
            sim.RegisterTradeStall(new TradeStallData(stallId, Vector2.zero, rationStock: 0, oreStock: 15, herbStock: 12, treasuryGold: 0));

            while (sim.CurrentTick < 20) sim.Tick();

            Assert(sim.TryGetTradeStall(stallId, out var stall), "Stall retrieved.");
            Assert(stall.OreStock == 10 && stall.HerbStock == 10, $"Export leaves the reserve (ore {stall.OreStock}, herb {stall.HerbStock}).");
            Assert(stall.RationStock == 50, $"Export revenue funded the restock (stock {stall.RationStock}).");
            Assert(stall.TreasuryGold == 80, $"Treasury = 180g export - 100g restock (got {stall.TreasuryGold}).");
            Assert(supply.ExportCount == 1 && ledger.TotalFaucetedGold == 180, $"Export faucet recorded 180g (got {ledger.TotalFaucetedGold}).");
            Assert(ledger.ValidateConservation(stall.TreasuryGold, 0, out int disc), $"Ledger conservation holds (discrepancy {disc}).");

            // Stock at reserve: no further export
            while (sim.CurrentTick < 40) sim.Tick();
            Assert(supply.ExportCount == 1, "Stock at reserve is not exported again.");

            Debug.Log("[SupplyLoopValidator] Test 4b Passed: Provisional caravan export is a named faucet that keeps the reserve.");
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
            var stallPos = new Vector2(0f, 9f);
            sim.RegisterTradeStall(new TradeStallData(stallId, stallPos, treasuryGold: 200, interactionRadius: 3f));
            ledger.RecordFaucet(200 + 10, "InitialSeed");

            // Agent stands just west of Herb Hollow's bounds (x = 5)
            var agentGo = CreateAgent(registry, sim, new Vector2(4.5f, 10f), out var node, out var agent);

            var bounty = new BountyContract(42, BountyType.ScoutSector, (byte)sector, ItemType.None, 1, 15, 0, 5000,
                claimantId: node.Id, creatorStallId: stallId, status: BountyStatus.Claimed);
            sim.RegisterBounty(bounty);
            Assert(ledger.HasEscrow(42) && ledger.GetEscrowAmount(42) == 15, "Scout bounty reward locked in escrow at registration.");
            Assert(sim.TryGetBounty(42, out var registered) && registered.TargetLocation != default, "Default target location derived from the sector.");

            var path = new PathResponse();
            path.Initialize(4, null);
            path.Waypoints[0] = new Vector2(6.5f, 10f);
            path.Length = 1;
            path.IsValid = true;

            var result = RunIntent(sim, new ScoutIntent(node.Id, 42, (byte)sector, path));
            Assert(result == IntentResult.Success, $"ScoutIntent returned {result} instead of Success.");
            Assert(agent.State.ScoutReportBountyId == 42, "Scout report recorded on entering the sector.");

            // Report at the stall
            node.SetLogicalPosition(stallPos + new Vector2(1f, 0f));
            int goldBefore = agent.Inventory.Gold;
            result = RunIntent(sim, new DeliverBountyIntent(node.Id, 42, stallId));
            Assert(result == IntentResult.Success, $"Scout report delivery returned {result} instead of Success.");
            Assert(agent.Inventory.Gold == goldBefore + 15, $"Reward paid (gold {agent.Inventory.Gold}, expected {goldBefore + 15}).");
            Assert(!ledger.HasEscrow(42) && ledger.TotalEscrowGold == 0, "Escrow released.");
            Assert(zones.IsExplored(sector), "Herb Hollow marked explored.");
            Assert(agent.State.ScoutReportBountyId == 0, "Scout report consumed.");
            Assert(sim.TryGetBounty(42, out var done) && done.Status == BountyStatus.Completed, "Scout bounty Completed.");

            Assert(sim.TryGetTradeStall(stallId, out var stall), "Stall retrieved.");
            Assert(ledger.ValidateConservation(stall.TreasuryGold, (uint)agent.Inventory.Gold, out int disc), $"Ledger conservation holds (discrepancy {disc}).");

            // F8: a bounty that cannot be escrowed is rejected instead of minting gold on delivery
            var broke = new TradeStallData(NextGuid(), Vector2.zero, treasuryGold: 5);
            sim.RegisterTradeStall(broke);
            bool accepted = sim.TryRegisterBounty(new BountyContract(43, BountyType.GatherResource, 0, ItemType.IronOre, 1, 50, 0, 5000, creatorStallId: broke.StallEntityId));
            Assert(!accepted && !sim.TryGetBounty(43, out _), "Un-escrowable bounty rejected at registration.");

            Destroy(agentGo);
            Debug.Log("[SupplyLoopValidator] Test 5 Passed: Scout bounty fulfilment via escrow and sector exploration.");
        }

        // ---------------------------------------------------------------------------------------------
        // 6. From a clean world state the planner builds Claim → Search → Gather → Travel → Deliver
        // ---------------------------------------------------------------------------------------------
        private void ValidateSalvageBountyPlan()
        {
            var snapshot = new SharedWorldSnapshot(10, 10, 2.0f, 8, _ => { });
            snapshot.SetBounties(new[] { new BountyContract(1, BountyType.GatherResource, 12, ItemType.IronOre, 1, 25, 0, 5000, new Vector2(-8f, 10f)) }, 1);
            snapshot.SetStalls(new[] { new TradeStallData(NextGuid(), new Vector2(20f, 20f)) }, 1);

            var memory = new AgentMemory();
            var inventory = new AgentInventory();
            var context = new AgentContext(NextGuid(), Vector2.zero, new AgentNeeds(10f, 90f, 80f), 0f, snapshot, memory, 1,
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

            Debug.Log("[SupplyLoopValidator] Test 6 Passed: Salvage bounty plan resolved from a clean world state.");
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
            sim.RegisterTradeStall(new TradeStallData(stallId, new Vector2(10f, 0f), treasuryGold: 300));
            sim.RegisterBounty(new BountyContract(1, BountyType.GatherResource, 0, ItemType.IronOre, 1, 25, 0, 5000, new Vector2(3f, 0f), creatorStallId: stallId));

            // A perceivable ore vein: knowledge comes from vision through the typed snapshot, not injection
            CreateResourceNode(registry, sim, new Vector2(3f, 0f), ItemType.IronOre, yield: 3, regrowTicks: 0, out var oreGo);
            var agentGo = CreateAgent(registry, sim, Vector2.zero, out var node, out var agent);
            agent.Hunger = 10f;
            sim.Tick(); // publish the snapshot (stall, bounty, typed ore node)

            var cognition = new AgentCognitionService(null, new AmbientWanderEvaluator(null));
            var goals = new List<GoapGoal> { new SatiateHungerGoal(), new FulfillBountyGoal(), new EarnIncomeGoal() };
            var evaluators = new ICognitiveEvaluator[] { new SurvivalUtilityEvaluator(null) };

            bool completed = cognition.EvaluateAgentCognitionAsync(agent.State, grid, sim, null, new GoapPlanner(), BuildProductionGraph(), goals, evaluators)
                .AsTask().Wait(TimeSpan.FromSeconds(5));
            Assert(completed, "Cognition evaluation completed.");

            var intent = agent.State.CurrentIntent;
            Assert(intent != null, "An intent was produced.");
            Assert(!(intent is IdleIntent) && !(intent is WanderIntent), $"Well-fed agent got {intent?.GetType().Name} instead of a GOAP intent.");
            Assert(agent.State.ActiveGoal is FulfillBountyGoal, $"Active goal is {agent.State.ActiveGoal?.Name ?? "null"} instead of FulfillBounty.");
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
            Debug.Log("[SupplyLoopValidator] Test 7 Passed: F1 regression — well-fed agent reaches Tier-2 GOAP.");
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
            sim.RegisterTradeStall(new TradeStallData(stallId, Vector2.zero, rationStock: 0, treasuryGold: 100000));

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

            Debug.Log("[SupplyLoopValidator] Test 8 Passed: Zero GC allocations over 1,000 supply ticks.");
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

        private static GUID CreateResourceNode(WorldRegistry registry, SimulationManager sim, Vector2 pos, ItemType kind, int yield, uint regrowTicks, out GameObject go)
        {
            go = new GameObject($"[Test_{kind}Node]");
            go.transform.position = pos;
            var node = go.AddComponent<NodeController>();
            node.Type = EntityType.Resource;
            node.ConfigureResourceKind(kind);
            var id = NextGuid();
            node.Initialize(id, new NodeConfiguration(0f));
            registry.RegisterNode(node);
            sim.RegisterResource(new ResourceData { EntityId = id, RemainingYield = yield, MaxYield = yield, YieldItem = kind, RegrowTicks = regrowTicks });
            return id;
        }

        private static GameObject CreateAgent(WorldRegistry registry, SimulationManager sim, Vector2 pos, out NodeController node, out AutonomousAgent agent)
        {
            var go = new GameObject("[Test_SupplyAgent]");
            go.transform.position = pos;
            node = go.AddComponent<NodeController>();
            node.Type = EntityType.Pawn;
            node.Initialize(NextGuid(), new NodeConfiguration(2f));
            agent = go.AddComponent<AutonomousAgent>();
            agent.Initialize(node.Id, null, null, sim, null);
            registry.RegisterNode(node);
            return go;
        }

        private static void Destroy(params GameObject[] objects)
        {
            for (int i = 0; i < objects.Length; i++)
            {
                if (objects[i] != null) UnityEngine.Object.DestroyImmediate(objects[i]);
            }
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition)
            {
                Debug.LogError($"[SupplyLoopValidator] FAILED: {message}");
                throw new Exception($"Assertion failed: {message}");
            }
        }
    }
}
