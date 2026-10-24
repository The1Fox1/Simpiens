using Simpiens.Cognition.Contracts;
using Simpiens.Diagnostics;
using ZLogger;
using Simpiens.Entities;
using Simpiens.Simulation;

namespace Simpiens.Cognition.Planning.Handlers
{
    public class PanicIntentHandler : IntentHandlerBase<PanicIntent>
    {
        /// <summary>
        /// A panic ends after this long even short of its last waypoint, and the crawler re-evaluates from where it is:
        /// sidestepping a crowd can circle a waypoint for good (one sprint lasted 4,000 ticks; Epic 10 G0).
        /// </summary>
        public const float MaxSeconds = 6f;

        protected override IntentResult Execute(WorldEntity node, PanicIntent intent, ActiveIntentState state, IIntentExecutionContext context)
        {
            var agent = node.Agent;
            if (agent != null)
            {
                agent.VisualState = AgentVisualState.Panicking;
            }

            if (state.ElapsedTime <= 0f)
            {
                // Panic movement deliberately ignores obstacle checks to break deadlocks
                SimLog.Drives.ZLogInformation($"{SimLog.Agent(node.Id)} panicking from {node.Position} ({intent.Path?.Length ?? 0} waypoints, obstacle checks bypassed)");
            }

            state.ElapsedTime += context.FixedTickDeltaTime;
            var panicResult = context.ExecutePanicMovement(node, intent.Path, ref state.CurrentWaypointIndex);
            if (panicResult == IntentResult.Success)
            {
                // Enforce a minimum duration of at least 1.0 second (10 ticks) for panic behavior to prevent sub-second visual jitter
                if (state.ElapsedTime < 1.0f)
                {
                    return IntentResult.InProgress;
                }
                return IntentResult.Success;
            }
            if (panicResult == IntentResult.InProgress && state.ElapsedTime >= MaxSeconds) return IntentResult.Success;
            return panicResult;
        }
    }
}
