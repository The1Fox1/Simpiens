using System.Threading;
using Cysharp.Threading.Tasks;
using Simpiens.Cognition.Contracts;
using Simpiens.Cognition.Pathfinding;

namespace Simpiens.Cognition.Planning.Actions
{
    /// <summary>
    /// Action: Scouts/wanders to discover new food locations.
    /// Preconditions: None.
    /// Effects: KnowsResourceLocation = true.
    /// With a pathfinder, walks to a deterministic exploration waypoint; without one, emits an empty wander (headless).
    /// </summary>
    public class SearchResourceAction : GoapAction
    {
        private readonly IAsyncPathfinder _pathfinder;

        public SearchResourceAction(float cost = 3.0f, IAsyncPathfinder pathfinder = null) : base("SearchResource", cost)
        {
            _pathfinder = pathfinder;
            Preconditions = WorldState.Empty;
            Effects = WorldState.Empty.With(StateFact.KnowsResourceLocation, true);
        }

        public override AgentIntent CreateIntent(AgentContext context)
        {
            return new WanderIntent(context.AgentId, new PathResponse());
        }

        public override UniTask<AgentIntent> CreateIntentAsync(AgentContext context, CancellationToken ct = default)
        {
            if (_pathfinder == null)
            {
                return UniTask.FromResult(CreateIntent(context));
            }

            return ResourceTargeting.BuildTravelIntentAsync(context, _pathfinder, ResourceTargeting.PickExplorationPoint(context));
        }
    }
}
