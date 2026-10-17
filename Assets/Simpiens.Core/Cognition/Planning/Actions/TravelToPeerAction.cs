using Simpiens.Cognition.Contracts;
using Simpiens.Cognition.Pathfinding;

namespace Simpiens.Cognition.Planning.Actions
{
    /// <summary>
    /// Action: Navigates toward a remembered peer to socialize.
    /// Preconditions: None.
    /// Effects: IsNearPeer = true, HasExchangedGossip = true.
    /// </summary>
    public class TravelToPeerAction : GoapAction
    {
        public TravelToPeerAction(float cost = 2.0f) : base("TravelToPeer", cost)
        {
            Preconditions = WorldState.Empty;
            Effects = WorldState.Empty
                .With(StateFact.IsNearPeer, true)
                .With(StateFact.HasExchangedGossip, true);
        }

        public override AgentIntent CreateIntent(AgentContext context)
        {
            return new WanderIntent(context.AgentId, new PathResponse());
        }
    }
}
