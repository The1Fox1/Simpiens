using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Simpiens.Cognition;
using Simpiens.Cognition.Contracts;
using Simpiens.Cognition.Evaluators;
using Simpiens.Simulation;
using Simpiens.Simulation.Spatial;
using UnityEngine;

namespace Simpiens.Testing.Environment
{
    /// <summary>
    /// Epic CORE C1: cognition on fixed ticks (ROADMAP §1A.2 #6, #15, #18). A decision dispatched on tick T lands at the
    /// start of tick T + 1 however long its evaluation takes, decisions land in agent-id order whatever order the
    /// evaluations finish in, nothing on the agent changes off the main thread, a failed preparation never freezes an
    /// agent, and a respawn drops decisions in flight.
    /// </summary>
    public class CognitionSchedulerValidator
    {
        public void RunAllValidationTests()
        {
            Debug.Log("[CognitionSchedulerValidator] Beginning Epic CORE C1 validation...");

            ValidateDecisionLandsOnTheNextTick(evaluationDelayMs: 0);
            ValidateDecisionLandsOnTheNextTick(evaluationDelayMs: 120);
            ValidateDecisionsLandInAgentIdOrder();
            ValidateFailedPreparationDoesNotFreeze();
            ValidateResetDropsDecisionsInFlight();
            ValidateFailedEvaluationReleases();

            Debug.Log("<color=green>[CognitionSchedulerValidator] ALL 6 TESTS PASSED SUCCESSFULLY!</color>");
        }

        private sealed class DelayedIdleEvaluator : ICognitiveEvaluator
        {
            private readonly int _delayMs;
            public int Calls;
            public DelayedIdleEvaluator(int delayMs) { _delayMs = delayMs; }

            public ValueTask<AgentIntent> EvaluateAsync(AgentContext context, CancellationToken ct)
            {
                Interlocked.Increment(ref Calls);
                if (_delayMs > 0) Thread.Sleep(_delayMs);
                return new ValueTask<AgentIntent>(new IdleIntent(context.AgentId, 5f));
            }
        }

        private sealed class ThrowingEvaluator : ICognitiveEvaluator
        {
            public ValueTask<AgentIntent> EvaluateAsync(AgentContext context, CancellationToken ct)
            {
                throw new InvalidOperationException("evaluation failed on purpose");
            }
        }

        private sealed class RecordingHandler : ICognitionJobHandler
        {
            public readonly List<GUID> Applied = new List<GUID>(8);
            public readonly List<GUID> Released = new List<GUID>(8);

            public void Apply(CognitionJob job, in CognitionResult result) { Applied.Add(job.Agent.AgentId); }
            public void Release(CognitionJob job) { Released.Add(job.Agent.AgentId); }
        }

        private static (SimulationClock clock, SpatialHashGrid grid, SimulationManager sim) CreateWorld()
        {
            var clock = new SimulationClock();
            var grid = new SpatialHashGrid();
            grid.Initialize(100f, 100f, 10f);
            grid.UpdateFromRegistry(null);
            var sim = new SimulationManager(null, grid, clock);
            return (clock, grid, sim);
        }

        private void ValidateDecisionLandsOnTheNextTick(int evaluationDelayMs)
        {
            var (clock, grid, sim) = CreateWorld();
            var agent = new AgentState(CreateGuid(1)) { Position = new Vector2(10f, 10f) };
            var evaluator = new DelayedIdleEvaluator(evaluationDelayMs);
            var service = new AgentCognitionService();

            clock.AdvanceTicks(5);
            service.BeginEvaluation(agent, grid, sim, clock, null, null, null, new ICognitiveEvaluator[] { evaluator });
            Assert(agent.IsEvaluating, "The agent is evaluating after dispatch.");
            Assert(sim.Cognition.PendingCount == 1, "One evaluation is in flight.");

            // Give the evaluation time to finish: it still must not touch the agent before its tick
            Thread.Sleep(evaluationDelayMs + 60);
            Assert(!agent.HasActiveIntent && agent.CurrentIntent == null, "Nothing is applied before the due tick, even once the evaluation has finished.");

            clock.AdvanceTicks(1); // tick 6 = dispatch + 1: the barrier waits, then applies
            Assert(agent.HasActiveIntent && agent.CurrentIntent is IdleIntent, $"The decision landed at T + 1 (delay {evaluationDelayMs} ms).");
            Assert(!agent.IsEvaluating, "The agent may decide again once its decision landed.");
            Assert(sim.Cognition.PendingCount == 0, "No evaluation is left in flight.");
            Assert(evaluator.Calls == 1, "The evaluator ran once.");

            Debug.Log($"[PASS] Decision dispatched at t5 lands at t6 with a {evaluationDelayMs} ms evaluation.");
        }

