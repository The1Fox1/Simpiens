using Simpiens.Cognition.Contracts;
using Simpiens.Entities;
using Simpiens.Simulation;

namespace Simpiens.Cognition.Planning.Handlers
{
    public class WanderIntentHandler : IntentHandlerBase<WanderIntent>
    {
        protected override IntentResult Execute(NodeController node, WanderIntent intent, ActiveIntentState state, IIntentExecutionContext context)
        {
            var agent = node.Agent;
            if (agent != null)
            {
                agent.VisualState = AgentVisualState.Walking;
            }
            return context.ExecutePathMovement(node, intent.Path, state);
        }
    }
}
