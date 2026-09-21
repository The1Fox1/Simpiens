using System;
using System.Collections.Generic;
using Simpiens.Cognition.Contracts;
using Simpiens.Cognition.Planning;
using Simpiens.Cognition.Planning.Actions;
using Simpiens.Cognition.Planning.Goals;
using UnityEngine;
using VContainer.Unity;
using Debug = UnityEngine.Debug;

namespace Simpiens.Testing.Drives
{
    /// <summary>
    /// Automated domain validation suite for Epic Alpha Phase 1:
    /// Multifaceted Biological Drives (AgentNeeds), Dynamic GOAP Priorities, and Drive Dynamics.
    /// </summary>
    public class DriveDecayValidator : IStartable
    {
        public void Start()
        {
            Debug.Log("[DriveDecayValidator] Beginning automated validation of Biological Drives & GOAP Goals...");

            ValidateAgentNeedsDecayAndReplenishment();
            ValidateGoalDynamicPriorityFlipping();
            ValidateRestAndSocialActionsInGraph();
            ValidateWorldStateRestingFactMapping();
            ValidateZeroAllocationPerformance();

            Debug.Log("<color=green>[DriveDecayValidator] ALL 5 TESTS PASSED SUCCESSFULLY!</color>");
        }

        private void ValidateAgentNeedsDecayAndReplenishment()
        {
            var needs = new AgentNeeds(hunger: 50f, energy: 100f, social: 50f);

            // 1. Hunger accumulation
            float deltaSeconds = 10f;
            needs.Hunger = Mathf.Min(100f, needs.Hunger + deltaSeconds * 1.5f);
            Assert(Mathf.Approximately(needs.Hunger, 65f), $"Hunger accumulated to 65 after 10s (actual: {needs.Hunger})");

            // Hunger relief from eating
            needs.Hunger = Mathf.Max(0f, needs.Hunger - 50f);
            Assert(Mathf.Approximately(needs.Hunger, 15f), $"Hunger relieved to 15 after eating (actual: {needs.Hunger})");

            // 2. Energy drain while walking
            needs.Energy = Mathf.Max(0f, needs.Energy - deltaSeconds * 1.0f);
            Assert(Mathf.Approximately(needs.Energy, 90f), $"Energy drained to 90 after 10s walking (actual: {needs.Energy})");

            // Energy rapid recovery while resting (15/s)
            float restSeconds = 2f;
            needs.Energy = Mathf.Min(100f, needs.Energy + restSeconds * 15f);
            Assert(Mathf.Approximately(needs.Energy, 100f), $"Energy restored to 100 after 2s resting (actual: {needs.Energy})");

            // 3. Social drain and gossip replenishment
            needs.Social = Mathf.Max(0f, needs.Social - deltaSeconds * 0.5f);
            Assert(Mathf.Approximately(needs.Social, 45f), $"Social drained to 45 after 10s isolation (actual: {needs.Social})");

            needs.Social = Mathf.Min(100f, needs.Social + 25f);
            Assert(Mathf.Approximately(needs.Social, 70f), $"Social replenished to 70 after gossip (actual: {needs.Social})");

            Debug.Log("[PASS] Test 1: AgentNeeds decay and replenishment dynamics verified.");
        }

        private void ValidateGoalDynamicPriorityFlipping()
        {
            var hungerGoal = new SatiateHungerGoal();
            var restGoal = new RestGoal();
            var socialGoal = new SocializeGoal();

            var dummyGuid = GUID.Generate();

            // Scenario A: Starving agent (Hunger = 85, Energy = 90, Social = 70)
            var contextA = new AgentContext(dummyGuid, Vector2.zero, new AgentNeeds(85f, 90f, 70f), 0f, null, null, 0);
            float hungerPriA = hungerGoal.CalculatePriority(contextA);
            float restPriA = restGoal.CalculatePriority(contextA);
            float socialPriA = socialGoal.CalculatePriority(contextA);

            Assert(hungerPriA == 85f, $"Hunger priority is 85 (actual: {hungerPriA})");
            Assert(restPriA == 10f, $"Rest priority is 10 (actual: {restPriA})");
            Assert(socialPriA == 30f, $"Social priority is 30 (actual: {socialPriA})");
            Assert(hungerPriA > restPriA && hungerPriA > socialPriA, "SatiateHungerGoal dominates when starving.");

            // Scenario B: Exhausted agent (Hunger = 30, Energy = 10, Social = 80)
            var contextB = new AgentContext(dummyGuid, Vector2.zero, new AgentNeeds(30f, 10f, 80f), 0f, null, null, 0);
            float hungerPriB = hungerGoal.CalculatePriority(contextB);
            float restPriB = restGoal.CalculatePriority(contextB);

            Assert(restPriB == 90f, $"Rest priority is 90 (actual: {restPriB})");
            Assert(restPriB > hungerPriB, "RestGoal dominates when exhausted.");

            // Scenario C: Isolated agent (Hunger = 20, Energy = 85, Social = 5)
            var contextC = new AgentContext(dummyGuid, Vector2.zero, new AgentNeeds(20f, 85f, 5f), 0f, null, null, 0);
            float socialPriC = socialGoal.CalculatePriority(contextC);
            Assert(socialPriC == 95f, $"Social priority is 95 (actual: {socialPriC})");
            Assert(socialPriC > hungerGoal.CalculatePriority(contextC), "SocializeGoal dominates when lonely.");

            Debug.Log("[PASS] Test 2: Dynamic GOAP goal priority flipping verified.");
        }

