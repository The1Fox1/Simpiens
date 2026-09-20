using Simpiens.Cognition.Contracts;

namespace Simpiens.Cognition.Planning.Actions
{
    /// <summary>
    /// Action that consumes food currently in the agent's inventory.
    /// Fast and low-cost (0.5f), serving as an optimal alternative to harvesting.
    /// </summary>
    public class EatCarriedFoodAction : GoapAction
    {
        public EatCarriedFoodAction() : base("EatCarriedFood", baseCost: 0.5f)
        {
            Preconditions = WorldState.Empty
                .With(StateFact.HasFoodInInventory, true);

            Effects = WorldState.Empty
                .With(StateFact.IsHungry, false)
                .With(StateFact.HasFoodInInventory, false);
        }

        public override AgentIntent CreateIntent(AgentContext context)
        {
            // Resting/eating takes 1.0s of idle time
            return new IdleIntent(context.AgentId, duration: 1.0f);
        }
    }
}
