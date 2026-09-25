using Simpiens.Cognition.Contracts;
using Simpiens.Cognition.Pathfinding;

namespace Simpiens.Cognition.Planning.Actions
{
    /// <summary>
    /// Action: Approaches and transfers food to a companion in need.
    /// Preconditions: None.
    /// Effects: HasAidedPeer = true.
    /// </summary>
    public class ShareFoodAction : GoapAction
    {
        public ShareFoodAction(float cost = 1.5f) : base("ShareFood", cost)
        {
            Preconditions = WorldState.Empty;
            Effects = WorldState.Empty
                .With(StateFact.HasAidedPeer, true);
        }

        public override AgentIntent CreateIntent(AgentContext context)
        {
            return new ShareResourceIntent(context.AgentId, default, new PathResponse());
        }
    }
}
