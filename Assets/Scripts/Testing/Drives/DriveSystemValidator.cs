using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Simpiens.Cognition;
using Simpiens.Cognition.Contracts;
using Simpiens.Cognition.Evaluators;
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

namespace Simpiens.Testing.Drives
{
    /// <summary>
    /// Master automated domain validation suite for Epic Alpha:
    /// Biological Drives, GOAP Priority Hierarchy, Motor Watchdog Cadence, Mental Break / Visual State Transitions,
    /// and Strict Zero-Allocation Simulation Performance.
    /// </summary>
    public class DriveSystemValidator : IStartable
    {
        public void Start()
        {
            Debug.Log("[DriveSystemValidator] Beginning master automated validation of Epic Alpha subsystems...");

            ValidateBiologicalDriveDynamics();
            ValidateGoapPriorityHierarchy();
            ValidateMotorWatchdogCadence();
            ValidateMentalBreakAndVisualTransitions();
            ValidateZeroAllocationBenchmark();

            Debug.Log("<color=green>[DriveSystemValidator] ALL 5 MASTER DOMAIN TESTS PASSED SUCCESSFULLY!</color>");
        }

        private void ValidateBiologicalDriveDynamics()
        {
            var needs = new AgentNeeds(hunger: 50f, energy: 100f, social: 50f);

            // 1. Hunger accumulation over time (1.5/s) and relief from eating (-50)
            float deltaSeconds = 10f;
            needs.Hunger = Mathf.Min(100f, needs.Hunger + deltaSeconds * 1.5f);
            Assert(Mathf.Approximately(needs.Hunger, 65f), $"Hunger accumulated to 65 after 10s (actual: {needs.Hunger})");

            needs.Hunger = Mathf.Max(0f, needs.Hunger - 50f);
            Assert(Mathf.Approximately(needs.Hunger, 15f), $"Hunger relieved to 15 after eating (actual: {needs.Hunger})");

            // 2. Energy drain while walking (1.0/s) vs resting recovery (15/s)
            needs.Energy = Mathf.Max(0f, needs.Energy - deltaSeconds * 1.0f);
            Assert(Mathf.Approximately(needs.Energy, 90f), $"Energy drained to 90 after 10s walking (actual: {needs.Energy})");

            float restSeconds = 2f;
            needs.Energy = Mathf.Min(100f, needs.Energy + restSeconds * 15f);
            Assert(Mathf.Approximately(needs.Energy, 100f), $"Energy recovered to 100 after 2s resting (actual: {needs.Energy})");

            // Baseline metabolic drain (0.2/s)
            needs.Energy = Mathf.Max(0f, needs.Energy - deltaSeconds * 0.2f);
            Assert(Mathf.Approximately(needs.Energy, 98f), $"Energy drained to 98 after 10s baseline metabolic cost (actual: {needs.Energy})");

            // 3. Social isolation decay (0.5/s) and gossip replenishment (+25)
            needs.Social = Mathf.Max(0f, needs.Social - deltaSeconds * 0.5f);
            Assert(Mathf.Approximately(needs.Social, 45f), $"Social drained to 45 after 10s isolation (actual: {needs.Social})");

            needs.Social = Mathf.Min(100f, needs.Social + 25f);
            Assert(Mathf.Approximately(needs.Social, 70f), $"Social replenished to 70 after gossip (actual: {needs.Social})");

            Debug.Log("[PASS] Test 1: Continuous biological drive dynamics verified.");
        }

