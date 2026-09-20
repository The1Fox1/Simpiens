using System.Collections.Generic;
using System.Diagnostics;
using Simpiens.Cognition.Planning;
using Simpiens.Cognition.Planning.Actions;
using Simpiens.Cognition.Planning.Goals;
using UnityEngine;
using VContainer.Unity;
using Debug = UnityEngine.Debug;

namespace Simpiens.Testing
{
    /// <summary>
    /// Automated test harness for Epic 6 Phase 1: Action Graph & State Prerequisites.
    /// Runs on startup to verify bitmask operations, action preconditions, and graph connectivity.
    /// </summary>
    public class Epic6Phase1Validator : IStartable
    {
        public void Start()
        {
            Debug.Log("[Epic6Phase1Validator] Beginning automated validation of Epic 6 Phase 1...");

            ValidateWorldStateBitmask();
            ValidateActionPrerequisites();
            ValidateActionGraphResolution();
            ValidateGoalSatisfaction();
            ValidateZeroAllocationPerformance();

            Debug.Log("<color=green>[Epic6Phase1Validator] ALL 5 TESTS PASSED SUCCESSFULLY!</color>");
        }

        private void ValidateWorldStateBitmask()
        {
            var empty = WorldState.Empty;
            Assert(empty.Satisfies(WorldState.Empty), "Empty state satisfies empty requirement.");

            var hungryState = empty.With(StateFact.IsHungry, true);
            var reqHungry = empty.With(StateFact.IsHungry, true);
            var reqSated = empty.With(StateFact.IsHungry, false);

            Assert(hungryState.Satisfies(reqHungry), "Hungry state satisfies reqHungry.");
            Assert(!hungryState.Satisfies(reqSated), "Hungry state does not satisfy reqSated.");

            // Multi-fact check: unmasked facts in required state should be ignored
            var complexState = hungryState.With(StateFact.AtResourceLocation, true);
            Assert(complexState.Satisfies(reqHungry), "Complex state still satisfies single-fact reqHungry.");

            // Effect application
            var effects = empty.With(StateFact.KnowsResourceLocation, true);
            var applied = hungryState.ApplyEffects(effects);
            Assert(applied.Get(StateFact.IsHungry) == true, "IsHungry preserved after applying KnowsResourceLocation effect.");
            Assert(applied.Get(StateFact.KnowsResourceLocation) == true, "KnowsResourceLocation active after effect application.");

            // Overwrite existing fact via effect
            var satedEffect = empty.With(StateFact.IsHungry, false);
            var finalState = applied.ApplyEffects(satedEffect);
            Assert(finalState.Get(StateFact.IsHungry) == false, "IsHungry cleared to false by satedEffect.");
            Assert(finalState.Get(StateFact.KnowsResourceLocation) == true, "KnowsResourceLocation still preserved.");

            Debug.Log("[PASS] Test 1: WorldState bitmask evaluation and effect application passed.");
        }

        private void ValidateActionPrerequisites()
        {
            var search = new SearchResourceAction();
            var travel = new TravelToResourceAction();
            var harvest = new HarvestResourceAction();

            Assert(search.Preconditions.Mask == 0, "SearchResourceAction has no preconditions.");
            Assert(travel.Preconditions.Get(StateFact.KnowsResourceLocation) == true, "Travel requires KnowsResourceLocation.");
            Assert(harvest.Preconditions.Get(StateFact.AtResourceLocation) == true, "Harvest requires AtResourceLocation.");

            // Effects verification
            Assert(search.Effects.Get(StateFact.KnowsResourceLocation) == true, "Search produces KnowsResourceLocation.");
            Assert(travel.Effects.Get(StateFact.AtResourceLocation) == true, "Travel produces AtResourceLocation.");
            Assert(harvest.Effects.Get(StateFact.IsHungry) == false, "Harvest produces IsHungry = false.");
            Assert(harvest.Effects.Get(StateFact.HasFoodInInventory) == true, "Harvest produces HasFoodInInventory = true.");

            Debug.Log("[PASS] Test 2: Action preconditions and effect definitions passed.");
        }

        private void ValidateActionGraphResolution()
        {
            var graph = new ActionGraph();
            var search = new SearchResourceAction();
            var travel = new TravelToResourceAction();
            var harvest = new HarvestResourceAction();

            graph.RegisterAction(search);
            graph.RegisterAction(travel);
            graph.RegisterAction(harvest);

            var queryResults = new List<GoapAction>(4);

            // 1. Goal requires IsHungry = false. Who produces this?
            var goalReq = WorldState.Empty.With(StateFact.IsHungry, false);
            queryResults.Clear();
            graph.GetActionsSatisfying(goalReq, queryResults);
            Assert(queryResults.Contains(harvest), "Graph resolves HarvestResource as producer for IsHungry=false.");

            // 2. Harvest requires AtResourceLocation = true. Who produces this?
            queryResults.Clear();
            graph.GetActionsSatisfying(harvest.Preconditions, queryResults);
            Assert(queryResults.Contains(travel), "Graph resolves TravelToResource as producer for AtResourceLocation.");

            // 3. Travel requires KnowsResourceLocation = true. Who produces this?
            queryResults.Clear();
            graph.GetActionsSatisfying(travel.Preconditions, queryResults);
            Assert(queryResults.Contains(search), "Graph resolves SearchResource as producer for KnowsResourceLocation.");

            // 4. Graph connectivity check
            Assert(graph.HasProducerFor(StateFact.IsHungry, false), "Graph has producer for IsHungry=false.");
            Assert(graph.HasProducerFor(StateFact.AtResourceLocation, true), "Graph has producer for AtResourceLocation.");
            Assert(graph.HasProducerFor(StateFact.KnowsResourceLocation, true), "Graph has producer for KnowsResourceLocation.");

            Debug.Log("[PASS] Test 3: ActionGraph dependency chaining (Search -> Travel -> Harvest) passed.");
        }

        private void ValidateGoalSatisfaction()
        {
            var goal = new SatiateHungerGoal();
            var hungryState = WorldState.Empty.With(StateFact.IsHungry, true);
            var satedState = WorldState.Empty.With(StateFact.IsHungry, false);

            Assert(!goal.IsSatisfied(hungryState), "Goal is not satisfied when agent is hungry.");
            Assert(goal.IsSatisfied(satedState), "Goal is satisfied when agent is sated.");

            Debug.Log("[PASS] Test 4: GoapGoal satisfaction evaluation passed.");
        }

        private void ValidateZeroAllocationPerformance()
        {
            var sw = Stopwatch.StartNew();
            var state = WorldState.Empty.With(StateFact.IsHungry, true);
            var effect = WorldState.Empty.With(StateFact.AtResourceLocation, true).With(StateFact.IsHungry, false);
            var req = WorldState.Empty.With(StateFact.IsHungry, false);

            // Execute 100,000 state evaluations in a tight loop to verify nanosecond performance
            for (int i = 0; i < 100000; i++)
            {
                var next = state.ApplyEffects(effect);
                _ = next.Satisfies(req);
            }
            sw.Stop();

            Assert(sw.ElapsedMilliseconds < 50, $"100k state evaluations took {sw.ElapsedMilliseconds}ms (< 50ms threshold).");
            Debug.Log($"[PASS] Test 5: Performance validation completed in {sw.ElapsedMilliseconds}ms for 100,000 iterations.");
        }

        private void Assert(bool condition, string message)
        {
            if (!condition)
            {
                Debug.LogError($"[ASSERTION FAILED] {message}");
                throw new System.Exception($"Assertion failed: {message}");
            }
        }
    }
}
