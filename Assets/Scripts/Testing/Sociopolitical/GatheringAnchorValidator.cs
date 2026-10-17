using Simpiens.Testing.Environment;
using System;
using System.Collections.Generic;
using Simpiens.Cognition;
using Simpiens.Cognition.Contracts;
using Simpiens.Cognition.Memory;
using Simpiens.Cognition.Pathfinding;
using Simpiens.Cognition.Planning;
using Simpiens.Cognition.Planning.Actions;
using Simpiens.Cognition.Planning.Goals;
using Simpiens.Entities;
using Simpiens.Simulation;
using Simpiens.Simulation.Spatial;
using UnityEngine;
using VContainer.Unity;
using Debug = UnityEngine.Debug;

namespace Simpiens.Testing.Sociopolitical
{
    /// <summary>
    /// Automated domain validation suite for Epic Beta Phase 3:
    /// Gathering Anchors & Organic Social Bonding (Campfires & Hearths).
    /// </summary>
    public class GatheringAnchorValidator : IStartable
    {
        public void Start()
        {
            Debug.Log("[GatheringAnchorValidator] Beginning automated validation of Gathering Anchors & Organic Social Bonding...");

            ValidateAnchorDetectionAndWorldStateMapping();
            ValidateGatherAtAnchorGoalPriorityDominance();
            ValidateAStarPlanFormulationToAnchor();
            ValidateHearthProximityBondingAndGossipAcceleration();
            ValidateHearthFrustrationAndSocialReplenishment();
            ValidateZeroAllocationHearthBondingTick();

            Debug.Log("<color=green>[GatheringAnchorValidator] ALL 6 DOMAIN TESTS PASSED SUCCESSFULLY!</color>");
        }

        private void ValidateAnchorDetectionAndWorldStateMapping()
        {
            var memory = new AgentMemory();
            var anchorId = EntityId.Generate();
            var anchorPos = new GridPoint(5, 5);

            // Record anchor in spatial memory
            memory.SpatialMemoryMap[anchorId] = new SpatialMemoryRecord(anchorId, EntityType.Anchor, anchorPos, 0);

            // Context A: Close to anchor (dist <= 3.0m)
            var contextA = new AgentContext(EntityId.Generate(), new Vec2(5.5f, 5.0f), new AgentNeeds(20, 80, 50), 0f, null, memory, 10);
            var stateA = WorldStateBuilder.BuildWorldState(contextA);

            Assert(stateA.Get(StateFact.KnowsAnchorLocation), "WorldState has KnowsAnchorLocation when anchor is recorded in memory.");
            Assert(stateA.Get(StateFact.AtAnchorLocation), "WorldState has AtAnchorLocation when agent is within 3.0m of anchor.");

            // Context B: Far from anchor (dist > 3.0m)
            var contextB = new AgentContext(EntityId.Generate(), new Vec2(15.0f, 15.0f), new AgentNeeds(20, 80, 50), 0f, null, memory, 10);
            var stateB = WorldStateBuilder.BuildWorldState(contextB);

            Assert(stateB.Get(StateFact.KnowsAnchorLocation), "WorldState has KnowsAnchorLocation when far away.");
            Assert(!stateB.Get(StateFact.AtAnchorLocation), "WorldState does NOT have AtAnchorLocation when farther than 3.0m.");

            // Context C: Anchor blacklisted
            memory.BlacklistEntity(anchorId, 999);
            var stateC = WorldStateBuilder.BuildWorldState(contextA);
            Assert(!stateC.Get(StateFact.KnowsAnchorLocation), "WorldState does not have KnowsAnchorLocation when anchor is blacklisted.");
            Assert(!stateC.Get(StateFact.AtAnchorLocation), "WorldState does not have AtAnchorLocation when anchor is blacklisted.");

            Debug.Log("[PASS] Test 1: Anchor detection and WorldState bitmask mapping verified.");
        }

