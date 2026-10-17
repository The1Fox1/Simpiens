using Simpiens.Cognition.Contracts;
using Simpiens.Diagnostics;
using ZLogger;
using Simpiens.Entities;
using Simpiens.Simulation;

namespace Simpiens.Cognition.Planning.Handlers
{
    /// <summary>
    /// Executes a SleepAtBunkIntent: claims the bunk on the first tick (one sleeper per bunk, the agent standing at
    /// it), then sleeps for the intent's duration. Energy recovers in AgentState while the plan's current action is
    /// SleepAtBunk; the completion bonus is the consequence service's. Occupancy is released when the sleep ends, and
    /// the simulation drops any claim whose sleeper is no longer sleeping there (preempted or removed).
    /// </summary>
    public class SleepAtBunkIntentHandler : IntentHandlerBase<SleepAtBunkIntent>
    {
        /// <summary>Ticks allowed for the last step onto the bunk.</summary>
        public const int MaxApproachTicks = 30;

        /// <summary>Ticks a bunk that could not be taken is left alone.</summary>
        public const uint BlacklistTicks = 100;

        protected override IntentResult Execute(WorldEntity node, SleepAtBunkIntent sleep, ActiveIntentState state, IIntentExecutionContext context)
        {
            if (state.ElapsedTime <= 0f)
            {
                // The last step onto the bunk first: walks often stop a step short (arrival tolerance, a pawn in the
                // way). Bounded, so a sleeper that cannot get there gives up and re-plans.
                var step = state.CurrentWaypointIndex++ < MaxApproachTicks
                    ? context.StepOntoBunk(node, sleep.BunkId)
                    : IntentResult.PathBlocked;
                if (step == IntentResult.InProgress) return IntentResult.InProgress;

                if (step != IntentResult.Success || !context.TryClaimBunk(sleep.BunkId, node.Id, node.Position))
                {
                    SimLog.Drives.ZLogDebug($"{SimLog.Agent(node.Id)} could not sleep in bunk {SimLog.Agent(sleep.BunkId)} from {node.Position} ({step})");
                    // Forget this bunk for a while so the next plan picks another one instead of retrying every tick
                    node.Agent?.Memory?.BlacklistEntity(sleep.BunkId, context.CurrentTick + BlacklistTicks);
                    return IntentResult.TargetMissing;
                }
            }

            state.ElapsedTime += context.FixedTickDeltaTime;
            var agent = node.Agent;
            if (agent != null) agent.VisualState = AgentVisualState.Sleeping;

            if (state.ElapsedTime >= sleep.Duration)
            {
                context.ReleaseBunk(sleep.BunkId, node.Id);
                context.RecordBunkSleep(node.Id);
                if (agent != null && agent != null) agent.Activity.RecordBunkSleep(context.CurrentTick);
                if (agent != null) agent.VisualState = AgentVisualState.Idle; // up and out of the bunk
                SimLog.Drives.ZLogDebug($"{SimLog.Agent(node.Id)} slept in bunk {SimLog.Agent(sleep.BunkId)}");
                return IntentResult.Success;
            }
            return IntentResult.InProgress;
        }
    }
}
