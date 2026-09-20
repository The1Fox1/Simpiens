using System;
using System.Diagnostics;
using Simpiens.Cognition.Contracts;
using Simpiens.Cognition.Planning;
using Simpiens.Cognition.Planning.Actions;
using Simpiens.Cognition.Planning.Goals;
using UnityEngine;
using VContainer.Unity;
using Debug = UnityEngine.Debug;

namespace Simpiens.Testing
{
    /// <summary>
    /// Automated test harness for Epic 6 Phase 2: A* Action Graph Search (GOAP Planner).
    /// Tests plan resolution, cost optimization, branch pruning, and zero-allocation performance.
    /// </summary>
    public class Epic6Phase2Validator : IStartable
    {
        private class MockInvalidAction : GoapAction
        {
            public MockInvalidAction() : base("MockInvalidAction", baseCost: 0.1f)
            {
                Preconditions = WorldState.Empty;
                Effects = WorldState.Empty.With(StateFact.IsHungry, false);
            }

            public override bool IsValid(AgentContext context) => false; // Always invalid

            public override AgentIntent CreateIntent(AgentContext context) => new IdleIntent(context.AgentId);
        }

        public void Start()
        {
            Debug.Log("[Epic6Phase2Validator] Beginning automated validation of Epic 6 Phase 2...");

            var planner = new GoapPlanner();
            var plan = new GoapPlan();
            var dummyContext = new AgentContext(
                new UnityEngine.GUID(),
                Vector2.zero,
                hunger: 80f,
                energy: 100f,
                frustration: 0f,
                snapshot: null,
                memory: null,
                currentTick: 0
            );

            ValidateSingleStepPlan(planner, plan, dummyContext);
            ValidateTwoStepPlan(planner, plan, dummyContext);
            ValidateThreeStepPlan(planner, plan, dummyContext);
            ValidateBranchOptimization(planner, plan, dummyContext);
            ValidateProceduralInvalidation(planner, plan, dummyContext);
            ValidateUnreachableGoal(planner, plan, dummyContext);
            ValidateAlreadySatisfiedGoal(planner, plan, dummyContext);
            ValidateZeroAllocationBenchmark(planner, plan, dummyContext);

            Debug.Log("<color=green>[Epic6Phase2Validator] ALL 8 TESTS PASSED SUCCESSFULLY!</color>");
        }

        private void ValidateSingleStepPlan(GoapPlanner planner, GoapPlan plan, AgentContext context)
        {
            var graph = CreateBaseGraph();
            var startState = WorldState.Empty
                .With(StateFact.IsHungry, true)
                .With(StateFact.AtResourceLocation, true);

            var goal = new SatiateHungerGoal();
            bool success = planner.Plan(startState, goal, graph, context, plan);

            Assert(success, "Single-step planning succeeded.");
            Assert(plan.Count == 1, $"Plan count is 1 (actual: {plan.Count}).");
            Assert(plan.Actions[0] is HarvestResourceAction, "Plan contains HarvestResourceAction.");
            Assert(Mathf.Approximately(plan.TotalCost, 1.0f), $"TotalCost is 1.0 (actual: {plan.TotalCost}).");

            Debug.Log("[PASS] Test 1: Single-step plan resolution passed.");
        }

        private void ValidateTwoStepPlan(GoapPlanner planner, GoapPlan plan, AgentContext context)
        {
            var graph = CreateBaseGraph();
            var startState = WorldState.Empty
                .With(StateFact.IsHungry, true)
                .With(StateFact.KnowsResourceLocation, true);

            var goal = new SatiateHungerGoal();
            bool success = planner.Plan(startState, goal, graph, context, plan);

            Assert(success, "Two-step planning succeeded.");
            Assert(plan.Count == 2, $"Plan count is 2 (actual: {plan.Count}).");
            Assert(plan.Actions[0] is TravelToResourceAction, "Step 1 is TravelToResourceAction.");
            Assert(plan.Actions[1] is HarvestResourceAction, "Step 2 is HarvestResourceAction.");
            Assert(Mathf.Approximately(plan.TotalCost, 3.0f), $"TotalCost is 3.0 (actual: {plan.TotalCost}).");

            Debug.Log("[PASS] Test 2: Two-step plan resolution passed.");
        }

        private void ValidateThreeStepPlan(GoapPlanner planner, GoapPlan plan, AgentContext context)
        {
            var graph = CreateBaseGraph();
            var startState = WorldState.Empty
                .With(StateFact.IsHungry, true);

            var goal = new SatiateHungerGoal();
            bool success = planner.Plan(startState, goal, graph, context, plan);

            Assert(success, "Three-step planning succeeded.");
            Assert(plan.Count == 3, $"Plan count is 3 (actual: {plan.Count}).");
            Assert(plan.Actions[0] is SearchResourceAction, "Step 1 is SearchResourceAction.");
            Assert(plan.Actions[1] is TravelToResourceAction, "Step 2 is TravelToResourceAction.");
            Assert(plan.Actions[2] is HarvestResourceAction, "Step 3 is HarvestResourceAction.");
            Assert(Mathf.Approximately(plan.TotalCost, 6.0f), $"TotalCost is 6.0 (actual: {plan.TotalCost}).");

            Debug.Log("[PASS] Test 3: Three-step full chain plan resolution passed.");
        }

