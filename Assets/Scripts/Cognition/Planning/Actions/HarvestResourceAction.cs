using Simpiens.Cognition.Contracts;
using Simpiens.Cognition.Pathfinding;

namespace Simpiens.Cognition.Planning.Actions
{
    /// <summary>
    /// Action: Harvests a resource at the agent's current location.
    /// Preconditions: AtResourceLocation = true.
    /// Effects: HasFoodInInventory = true, IsHungry = false.
    /// </summary>
    public class HarvestResourceAction : GoapAction
    {
        public HarvestResourceAction(float cost = 1.0f) : base("HarvestResource", cost)
        {
            Preconditions = WorldState.Empty.With(StateFact.AtResourceLocation, true);
            Effects = WorldState.Empty
                .With(StateFact.HasFoodInInventory, true)
                .With(StateFact.IsHungry, false);
        }

        public override AgentIntent CreateIntent(AgentContext context)
        {
            return new IdleIntent(context.AgentId, 1.0f);
        }
    }
}
