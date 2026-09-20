using Simpiens.Cognition.Contracts;
using Simpiens.Cognition.Pathfinding;

namespace Simpiens.Cognition.Planning.Actions
{
    /// <summary>
    /// Action: Navigates to a known resource location.
    /// Preconditions: KnowsResourceLocation = true.
    /// Effects: AtResourceLocation = true.
    /// </summary>
    public class TravelToResourceAction : GoapAction
    {
        public TravelToResourceAction(float cost = 2.0f) : base("TravelToResource", cost)
        {
            Preconditions = WorldState.Empty.With(StateFact.KnowsResourceLocation, true);
            Effects = WorldState.Empty.With(StateFact.AtResourceLocation, true);
        }

        public override AgentIntent CreateIntent(AgentContext context)
        {
            return new WanderIntent(context.AgentId, new PathResponse());
        }
    }
}
