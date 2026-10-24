using Simpiens.Cognition.Contracts;
using Simpiens.Entities;
using Simpiens.Simulation;

namespace Simpiens.Cognition.Planning.Handlers
{
    /// <summary>
    /// Executes a LeaveOutpostIntent (Epic 10 G1): follows the path to the Road Gate and, at its end, leaves through
    /// the simulation's departure (main thread, single writer). Without a route there is no leaving: a crawler never
    /// vanishes from the middle of the floor.
    /// </summary>
    public class LeaveOutpostIntentHandler : IntentHandlerBase<LeaveOutpostIntent>
    {
        protected override IntentResult Execute(WorldEntity node, LeaveOutpostIntent leave, ActiveIntentState state, IIntentExecutionContext context)
        {
            if (leave.Path == null || !leave.Path.IsValid) return IntentResult.TargetMissing;

            var moveResult = context.ExecutePathMovement(node, leave.Path, state);
            if (moveResult == IntentResult.InProgress)
            {
                if (node.Agent != null) node.Agent.VisualState = AgentVisualState.Walking;
                return IntentResult.InProgress;
            }
            if (moveResult != IntentResult.Success) return moveResult;

            context.DepartOutpost(node);
            return IntentResult.Success;
        }
    }
}
