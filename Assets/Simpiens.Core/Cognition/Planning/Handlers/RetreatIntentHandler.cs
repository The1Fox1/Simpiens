using Simpiens.Cognition.Contracts;
using Simpiens.Entities;
using Simpiens.Simulation;

namespace Simpiens.Cognition.Planning.Handlers
{
    /// <summary>Walks a hurt crawler along its retreat (Epic 10 G2); a downed one limps (ExecutePathMovement slows it).</summary>
    public class RetreatIntentHandler : IntentHandlerBase<RetreatIntent>
    {
        protected override IntentResult Execute(WorldEntity node, RetreatIntent retreat, ActiveIntentState state, IIntentExecutionContext context)
        {
            if (retreat.Path == null || !retreat.Path.IsValid) return IntentResult.TargetMissing;
            var result = context.ExecutePathMovement(node, retreat.Path, state);
            if (result == IntentResult.InProgress && node.Agent != null) node.Agent.VisualState = AgentVisualState.Walking;
            return result;
        }
    }
}
