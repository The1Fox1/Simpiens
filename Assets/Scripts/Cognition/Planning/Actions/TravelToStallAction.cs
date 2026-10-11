using System.Threading;
using Cysharp.Threading.Tasks;
using Simpiens.Cognition.Contracts;
using Simpiens.Cognition.Pathfinding;
using Simpiens.Simulation.Spatial;
using UnityEngine;

namespace Simpiens.Cognition.Planning.Actions
{
    /// <summary>
    /// Action: Navigates toward the perimeter of an outpost trade stall.
    /// Preconditions: None.
    /// Effects: AtTradeStall = true.
    /// </summary>
    public class TravelToStallAction : GoapAction
    {
        private readonly IAsyncPathfinder _pathfinder;

        public TravelToStallAction(IAsyncPathfinder pathfinder = null, float cost = 2.0f) : base("TravelToStall", cost)
        {
            _pathfinder = pathfinder;
            Preconditions = WorldState.Empty;
            Effects = WorldState.Empty.With(StateFact.AtTradeStall, true);
        }

        /// <summary>
        /// Invalid when stalls exist but every one is walled off from the agent, so plans never "arrive"
        /// at an unroutable stall and then fail the trade out of range.
        /// </summary>
        public override bool IsValid(AgentContext context)
        {
            return ResourceTargeting.GetStallAccess(context, out _) != ResourceTargeting.StallAccess.Unreachable;
        }

        public override AgentIntent CreateIntent(AgentContext context)
        {
            return new WanderIntent(context.AgentId, new PathResponse());
        }

        public override async UniTask<AgentIntent> CreateIntentAsync(AgentContext context, CancellationToken ct = default)
        {
            if (_pathfinder == null)
            {
                return CreateIntent(context);
            }

            Vector2 stallPos = Vector2.zero;
            bool found = false;

            if (context.Snapshot != null && context.Snapshot.ActiveStallCount > 0)
            {
                // Nearest stall the agent can actually walk to
                if (ResourceTargeting.TryGetNearestStall(context, out var reachable))
                {
                    stallPos = reachable.Position;
                    found = true;
                }
            }
            else if (context.Memory != null && context.Memory.SpatialMemoryMap != null)
            {
                float closestDistSqr = float.MaxValue;
                foreach (var kvp in context.Memory.SpatialMemoryMap)
                {
                    if (kvp.Value.Type == EntityType.Stall && !context.Memory.IsBlacklisted(kvp.Key, context.CurrentTick))
                    {
                        float distSqr = ((Vector2)kvp.Value.LastKnownLocation - context.Position).sqrMagnitude;
                        if (distSqr < closestDistSqr)
                        {
                            closestDistSqr = distSqr;
                            stallPos = kvp.Value.LastKnownLocation;
                            found = true;
                        }
                    }
                }
            }

            if (!found)
            {
                return CreateIntent(context);
            }

            // Stand 1.5m away from stall counter
            Vector2 toAgent = (context.Position - stallPos).normalized;
            if (toAgent == Vector2.zero) toAgent = Vector2.up;
            Vector2 targetPos = stallPos + toAgent * 1.5f;

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