        private void ValidateBranchOptimization(GoapPlanner planner, GoapPlan plan, AgentContext context)
        {
            var graph = CreateBaseGraph();
            var eatFood = new EatCarriedFoodAction(); // Cost = 0.5f
            graph.RegisterAction(eatFood);

            // Agent has food in inventory and is hungry. Both EatFood (cost 0.5) and Search->Travel->Harvest (cost 6.0) are valid.
            var startState = WorldState.Empty
                .With(StateFact.IsHungry, true)
                .With(StateFact.HasFoodInInventory, true);

            var goal = new SatiateHungerGoal();
            bool success = planner.Plan(startState, goal, graph, context, plan);

            Assert(success, "Branch optimization planning succeeded.");
            Assert(plan.Count == 1, $"Planner chose 1-step plan (actual: {plan.Count}).");
            Assert(plan.Actions[0] is EatCarriedFoodAction, "Planner chose lowest-cost EatCarriedFoodAction.");
            Assert(Mathf.Approximately(plan.TotalCost, 0.5f), $"TotalCost is 0.5 (actual: {plan.TotalCost}).");

            Debug.Log("[PASS] Test 4: Branch cost optimization passed.");
        }

        private void ValidateProceduralInvalidation(GoapPlanner planner, GoapPlan plan, AgentContext context)
        {
            var graph = CreateBaseGraph();
            var invalidCheapAction = new MockInvalidAction(); // Cost 0.1f, but IsValid() returns false
            graph.RegisterAction(invalidCheapAction);

            var startState = WorldState.Empty
                .With(StateFact.IsHungry, true);

            var goal = new SatiateHungerGoal();
            bool success = planner.Plan(startState, goal, graph, context, plan);

            Assert(success, "Planning succeeded despite invalid cheap action.");
            Assert(plan.Count == 3, $"Planner avoided invalid action and built 3-step plan (actual: {plan.Count}).");
            Assert(plan.Actions[0] is SearchResourceAction, "Step 1 is SearchResourceAction.");

            Debug.Log("[PASS] Test 5: Procedural invalidation pruning passed.");
        }

        private void ValidateUnreachableGoal(GoapPlanner planner, GoapPlan plan, AgentContext context)
        {
            var graph = CreateBaseGraph();
            var startState = WorldState.Empty.With(StateFact.IsHungry, true);

            // Goal desires HasTool = true, which no registered action produces
            var unreachableGoal = new DummyGoal("EquipToolGoal", WorldState.Empty.With(StateFact.HasTool, true));
            bool success = planner.Plan(startState, unreachableGoal, graph, context, plan);

            Assert(!success, "Unreachable goal correctly returns false.");
            Assert(plan.IsEmpty, "Plan is empty for unreachable goal.");

            Debug.Log("[PASS] Test 6: Unreachable goal handling passed.");
        }

        private void ValidateAlreadySatisfiedGoal(GoapPlanner planner, GoapPlan plan, AgentContext context)
        {
            var graph = CreateBaseGraph();
            var startState = WorldState.Empty.With(StateFact.IsHungry, false);

            var goal = new SatiateHungerGoal();
            bool success = planner.Plan(startState, goal, graph, context, plan);

            Assert(success, "Already-satisfied goal returns true.");
            Assert(plan.IsEmpty, "Plan is empty when goal is already satisfied.");
            Assert(Mathf.Approximately(plan.TotalCost, 0f), "TotalCost is 0.0.");

            Debug.Log("[PASS] Test 7: Already-satisfied goal evaluation passed.");
        }

        private void ValidateZeroAllocationBenchmark(GoapPlanner planner, GoapPlan plan, AgentContext context)
        {
            var graph = CreateBaseGraph();
            var startState = WorldState.Empty.With(StateFact.IsHungry, true);
            var goal = new SatiateHungerGoal();

            // Warm up
            for (int i = 0; i < 500; i++)
            {
                planner.Plan(startState, goal, graph, context, plan);
            }

            // Benchmark 10,000 plan searches
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            var sw = new Stopwatch();
            long memoryBefore = GC.GetAllocatedBytesForCurrentThread();
            sw.Start();

            for (int i = 0; i < 10000; i++)
            {
                planner.Plan(startState, goal, graph, context, plan);
            }

            sw.Stop();
            long memoryAfter = GC.GetAllocatedBytesForCurrentThread();
            long bytesAllocated = memoryAfter - memoryBefore;

            Assert(bytesAllocated == 0, $"Zero heap allocations asserted! (actual bytes: {bytesAllocated})");
            Assert(sw.ElapsedMilliseconds < 50, $"10k A* searches took {sw.ElapsedMilliseconds}ms (< 50ms threshold).");

            Debug.Log($"[PASS] Test 8: Performance benchmark passed (10,000 A* searches in {sw.ElapsedMilliseconds}ms, {bytesAllocated} bytes allocated).");
        }

        private ActionGraph CreateBaseGraph()
        {
            var graph = new ActionGraph();
            graph.RegisterAction(new SearchResourceAction());
            graph.RegisterAction(new TravelToResourceAction());
            graph.RegisterAction(new HarvestResourceAction());
            return graph;
        }

        private void Assert(bool condition, string message)
        {
            if (!condition)
            {
                Debug.LogError($"[ASSERTION FAILED] {message}");
                throw new Exception($"Assertion failed: {message}");
            }
        }

        private class DummyGoal : GoapGoal
        {
            public DummyGoal(string name, in WorldState desiredState) : base(name, desiredState) { }
            public override float CalculatePriority(AgentContext context) => 1f;
        }
    }
}