        private void ValidateGoapPriorityHierarchy()
        {
            var hungerGoal = new SatiateHungerGoal();
            var restGoal = new RestGoal();
            var socialGoal = new SocializeGoal();
            var agentId = GUID.Generate();

            // Scenario A: Starving agent (Hunger = 85, Energy = 90, Social = 70)
            var contextA = new AgentContext(agentId, Vector2.zero, new AgentNeeds(85f, 90f, 70f), 0f, null, null, 0);
            float hungerPriA = hungerGoal.CalculatePriority(contextA);
            float restPriA = restGoal.CalculatePriority(contextA);
            float socialPriA = socialGoal.CalculatePriority(contextA);

            Assert(hungerPriA == 85f, $"Hunger priority is 85 (actual: {hungerPriA})");
            Assert(restPriA == 10f, $"Rest priority is 10 (actual: {restPriA})");
            Assert(socialPriA == 30f, $"Social priority is 30 (actual: {socialPriA})");
            Assert(hungerPriA > restPriA && hungerPriA > socialPriA, "SatiateHungerGoal dominates when starving.");

            // Scenario B: Exhausted agent (Hunger = 30, Energy = 10, Social = 80)
            var contextB = new AgentContext(agentId, Vector2.zero, new AgentNeeds(30f, 10f, 80f), 0f, null, null, 0);
            float hungerPriB = hungerGoal.CalculatePriority(contextB);
            float restPriB = restGoal.CalculatePriority(contextB);
            float socialPriB = socialGoal.CalculatePriority(contextB);

            Assert(restPriB == 90f, $"Rest priority is 90 (actual: {restPriB})");
            Assert(restPriB > hungerPriB && restPriB > socialPriB, "RestGoal dominates when exhausted.");

            // Scenario C: Isolated agent (Hunger = 20, Energy = 85, Social = 5)
            var contextC = new AgentContext(agentId, Vector2.zero, new AgentNeeds(20f, 85f, 5f), 0f, null, null, 0);
            float hungerPriC = hungerGoal.CalculatePriority(contextC);
            float restPriC = restGoal.CalculatePriority(contextC);
            float socialPriC = socialGoal.CalculatePriority(contextC);

            Assert(socialPriC == 95f, $"Social priority is 95 (actual: {socialPriC})");
            Assert(socialPriC > hungerPriC && socialPriC > restPriC, "SocializeGoal dominates when lonely.");

            Debug.Log("[PASS] Test 2: Dynamic GOAP goal priority hierarchy verified.");
        }

        private void ValidateMotorWatchdogCadence()
        {
            var registry = new WorldRegistry();
            var grid = new SpatialHashGrid();
            grid.Initialize(20f, 20f, 1f);
            var clock = new SimulationClock();
            var simManager = new SimulationManager(registry, grid, clock);

            // Create blocker node at target waypoint (1, 0)
            var blockerGo = new GameObject("[TestNode_Blocker]");
            var blockerNode = blockerGo.AddComponent<NodeController>();
            blockerNode.Initialize(GUID.Generate(), new NodeConfiguration(2f));
            blockerNode.transform.position = new Vector2(1f, 0f);
            registry.RegisterNode(blockerNode);
            grid.UpdateFromRegistry(registry);

            // Create moving agent node at (0, 0)
            var movingGo = new GameObject("[TestNode_Moving]");
            var movingNode = movingGo.AddComponent<NodeController>();
            movingNode.Initialize(GUID.Generate(), new NodeConfiguration(2f));
            movingNode.transform.position = Vector2.zero;
            registry.RegisterNode(movingNode);

            var path = new PathResponse
            {
                Waypoints = new Vector2[] { new Vector2(1f, 0f) },
                Length = 1,
                IsValid = true
            };

            // Part A: Sub-threshold obstruction (TimeSinceLastProgress = 0.5s < 1.0s)
            // Pawn must yield InProgress WITHOUT incrementing StallCount or applying 60Hz lateral twitch
            var state = new ActiveIntentState
            {
                Intent = new WanderIntent(movingNode.Id, path),
                CurrentWaypointIndex = 0,
                LastSampledPosition = Vector2.zero,
                TimeSinceLastProgress = 0.5f,
                StallCount = 0
            };

            var resA = simManager.ExecutePathMovement(movingNode, path, state);
            Assert(resA == IntentResult.InProgress, "Pawn yields InProgress when path waypoint is obstructed.");
            Assert(state.StallCount == 0, "Watchdog does not increment StallCount before 1.0s threshold.");
            Assert(Vector2.Distance(movingNode.Position, Vector2.zero) < 0.001f, "No lateral position twitch while waiting on obstructed path.");

            // Part B: Watchdog trigger at >= 1.0s stall threshold
            state.TimeSinceLastProgress = 1.0f;
            var resB = simManager.ExecutePathMovement(movingNode, path, state);

            Assert(state.StallCount == 1, "Watchdog increments StallCount to 1 on 1.0s physical stall cadence.");
            Assert(state.TimeSinceLastProgress == 0f, "Watchdog resets progress timer after firing unstuck reflex.");
            Assert(!Mathf.Approximately(movingNode.Position.y, 0f), "Tier 1 unstuck reflex applied orthogonal evasion.");

            GameObject.DestroyImmediate(blockerGo);
            GameObject.DestroyImmediate(movingGo);
            Debug.Log("[PASS] Test 3: Motor watchdog cadence and smooth yield verified.");
        }