        private void ValidateDecisionsLandInAgentIdOrder()
        {
            var scheduler = new CognitionScheduler();
            var handler = new RecordingHandler();

            // Agent 3 finishes first, agent 1 last
            int[] ids = { 3, 1, 2 };
            int[] delays = { 0, 150, 60 };
            for (int i = 0; i < ids.Length; i++)
            {
                int delay = delays[i];
                scheduler.Submit(new CognitionJob
                {
                    Agent = new AgentState(CreateGuid(ids[i])),
                    DispatchTick = 10,
                    DueTick = 11,
                    Handler = handler,
                    Work = Task.Run(() => { Thread.Sleep(delay); return new CognitionResult(); })
                });
            }

            scheduler.ApplyDue(10);
            Assert(handler.Applied.Count == 0, "Nothing is due at the dispatch tick.");
            scheduler.ApplyDue(11);
            Assert(handler.Applied.Count == 3, "All three decisions landed at the due tick.");
            for (int i = 1; i < handler.Applied.Count; i++)
            {
                Assert(handler.Applied[i - 1].CompareTo(handler.Applied[i]) < 0, "Decisions landed in agent-id order, not completion order.");
            }
            Assert(handler.Released.Count == 3, "Every job was released.");

            Debug.Log("[PASS] Decisions land in agent-id order whatever order the evaluations finish in.");
        }

        private void ValidateFailedPreparationDoesNotFreeze()
        {
            var (clock, grid, sim) = CreateWorld();
            var agent = new AgentState(CreateGuid(7)) { Memory = null }; // the memory update throws
            var service = new AgentCognitionService();

            clock.AdvanceTicks(1);
            service.BeginEvaluation(agent, grid, sim, clock, null, null, null, new ICognitiveEvaluator[] { new DelayedIdleEvaluator(0) });
            Assert(!agent.IsEvaluating, "A failed preparation does not leave the agent marked as evaluating (§1A.2 #18).");
            Assert(sim.Cognition.PendingCount == 0, "Nothing was dispatched.");

            Debug.Log("[PASS] A failed preparation does not freeze the agent.");
        }

        private void ValidateResetDropsDecisionsInFlight()
        {
            var (clock, grid, sim) = CreateWorld();
            var agent = new AgentState(CreateGuid(9));
            var service = new AgentCognitionService();

            clock.AdvanceTicks(1);
            service.BeginEvaluation(agent, grid, sim, clock, null, null, null, new ICognitiveEvaluator[] { new DelayedIdleEvaluator(50) });
            Assert(sim.Cognition.PendingCount == 1, "One evaluation is in flight before the respawn.");

            sim.ResetState(); // a respawn or a load
            Assert(sim.Cognition.PendingCount == 0, "A respawn drops evaluations in flight.");
            Assert(!agent.IsEvaluating && !agent.HasActiveIntent, "The dropped decision was released, not applied.");

            clock.AdvanceTicks(2);
            Assert(!agent.HasActiveIntent, "A dropped decision never lands.");

            Debug.Log("[PASS] A respawn drops decisions in flight.");
        }

        private void ValidateFailedEvaluationReleases()
        {
            var (clock, grid, sim) = CreateWorld();
            var agent = new AgentState(CreateGuid(11));
            var service = new AgentCognitionService();

            clock.AdvanceTicks(1);
            service.BeginEvaluation(agent, grid, sim, clock, null, null, null, new ICognitiveEvaluator[] { new ThrowingEvaluator() });
            clock.AdvanceTicks(1);
            Assert(!agent.IsEvaluating, "A failed evaluation is released at its due tick.");
            Assert(!agent.HasActiveIntent, "A failed evaluation applies nothing.");

            Debug.Log("[PASS] A failed evaluation is released and applies nothing.");
        }

        private static GUID CreateGuid(int value) => new GUID((uint)value, 0, 0, 0);

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new Exception($"[CognitionSchedulerValidator ASSERTION FAILED] {message}");
        }
    }
}