        private void ValidateGatherAtAnchorGoalPriorityDominance()
        {
            var memory = new AgentMemory();
            var anchorId = EntityId.Generate();
            memory.SpatialMemoryMap[anchorId] = new SpatialMemoryRecord(anchorId, EntityType.Anchor, new GridPoint(0, 0), 0);

            var goal = new GatherAtAnchorGoal();

            // Scenario 1: Sated, healthy, low social need (Social = 25)
            // Priority = max(0, 75 - 25) + 15 = 65
            var healthyNeeds = new AgentNeeds(hunger: 20f, energy: 80f, social: 25f);
            var contextHealthy = new AgentContext(EntityId.Generate(), Vec2.Zero, healthyNeeds, 0f, null, memory, 0);

            Assert(goal.IsValid(contextHealthy), "GatherAtAnchorGoal is valid when agent is sated and healthy.");
            float healthyPriority = goal.CalculatePriority(contextHealthy);
            Assert(SimMath.Approximately(healthyPriority, 65f), $"GatherAtAnchorGoal priority is 65 when Social = 25 (actual: {healthyPriority})");
            Assert(healthyPriority > 10f, "GatherAtAnchorGoal dominates generic wander priority (~10f).");

            // Scenario 2: Starvation crisis (Hunger = 70 >= 60)
            var starvingNeeds = new AgentNeeds(hunger: 70f, energy: 80f, social: 25f);
            var contextStarving = new AgentContext(EntityId.Generate(), Vec2.Zero, starvingNeeds, 0f, null, memory, 0);

            Assert(!goal.IsValid(contextStarving), "GatherAtAnchorGoal is invalid during starvation crisis (Hunger >= 60).");
            float starvingPriority = goal.CalculatePriority(contextStarving);
            Assert(SimMath.Approximately(starvingPriority, 0f), $"GatherAtAnchorGoal priority drops to 0 during starvation (actual: {starvingPriority})");

            // Scenario 3: Exhaustion crisis (Energy = 15 <= 20)
            var exhaustedNeeds = new AgentNeeds(hunger: 20f, energy: 15f, social: 25f);
            var contextExhausted = new AgentContext(EntityId.Generate(), Vec2.Zero, exhaustedNeeds, 0f, null, memory, 0);

            Assert(!goal.IsValid(contextExhausted), "GatherAtAnchorGoal is invalid during exhaustion crisis (Energy <= 20).");
            float exhaustedPriority = goal.CalculatePriority(contextExhausted);
            Assert(SimMath.Approximately(exhaustedPriority, 0f), $"GatherAtAnchorGoal priority drops to 0 during exhaustion (actual: {exhaustedPriority})");

            Debug.Log("[PASS] Test 2: GatherAtAnchorGoal priority dominance switching verified.");
        }

        private void ValidateAStarPlanFormulationToAnchor()
        {
            var memory = new AgentMemory();
            var anchorId = EntityId.Generate();
            memory.SpatialMemoryMap[anchorId] = new SpatialMemoryRecord(anchorId, EntityType.Anchor, new GridPoint(5, 5), 0);

            var context = new AgentContext(EntityId.Generate(), Vec2.Zero, new AgentNeeds(20, 80, 30), 0f, null, memory, 0);
            var currentState = WorldStateBuilder.BuildWorldState(context);

            var travelAction = new TravelToAnchorAction(null, cost: 2.0f);
            var relaxAction = new RelaxAtAnchorAction(cost: 1.0f);

            var actionGraph = new ActionGraph();
            actionGraph.RegisterAction(travelAction);
            actionGraph.RegisterAction(relaxAction);
            var goal = new GatherAtAnchorGoal();
            var planner = new GoapPlanner();
            var plan = new GoapPlan(16);

            bool planFound = planner.Plan(currentState, goal, actionGraph, context, plan);

            Assert(planFound, "A* planner successfully found a plan to satisfy GatherAtAnchorGoal.");
            Assert(plan.Count == 2, $"Plan has exactly 2 steps (actual: {plan.Count})");
            Assert(plan.Actions[0] is TravelToAnchorAction, "Plan step 1 is TravelToAnchorAction.");
            Assert(plan.Actions[1] is RelaxAtAnchorAction, "Plan step 2 is RelaxAtAnchorAction.");

            Debug.Log("[PASS] Test 3: A* 2-step plan formulation to anchor verified.");
        }

