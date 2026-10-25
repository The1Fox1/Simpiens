using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Simpiens.Cognition.Contracts;
using Simpiens.Cognition.Planning;
using Simpiens.Diagnostics;
using Simpiens.Simulation;
using Simpiens.Simulation.Spatial;
using ZLogger;

namespace Simpiens.Cognition
{
    /// <summary>
    /// One decision as data, built off the main thread (ROADMAP §1A.2 #15): what the agent's plan becomes and the intent
    /// it carries out next. The evaluation writes nothing on <see cref="AgentState"/> except its own
    /// <see cref="AgentState.ScratchPlan"/>; the main thread applies the result.
    /// </summary>
    public struct CognitionResult
    {
        /// <summary>The intent to carry out; null applies nothing.</summary>
        public AgentIntent Intent;

        /// <summary>Drop the active plan first (a reflex took over, or its step is no longer valid).</summary>
        public bool AbortPlan;

        /// <summary>Adopt the plan the evaluation built in <see cref="AgentState.ScratchPlan"/>, for <see cref="Goal"/>.</summary>
        public bool AdoptScratchPlan;

        public GoapGoal Goal;
    }

    /// <summary>Applies a finished <see cref="CognitionJob"/> on the main thread.</summary>
    public interface ICognitionJobHandler
    {
        /// <summary>Applies the decision. Main thread, at the job's due tick.</summary>
        void Apply(CognitionJob job, in CognitionResult result);

        /// <summary>Always called last, applied or not: releases the snapshot and lets the agent decide again.</summary>
        void Release(CognitionJob job);
    }

    /// <summary>One agent's evaluation in flight, from its dispatch tick to its due tick.</summary>
    public sealed class CognitionJob
    {
        public AgentState Agent;
        public long DispatchTick;
        public long DueTick;
        public uint SessionEpoch;
        public SharedWorldSnapshot Snapshot;
        public ISimulationClock Clock;
        public ISimulationManager Simulation;
        public ICognitionJobHandler Handler;
        public Task<CognitionResult> Work;
    }

    /// <summary>
    /// Cognition on fixed ticks (ROADMAP §1A.2 #6). An evaluation dispatched on tick T runs off the main thread; at the
    /// start of tick T + <see cref="LatencyTicks"/> the simulation waits for it and applies every due decision in
    /// agent-id order. The tick a decision lands on, and the order decisions land in, no longer depend on thread timing,
    /// so same-seed runs match on any machine, warm or cold, and in a host that steps ticks as fast as it can.
    /// Evaluations dispatched in one tick still run in parallel. Main thread only.
    /// </summary>
    public sealed class CognitionScheduler
    {
        /// <summary>Ticks from dispatch to apply. One matches when results used to land on a quiet machine.</summary>
        public const int LatencyTicks = 1;

        private readonly List<CognitionJob> _pending = new List<CognitionJob>(64);
        private readonly List<CognitionJob> _due = new List<CognitionJob>(64);

        /// <summary>Evaluations in flight.</summary>
        public int PendingCount => _pending.Count;

        public void Submit(CognitionJob job)
        {
            if (job != null) _pending.Add(job);
        }

        /// <summary>
        /// Waits for every evaluation due by <paramref name="tick"/> and applies them in agent-id order. Call at the very
        /// start of the tick, before intents are registered.
        /// </summary>
        public void ApplyDue(long tick)
        {
            if (_pending.Count == 0) return;

            _due.Clear();
            int kept = 0;
            for (int i = 0; i < _pending.Count; i++)
            {
                var job = _pending[i];
                if (job.DueTick <= tick) _due.Add(job);
                else _pending[kept++] = job;
            }
            _pending.RemoveRange(kept, _pending.Count - kept);
            if (_due.Count == 0) return;

            // Stable insertion sort by agent id: small lists, no allocation
            for (int i = 1; i < _due.Count; i++)
            {
                var job = _due[i];
                int j = i - 1;
                while (j >= 0 && _due[j].Agent.AgentId.CompareTo(job.Agent.AgentId) > 0)
                {
                    _due[j + 1] = _due[j];
                    j--;
                }
                _due[j + 1] = job;
            }

            for (int i = 0; i < _due.Count; i++)
            {
                Complete(_due[i], apply: true);
            }
            _due.Clear();
        }

        /// <summary>Waits for every evaluation in flight and drops its decision (a respawn or a load).</summary>
        public void Reset()
        {
            for (int i = 0; i < _pending.Count; i++)
            {
                Complete(_pending[i], apply: false);
            }
            _pending.Clear();
            _due.Clear();
        }

        private static void Complete(CognitionJob job, bool apply)
        {
            try
            {
                CognitionResult result = default;
                bool done = false;
                try
                {
                    if (job.Work != null)
                    {
                        result = job.Work.GetAwaiter().GetResult(); // the barrier: blocks until the evaluation ends
                        done = true;
                    }
                }
                catch (OperationCanceledException)
                {
                    // Cancelled (the agent's view was destroyed): nothing to apply
                }
                catch (Exception ex)
                {
                    SimLog.Cognition.ZLogError(ex, $"Cognition evaluation failed for {SimLog.Agent(job.Agent.AgentId)}");
                }

                if (apply && done && job.Handler != null)
                {
                    job.Handler.Apply(job, result);
                }
            }
            catch (Exception ex)
            {
                SimLog.Cognition.ZLogError(ex, $"Applying a decision failed for {SimLog.Agent(job.Agent.AgentId)}");
            }
            finally
            {
                job.Handler?.Release(job);
            }
        }
    }
}
