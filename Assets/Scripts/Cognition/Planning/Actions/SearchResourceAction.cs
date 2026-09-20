using Simpiens.Cognition.Contracts;
using Simpiens.Cognition.Pathfinding;
using UnityEngine;

namespace Simpiens.Cognition.Planning.Actions
{
    /// <summary>
    /// Action: Scouts/wanders to discover new resource locations.
    /// Preconditions: None.
    /// Effects: KnowsResourceLocation = true.
    /// </summary>
    public class SearchResourceAction : GoapAction
    {
        public SearchResourceAction(float cost = 3.0f) : base("SearchResource", cost)
        {
            Preconditions = WorldState.Empty;
            Effects = WorldState.Empty.With(StateFact.KnowsResourceLocation, true);
        }

        public override AgentIntent CreateIntent(AgentContext context)
        {
            // Simple wander intent to scout
            return new WanderIntent(context.AgentId, new PathResponse());
        }
    }
}