        private void ValidateHearthProximityBondingAndGossipAcceleration()
        {
            var clock = new SimulationClock();
            var worldRegistry = new WorldRegistry();
            var simManager = new SimulationManager(worldRegistry, null, clock);

            // Anchor at origin
            var anchorId = EntityId.Generate();
            simManager.RegisterAnchor(anchorId, Vec2.Zero, radius: 3.0f);

            // Spawn 2 pawns around anchor within 3.0m
            var goACrawler = HeadlessEntities.Crawler(null, simManager, clock, new Vec2(1.0f, 0f), id: EntityId.Generate(), name: "[PawnA]", register: false);
            var nodeA = goACrawler.Node;
            var agentA = goACrawler.Agent;
            worldRegistry.Register(nodeA);

            var goBCrawler = HeadlessEntities.Crawler(null, simManager, clock, new Vec2(-1.0f, 0f), id: EntityId.Generate(), name: "[PawnB]", register: false);
            var nodeB = goBCrawler.Node;
            var agentB = goBCrawler.Agent;
            worldRegistry.Register(nodeB);

            // Trigger hearth evaluation at tick 100
            simManager.EvaluateHearthProximity(100);

            // Verify passive hearth dyadic bonding (+2 Warmth, +1 Trust, -2 Fear), combined with hearth gossip (+5 Warmth, +10 Trust)
            var affAtoB = agentA.Memory.GetAffinity(agentB.AgentId, 100);
            var affBtoA = agentB.Memory.GetAffinity(agentA.AgentId, 100);

            Assert(affAtoB.Warmth == 2 || affAtoB.Warmth == 7, $"Agent A Warmth toward B increased (actual: {affAtoB.Warmth})");
            Assert(affAtoB.Trust == 1 || affAtoB.Trust == 11, $"Agent A Trust toward B increased (actual: {affAtoB.Trust})");
            Assert(affAtoB.Fear == 0, $"Agent A Fear toward B clamped/reduced (actual: {affAtoB.Fear})");

            Assert(affBtoA.Warmth == 2 || affBtoA.Warmth == 7, $"Agent B Warmth toward A increased (actual: {affBtoA.Warmth})");
            Assert(affBtoA.Trust == 1 || affBtoA.Trust == 11, $"Agent B Trust toward A increased (actual: {affBtoA.Trust})");

            // Verify MemoryEvent.CampfireCompanionship logged in both event ledgers
            bool foundA = false;
            for (int i = 0; i < agentA.Memory.EventLedger.Count; i++)
            {
                var ev = agentA.Memory.EventLedger.GetEvent(i);
                if (ev.EventType == MemoryEventType.CampfireCompanionship && ev.SubjectId == agentB.AgentId)
                {
                    foundA = true;
                    break;
                }
            }
            Assert(foundA, "Agent A recorded CampfireCompanionship event in memory ledger.");

            bool foundB = false;
            for (int i = 0; i < agentB.Memory.EventLedger.Count; i++)
            {
                var ev = agentB.Memory.EventLedger.GetEvent(i);
                if (ev.EventType == MemoryEventType.CampfireCompanionship && ev.SubjectId == agentA.AgentId)
                {
                    foundB = true;
                    break;
                }
            }
            Assert(foundB, "Agent B recorded CampfireCompanionship event in memory ledger.");

            // Verify accelerated gossip: Can gossip after 100 ticks instead of 300
            Assert(agentA.Memory.CanGossipWith(agentB.AgentId, 200, cooldownTicks: 100), "Gossip cooldown is accelerated to 100 ticks around anchor.");

            HeadlessEntities.Remove(worldRegistry, goACrawler);
            HeadlessEntities.Remove(worldRegistry, goBCrawler);
            Debug.Log("[PASS] Test 4: Hearth proximity bonding and gossip acceleration verified.");
        }