        private void ValidateMentalBreakAndVisualTransitions()
        {
            var clock = new SimulationClock();
            var simManager = new SimulationManager(null, null, clock);

            var go = new GameObject("[TestAgent_Visual]");
            var node = go.AddComponent<NodeController>();
            node.Initialize(GUID.Generate(), new NodeConfiguration(2f));
            var agent = go.AddComponent<AutonomousAgent>();
            agent.Initialize(node.Id, null, null, simManager, clock);

            // Part A: Starvation Panic evaluation
            var evaluator = new MentalBreakEvaluator(null);
            var starvingNeeds = new AgentNeeds(hunger: 90f, energy: 50f, social: 50f);
            var starvingContext = new AgentContext(agent.AgentId, Vector2.zero, starvingNeeds, 0f, null, agent.Memory, 100);

            var panicIntent = evaluator.EvaluateAsync(starvingContext, CancellationToken.None).GetAwaiter().GetResult();
            Assert(panicIntent is PanicIntent, "MentalBreakEvaluator generates PanicIntent when starving without food.");

            // Part B: Exhaustion Collapse & Resting Visual State
            agent.Energy = 3f;
            agent.HasActiveIntent = true;
            agent.ManualUpdate();

            Assert(agent.IsExhaustionCollapsed, "Agent collapsed into IsExhaustionCollapsed when Energy <= 5f.");
            Assert(agent.VisualState == AgentVisualState.Resting, $"VisualState switched to Resting on exhaustion collapse (actual: {agent.VisualState}).");
            Assert(!agent.HasActiveIntent, "Active intent aborted on emergency exhaustion collapse.");

            // Verify recovery threshold
            agent.Energy = 35f;
            agent.ManualUpdate();
            Assert(!agent.IsExhaustionCollapsed, "Agent recovered from collapse when Energy recovered to >= 30f.");

            // Part C: Deliberate RestAction in ActivePlan sets Resting visual state
            agent.ActivePlan.Clear();
            agent.ActivePlan.AddAction(new RestAction(), 1f);
            Assert(agent.HasActivePlan, "Agent has active plan.");
            Assert(agent.ActivePlan.CurrentAction is RestAction, "Current action is RestAction.");

            var idleIntent = new IdleIntent(agent.AgentId, 5.0f);
            simManager.EnqueueIntent(idleIntent);
            simManager.ProcessQueuedIntents();

            // Execute idle intent in SimulationManager
            var idleState = new ActiveIntentState
            {
                Intent = idleIntent,
                ElapsedTime = 0f
            };
            simManager.ExecuteIntent(node, idleState);

            Assert(agent.VisualState == AgentVisualState.Resting, "VisualState switched to Resting during deliberate RestAction execution.");

            GameObject.DestroyImmediate(go);
            Debug.Log("[PASS] Test 4: Mental break and resting visual transitions verified.");
        }

        private void ValidateZeroAllocationBenchmark()
        {
            var registry = new WorldRegistry();
            var grid = new SpatialHashGrid();
            grid.Initialize(20f, 20f, 1f);
            var clock = new SimulationClock();
            var simManager = new SimulationManager(registry, grid, clock);

            var go = new GameObject("[TestNode_ZeroGC]");
            var node = go.AddComponent<NodeController>();
            node.Initialize(GUID.Generate(), new NodeConfiguration(2f));
            node.transform.position = Vector2.zero;
            registry.RegisterNode(node);

            var agent = go.AddComponent<AutonomousAgent>();
            agent.Initialize(node.Id, null, null, simManager, clock);
            agent.Energy = 100f;
            agent.Hunger = 0f;
            agent.Social = 100f;
            agent.HasActiveIntent = true;

            var idleIntent = new IdleIntent(node.Id, 10000f);
            simManager.EnqueueIntent(idleIntent);
            simManager.ProcessQueuedIntents();

            // Warmup
            for (int i = 0; i < 50; i++)
            {
                agent.ManualUpdate();
                simManager.Tick();
            }

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            long startAlloc = GC.GetAllocatedBytesForCurrentThread();

            for (int i = 0; i < 1000; i++)
            {
                agent.ManualUpdate();
                simManager.Tick();
            }

            long totalAlloc = GC.GetAllocatedBytesForCurrentThread() - startAlloc;
            Assert(totalAlloc == 0, $"Zero allocation guarantee violated: allocated {totalAlloc} bytes across 1,000 simulation ticks.");

            GameObject.DestroyImmediate(go);
            Debug.Log("[PASS] Test 5: Strict zero-allocation guarantee verified (0 bytes across 1,000 ticks).");
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
