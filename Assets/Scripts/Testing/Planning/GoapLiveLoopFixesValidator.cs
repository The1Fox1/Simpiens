using System;
using Simpiens.Cognition.Contracts;
using Simpiens.Cognition.Inventory;
using Simpiens.Cognition.Memory;
using Simpiens.Cognition.Planning;
using Simpiens.Cognition.Planning.Actions;
using Simpiens.Cognition.Planning.Goals;
using Simpiens.Simulation.Economy;
using Simpiens.Simulation.Spatial;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Simpiens.Testing.Planning
{
    /// <summary>
    /// Mechanism validation for the fixes derived from the SimLog crawler trace (architecture_history.md, "GOAP Live-Loop Fixes from the SimLog Trace"):
    /// need-driven companion aid, static reachability regions, reachability-aware stall/bounty planning,
    /// and GOAP relevance pruning.
    /// </summary>
    public class GoapLiveLoopFixesValidator
    {
        // 20x20 grid, 1m cells, world spans [-10, 10); cell (cx, cy) has its centre at (cx - 9.5, cy - 9.5)
        private const int GridSize = 20;

        private static uint _guidSeed = 52000;
        private static GUID NextGuid() => new GUID(_guidSeed++, 0x60A9, 0x4004, 0xF1F1);

        public void Start()
        {
            Debug.Log("[GoapLiveLoopFixesValidator] Beginning validation of GOAP live-loop fixes...");

            ValidateNeedDrivenAid();
            ValidateStaticRegions();
            ValidateReachabilityAwarePlanning();
            ValidateRelevancePruning();
            ValidateZeroAllocation();

            Debug.Log("<color=green>[GoapLiveLoopFixesValidator] ALL 5 TESTS PASSED SUCCESSFULLY!</color>");
        }

        // 1. AidCompanion / ShareFood only when someone in view actually needs help
        private void ValidateNeedDrivenAid()
        {
            var helperId = NextGuid();
            var peerId = NextGuid();
            var memory = new AgentMemory();
            memory.RecordInteraction(peerId, deltaWarmth: 70, deltaTrust: 60, deltaFear: 0, deltaRespect: 10, 0);

            var goal = new AidCompanionGoal();
            var share = new ShareFoodAction();

            var calm = SnapshotWithPeer(peerId, new Vector2(2f, 0f), peerHunger: 30f);
            var calmContext = Context(helperId, Vector2.zero, hunger: 20f, calm, memory);
            Assert(!goal.IsValid(calmContext), "AidCompanion invalid when no companion is in distress (the 16k-loop root cause).");
            Assert(!share.IsValid(calmContext), "ShareFood invalid without a distressed companion.");

            var starving = SnapshotWithPeer(peerId, new Vector2(2f, 0f), peerHunger: 92f);
            var needContext = Context(helperId, Vector2.zero, hunger: 20f, starving, memory);
            Assert(goal.IsValid(needContext), "AidCompanion valid for a sated helper with a starving warm companion in view.");
            Assert(goal.CalculatePriority(needContext) >= AltruismTargeting.AltruismThreshold, "Priority is the companion's altruism score.");
            var intent = share.CreateIntent(needContext) as ShareResourceIntent;
            Assert(intent != null && intent.TargetPawnId == peerId, "ShareFood targets the distressed companion.");

            var hungryHelper = Context(helperId, Vector2.zero, hunger: 55f, starving, memory);
            Assert(!goal.IsValid(hungryHelper), "A hungry helper (>= 50) does not pursue AidCompanion.");

            Debug.Log("[GoapLiveLoopFixesValidator] Test 1 Passed: need-driven AidCompanion/ShareFood.");
        }

        // 2. Connected regions over the static nav grid
        private void ValidateStaticRegions()
        {
            var grid = new StaticNavGrid(GridSize, GridSize, 1f);
            Assert(grid.AreConnected(CellCenter(1, 1), CellCenter(7, 7)), "Unknown (regions not built) is treated as connected.");

            BuildPen(grid);
            grid.EnsureRegions();
            int builds = grid.RegionRebuildCount;

            Assert(!grid.AreConnected(CellCenter(1, 1), CellCenter(7, 7)), "Inside a walled pen is disconnected from outside.");
            Assert(grid.AreConnected(CellCenter(1, 1), CellCenter(18, 15)), "Two open-yard points are connected.");
            Assert(grid.AreConnected(CellCenter(6, 6), CellCenter(8, 8)), "Two points inside the pen are connected.");
            Assert(grid.AreConnected(new Vector2(500f, 0f), CellCenter(7, 7)), "Out-of-bounds queries are treated as connected (unknown).");

            grid.SetWall(5, 5); // already a wall: walkability unchanged
            grid.EnsureRegions();
            Assert(grid.RegionRebuildCount == builds, "No rebuild when walkability is unchanged.");

            grid.SetCellWalkable(5, 7, true); // open a door in the pen's west wall
            grid.EnsureRegions();
            Assert(grid.RegionRebuildCount == builds + 1, "Rebuilt once after walkability changed.");
            Assert(grid.AreConnected(CellCenter(1, 1), CellCenter(7, 7)), "Opening a door connects the pen to the yard.");

            Debug.Log("[GoapLiveLoopFixesValidator] Test 2 Passed: static connected regions.");
        }

        // 3. Walled-in agents ignore stalls and bounties they cannot reach
        private void ValidateReachabilityAwarePlanning()
        {
            var grid = new StaticNavGrid(GridSize, GridSize, 1f);
            BuildPen(grid);
            grid.EnsureRegions();

            var stallId = NextGuid();
            var stall = new TradeStallData(stallId, CellCenter(15, 15), rationSellPrice: 5, rationStock: 20, treasuryGold: 200);
            var bounty = new BountyContract(1, BountyType.GatherResource, 12, ItemType.IronOre, 1, 25, 0, 5000, CellCenter(16, 3), creatorStallId: stallId);

            var snapshot = new SharedWorldSnapshot(GridSize, GridSize, 1f, 8, _ => { });
            snapshot.SetStaticGrid(grid);
            snapshot.SetStalls(new[] { stall }, 1);
            snapshot.SetBounties(new[] { bounty }, 1);

            var inventory = new AgentInventory(startingGold: 50);
            var penned = Context(NextGuid(), CellCenter(7, 7), hunger: 60f, snapshot, new AgentMemory(), inventory);
            var free = Context(NextGuid(), CellCenter(2, 2), hunger: 60f, snapshot, new AgentMemory(), inventory);

            var claim = new ClaimGatherBountyAction();
            var fulfil = new FulfillBountyGoal();
            var travel = new TravelToStallAction(null);
            var purchase = new PurchaseProvisionsGoal();

            Assert(!claim.IsValid(penned), "Penned agent cannot claim a bounty outside its region.");
            Assert(!travel.IsValid(penned), "Penned agent cannot plan travel to an unreachable stall.");
            Assert(!purchase.IsValid(penned), "Penned agent does not pursue provisions at an unreachable stall.");
            var calmPenned = Context(penned.AgentId, penned.Position, hunger: 20f, snapshot, new AgentMemory(), inventory);
            Assert(!fulfil.IsValid(calmPenned), "FulfillBounty invalid when no reachable bounty exists.");

            Assert(claim.IsValid(free), "Free agent can claim the reachable bounty.");
            Assert(travel.IsValid(free), "Free agent can travel to the stall.");
            Assert(purchase.IsValid(free), "Free agent pursues provisions at a reachable stall.");
            var calmFree = Context(free.AgentId, free.Position, hunger: 20f, snapshot, new AgentMemory(), inventory);
            Assert(fulfil.IsValid(calmFree), "FulfillBounty valid for a free agent.");

            Debug.Log("[GoapLiveLoopFixesValidator] Test 3 Passed: reachability-aware stall and bounty planning.");
        }

        // 4. Relevance pruning: impossible goals fail fast; solvable goals keep their optimal plan
        private void ValidateRelevancePruning()
        {
            var graph = BuildWideGraph();
            var planner = new GoapPlanner();

            var empty = new SharedWorldSnapshot(10, 10, 2f, 8, _ => { });
            var noBounty = Context(NextGuid(), Vector2.zero, hunger: 10f, empty, new AgentMemory());
            var state = WorldStateBuilder.BuildWorldState(noBounty, noBounty.Inventory);
            var plan = new GoapPlan();

            bool impossible = planner.Plan(state, new FulfillBountyGoal(), graph, noBounty, plan);
            Assert(!impossible, "FulfillBounty is impossible without any bounty.");
            Assert(planner.LastNodeCount == 0, $"Impossible goal rejected before search (allocated {planner.LastNodeCount} nodes).");

            var withBounty = new SharedWorldSnapshot(10, 10, 2f, 8, _ => { });
            withBounty.SetBounties(new[] { new BountyContract(1, BountyType.GatherResource, 12, ItemType.IronOre, 1, 25, 0, 5000, new Vector2(-8f, 10f)) }, 1);
            withBounty.SetStalls(new[] { new TradeStallData(NextGuid(), new Vector2(20f, 20f)) }, 1);
            var solvable = Context(NextGuid(), Vector2.zero, hunger: 10f, withBounty, new AgentMemory());
            state = WorldStateBuilder.BuildWorldState(solvable, solvable.Inventory);

            bool resolved = planner.Plan(state, new FulfillBountyGoal(), graph, solvable, plan);
            Assert(resolved && plan.Actions.Count == 5, $"Salvage bounty plan still resolves with 5 steps (got {plan.Actions.Count}).");
            Assert(planner.LastNodeCount < 64, $"Pruned search stays small ({planner.LastNodeCount} nodes, pool 256).");

            Debug.Log($"[GoapLiveLoopFixesValidator] Test 4 Passed: relevance pruning (solvable plan in {planner.LastNodeCount} nodes).");
        }

        // 5. Region queries and pruned planning allocate nothing in steady state
        private void ValidateZeroAllocation()
        {
            var grid = new StaticNavGrid(GridSize, GridSize, 1f);
            BuildPen(grid);
            grid.EnsureRegions();

            var graph = BuildWideGraph();
            var planner = new GoapPlanner();
            var snapshot = new SharedWorldSnapshot(10, 10, 2f, 8, _ => { });
            snapshot.SetBounties(new[] { new BountyContract(1, BountyType.GatherResource, 12, ItemType.IronOre, 1, 25, 0, 5000, new Vector2(-8f, 10f)) }, 1);
            snapshot.SetStalls(new[] { new TradeStallData(NextGuid(), new Vector2(20f, 20f)) }, 1);
            var context = Context(NextGuid(), Vector2.zero, hunger: 10f, snapshot, new AgentMemory());
            var state = WorldStateBuilder.BuildWorldState(context, context.Inventory);
            var goal = new FulfillBountyGoal();
            var plan = new GoapPlan();

            planner.Plan(state, goal, graph, context, plan); // warm-up
            bool connected = grid.AreConnected(CellCenter(1, 1), CellCenter(7, 7));

            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 1000; i++)
            {
                connected ^= grid.AreConnected(CellCenter(1, 1), CellCenter(7, 7));
            }
            for (int i = 0; i < 100; i++)
            {
                planner.Plan(state, goal, graph, context, plan);
            }
            long after = GC.GetAllocatedBytesForCurrentThread();

            Assert(after - before == 0, $"Region queries + pruned planning allocated {after - before} bytes.");
            Debug.Log("[GoapLiveLoopFixesValidator] Test 5 Passed: zero allocations in steady state.");
        }

        // ---------------------------------------------------------------------------------------------
        private static ActionGraph BuildWideGraph()
        {
            // The live action set, including actions irrelevant to bounties (Rest, social, anchor, directives)
            var graph = new ActionGraph();
            graph.RegisterAction(new SearchResourceAction());
            graph.RegisterAction(new TravelToResourceAction());
            graph.RegisterAction(new HarvestResourceAction());
            graph.RegisterAction(new EatCarriedFoodAction());
            graph.RegisterAction(new RestAction());
            graph.RegisterAction(new TravelToPeerAction());
            graph.RegisterAction(new ShareFoodAction());
            graph.RegisterAction(new TravelToAnchorAction(null));
            graph.RegisterAction(new RelaxAtAnchorAction());
            graph.RegisterAction(new TravelToDirectiveAction(null));
            graph.RegisterAction(new ExecuteDirectiveAction());
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

        /// <summary>Walls a 5x5 ring (cells 5..9) so cells 6..8 form a closed pen.</summary>
        private static void BuildPen(StaticNavGrid grid)
        {
            for (int i = 5; i <= 9; i++)
            {
                grid.SetWall(i, 5);
                grid.SetWall(i, 9);
                grid.SetWall(5, i);
                grid.SetWall(9, i);
            }
        }

        private static Vector2 CellCenter(int cx, int cy) => new Vector2(cx - GridSize * 0.5f + 0.5f, cy - GridSize * 0.5f + 0.5f);

        private static SharedWorldSnapshot SnapshotWithPeer(GUID peerId, Vector2 peerPos, float peerHunger)
        {
            var snapshot = new SharedWorldSnapshot(GridSize, GridSize, 1f, 8, _ => { });
            var entity = new EntitySnapshot(peerId, peerPos, Vector2.zero, 0.35f, EntityType.Pawn, false, peerHunger);
            snapshot.AddEntity(Mathf.FloorToInt(peerPos.x + GridSize * 0.5f), Mathf.FloorToInt(peerPos.y + GridSize * 0.5f), in entity);
            return snapshot;
        }

        private static AgentContext Context(GUID id, Vector2 position, float hunger, SharedWorldSnapshot snapshot, AgentMemory memory, AgentInventory inventory = null)
        {
            return new AgentContext(id, position, new AgentNeeds(hunger, 90f, 80f), 0f, snapshot, memory, 1,
                inventory: (inventory ?? new AgentInventory()).GetSnapshot());
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition)
            {
                Debug.LogError($"[GoapLiveLoopFixesValidator] FAILED: {message}");
                throw new Exception($"Assertion failed: {message}");
            }
        }
    }
}