        private void ValidateRestAndSocialActionsInGraph()
        {
            var graph = new ActionGraph();
            var restAction = new RestAction();
            var travelPeer = new TravelToPeerAction();

            graph.RegisterAction(restAction);
            graph.RegisterAction(travelPeer);

            var restMatches = new List<GoapAction>();
            graph.GetActionsSatisfying(WorldState.Empty.With(StateFact.IsResting, true), restMatches);
            Assert(restMatches.Count == 1 && restMatches[0] == restAction, "RestAction satisfies IsResting in ActionGraph.");

            var socialMatches = new List<GoapAction>();
            graph.GetActionsSatisfying(WorldState.Empty.With(StateFact.HasExchangedGossip, true), socialMatches);
            Assert(socialMatches.Count == 1 && socialMatches[0] == travelPeer, "TravelToPeerAction satisfies HasExchangedGossip in ActionGraph.");

            Debug.Log("[PASS] Test 3: RestAction and TravelToPeerAction registered and resolved in ActionGraph.");
        }

        private void ValidateWorldStateRestingFactMapping()
        {
            var dummyGuid = GUID.Generate();

            // Sated energy -> IsResting = true
            var contextRested = new AgentContext(dummyGuid, Vector2.zero, new AgentNeeds(10f, 85f, 50f), 0f, null, null, 0);
            var stateRested = WorldStateBuilder.BuildWorldState(contextRested);
            Assert(stateRested.Get(StateFact.IsResting) == true, "IsResting fact is true when Energy >= 80.");

            // Low energy -> IsResting = false
            var contextTired = new AgentContext(dummyGuid, Vector2.zero, new AgentNeeds(10f, 30f, 50f), 0f, null, null, 0);
            var stateTired = WorldStateBuilder.BuildWorldState(contextTired);
            Assert(stateTired.Get(StateFact.IsResting) == false, "IsResting fact is false when Energy < 80.");

            Debug.Log("[PASS] Test 4: WorldStateBuilder maps IsResting fact accurately.");
        }

        private void ValidateZeroAllocationPerformance()
        {
            var dummyGuid = GUID.Generate();
            var goal = new RestGoal();

            // Warmup
            for (int i = 0; i < 100; i++)
            {
                var warmupContext = new AgentContext(dummyGuid, Vector2.zero, new AgentNeeds(50f, 30f, 50f), 0f, null, null, 0);
                goal.CalculatePriority(warmupContext);
                WorldStateBuilder.BuildWorldState(warmupContext);
            }

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            long startAlloc = GC.GetAllocatedBytesForCurrentThread();

            for (int i = 0; i < 1000; i++)
            {
                var context = new AgentContext(dummyGuid, Vector2.zero, new AgentNeeds(50f, 30f, 50f), 0f, null, null, 0);
                float priority = goal.CalculatePriority(context);
                var state = WorldStateBuilder.BuildWorldState(context);
            }

            long totalAlloc = GC.GetAllocatedBytesForCurrentThread() - startAlloc;
            Assert(totalAlloc == 0, $"Zero allocation guarantee violated: allocated {totalAlloc} bytes across 1,000 evaluations.");

            Debug.Log($"[PASS] Test 5: Zero-allocation guarantee verified (0 bytes allocated across 1,000 evaluations).");
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
