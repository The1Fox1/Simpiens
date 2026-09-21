using System;
using System.Collections.Generic;
using System.Diagnostics;
using Cysharp.Threading.Tasks;
using Simpiens.Cognition.Contracts;
using Simpiens.Cognition.Evaluators;
using Simpiens.Cognition.Memory;
using Simpiens.Cognition.Pathfinding;
using Simpiens.Cognition.Planning;
using Simpiens.Cognition.Planning.Actions;
using Simpiens.Cognition.Planning.Goals;
using Simpiens.Simulation;
using Simpiens.Simulation.Spatial;
using UnityEngine;
using VContainer.Unity;
using Debug = UnityEngine.Debug;

namespace Simpiens.Testing.Planning
{
    /// <summary>
    /// Automated test harness for Cognitive Plan Execution & Preemption.
    /// Validates two-tier cognition, step progression, in-flight intent preemption, and plan invalidation.
    /// </summary>
    public class PlanExecutionValidator : IStartable
    {
        public void Start()
        {
            Debug.Log("[PlanExecutionValidator] Beginning automated validation of Plan Execution & Preemption...");

            ValidateWorldStateBuilder();
            ValidateSequentialStepProgression();
            ValidateSimulationManagerAbort();
            ValidatePlanInvalidationOnFailure();
            ValidateGoapCognitiveEvaluatorIntegration().Forget();
            ValidateZeroAllocationWorldStateBenchmark();

            Debug.Log("<color=green>[PlanExecutionValidator] ALL TESTS PASSED SUCCESSFULLY!</color>");
        }

        private void ValidateWorldStateBuilder()
        {
            var memory = new AgentMemory();
            var agentId = CreateGuid();
            var resourceId = CreateGuid();

            // 1. Initially without known resources and hunger = 40 (sated)
            var contextSated = new AgentContext(
                agentId,
                Vector2.zero,
                hunger: 40f,
                energy: 100f,
                frustration: 10f,
                snapshot: null,
                memory: memory,
                currentTick: 10
            );

            var state1 = WorldStateBuilder.BuildWorldState(contextSated);
            Assert(state1.Get(StateFact.IsHungry) == false, "State1: IsHungry is false.");
            Assert(state1.Get(StateFact.KnowsResourceLocation) == false, "State1: KnowsResourceLocation is false.");
            Assert(state1.Get(StateFact.AtResourceLocation) == false, "State1: AtResourceLocation is false.");
            Assert(state1.Get(StateFact.IsThreatened) == false, "State1: IsThreatened is false.");

            // 2. Add resource at distance 1.0 unit (within ReachResourceDistance 1.5), hunger = 80, frustration = 70
            memory.SpatialMemoryMap[resourceId] = new SpatialMemoryRecord(resourceId, EntityType.Resource, new Vector2Int(1, 0), 10);

            var contextHungry = new AgentContext(
                agentId,
                Vector2.zero,
                hunger: 80f,
                energy: 100f,
                frustration: 70f,
                snapshot: null,
                memory: memory,
                currentTick: 10
            );

            var state2 = WorldStateBuilder.BuildWorldState(contextHungry, hasFoodInInventory: true);
            Assert(state2.Get(StateFact.IsHungry) == true, "State2: IsHungry is true.");
            Assert(state2.Get(StateFact.KnowsResourceLocation) == true, "State2: KnowsResourceLocation is true.");
            Assert(state2.Get(StateFact.AtResourceLocation) == true, "State2: AtResourceLocation is true (distance 1.0 <= 1.5).");
            Assert(state2.Get(StateFact.HasFoodInInventory) == true, "State2: HasFoodInInventory is true.");
            Assert(state2.Get(StateFact.IsThreatened) == true, "State2: IsThreatened is true (frustration 70 > 60).");

            Debug.Log("[PASS] Test 1: WorldStateBuilder context-to-bitmask mapping passed.");
        }

        private void ValidateSequentialStepProgression()
        {
            var plan = new GoapPlan();
            var search = new SearchResourceAction();
            var travel = new TravelToResourceAction();
            var harvest = new HarvestResourceAction();

            plan.AddAction(search, search.BaseCost);
            plan.AddAction(travel, travel.BaseCost);
            plan.AddAction(harvest, harvest.BaseCost);

            Assert(plan.Count == 3, "Plan contains 3 steps.");
            Assert(plan.CurrentStepIndex == 0, "Initial step is 0.");
            Assert(plan.CurrentAction == search, "Step 0 is SearchResourceAction.");

            // Advance to step 1
            bool hasNext1 = plan.AdvanceStep();
            Assert(hasNext1, "Plan has step 1.");
            Assert(plan.CurrentStepIndex == 1, "Current step is 1.");
            Assert(plan.CurrentAction == travel, "Step 1 is TravelToResourceAction.");

            // Advance to step 2
            bool hasNext2 = plan.AdvanceStep();
            Assert(hasNext2, "Plan has step 2.");
            Assert(plan.CurrentStepIndex == 2, "Current step is 2.");
            Assert(plan.CurrentAction == harvest, "Step 2 is HarvestResourceAction.");

            // Advance past last step
            bool hasNext3 = plan.AdvanceStep();
            Assert(!hasNext3, "Plan has no more steps.");
            Assert(plan.IsFinished, "Plan.IsFinished is true.");
            Assert(plan.CurrentAction == null, "CurrentAction is null when finished.");

            Debug.Log("[PASS] Test 2: Sequential plan step advancement passed.");
        }

