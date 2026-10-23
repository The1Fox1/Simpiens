using System.Threading;
using System.Threading.Tasks;
using Simpiens.Cognition.Contracts;
using Simpiens.Cognition.Evaluators;
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

        public override ValueTask<AgentIntent> CreateIntentAsync(AgentContext context, CancellationToken ct = default)
        {
            if (_pathfinder == null)
            {
                return new ValueTask<AgentIntent>(CreateIntent(context));
            }

            // A crawler that has seen a food sector goes back to its nearest plot and waits there for regrowth, as the
            // starvation reflex does, instead of exploring away from the only free food it knows: one searched the Road
            // Gate and starved on the walk back (Epic 10 G0, finding F2)
            var map = context.Snapshot?.Map;
            if (map != null && ResourceTargeting.TryPickFoodPlotWaypoint(context, map, out var plot))
            {
                float wait = MentalBreakEvaluator.WaitAtPlotRadius;
                if ((plot - context.Position).SqrMagnitude <= wait * wait)
                {
                    return new ValueTask<AgentIntent>(new IdleIntent(context.AgentId, MentalBreakEvaluator.WaitAtPlotSeconds));
                }
                return ResourceTargeting.BuildTravelIntentAsync(context, _pathfinder, plot);
            }

            return ResourceTargeting.BuildTravelIntentAsync(context, _pathfinder, ResourceTargeting.PickExplorationPoint(context));
        }
    }
}