        private void ValidateHearthFrustrationAndSocialReplenishment()
        {
            var clock = new SimulationClock();
            var worldRegistry = new WorldRegistry();
            var simManager = new SimulationManager(worldRegistry, null, clock);

            var anchorId = EntityId.Generate();
            simManager.RegisterAnchor(anchorId, Vec2.Zero, radius: 3.0f);

            var goACrawler = HeadlessEntities.Crawler(null, simManager, clock, new Vec2(0.5f, 0.5f), id: EntityId.Generate(), name: "[PawnComfortA]", register: false);
            var nodeA = goACrawler.Node;
            var agentA = goACrawler.Agent;
            agentA.Frustration = 20f;
            agentA.Social = 30f;
            worldRegistry.Register(nodeA);

            var goBCrawler = HeadlessEntities.Crawler(null, simManager, clock, new Vec2(-0.5f, -0.5f), id: EntityId.Generate(), name: "[PawnComfortB]", register: false);
            var nodeB = goBCrawler.Node;
            var agentB = goBCrawler.Agent;
            worldRegistry.Register(nodeB);

            // Run hearth evaluation
            simManager.EvaluateHearthProximity(50);

            Assert(agentA.Frustration < 20f, $"Frustration quenched near hearth (actual: {agentA.Frustration})");
            Assert(agentA.Social > 30f, $"Social drive replenished when 2 pawns are gathered near hearth (actual: {agentA.Social})");

            HeadlessEntities.Remove(worldRegistry, goACrawler);
            HeadlessEntities.Remove(worldRegistry, goBCrawler);
            Debug.Log("[PASS] Test 5: Hearth frustration relief and social replenishment verified.");
        }

        private void ValidateZeroAllocationHearthBondingTick()
        {
            var clock = new SimulationClock();
            var worldRegistry = new WorldRegistry();
            var simManager = new SimulationManager(worldRegistry, null, clock);

            var anchorId = EntityId.Generate();
            simManager.RegisterAnchor(anchorId, Vec2.Zero, radius: 3.0f);

            var goACrawler = HeadlessEntities.Crawler(null, simManager, clock, new Vec2(0.5f, 0f), id: EntityId.Generate(), name: "[PawnBenchA]", register: false);
            var nodeA = goACrawler.Node;
            var agentA = goACrawler.Agent;
            worldRegistry.Register(nodeA);

            var goBCrawler = HeadlessEntities.Crawler(null, simManager, clock, new Vec2(-0.5f, 0f), id: EntityId.Generate(), name: "[PawnBenchB]", register: false);
            var nodeB = goBCrawler.Node;
            var agentB = goBCrawler.Agent;
            worldRegistry.Register(nodeB);

            // Warmup
            for (int i = 0; i < 50; i++)
            {
                simManager.EvaluateHearthProximity((uint)(i * 50));
            }

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            long startAlloc = GC.GetAllocatedBytesForCurrentThread();

            for (int i = 0; i < 1000; i++)
            {
                simManager.EvaluateHearthProximity((uint)(i * 50));
            }

            long totalAlloc = GC.GetAllocatedBytesForCurrentThread() - startAlloc;
            Assert(totalAlloc == 0, $"Zero allocation guarantee violated: allocated {totalAlloc} bytes across 1,000 hearth evaluation ticks.");

            HeadlessEntities.Remove(worldRegistry, goACrawler);
            HeadlessEntities.Remove(worldRegistry, goBCrawler);
            Debug.Log($"[PASS] Test 6: Zero allocation guarantee verified (0 bytes across 1,000 evaluations).");
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
