using System.Threading;
using System.Threading.Tasks;
using Simpiens.Cognition.Contracts;
using Simpiens.Cognition.Pathfinding;
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

        /// <summary>How far from a food node the walk stops; the harvest handler reaches 0.8.</summary>
        public const float ApproachDistance = 0.7f;

        public override AgentIntent CreateIntent(AgentContext context)
        {
            return new WanderIntent(context.AgentId, new PathResponse());
        }

        public override ValueTask<AgentIntent> CreateIntentAsync(AgentContext context, CancellationToken ct = default)
        {
            // Near food first; else any food it knows: the planner counts every remembered plot, and from the Road Gate the
            // Garden is just past the search distance, so a hungry crawler there "arrived" in place every tick (Epic 10 G1)
            if (_pathfinder == null || (!ResourceTargeting.TrySelectFood(context, ResourceTargeting.DefaultFoodSearchDistance, out var food)
                                        && !ResourceTargeting.TrySelectFood(context, float.MaxValue, out food)))
            {
                return new ValueTask<AgentIntent>(CreateIntent(context));
            }

            // Stop short of the node itself; the harvest step closes the final distance. At 0.7, inside the harvest reach
            // (0.8) by a margin: stopping at exactly 0.8 put every arrival on the reach test's edge, which Unity's Mono and
            // CoreCLR round apart (Epic CORE F1; golden seed 424242 diverged there at t2586, Epic 10 G1)
            Vec2 foodPos = food.LastKnownLocation;
            Vec2 toAgent = context.Position - foodPos;
            Vec2 approach = toAgent.SqrMagnitude > 0.0001f ? foodPos + toAgent.Normalized * ApproachDistance : foodPos;
            return ResourceTargeting.BuildTravelIntentAsync(context, _pathfinder, approach);
        }
    }
}
