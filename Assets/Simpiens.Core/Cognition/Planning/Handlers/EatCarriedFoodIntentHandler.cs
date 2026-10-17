using Simpiens.Cognition.Contracts;
using Simpiens.Entities;
using Simpiens.Simulation;

namespace Simpiens.Cognition.Planning.Handlers
{
    /// <summary>
    /// Eating takes <see cref="EatCarriedFoodIntent.Duration"/> on the spot. TargetMissing if the ration is gone (eaten
    /// or shared meanwhile); the consequence service applies the meal on success.
    /// </summary>
    public class EatCarriedFoodIntentHandler : IntentHandlerBase<EatCarriedFoodIntent>
    {
        protected override IntentResult Execute(WorldEntity node, EatCarriedFoodIntent eat, ActiveIntentState state, IIntentExecutionContext context)
        {
            var agent = node.Agent;
            if (agent != null && !agent.HasFoodInInventory) return IntentResult.TargetMissing;
            if (agent != null && !agent.IsGossiping) agent.VisualState = AgentVisualState.Idle;

            state.ElapsedTime += context.FixedTickDeltaTime;
            return state.ElapsedTime >= eat.Duration ? IntentResult.Success : IntentResult.InProgress;
        }
    }
}