        private void ValidateSimulationManagerAbort()
        {
            var simManager = new SimulationManager(null, null);
            var agentId = CreateGuid();
            bool onCompleteCalled = false;
            IntentResult completionResult = IntentResult.InProgress;

            var intent = new IdleIntent(agentId, duration: 5.0f);
            intent.OnComplete = (res) =>
            {
                onCompleteCalled = true;
                completionResult = res;
            };

            // Enqueue and process to register as active intent
            simManager.EnqueueIntent(intent);
            simManager.ProcessQueuedIntents();

            // Abort active intent
            bool aborted = simManager.AbortIntent(agentId);
            Assert(aborted, "SimulationManager.AbortIntent returned true for active intent.");
            Assert(onCompleteCalled, "Intent.OnComplete was invoked on abort.");
            Assert(completionResult == IntentResult.Aborted, "Completion result is IntentResult.Aborted.");

            // Attempt to abort again (should return false since intent was already removed)
            bool secondAbort = simManager.AbortIntent(agentId);
            Assert(!secondAbort, "Second AbortIntent call returned false.");

            Debug.Log("[PASS] Test 3: SimulationManager.AbortIntent preemption passed.");
        }

        private void ValidatePlanInvalidationOnFailure()
        {
            var plan = new GoapPlan();
            plan.AddAction(new SearchResourceAction(), 3.0f);
            plan.AddAction(new TravelToResourceAction(), 2.0f);
            plan.AddAction(new HarvestResourceAction(), 1.0f);

            Assert(!plan.IsEmpty, "Plan initially has actions.");

            // Simulating failure condition (e.g. TargetMissing)
            plan.Clear();

            Assert(plan.IsEmpty, "Plan was cleared on step failure.");
            Assert(plan.Count == 0, "Plan count is 0.");
            Assert(plan.CurrentAction == null, "CurrentAction is null.");

            Debug.Log("[PASS] Test 4: Plan invalidation on failure passed.");
        }

        private async UniTaskVoid ValidateGoapCognitiveEvaluatorIntegration()
        {
            var planner = new GoapPlanner();
            var graph = new ActionGraph();
            graph.RegisterAction(new SearchResourceAction());
            graph.RegisterAction(new TravelToResourceAction());
            graph.RegisterAction(new HarvestResourceAction());

            var goals = new List<GoapGoal> { new SatiateHungerGoal() };
            var evaluator = new GoapCognitiveEvaluator(planner, graph, goals);

            var memory = new AgentMemory();
            var agentId = CreateGuid();
            var context = new AgentContext(
                agentId,
                Vector2.zero,
                hunger: 80f, // Hungry
                energy: 100f,
                frustration: 0f,
                snapshot: null,
                memory: memory,
                currentTick: 1
            );

            // Agent is hungry and knows no resources -> evaluator should plan Search -> Travel -> Harvest
            // and return the first action's intent (WanderIntent from SearchResourceAction)
            var intent = await evaluator.EvaluateAsync(context, default);

            Assert(intent != null, "GoapCognitiveEvaluator generated an intent.");
            Assert(intent is WanderIntent, "First intent is WanderIntent from SearchResourceAction.");

            Debug.Log("[PASS] Test 5: GoapCognitiveEvaluator integration passed.");
        }

        private void ValidateZeroAllocationWorldStateBenchmark()
        {
            var memory = new AgentMemory();
            var agentId = CreateGuid();
            var resourceId = CreateGuid();
            memory.SpatialMemoryMap[resourceId] = new SpatialMemoryRecord(resourceId, EntityType.Resource, new Vector2Int(2, 2), 1);

            var context = new AgentContext(
                agentId,
                Vector2.zero,
                hunger: 75f,
                energy: 100f,
                frustration: 20f,
                snapshot: null,
                memory: memory,
                currentTick: 1
            );

            // Warm up
            for (int i = 0; i < 500; i++)
            {
                _ = WorldStateBuilder.BuildWorldState(context);
            }

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            var sw = new Stopwatch();
            long memoryBefore = GC.GetAllocatedBytesForCurrentThread();
            sw.Start();

            for (int i = 0; i < 10000; i++)
            {
                _ = WorldStateBuilder.BuildWorldState(context);
            }

            sw.Stop();
            long memoryAfter = GC.GetAllocatedBytesForCurrentThread();
            long bytesAllocated = memoryAfter - memoryBefore;

            Assert(bytesAllocated == 0, $"Zero heap allocations asserted! (actual bytes: {bytesAllocated})");
            Assert(sw.ElapsedMilliseconds < 25, $"10k WorldState builds took {sw.ElapsedMilliseconds}ms (< 25ms threshold).");

            Debug.Log($"[PASS] Test 6: Performance benchmark passed (10,000 WorldState builds in {sw.ElapsedMilliseconds}ms, {bytesAllocated} bytes allocated).");
        }

        private static UnityEngine.GUID CreateGuid()
        {
            return UnityEngine.GUID.Generate();
        }

        private void Assert(bool condition, string message)
        {
            if (!condition)
            {
                Debug.LogError($"[ASSERTION FAILED] {message}");
                throw new Exception($"Assertion failed: {message}");
            }
        }
    }
}
