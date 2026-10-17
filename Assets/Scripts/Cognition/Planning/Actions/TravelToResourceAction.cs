using System.Threading;
using System.Threading.Tasks;
using Simpiens.Cognition.Contracts;
using Simpiens.Cognition.Pathfinding;
using UnityEngine;

namespace Simpiens.Cognition.Planning.Actions
{
    /// <summary>
    /// Action: Navigates to a known food location.
    /// Preconditions: KnowsResourceLocation = true.
    /// Effects: AtResourceLocation = true.
    /// </summary>
    public class TravelToResourceAction : GoapAction
    {
        private readonly IAsyncPathfinder _pathfinder;

        public TravelToResourceAction(float cost = 2.0f, IAsyncPathfinder pathfinder = null) : base("TravelToResource", cost)
        {
            _pathfinder = pathfinder;
            Preconditions = WorldState.Empty.With(StateFact.KnowsResourceLocation, true);
            Effects = WorldState.Empty.With(StateFact.AtResourceLocation, true);
        }

        public override AgentIntent CreateIntent(AgentContext context)
        {
            return new WanderIntent(context.AgentId, new PathResponse());
        }

        public override ValueTask<AgentIntent> CreateIntentAsync(AgentContext context, CancellationToken ct = default)
        {
            if (_pathfinder == null || !ResourceTargeting.TrySelectFood(context, ResourceTargeting.DefaultFoodSearchDistance, out var food))
            {
                return new ValueTask<AgentIntent>(CreateIntent(context));
            }

            // Stop short of the node itself; the harvest step closes the final distance
            Vector2 foodPos = food.LastKnownLocation;
            Vector2 toAgent = context.Position - foodPos;
            Vector2 approach = toAgent.sqrMagnitude > 0.0001f ? foodPos + toAgent.normalized * 0.8f : foodPos;
            return ResourceTargeting.BuildTravelIntentAsync(context, _pathfinder, approach);
        }
    }
}
