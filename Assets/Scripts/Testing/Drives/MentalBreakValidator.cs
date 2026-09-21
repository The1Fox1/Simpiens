using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Simpiens.Cognition;
using Simpiens.Cognition.Contracts;
using Simpiens.Cognition.Evaluators;
using Simpiens.Cognition.Memory;
using Simpiens.Cognition.Pathfinding;
using Simpiens.Entities;
using Simpiens.Simulation;
using Simpiens.Simulation.Spatial;
using UnityEngine;
using VContainer.Unity;
using Debug = UnityEngine.Debug;

namespace Simpiens.Testing.Drives
{
    /// <summary>
    /// Automated domain validation suite for Epic Alpha Phase 3:
    /// Mental Breaks & Emergency Reflexes (Starvation Panic & Exhaustion Collapse).
    /// </summary>
    public class MentalBreakValidator : IStartable
    {
        public void Start()
        {
            Debug.Log("[MentalBreakValidator] Beginning automated validation of Mental Breaks & Emergency Reflexes...");

            ValidateStarvationPanicActivation();
            ValidateStarvationPanicSuppressedWhenFoodKnown();
            ValidateExhaustionCollapse();
            ValidateIntentPreemptionOnBreak();
            ValidateZeroAllocationMentalBreakCheck();

            Debug.Log("<color=green>[MentalBreakValidator] ALL 5 TESTS PASSED SUCCESSFULLY!</color>");
        }

        private void ValidateStarvationPanicActivation()
        {
            var evaluator = new MentalBreakEvaluator(null);
            var memory = new AgentMemory();

            // Add a stale resource memory that should be pruned
            var staleId = GUID.Generate();
            memory.SpatialMemoryMap[staleId] = new SpatialMemoryRecord(staleId, EntityType.Resource, new Vector2Int(10, 10), 0);
            memory.BlacklistEntity(staleId, 9999); // Blacklisted, so not a valid food source

            var needs = new AgentNeeds(hunger: 90f, energy: 50f, social: 50f);
            var context = new AgentContext(GUID.Generate(), Vector2.zero, needs, 0f, null, memory, 100);

            var intent = evaluator.EvaluateAsync(context, CancellationToken.None).GetAwaiter().GetResult();

            Assert(intent is PanicIntent, "MentalBreakEvaluator returned PanicIntent during starvation with no food.");
            Assert(!memory.SpatialMemoryMap.ContainsKey(staleId), "Stale resource memory was pruned on starvation panic.");
            Debug.Log("[PASS] Test 1: Starvation Panic triggered and stale resource memories pruned.");
        }

        private void ValidateStarvationPanicSuppressedWhenFoodKnown()
        {
            var evaluator = new MentalBreakEvaluator(null);
            var memory = new AgentMemory();

            // Add an accessible food resource to memory
            var validFoodId = GUID.Generate();
            memory.SpatialMemoryMap[validFoodId] = new SpatialMemoryRecord(validFoodId, EntityType.Resource, new Vector2Int(5, 5), 100);

            var needs = new AgentNeeds(hunger: 90f, energy: 50f, social: 50f);
            var context = new AgentContext(GUID.Generate(), Vector2.zero, needs, 0f, null, memory, 100);

            var intent = evaluator.EvaluateAsync(context, CancellationToken.None).GetAwaiter().GetResult();

            Assert(intent == null, "MentalBreakEvaluator suppressed panic because valid food location is known in memory.");
            Debug.Log("[PASS] Test 2: Starvation Panic suppressed when food is known (allowing GOAP deliberative plan).");
        }

        private void ValidateExhaustionCollapse()
        {
            var evaluator = new MentalBreakEvaluator(null);
            var memory = new AgentMemory();

            var needs = new AgentNeeds(hunger: 20f, energy: 3f, social: 50f);
            var context = new AgentContext(GUID.Generate(), Vector2.zero, needs, 0f, null, memory, 100);

            var intent = evaluator.EvaluateAsync(context, CancellationToken.None).GetAwaiter().GetResult();

            Assert(intent is IdleIntent idle && Mathf.Approximately(idle.Duration, 2.0f), "MentalBreakEvaluator returned emergency IdleIntent (2s) on exhaustion collapse.");
            Debug.Log("[PASS] Test 3: Exhaustion Collapse triggered emergency rest reflex.");
        }

        private void ValidateIntentPreemptionOnBreak()
        {
            var clock = new SimulationClock();
            var simManager = new SimulationManager(null, null, clock);
            var go = new GameObject("[TestNode_Preempt]");
            var node = go.AddComponent<NodeController>();
            node.Initialize(GUID.Generate(), new NodeConfiguration(2f));

            var agent = go.AddComponent<AutonomousAgent>();
            agent.Initialize(node.Id, null, null, simManager, clock);

            // Seed agent with an active intent
            var wanderIntent = new WanderIntent(node.Id, new PathResponse());
            simManager.EnqueueIntent(wanderIntent);
            simManager.ProcessQueuedIntents();

            // Force agent into active intent state with critically low energy
            agent.Energy = 3f;
            typeof(AutonomousAgent).GetProperty("HasActiveIntent").SetValue(agent, true);

            // ManualUpdate should trigger emergency exhaustion preemption
            agent.ManualUpdate();

            Assert(agent.IsExhaustionCollapsed, "Agent transitioned into IsExhaustionCollapsed state.");
            Assert(!agent.HasActiveIntent, "Active intent was preempted/aborted by emergency mental break.");

            GameObject.DestroyImmediate(go);
            Debug.Log("[PASS] Test 4: Main-thread intent preemption on mental break verified.");
        }

        private void ValidateZeroAllocationMentalBreakCheck()
        {
            var evaluator = new MentalBreakEvaluator(null);
            var memory = new AgentMemory();
            var needs = new AgentNeeds(hunger: 40f, energy: 70f, social: 60f);
            var context = new AgentContext(GUID.Generate(), Vector2.zero, needs, 0f, null, memory, 100);

            // Warmup
            for (int i = 0; i < 50; i++)
            {
                evaluator.EvaluateAsync(context, CancellationToken.None).GetAwaiter().GetResult();
            }

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            long startAlloc = GC.GetAllocatedBytesForCurrentThread();

            for (int i = 0; i < 1000; i++)
            {
                evaluator.EvaluateAsync(context, CancellationToken.None).GetAwaiter().GetResult();
            }

            long totalAlloc = GC.GetAllocatedBytesForCurrentThread() - startAlloc;
            Assert(totalAlloc == 0, $"Zero allocation guarantee violated: allocated {totalAlloc} bytes across 1,000 mental break evaluations.");

            Debug.Log($"[PASS] Test 5: Zero allocation guarantee verified (0 bytes across 1,000 evaluations).");
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
