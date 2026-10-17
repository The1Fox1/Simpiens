using System.Threading;
using System.Threading.Tasks;
using Simpiens.Cognition.Contracts;
using Simpiens.Cognition.Pathfinding;
using Simpiens.Simulation.Spatial;
namespace Simpiens.Cognition.Planning.Actions
{
    /// <summary>
    /// Action: Navigates toward a known gathering anchor (Campfire / Hearth).
    /// Preconditions: KnowsAnchorLocation = true.
    /// Effects: AtAnchorLocation = true.
    /// </summary>
    public class TravelToAnchorAction : GoapAction
    {
        private readonly IAsyncPathfinder _pathfinder;

        public TravelToAnchorAction(IAsyncPathfinder pathfinder = null, float cost = 2.0f) : base("TravelToAnchor", cost)
        {
            _pathfinder = pathfinder;
            Preconditions = WorldState.Empty.With(StateFact.KnowsAnchorLocation, true);
            Effects = WorldState.Empty.With(StateFact.AtAnchorLocation, true);
        }

        public override AgentIntent CreateIntent(AgentContext context)
        {
            return new WanderIntent(context.AgentId, new PathResponse());
        }

        public override async ValueTask<AgentIntent> CreateIntentAsync(AgentContext context, CancellationToken ct = default)
        {
            if (_pathfinder == null || context.Memory == null || context.Memory.SpatialMemoryMap == null)
            {
                return CreateIntent(context);
            }

            Vec2 anchorPos = Vec2.Zero;
            float closestDistSqr = float.MaxValue;
            bool found = false;

            foreach (var kvp in context.Memory.SpatialMemoryMap)
            {
                var record = kvp.Value;
                if (record.Type == EntityType.Anchor && !context.Memory.IsBlacklisted(record.EntityId, context.CurrentTick))
                {
                    float distSqr = ((Vec2)record.LastKnownLocation - context.Position).SqrMagnitude;
                    if (distSqr < closestDistSqr)
                    {
                        closestDistSqr = distSqr;
                        anchorPos = record.LastKnownLocation;
                        found = true;
                    }
                }
            }

            if (!found) return CreateIntent(context);

            // Compute perimeter position around anchor (2.0m away)
            Vec2 toAgent = (context.Position - anchorPos).Normalized;
            if (toAgent == Vec2.Zero) toAgent = Vec2.Up;
            Vec2 targetPos = anchorPos + toAgent * 2.0f;

            var req = new PathRequest
            {
                StartPosition = context.Position,
                TargetPosition = targetPos
            };

            var pathResponse = await _pathfinder.CalculatePathAsync(req, context.Snapshot);
            return new WanderIntent(context.AgentId, pathResponse);
        }
    }
}
